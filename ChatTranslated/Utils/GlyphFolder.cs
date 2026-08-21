using Dalamud.Interface.GameFonts;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChatTranslated.Utils;

// The game draws anything missing from AXIS as '=', so swap those letters for the closest match.
internal static class GlyphFolder
{
    private const string AxisPath = "common/font/axis_12.fdt";

    // no useful decomposition, mapped by hand
    private static readonly FrozenDictionary<char, string> Replacements = new Dictionary<char, string>
    {
        ['ß'] = "ss", ['æ'] = "ae", ['Æ'] = "AE", ['œ'] = "oe", ['Œ'] = "OE",
        ['ø'] = "o",  ['Ø'] = "O",  ['đ'] = "d",  ['Đ'] = "D",  ['ð'] = "d",
        ['Ð'] = "D",  ['þ'] = "th", ['Þ'] = "Th", ['ł'] = "l",  ['Ł'] = "L",
        ['ı'] = "i",  ['İ'] = "I",  ['ħ'] = "h",  ['Ħ'] = "H",  ['ŋ'] = "n",
        ['Ŋ'] = "N",  ['ŧ'] = "t",  ['Ŧ'] = "T",  ['ĸ'] = "k",  ['ſ'] = "s",
        ['ƒ'] = "f",
    }.ToFrozenDictionary();

    private static readonly Lazy<FrozenSet<char>> SupportedChars = new(LoadAxisGlyphs);

    private static FrozenSet<char> LoadAxisGlyphs()
    {
        try
        {
            var file = Service.dataManager.GetFile(AxisPath);
            if (file == null)
            {
                Service.pluginLog.Warning($"[GlyphFolder] {AxisPath} not found, folding disabled.");
                return FrozenSet<char>.Empty;
            }

            var chars = new HashSet<char>();
            foreach (var glyph in new FdtReader(file.Data).Glyphs)
                if (glyph.CharInt <= char.MaxValue)
                    chars.Add(glyph.Char);

            Service.pluginLog.Information($"[GlyphFolder] Loaded {chars.Count} game glyphs.");
            return chars.ToFrozenSet();
        }
        catch (Exception ex)
        {
            Service.pluginLog.Warning($"[GlyphFolder] Failed to read game glyphs, folding disabled: {ex.Message}");
            return FrozenSet<char>.Empty;
        }
    }

    // load up front so the first translation does not pay for it
    public static void Warmup() => _ = SupportedChars.Value;

    // letters with no sensible substitute are left untouched
    public static string Fold(string text)
    {
        if (string.IsNullOrEmpty(text) || SupportedChars.Value.Count == 0)
            return text;

        StringBuilder? sb = null;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (IsRenderable(c))
            {
                sb?.Append(c);
                continue;
            }

            var replacement = Substitute(c);
            if (replacement == null)
            {
                sb?.Append(c);
                continue;
            }

            sb ??= new StringBuilder(text.Length + 8).Append(text, 0, i);
            sb.Append(replacement);
        }

        return sb?.ToString() ?? text;
    }

    // private use area holds the game's own icons
    private static bool IsRenderable(char c) =>
        c < 0x80 || char.IsWhiteSpace(c) || c is >= '\uE000' and <= '\uF8FF' || SupportedChars.Value.Contains(c);

    private static string? Substitute(char c)
    {
        if (Replacements.TryGetValue(c, out var mapped))
            return IsRenderable(mapped) ? mapped : null;

        // drop the accent, keep the base letter
        var sb = new StringBuilder(4);
        foreach (var d in c.ToString().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(d) != UnicodeCategory.NonSpacingMark)
                sb.Append(d);

        if (sb.Length == 0 || (sb.Length == 1 && sb[0] == c))
            return null;

        var stripped = sb.ToString();
        return IsRenderable(stripped) ? stripped : null;
    }

    private static bool IsRenderable(string text)
    {
        foreach (var c in text)
            if (!IsRenderable(c))
                return false;
        return true;
    }
}
