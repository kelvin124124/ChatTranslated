using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using ChatTranslated.Windows;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using System.Threading.Tasks;

namespace ChatTranslated
{
    public sealed class Plugin : IDalamudPlugin
    {
        public static string Name => "ChatTranslated";
        private const string CommandName = "/pchat";

        private readonly MenuItem contextMenuItem;

        public WindowSystem WindowSystem { get; } = new("ChatTranslated");

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] ICommandManager commandManager)
        {
            Service.pluginInterface = pluginInterface;
            Service.commandManager = commandManager;

            Service.configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            _ = pluginInterface.Create<Service>();

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
                // still in stg
                UseDefaultPrefix = true,
                Name = "Translate",
                OnClicked = TranslatePF
            };
            Service.contextMenu.OnMenuOpened += OnContextMenuOpened;

            Service.commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Chat Translated main window. /pchat config to open config window."
            });

            if (Service.configuration.Version != 3)
            {
                OutputChatLine("Plugin has been updated to v2.0 and requires a config reset.");
                Service.configuration = new Configuration();
            }

            LocManager.LoadLocalization();
        }

        private void OnContextMenuOpened(MenuOpenedArgs args)
        {
            if (args.AddonName == "LookingForGroupDetail")
                args.AddMenuItem(contextMenuItem);
        }

        private unsafe void TranslatePF(MenuItemClickedArgs args)
        {
            AddonLookingForGroupDetail* PfAddonPtr = (AddonLookingForGroupDetail*)args.AddonPtr;
            string description = PfAddonPtr->DescriptionString.ToString();

            string message = ChatHandler.Sanitize(description ?? "null");
            Task.Run(() => TranslationHandler.TranslatePFMessage(message));
        }

        public static void OutputChatLine(XivChatType type, string sender, string message)
        {
            Service.chatGui.Print(new XivChatEntry
            {
                Type = type,
                Name = "[CT] " + sender,
                Message = new SeStringBuilder().AddUiForegroundOff().Append(message).Build()
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

            Service.configWindow.Dispose();
            Service.mainWindow.Dispose();
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
                    return;
                case "off":
                    Service.configuration.Enabled = false;
                    Service.configuration.Save();
                    return;
                case "integration":
                    Service.configuration.ChatIntegration = !Service.configuration.ChatIntegration;
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
