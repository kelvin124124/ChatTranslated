using ChatTranslated.Utils;
using Dalamud.Interface.ManagedFontAtlas;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ChatTranslated.Font
{
    public class FontManager : IDisposable
    {
        internal IFontHandle? ExtendedFontHandle { get; private set; } = null!;

        private static byte[] GetFont(string resourceName)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                ?? throw new Exception($"Failed to load font resource: {resourceName}");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        private static unsafe ushort[] ConvertRanges(IntPtr ranges)
        {
            var rangeList = new List<ushort>();
            var rangePtr = (ushort*)ranges;

            while (true)
            {
                ushort start = *rangePtr++;
                ushort end = *rangePtr++;

                if (start == 0 && end == 0)
                    break;

                rangeList.Add(start);
                rangeList.Add(end);
            }

            // Ensure the list is terminated with two zeros
            rangeList.Add(0);
            rangeList.Add(0);

            return [.. rangeList];
        }

        public void LoadFonts()
        {
            var io = ImGui.GetIO();

            byte[] notoSansData = GetFont("ChatTranslated.Font.NotoSans-Medium.ttf");
            byte[] scFontData = GetFont("ChatTranslated.Font.NotoSansSC-Medium.otf");
            byte[] tcFontData = GetFont("ChatTranslated.Font.NotoSansTC-Medium.otf");
            byte[] jpFontData = GetFont("ChatTranslated.Font.NotoSansJP-Medium.otf");
            byte[] krFontData = GetFont("ChatTranslated.Font.NotoSansKR-Medium.otf");

            var sizePx = Service.pluginInterface.UiBuilder.DefaultFontSpec.SizePx;

            ExtendedFontHandle = Service.pluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
                e => e.OnPreBuild(
                    tk =>
                    {
                        // Load and merge fonts sequentially
                        var config = new SafeFontConfig { SizePx = sizePx, GlyphRanges = ConvertRanges(io.Fonts.GetGlyphRangesDefault()) };
                        var font = tk.AddFontFromMemory(notoSansData, config, "NotoSans");

                        // Merge Vietnamese ranges into the base font
                        config.GlyphRanges = ConvertRanges(io.Fonts.GetGlyphRangesVietnamese());
                        config.MergeFont = font;
                        tk.AddFontFromMemory(notoSansData, config, "NotoSans-Vietnamese");

                        // Merge Chinese (Simplified) ranges
                        config.GlyphRanges = ConvertRanges(io.Fonts.GetGlyphRangesChineseFull());
                        tk.AddFontFromMemory(scFontData, config, "NotoSansSC");

                        // Merge Chinese (Traditional) ranges
                        tk.AddFontFromMemory(tcFontData, config, "NotoSansTC");

                        // Merge Japanese ranges
                        config.GlyphRanges = ConvertRanges(io.Fonts.GetGlyphRangesJapanese());
                        tk.AddFontFromMemory(jpFontData, config, "NotoSansJP");

                        // Merge Korean ranges
                        config.GlyphRanges = ConvertRanges(io.Fonts.GetGlyphRangesKorean());
                        tk.AddFontFromMemory(krFontData, config, "NotoSansKR");

                        // Add game symbols
                        tk.AddGameSymbol(config with { MergeFont = font });
                    }
                ));
            io.Fonts.Build();
        }

        public void Dispose()
        {
            // Ensure proper cleanup of the font handle
            if (ExtendedFontHandle != null)
            {
                ExtendedFontHandle.Dispose();
                ExtendedFontHandle = null;
            }
        }
    }
}
