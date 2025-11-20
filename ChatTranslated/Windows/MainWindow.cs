using ChatTranslated.Chat;
using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using System;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ChatTranslated.Windows
{
    public partial class MainWindow : Window
    {
        private static readonly StringBuilder sb = new();
        private static readonly Lock sbLock = new();
        private readonly string[] languages = ["Japanese", "English", "German", "French"];

        internal string outputText = "";
        internal string inputText = "";
        private float lastOutputFieldWidth = 0;
        private int lastContentHash = 0;

        [GeneratedRegex(@"(\p{IsCJKUnifiedIdeographs}|[^\x00-\x7F]|\w+|\s+|[^\w\s])")]
        private static partial Regex WordRegex();

        public MainWindow(Plugin plugin) : base("ChatTranslated", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
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

            string currentContent;
            int currentHash;

            lock (sbLock)
            {
                currentContent = sb.ToString();
                currentHash = currentContent.GetHashCode();
            }

            if (currentHash != lastContentHash || Math.Abs(outputFieldWidth - lastOutputFieldWidth) > 0.1f)
            {
                lastOutputFieldWidth = outputFieldWidth;
                lastContentHash = currentHash;
                outputText = currentContent;
                AddSoftReturnsToText(ref outputText, outputFieldWidth);
            }

            int bufferSize = Math.Max(outputText.Length + 4096, 8192);
            ImGui.InputTextMultiline("##ChatTranslated_MainWindow_output", ref outputText, bufferSize, new Vector2(-1, -1), ImGuiInputTextFlags.ReadOnly);
            ImGui.EndChild();
            ImGui.Separator();

            if (ImGui.IsKeyPressed(ImGuiKey.C) && (ImGui.GetIO().KeyCtrl || ImGui.GetIO().KeySuper))
            {
                Task.Run(() =>
                {
                    string clipboardText = ImGui.GetClipboardText();
                    string cleanedText = RemoveSoftReturns(clipboardText);
                    if (clipboardText != cleanedText)
                        ImGui.SetClipboardText(cleanedText);
                });
            }
        }

        private void DrawInputField(float scale)
        {
            // Language selector
            int langIndex = Math.Max(0, Array.IndexOf(languages, Service.configuration.SelectedMainWindowTargetLanguage));
            string[] localizedLangs = languages.Select(l => Resources.ResourceManager.GetString(l, Resources.Culture) ?? l).ToArray();
            if (ImGui.Combo("##LanguageCombo", ref langIndex, localizedLangs, languages.Length))
            {
                Service.configuration.SelectedMainWindowTargetLanguage = languages[langIndex];
                TranslationHandler.ClearTranslationCache();
                Service.configuration.Save();
            }

            // Input field
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - (100 * scale));
            ImGui.InputText("##input", ref inputText, 500);
            ImGui.SameLine();
            if (ImGui.Button(Resources.Translate, new Vector2(60 * scale, 0)))
            {
                if (!string.IsNullOrWhiteSpace(inputText))
                {
                    var message = new Message(null!, MessageSource.MainWindow, inputText) { Context = "null" };
                    Task.Run(() => ProcessInputAsync(message));
                    inputText = "";
                }
            }
            ImGui.SameLine();
            ImGui.TextDisabled("?");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(Resources.TranslateButtonTooltip);
        }

        private static async void ProcessInputAsync(Message message)
        {
            var translatedMessage = await TranslationHandler.TranslateMessage(message, Service.configuration.SelectedMainWindowTargetLanguage);

            if (translatedMessage.TranslatedContent == null)
            {
                Service.mainWindow.PrintToOutput("[CT] Failed to process message.");
                return;
            }

            var reverseTranslationResult = await MachineTranslate.Translate(translatedMessage.TranslatedContent, Service.configuration.SelectedPluginLanguage);
            Service.mainWindow.PrintToOutput($"\n Original:\n        {translatedMessage.OriginalContent}" +
                $" \nTranslated Content:\n        {translatedMessage.TranslatedContent}" +
                $" \nReverse Translation:\n        {reverseTranslationResult.Item1}");
        }

        public void PrintToOutput(string message)
        {
            lock (sbLock)
            {
                sb.Append($"[{DateTime.Now:HH:mm}] {message}\n");
            }
            lastContentHash = 0;
        }

        private static void AddSoftReturnsToText(ref string str, float multilineWidth)
        {
            var sb = new StringBuilder();
            foreach (var line in str.Split('\n'))
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(WrapLine(line, multilineWidth));
            }
            str = sb.ToString();
        }

        private static string WrapLine(string line, float multilineWidth)
        {
            var wrappedLine = new StringBuilder();
            foreach (var word in WordRegex().Matches(line).Cast<Match>().Select(m => m.Value))
            {
                if (ImGui.CalcTextSize(wrappedLine + word).X + 20f > multilineWidth)
                    wrappedLine.AppendLine();
                wrappedLine.Append(word);
            }
            return wrappedLine.ToString().TrimEnd();
        }

        private static string RemoveSoftReturns(string str) => str.Replace("\r\n", string.Empty);
    }
}
