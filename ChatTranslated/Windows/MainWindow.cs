using ChatTranslated.Chat;
using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatTranslated.Windows
{
    public partial class MainWindow : Window
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

        public override void Draw()
        {
            float scale = ImGuiHelpers.GlobalScale;
            DrawOutputField(scale);
            DrawInputField(scale);
        }

        private void DrawOutputField(float scale)
        {
            ImGui.BeginChild("outputField", new Vector2(-1, -55 * scale), false);
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
            ImGui.Separator();


            if (ImGui.IsKeyPressed(ImGuiKey.C) && (ImGui.GetIO().KeyCtrl || ImGui.GetIO().KeySuper))
            {
                Task.Run(() =>
                {
                    string clipboardText = ImGui.GetClipboardText();
                    string cleanedText = RemoveSoftReturns(clipboardText);
                    if (clipboardText != cleanedText)
                    {
                        ImGui.SetClipboardText(cleanedText);
                    }
                });
            }
        }

        private void DrawInputField(float scale)
        {
            DrawLanguageSelector();

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - (100 * scale));
            ImGui.InputText("##input", ref inputText, 500);
            ImGui.SameLine();
            if (ImGui.Button(Resources.Translate, new Vector2(60 * scale, 0)))
            {
                ProcessInput(inputText);
                inputText = "";
            }
            ImGui.SameLine();
            ImGui.TextDisabled("?");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(Resources.TranslateButtonTooltip);
        }

        private void DrawLanguageSelector()
        {
            int currentLanguageIndex = Array.IndexOf(languages, Service.configuration.SelectedMainWindowTargetLanguage);
            if (currentLanguageIndex == -1) currentLanguageIndex = 0;

            string[] localizedLanguages = languages.Select(lang => Resources.ResourceManager.GetString(lang, Resources.Culture) ?? lang).ToArray();
            if (ImGui.Combo("##LanguageCombo", ref currentLanguageIndex, localizedLanguages, languages.Length))
            {
                Service.configuration.SelectedMainWindowTargetLanguage = languages[currentLanguageIndex];
                Translator.ClearTranslationCache();
                Service.configuration.Save();
            }
        }

        private static void ProcessInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return;

            Message message = new Message(null!, MessageSource.MainWindow, input);
            message.Context = "null";
            Task.Run(() => ProcessInputAsync(message));
        }

        private static async void ProcessInputAsync(Message message)
        {
            var translatedMessage = await Translator.TranslateMessage(message, Service.configuration.SelectedMainWindowTargetLanguage);

            if (translatedMessage.TranslatedContent == null)
                Service.mainWindow.PrintToOutput("[CT] Failed to process message.");

            var reverseTranslationResult = await MachineTranslate.Translate(translatedMessage.TranslatedContent!, Service.configuration.SelectedPluginLanguage);

            string output = $"\n Original:\n {translatedMessage.OriginalContent}" +
                $" \nTranslated Content:\n  {translatedMessage.TranslatedContent}" +
                $" \nReverse Translation:\n  {reverseTranslationResult.Item1}";

            Service.mainWindow.PrintToOutput(output);
        }

        public void PrintToOutput(string message)
        {
            string timeStampedMessage = $"[{DateTime.Now:HH:mm}] {message}\n";
            cleanOutputText += timeStampedMessage;
            isOutputFieldWrapped = false; // Force re-wrap on next Draw
        }

        private static void AddSoftReturnsToText(ref string str, float multilineWidth)
        {
            var lines = str.Split('\n');
            var wrappedLines = new StringBuilder();
            foreach (var line in lines)
            {
                var wrappedLine = WrapLine(line, multilineWidth);
                wrappedLines.AppendLine(wrappedLine);
            }
            str = wrappedLines.ToString().TrimEnd();
        }

        private static string WrapLine(string line, float multilineWidth)
        {
            var wrappedLine = new StringBuilder();
            var words = WordRegex().Matches(line).Cast<Match>().Select(m => m.Value);

            foreach (var word in words)
            {
                if (ImGui.CalcTextSize(wrappedLine + word).X + 20f > multilineWidth)
                {
                    wrappedLine.AppendLine();
                }
                wrappedLine.Append(word);
            }
            return wrappedLine.ToString().TrimEnd();
        }

        private static string RemoveSoftReturns(string str) => str.Replace("\r\n", string.Empty);
    }
}
