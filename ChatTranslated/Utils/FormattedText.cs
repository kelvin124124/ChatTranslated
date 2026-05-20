using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace ChatTranslated.Utils;

internal static partial class FormattedText
{
    private const string OpenTag = "[hl]";
    private const string CloseTag = "[/hl]";

    private static readonly Vector4 HighlightColor = new(1.00f, 0.45f, 0.25f, 1.0f);

    [GeneratedRegex(@"\s+|\S+")]
    private static partial Regex TokenRegex();

    public static string Strip(string text) =>
        text.Replace(OpenTag, "").Replace(CloseTag, "");

    public static void Draw(string text)
    {
        float maxWidth = ImGui.GetContentRegionAvail().X;
        uint disabled = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        uint accent = ImGui.GetColorU32(HighlightColor);
        float x = 0;
        bool firstOnLine = true;

        foreach (var (segText, highlighted) in Parse(text))
        {
            uint color = highlighted ? accent : disabled;
            foreach (var token in TokenRegex().Matches(segText).Select(m => m.Value))
            {
                float w = ImGui.CalcTextSize(token).X;

                if (!firstOnLine && x + w > maxWidth)
                {
                    x = 0;
                    firstOnLine = true;
                    if (string.IsNullOrWhiteSpace(token)) continue;
                }

                if (!firstOnLine) ImGui.SameLine(0, 0);
                ImGui.PushStyleColor(ImGuiCol.Text, color);
                ImGui.TextUnformatted(token);
                ImGui.PopStyleColor();

                if (token.Contains('\n'))
                {
                    x = 0;
                    firstOnLine = true;
                }
                else
                {
                    x += w;
                    firstOnLine = false;
                }
            }
        }
    }

    private static IEnumerable<(string Text, bool Highlighted)> Parse(string text)
    {
        int pos = 0;
        while (pos < text.Length)
        {
            int start = text.IndexOf(OpenTag, pos, StringComparison.Ordinal);
            if (start < 0) { yield return (text[pos..], false); yield break; }
            if (start > pos) yield return (text[pos..start], false);

            int end = text.IndexOf(CloseTag, start, StringComparison.Ordinal);
            if (end < 0) { yield return (text[(start + OpenTag.Length)..], false); yield break; }

            yield return (text[(start + OpenTag.Length)..end], true);
            pos = end + CloseTag.Length;
        }
    }
}
