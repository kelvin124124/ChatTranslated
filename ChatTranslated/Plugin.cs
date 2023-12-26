using ChatTranslated.Utils;
using ChatTranslated.Windows;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace ChatTranslated
{
    public sealed class Plugin : IDalamudPlugin
    {
        public static string Name => "ChatTranslated";
        private const string CommandName = "/pchat";
        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("ChatTranslated");

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
        {
            Service.pluginInterface = pluginInterface;

            Service.configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Service.configuration.Initialize(pluginInterface);

            _ = pluginInterface.Create<Service>();
            Service.plugin = this;
            Service.configWindow = new ConfigWindow(this);
            Service.mainWindow = new MainWindow(this);

            WindowSystem.AddWindow(Service.configWindow);
            WindowSystem.AddWindow(Service.mainWindow);

            pluginInterface.UiBuilder.Draw += DrawUI;
            pluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            Service.commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Chat Translated main window."
            });
        }

        public static void OutputChatLine(SeString message)
        {
            var sb = new SeStringBuilder().AddUiForeground("[CT] ", 58).Append(message);
            Service.chatGui.Print(new XivChatEntry { Message = sb.BuiltString });
        }

        public void Dispose()
        {
            WindowSystem.RemoveAllWindows();

            Service.configWindow.Dispose();
            Service.mainWindow.Dispose();
            Service.commandManager.RemoveHandler(CommandName);
        }

        private void OnCommand(string command, string args)
        {
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
