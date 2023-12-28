using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace ChatTranslated.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    private string outputText = ""; // Holds the text for the output field
    private string inputText = "";  // Holds the text for the input field

    public MainWindow(Plugin plugin) : base(
        "Chat Translated",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(360, 250);
        SizeCondition = ImGuiCond.Always;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public override void Draw()
    {
        // Output text field
        ImGui.BeginChild("outputField", new Vector2(-1, -60), false, ImGuiWindowFlags.HorizontalScrollbar);
        ImGui.InputTextMultiline("##output", ref outputText, 10000, new Vector2(-1, -1), ImGuiInputTextFlags.ReadOnly);
        ImGui.EndChild();

        // Input text field with send button
        ImGui.InputText("##input", ref inputText, 100);
        ImGui.SameLine();
        if (ImGui.Button("Send"))
        {
            ProcessInput(inputText);
            inputText = ""; // Clear the input field after sending
        }
    }

    private void ProcessInput(string input)
    {
        // Placeholder for input processing logic
        // For now, just prints the input text to the output window
        PrintToOutput($"You entered: {input}\n");
    }

    public void PrintToOutput(string message)
    {
        // Append the given text to the output field
        outputText = outputText + message + "\n";
    }
}
