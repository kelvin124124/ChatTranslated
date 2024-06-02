
using ChatTranslated.Windows;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ChatTranslated.Utils
{
    internal class Service
    {
        internal static Plugin plugin { get; set; } = null!;
        internal static ConfigWindow configWindow { get; set; } = null!;
        internal static Configuration configuration { get; set; } = null!;
        internal static MainWindow mainWindow { get; set; } = null!;
        internal static ChatHandler chatHandler { get; set; } = null!;

        [PluginService] public static DalamudPluginInterface pluginInterface { get; set; } = null!;
        [PluginService] public static IChatGui chatGui { get; private set; } = null!;
        [PluginService] public static IContextMenu contextMenu { get; private set; } = null!;
        [PluginService] public static ICondition condition { get; private set; } = null!;
        [PluginService] public static IPluginLog pluginLog { get; private set; } = null!;
        [PluginService] public static IClientState clientState { get; private set; } = null!;
        [PluginService] public static ICommandManager commandManager { get; set; } = null!;
    }
}
