using ChatTranslated.Chat;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using ChatTranslated.Windows;
using ChatTranslated.Windows.ConfigTabs;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatTranslated;

public sealed class Plugin : IDalamudPlugin
{
    public static string Name => "ChatTranslated";
    private const string CommandName = "/pchat";

    private readonly MenuItem contextMenuItem;

    public WindowSystem WindowSystem { get; } = new("ChatTranslated");

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        var configuration = Service.configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        IpcManager.Register(pluginInterface);
        ChatTwoDetector.Register();

        Service.configWindow = new ConfigWindow();
        Service.mainWindow = new MainWindow();
        Service.setupWizard = new SetupWizard();

        WindowSystem.AddWindow(Service.configWindow);
        WindowSystem.AddWindow(Service.mainWindow);
        WindowSystem.AddWindow(Service.setupWizard);

        pluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        pluginInterface.UiBuilder.OpenMainUi += DrawMainUI;

        Service.chatHandler = new ChatHandler();

        contextMenuItem = new MenuItem
        {
            UseDefaultPrefix = true,
            Name = "Translate",
            OnClicked = TranslatePF
        };
        Service.contextMenu.OnMenuOpened += OnContextMenuOpened;

        Service.commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Chat Translated main window."
        });

        GeneralTab.SetLanguageCulture(configuration.SelectedPluginLanguage);

        // initialize chat channels
        configuration.SelectedChatTypes ??= [.. ChatChannelsTab.genericChatTypes];

        // settings migration
        if (configuration.Version < 8)
        {
            if (configuration.Version < 6)
                configuration.SelectedChatTypes.Add(XivChatType.CustomEmote);
            if (configuration.Version < 7)
                configuration.KnownLanguages = [configuration.SelectedTargetLanguage];

            configuration.Version = 8;
            configuration.Save();
        }

        if (configuration.Proxy_Url != "https://cfv5.kelpcc.com")
        {
            configuration.Proxy_Url = "https://cfv5.kelpcc.com";
            configuration.Save();
        }

        _ = LanguageDetector.RebuildDetectorAsync();
        _ = Task.Run(GlyphFolder.Warmup);
        TranslationHandler.LoadCache();

        if (!configuration.ShowedWizard)
            Service.setupWizard.IsOpen = true;
    }

    private void OnContextMenuOpened(IMenuOpenedArgs args)
    {
        if (args.AddonName == "LookingForGroupDetail")
            args.AddMenuItem(contextMenuItem);
    }

    private unsafe void TranslatePF(IMenuItemClickedArgs args)
    {
        var addon = (AddonLookingForGroupDetail*)args.AddonPtr;

        // fix weird characters in pf description
        var description = addon->DescriptionString.ToString()
            .Replace("\u0002\u0012\u00027\u0003", "  ")
            .Replace("\u0002\u0012\u00028\u0003", "  ");

        var recruiterName = addon->PartyLeaderTextNode->GetText().ExtractText();
        if (string.IsNullOrWhiteSpace(recruiterName))
            recruiterName = "PF";

        var category = string.Join(" ", Regex.Matches(addon->CategoriesString.ToString(), @"\[[^\]]+\]").Select(m => m.Value));
        if (string.IsNullOrWhiteSpace(category)) category = "null";

        var duty = addon->DutyNameTextNode->GetText().ExtractText();
        if (string.IsNullOrWhiteSpace(duty)) duty = "null";

        // the detail window shows the listing the agent opened last
        var dutyId = AgentLookingForGroup.Instance()->LastViewedListing.DutyId;

        var message = new Message(recruiterName, MessageSource.PartyFinder, description)
        {
            Context = $"Tags: {category}, Duty: {duty}",
            DutyId = dutyId == 0 ? null : dutyId
        };

        Task.Run(() => TranslatePFAsync(message));
    }

    private static async Task TranslatePFAsync(Message message)
    {
        await TranslationHandler.TranslateMessage(message);
        ChatHandler.OutputMessage(message);
    }

    // leave the text alone when Chat 2 can draw it, fold it when nothing else will
    private static string ForChatLog(string message) =>
        ChatTwoDetector.HasGlyphCoverage ? message : GlyphFolder.Fold(message);

    public static void OutputChatLine(XivChatType type, string sender, string message)
    {
        var sb = new SeStringBuilder();
        if (!Service.configuration.ChatIntegration_ShowColoredText)
            sb.AddUiForegroundOff();
        sb.Append(ForChatLog(message));

        Print(new XivChatEntry
        {
            Type = type,
            Name = new SeString(new PlayerPayload("[CT] " + sender, 0)), // use player payload to fake chat bubbles
            Message = sb.Build()
        });
    }

    public static void OutputChatLine(string message)
    {
        SeStringBuilder sb = new();
        sb.AddUiForeground("[CT] ", 58).Append(ForChatLog(message));

        Print(new XivChatEntry { Message = sb.BuiltString });
    }

    // translations finish on the thread pool, but ChatGui's queue is only safe to use from the framework thread
    private static void Print(XivChatEntry entry) =>
        _ = Service.framework.RunOnFrameworkThread(() => Service.chatGui.Print(entry));

    public void Dispose()
    {
        IpcManager.Unregister();
        ChatTwoDetector.Unregister();
        LanguageDetector.Dispose();
        TranslationHandler.SaveCache();
        TranslationHandler.HttpClient.Dispose();

        Service.contextMenu.OnMenuOpened -= OnContextMenuOpened;
        Service.pluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        Service.pluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
        Service.pluginInterface.UiBuilder.OpenMainUi -= DrawMainUI;
        WindowSystem.RemoveAllWindows();

        Service.chatHandler?.Dispose();
        Service.commandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        switch (args)
        {
            case "config":
                Service.configWindow.IsOpen = true;
                return;
            case "on":
                Service.configuration.Enabled = true;
                Service.configuration.Save();
                OutputChatLine("Plugin enabled.");
                return;
            case "off":
                Service.configuration.Enabled = false;
                Service.configuration.Save();
                OutputChatLine("Plugin disabled.");
                return;
            case "integration":
                Service.configuration.ChatIntegration = !Service.configuration.ChatIntegration;
                Service.configuration.Save();
                OutputChatLine($"Chat integration {(Service.configuration.ChatIntegration ? "enabled" : "disabled")}.");
                return;
            case "wizard":
                Service.setupWizard.IsOpen = true;
                return;
        }

        Service.mainWindow.IsOpen = true;
    }

    public static void DrawConfigUI() => Service.configWindow.IsOpen = true;

    public static void DrawMainUI() => Service.mainWindow.IsOpen = true;
}
