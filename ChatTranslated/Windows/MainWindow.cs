using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace ChatTranslated.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly string[] languages = ["Japanese", "English", "German", "French"];

    internal string outputText = ""; // Holds the text for the output field
    internal string inputText = "";  // Holds the text for the input field

    public MainWindow(Plugin plugin) : base(
        "Chat Translated", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(360, 220);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public override void Draw()
    {
        // Output text field
        ImGui.BeginChild("outputField", new Vector2(-1, -55), false, ImGuiWindowFlags.HorizontalScrollbar);
        ImGui.InputTextMultiline("##output", ref outputText, 0, new Vector2(-1, -1), ImGuiInputTextFlags.ReadOnly);
        ImGui.SetScrollHereY(1.0f); // Scroll to bottom
        ImGui.EndChild();

        ImGui.Separator();

        // Input text field
        ImGui.AlignTextToFramePadding();

        int currentLanguageIndex = Array.IndexOf(languages, Service.configuration.SelectedMainWindowTargetLanguage);
        if (currentLanguageIndex == -1) currentLanguageIndex = 0;

        string[] localizedLanguages = languages.Select(lang => lang.GetLocalization()).ToArray();
        if (ImGui.Combo("##LanguageCombo", ref currentLanguageIndex, localizedLanguages, languages.Length))
        {
            Service.configuration.SelectedMainWindowTargetLanguage = languages[currentLanguageIndex];
            Service.configuration.Save();
            TranslationHandler.ClearTranslationCache();
        }

        ImGui.InputText("##input", ref inputText, 500);

        ImGui.SameLine();
        if (ImGui.Button("Translate".GetLocalization()))
        {
            ProcessInput(inputText);
            inputText = "";
        }

        // Tooltip explaining main window function
        ImGui.SameLine();
        ImGui.TextDisabled("?");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("Translate button only print translated text in the main window.".GetLocalization());
            ImGui.Text("It does not send translated text in chat or make it visible to other players.".GetLocalization());
            ImGui.EndTooltip();
        }
    }

    private static void ProcessInput(string input)
    {
        Task.Run(() => TranslationHandler.TranslateMainWindowMessage(input));
    }

    public void PrintToOutput(string message)
    {
        // Append the given text to the output field
        outputText += $"[{DateTime.Now:HH:mm}] " + message + "\n";
    }
}
