using ChatTranslated.Chat;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using ChatTranslated.Windows;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.Memory;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatTranslated
{
    public sealed class Plugin : IDalamudPlugin
    {
        public static string Name => "ChatTranslated";
        private const string CommandName = "/pchat";

        private readonly MenuItem contextMenuItem;

        public WindowSystem WindowSystem { get; } = new("ChatTranslated");

        public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
        {
            _ = pluginInterface.Create<Service>();

            Service.pluginInterface = pluginInterface;
            Service.commandManager = commandManager;

            Service.configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            Service.plugin = this;

            Service.configWindow = new ConfigWindow(this);
            Service.mainWindow = new MainWindow(this);

            WindowSystem.AddWindow(Service.configWindow);
            WindowSystem.AddWindow(Service.mainWindow);

            pluginInterface.UiBuilder.Draw += DrawUI;
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
                HelpMessage = "Open Chat Translated main window. '/pchat config' to open config window."
            });

            ConfigWindow.SetLanguageCulture(Service.configuration.SelectedPluginLanguage);

            // initialize chat channels
            Service.configuration.SelectedChatTypes ??= ConfigWindow.genericChatTypes;

            // settings migration
            if (Service.configuration.Version != 5)
            {
                // migrate channel settings
                var _ChatTypes = Service.configuration.SelectedChatTypes;
                Service.configuration.SelectedChatTypes = [];
                _ChatTypes.ForEach(type =>
                {
                    if (!Service.configuration.SelectedChatTypes.Contains(type))
                        Service.configuration.SelectedChatTypes.Add(type);
                });
                Service.configuration.Version = 5;
                Service.configuration.Save();
            }

            if (Service.configuration.Proxy_Url == "https://cfv5.kelpcc.com")
            {
                Service.configuration.Proxy_Url = "https://cfv5.kelpcc.com";
                Service.configuration.Save();
            }
        }

        private void OnContextMenuOpened(IMenuOpenedArgs args)
        {
            if (args.AddonName == "LookingForGroupDetail")
                args.AddMenuItem(contextMenuItem);
        }

        private unsafe void TranslatePF(IMenuItemClickedArgs args)
        {
            AddonLookingForGroupDetail* PfAddonPtr = (AddonLookingForGroupDetail*)args.AddonPtr;
            string description = PfAddonPtr->DescriptionString.ToString();

            // fix weird characters in pf description
            description = description
                .Replace("\u0002\u0012\u0002\u0037\u0003", " \uE040 ")
                .Replace("\u0002\u0012\u0002\u0038\u0003", " \uE041 ");

            Message PFmessage = new Message("PF", MessageSource.PartyFinder, description);

            string category = PfAddonPtr->CategoriesString.ToString();
            category = string.Join(" ", Regex.Matches(category, @"\[[^\]]+\]")
                                             .Cast<Match>()
                                             .Select(m => m.Value));
            if (string.IsNullOrWhiteSpace(category))
            {
                category = "null";
            }

            byte* dutyText = PfAddonPtr->DutyNameTextNode->GetText();
            string duty = MemoryHelper.ReadSeStringNullTerminated((nint)dutyText).TextValue;
            if (string.IsNullOrWhiteSpace(duty))
            {
                duty = "null";
            }

            string context = $"Tags: {category}, Duty: {duty}";
            PFmessage.Context = context;

            Task.Run(() => TranslatePFAsync(PFmessage));
        }

        private async Task TranslatePFAsync(Message message)
        {
            await Translator.TranslateMessage(message);
            ChatHandler.OutputMessage(message);
        }

        public static void OutputChatLine(XivChatType type, string sender, string message)
        {
            var sb = new SeStringBuilder();
            if (!Service.configuration.ChatIntegration_ShowColoredText)
                sb.AddUiForegroundOff();
            sb.Append(message);

            Service.chatGui.Print(new XivChatEntry
            {
                Type = type,
                Name = new SeString(new PlayerPayload("[CT] " + sender, 0)), // use player payload to fake chat bubbles
                Message = sb.Build()
            });
        }

        public static void OutputChatLine(string message)
        {
            SeStringBuilder sb = new();
            sb.AddUiForeground("[CT] ", 58).Append(message);

            Service.chatGui.Print(new XivChatEntry { Message = sb.BuiltString });
        }

        public void Dispose()
        {
            WindowSystem?.RemoveAllWindows();

            Service.chatHandler?.Dispose();
            Service.commandManager?.RemoveHandler(CommandName);
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
            }

            Service.mainWindow.IsOpen = true;
        }

        private void DrawUI()
        {
            WindowSystem.Draw();
        }

        public static void DrawConfigUI()
        {
            Service.configWindow.IsOpen = true;
        }

        public static void DrawMainUI()
        {
            Service.mainWindow.IsOpen = true;
        }
    }
}
