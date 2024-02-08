using ChatTranslated.Utils;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace ChatTranslated.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly string[] languages = { "Japanese", "English", "German", "French" };

    internal string outputText = ""; // Holds the text for the output field
    internal string inputText = "";  // Holds the text for the input field

    public MainWindow(Plugin plugin) : base(
        "Chat Translated",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(360, 200);
        SizeCondition = ImGuiCond.Always;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public override void Draw()
    {
        // Output text field
        ImGui.BeginChild("outputField", new Vector2(-1, -30), false, ImGuiWindowFlags.HorizontalScrollbar);
        ImGui.InputTextMultiline("##output", ref outputText, 50000, new Vector2(-1, -1), ImGuiInputTextFlags.ReadOnly);
        ImGui.SetScrollHereY(1.0f); // Scroll to bottom
        ImGui.EndChild();

        ImGui.Separator();

        // Input text field
        ImGui.AlignTextToFramePadding();

        int currentLanguageIndex = Array.IndexOf(languages, Service.configuration.SelectedChatLanguage);
        if (currentLanguageIndex == -1) currentLanguageIndex = 0;

        if (ImGui.Combo("##LanguageCombo", ref currentLanguageIndex, languages, languages.Length))
        {
            Service.configuration.SelectedMainWindowLanguage = languages[currentLanguageIndex];
            Service.configuration.Save();
        }

        ImGui.InputText("##input", ref inputText, 100);
        ImGui.SameLine();

        if (ImGui.Button("Translate"))
        {
            ProcessInput(inputText);
            inputText = "";
        }
    }

    private static void ProcessInput(string input)
    {
        Task.Run(() => Translator.TranslateMainWindow(input));
    }

    public void PrintToOutput(string message)
    {
        // Append the given text to the output field
        outputText = outputText + $"[{DateTime.Now:HH:mm}] " + message + "\n";
    }
}
