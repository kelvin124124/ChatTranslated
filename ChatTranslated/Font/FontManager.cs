using ChatTranslated.Utils;
using Dalamud;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Storage.Assets;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ChatTranslated.Font
{

    // NOTE: I have no idea what I'm doing.

    public class FontManager(IDalamudAssetManager assetManager, IFontAtlasBuildToolkit fontAtlasBuildToolkit)
    {
        private readonly IDalamudAssetManager assetManager = assetManager;
        private readonly IFontAtlasBuildToolkit fontAtlasBuildToolkit = fontAtlasBuildToolkit;

        public async Task LoadFontsAsync()
        {
            ImGui.CreateContext();
            var io = ImGui.GetIO();

            var fontConfig = new ImFontConfig { MergeMode = 1 };

            var combinedRanges = CombineGlyphRanges(io.Fonts.GetGlyphRangesChineseFull(), io.Fonts.GetGlyphRangesKorean());
            var handle = GCHandle.Alloc(combinedRanges, GCHandleType.Pinned);
            var combinedRangesPtr = handle.AddrOfPinnedObject();

            try
            {
                await LoadFontAsync(DalamudAsset.NotoSansJpMedium, fontConfig, combinedRangesPtr, io);
                await LoadFontAsync(DalamudAsset.NotoSansKrRegular, fontConfig, combinedRangesPtr, io);
            } catch (Exception e)
            {
                Service.pluginLog.Warning($"Failed to load fonts: {e}");
            }
            finally
            {
                handle.Free();
            }

            io.Fonts.Build();
        }

        private async Task LoadFontAsync(DalamudAsset asset, ImFontConfig fontConfig, nint combinedRangesPtr, ImGuiIOPtr io)
        {
            using var fontStream = await assetManager.CreateStreamAsync(asset);
            using var ms = new MemoryStream();
            await fontStream.CopyToAsync(ms);
            var fontData = ms.ToArray();
            unsafe
            {
                fixed (byte* fontPtr = fontData)
                {
                    var fontDataPtr = new nint(fontPtr);
                    var font = io.Fonts.AddFontFromMemoryTTF(fontDataPtr, fontData.Length, 18.0f, nint.Zero, combinedRangesPtr);
                    var fontConfigPtr = fontAtlasBuildToolkit.FindConfigPtr(font);
                }
            }
        }

        private ushort[] CombineGlyphRanges(nint rangesChinese, nint rangesKorean)
        {
            var glyphRangeChinese = ConvertGlyphRangeToChars(rangesChinese);
            var glyphRangeKorean = ConvertGlyphRangeToChars(rangesKorean);

            var combinedRanges = new List<char>();
            combinedRanges.AddRange(glyphRangeChinese);
            combinedRanges.AddRange(glyphRangeKorean);

            return combinedRanges.ToGlyphRange();
        }

        private List<char> ConvertGlyphRangeToChars(nint glyphRangePtr)
        {
            var chars = new List<char>();

            unsafe
            {
                var p = (ushort*)glyphRangePtr.ToPointer();
                while (*p != 0)
                {
                    for (var c = *p; c <= *(p + 1); c++)
                    {
                        chars.Add((char)c);
                    }
                    p += 2;
                }
            }

            return chars;
        }
    }
}
