using ChatTranslated.Utils;
using ChatTranslated.Windows;
using Dalamud.ContextMenu;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Threading.Tasks;

namespace ChatTranslated
{
    public sealed class Plugin : IDalamudPlugin
    {
        public static string Name => "ChatTranslated";
        private const string CommandName = "/pchat";

        public readonly DalamudContextMenu contextMenu;
        private readonly GameObjectContextMenuItem gameObjectContextMenuItem;

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

            Service.translator = new Translator();
            Service.chatHandler = new ChatHandler();

            contextMenu = new DalamudContextMenu(pluginInterface);
            gameObjectContextMenuItem = new GameObjectContextMenuItem(
                new SeString(new TextPayload("Translate")), TranslatePF, true);
            contextMenu.OnOpenGameObjectContextMenu += OpenGameObjectContextMenu;

            Service.commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Chat Translated main window. \"/pchat config\" open config window."
            });
        }

        private unsafe void OpenGameObjectContextMenu(GameObjectContextMenuOpenArgs args)
        {
            if (args.ParentAddonName == "LookingForGroupDetail")
                args.AddCustomItem(gameObjectContextMenuItem);
        }

        private void TranslatePF(GameObjectContextMenuItemSelectedArgs args)
        {
            string message = ChatHandler.Sanitize(args?.Text?.ToString() ?? "null");
            Task.Run(() => Translator.TranslateChat("PF", message));
        }

        public static void OutputChatLine(string sender, string message, XivChatType type = XivChatType.Say)
        {
            Service.chatGui.Print(new XivChatEntry
            {
                Type = type,
                Name = "[CT] " + sender ?? "<empty>",
                Message = new SeStringBuilder().AddUiForeground(50).Append(message ?? "<empty>").Build()
            });
        }

        public static void OutputChatLine(string message)
        {
            SeStringBuilder sb = new();
            sb.AddUiForeground("[CT] ", 58).Append(message ?? "<empty>");

            Service.chatGui.Print(new XivChatEntry { Message = sb.BuiltString });
        }

        public void Dispose()
        {
            WindowSystem?.RemoveAllWindows();

            Service.chatHandler?.Dispose();

            contextMenu.OnOpenGameObjectContextMenu -= OpenGameObjectContextMenu;
            contextMenu.Dispose();

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
                    Service.configuration.ChatIntegration = true;
                    Service.configuration.Save();
                    return;
                case "off":
                    Service.configuration.ChatIntegration = false;
                    Service.configuration.Save();
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
    }
}
