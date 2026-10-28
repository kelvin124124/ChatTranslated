using ChatTranslated.Chat;
using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using ChatTranslated.Windows.ConfigTabs;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ChatTranslated.Windows;

public partial class MainWindow : Window
{
    private const int MaxOutputLines = 1000;

    // inserted only to wrap the read-only log; copying strips them again (real line ends are "\n")
    private const string SoftBreak = "\r\n";

    private static readonly string[] languages = LanguagesTab.SupportedLanguages;

    // filled from any thread, drawn on the framework thread
    private readonly Queue<string> outputLines = new();
    private readonly Lock outputLock = new();
    private int outputVersion;

    private string outputText = "";
    private string inputText = "";
    private string translatedText = "";
    private string reverseTranslatedText = "";
    private float lastOutputFieldWidth = 0;
    private int renderedVersion = -1;

    [GeneratedRegex(@"(\p{IsCJKUnifiedIdeographs}|[^\x00-\x7F]|\w+|\s+|[^\w\s])")]
    private static partial Regex WordRegex();

    public MainWindow() : base("ChatTranslated", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(360, 220);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        float scale = ImGuiHelpers.GlobalScale;
        DrawOutputField(scale);
        DrawInputField(scale);
    }

    private void DrawOutputField(float scale)
    {
        using (ImRaii.Child("outputField", new Vector2(-1, -68 * scale), false))
        {
            float outputFieldWidth = ImGui.GetContentRegionAvail().X;
            if (renderedVersion != outputVersion || Math.Abs(outputFieldWidth - lastOutputFieldWidth) > 0.1f)
            {
                string content;
                lock (outputLock)
                {
                    renderedVersion = outputVersion;
                    content = string.Join('\n', outputLines);
                }

                lastOutputFieldWidth = outputFieldWidth;
                outputText = string.Join('\n', content.Split('\n').Select(line => WrapLine(line, outputFieldWidth)));
            }

            int bufferSize = Math.Max(outputText.Length + 4096, 8192);
            ImGui.InputTextMultiline("##ChatTranslated_MainWindow_output", ref outputText, bufferSize, new Vector2(-1, -1), ImGuiInputTextFlags.ReadOnly);

            // the log just copied the selection; take the wrapping back out
            if (ImGui.IsItemActive() && ImGui.IsKeyPressed(ImGuiKey.C) && (ImGui.GetIO().KeyCtrl || ImGui.GetIO().KeySuper))
            {
                string clipboardText = ImGui.GetClipboardText();
                if (clipboardText.Contains(SoftBreak))
                    ImGui.SetClipboardText(clipboardText.Replace(SoftBreak, string.Empty));
            }
        }
        ImGui.Separator();
    }

    private void DrawInputField(float scale)
    {
        int langIndex = Array.IndexOf(languages, Service.configuration.SelectedMainWindowTargetLanguage);
        if (langIndex < 0)
        {
            langIndex = 0;
            Service.configuration.SelectedMainWindowTargetLanguage = languages[0];
            Service.configuration.Save();
        }
        string[] localizedLangs = [.. languages.Select(Loc.Get)];
        ImGui.SetNextItemWidth(140 * scale);
        if (ImGui.Combo("##LanguageCombo", ref langIndex, localizedLangs, 7))
        {
            Service.configuration.SelectedMainWindowTargetLanguage = languages[langIndex];
            TranslationHandler.ClearTranslationCache();
            Service.configuration.Save();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - (120 * scale));
        ImGui.InputText("##input", ref inputText, 500);
        ImGui.SameLine();
        if (ImGui.Button(Resources.Translate, new Vector2(60 * scale, 0)))
        {
            if (!string.IsNullOrWhiteSpace(inputText))
            {
                var message = new Message(null!, MessageSource.MainWindow, inputText) { Context = "null" };
                _ = ProcessInputAsync(message);
                inputText = "";
            }
        }
        ImGui.SameLine();
        if (ImGui.Button(Resources.Copy, new Vector2(45 * scale, 0)))
        {
            if (!string.IsNullOrEmpty(translatedText))
                ImGui.SetClipboardText(translatedText);
        }

        ImGui.TextDisabled("TL:");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(Resources.Main_Window_Translated_text);
        ImGui.SameLine();
        ImGui.TextUnformatted(translatedText);

        ImGui.TextDisabled("RT:");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(Resources.Main_Window_Reverse_translation);
        ImGui.SameLine();
        ImGui.TextUnformatted(reverseTranslatedText);
    }

    private async Task ProcessInputAsync(Message message)
    {
        try
        {
            var translatedMessage = await TranslationHandler.TranslateMessage(message, Service.configuration.SelectedMainWindowTargetLanguage);

            if (translatedMessage.TranslatedContent == null)
            {
                translatedText = Resources.Main_Window_Translation_Failed;
                reverseTranslatedText = "";
                return;
            }

            translatedText = translatedMessage.TranslatedContent;

            var (reverseText, _) = await MachineTranslate.Translate(translatedMessage.TranslatedContent, Service.configuration.SelectedPluginLanguage);
            reverseTranslatedText = reverseText;
        }
        catch (Exception ex)
        {
            Service.pluginLog.Warning($"[MainWindow] Translation failed: {ex.Message}");
            translatedText = Resources.Main_Window_Translation_Failed;
            reverseTranslatedText = "";
        }
    }

    public void PrintToOutput(string message)
    {
        lock (outputLock)
        {
            outputLines.Enqueue($"[{DateTime.Now:HH:mm}] {message}");
            if (outputLines.Count > MaxOutputLines)
                outputLines.Dequeue();
            outputVersion++;
        }
    }

    private static string WrapLine(string line, float multilineWidth)
    {
        var wrappedLine = new StringBuilder(line.Length + 8);
        float lineWidth = 0;
        foreach (var word in WordRegex().Matches(line).Select(m => m.Value))
        {
            float wordWidth = ImGui.CalcTextSize(word).X;
            if (lineWidth > 0 && lineWidth + wordWidth + 20f > multilineWidth)
            {
                wrappedLine.Append(SoftBreak);
                lineWidth = 0;
            }
            wrappedLine.Append(word);
            lineWidth += wordWidth;
        }
        return wrappedLine.ToString().TrimEnd();
    }
}
