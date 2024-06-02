using ChatTranslated.Utils;
using Dalamud.Interface.ManagedFontAtlas;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ChatTranslated.Font
{
    public class FontManager()
    {
        internal IFontHandle? fontHandle { get; private set; } = null!;

        private static byte[] GetFont(string resourceName)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                ?? throw new Exception($"Failed to load font resource: {resourceName}");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        public void LoadFonts()
        {
            byte[]? fontFile = null;
            try
            {
                fontFile = GetFont("ChatTranslated.Font.NotoSans-Regular.ttf");
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"Failed to load font: {ex.Message}");
                return;
            }

            // Get Ranges
            var io = ImGui.GetIO();
            var ranges = new List<IntPtr> { io.Fonts.GetGlyphRangesDefault(), io.Fonts.GetGlyphRangesChineseFull(), io.Fonts.GetGlyphRangesKorean(), io.Fonts.GetGlyphRangesJapanese() };
            //var combinedRanges = BuildRange(null, [.. ranges]);

            fontHandle = Service.pluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
                e => e.OnPreBuild(
                    tk =>
                    {
                        var config = new SafeFontConfig { SizePx = Service.pluginInterface.UiBuilder.DefaultFontSpec.SizePx/* , GlyphRanges = combinedRanges */};

                        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ChatTranslated.Font.NotoSans-Regular.ttf");
                        if (stream == null)
                        {
                            Service.pluginLog.Warning("Failed to load font resource");
                            return;
                        }

                        var font = tk.AddFontFromStream(stream, config, false, "Expanded font");
                        tk.AddGameSymbol(config with { MergeFont = font });
                    }
                ));
        }

        // stolen from Chat2
        //private static unsafe ushort[] BuildRange(IReadOnlyList<ushort>? chars, params IntPtr[] ranges)
        //{
        //    var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
        //    // text
        //    foreach (var range in ranges)
        //        builder.AddRanges(range);

        //    // chars
        //    if (chars != null)
        //    {
        //        for (var i = 0; i < chars.Count; i += 2)
        //        {
        //            if (chars[i] == 0)
        //                break;

        //            for (var j = (uint)chars[i]; j <= chars[i + 1]; j++)
        //                builder.AddChar((ushort)j);
        //        }
        //    }

        //    // various symbols
        //    // French
        //    // Romanian
        //    builder.AddText("←→↑↓《》■※☀★★☆♥♡ヅツッシ☀☁☂℃℉°♀♂♠♣♦♣♧®©™€$£♯♭♪✓√◎◆◇♦■□〇●△▽▼▲‹›≤≥<«“”─＼～");
        //    builder.AddText("Œœ");
        //    builder.AddText("ĂăÂâÎîȘșȚț");

        //    // "Enclosed Alphanumerics" (partial) https://www.compart.com/en/unicode/block/U+2460
        //    for (var i = 0x2460; i <= 0x24B5; i++)
        //        builder.AddChar((char)i);

        //    builder.AddChar('⓪');
        //    return builder.BuildRangesToArray();
        //}
    }
}
