using Dalamud.Plugin;
using System;
using System.Linq;

namespace ChatTranslated.Utils;

// Chat 2 draws the log with its own font, so it can show what the game font cannot.
internal static class ChatTwoDetector
{
    private const string ChatTwoInternalName = "ChatTwo";

    // older builds lack the glyph range, so they need folding too
    private static readonly Version MinimumGlyphCoverage = new(1, 40, 7);

    public static bool IsLoaded { get; private set; }
    public static bool HasGlyphCoverage { get; private set; }

    public static void Register()
    {
        Service.pluginInterface.ActivePluginsChanged += OnActivePluginsChanged;
        Refresh();
    }

    public static void Unregister() => Service.pluginInterface.ActivePluginsChanged -= OnActivePluginsChanged;

    private static void OnActivePluginsChanged(IActivePluginsChangedEventArgs args) => Refresh();

    private static void Refresh()
    {
        var chatTwo = Service.pluginInterface.InstalledPlugins
            .FirstOrDefault(plugin => plugin.InternalName == ChatTwoInternalName && plugin.IsLoaded);

        IsLoaded = chatTwo != null;
        // a local build keeps the old version number, so trust whoever compiled it
        HasGlyphCoverage = chatTwo != null && (chatTwo.Version >= MinimumGlyphCoverage || chatTwo.IsDev);

        Service.pluginLog.Information(
            $"[ChatTwoDetector] Chat 2 loaded: {IsLoaded}, glyph coverage: {HasGlyphCoverage}");
    }
}
