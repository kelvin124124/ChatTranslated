using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatTranslated.Windows
{
    public partial class MainWindow : Window, IDisposable
    {
        private readonly string[] languages = ["Japanese", "English", "German", "French"];
        internal string outputText = "";
        private string cleanOutputText = "";
        internal string inputText = "";
        private float lastOutputFieldWidth = 0;
        private bool isOutputFieldWrapped = false;

        [GeneratedRegex(@"(\p{IsCJKUnifiedIdeographs}|[^\x00-\x7F]|\w+|\s+|[^\w\s])")]
        private static partial Regex WordRegex();

        public MainWindow(Plugin plugin) : base("Chat Translated", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            Size = new Vector2(360, 220);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public void Dispose() { Service.fontManager.ExtendedFontHandle?.Pop(); }

        public override void Draw()
        {
            using (Service.fontManager.ExtendedFontHandle?.Push())
            {
                DrawOutputField();
                DrawLanguageSelector();
                DrawInputField();
            }
        }

        private void DrawOutputField()
        {
            ImGui.BeginChild("outputField", new Vector2(-1, -55), false);
            float outputFieldWidth = ImGui.GetContentRegionAvail().X;

            if (!isOutputFieldWrapped || Math.Abs(outputFieldWidth - lastOutputFieldWidth) > 0.1f)
            {
                lastOutputFieldWidth = outputFieldWidth;
                string wrappedText = cleanOutputText;
                AddSoftReturnsToText(ref wrappedText, outputFieldWidth);
                outputText = wrappedText;
                isOutputFieldWrapped = true;
            }

            ImGui.InputTextMultiline("##output", ref outputText, 0, new Vector2(-1, -1), ImGuiInputTextFlags.ReadOnly);
            ImGui.SetScrollHereY(1.0f);
            ImGui.EndChild();

            if (ImGui.IsKeyPressed(ImGuiKey.C) && (ImGui.GetIO().KeyCtrl || ImGui.GetIO().KeySuper))
            {
                ImGui.SetClipboardText(RemoveSoftReturns(ImGui.GetClipboardText()));
            }

            ImGui.Separator();
        }

        private void DrawLanguageSelector()
        {
            int currentLanguageIndex = Array.IndexOf(languages, Service.configuration.SelectedMainWindowTargetLanguage);
            if (currentLanguageIndex == -1) currentLanguageIndex = 0;

            string[] localizedLanguages = languages.Select(lang => lang.GetLocalization()).ToArray();
            if (ImGui.Combo("##LanguageCombo", ref currentLanguageIndex, localizedLanguages, languages.Length))
            {
                Service.configuration.SelectedMainWindowTargetLanguage = languages[currentLanguageIndex];
                Service.configuration.Save();
                TranslationHandler.ClearTranslationCache();
            }
        }

        private void DrawInputField()
        {
            ImGui.InputText("##input", ref inputText, 500);

            ImGui.SameLine();
            if (ImGui.Button("Translate".GetLocalization()))
            {
                ProcessInput(inputText);
                inputText = "";
            }

            ImGui.SameLine();
            ImGui.TextDisabled("?");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("Translate button only prints translated text in the main window.".GetLocalization());
                ImGui.Text("It does not send translated text in chat or make it visible to other players.".GetLocalization());
                ImGui.EndTooltip();
            }
        }

        private static void ProcessInput(string input) => Task.Run(() => TranslationHandler.TranslateMainWindowMessage(input));

        public void PrintToOutput(string message)
        {
            string timeStampedMessage = $"[{DateTime.Now:HH:mm}] {message}\n";
            cleanOutputText += timeStampedMessage;
            isOutputFieldWrapped = false; // Force re-wrap on next Draw
        }

        private static void AddSoftReturnsToText(ref string str, float multilineWidth)
        {
            var lines = str.Split('\n');
            str = string.Join("\r\n", lines.SelectMany(line => WrapLine(line, multilineWidth)));
        }

        private static IEnumerable<string> WrapLine(string line, float multilineWidth)
        {
            var wrappedLine = string.Empty;
            var words = WordRegex().Matches(line).Cast<Match>().Select(m => m.Value);

            foreach (var word in words)
            {
                if (ImGui.CalcTextSize(wrappedLine + word).X + 20f > multilineWidth)
                {
                    yield return wrappedLine.TrimEnd();
                    wrappedLine = string.Empty;
                }
                wrappedLine += word;
            }
            yield return wrappedLine.TrimEnd();
        }

        private static string RemoveSoftReturns(string str) => str.Replace("\r\n", string.Empty);
    }
}
