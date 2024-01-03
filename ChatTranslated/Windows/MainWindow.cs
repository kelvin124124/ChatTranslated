using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace ChatTranslated.Windows;

public class MainWindow : Window, IDisposable
{
    private string outputText = ""; // Holds the text for the output field
    private string inputText = "";  // Holds the text for the input field

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
        ImGui.InputTextMultiline("##output", ref outputText, 10000, new Vector2(-1, -1), ImGuiInputTextFlags.ReadOnly);
        ImGui.SetScrollHereY(1.0f); // Scroll to bottom
        ImGui.EndChild();

        ImGui.Separator();

        // Input text field with send button
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Input:");
        ImGui.SameLine();

        ImGui.InputText("##input", ref inputText, 100);
        inputText = "currently does nothing";
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
        PrintToOutput($"You entered: {input}");
    }

    public void PrintToOutput(string message)
    {
        // Append the given text to the output field
        outputText = outputText + message + "\n";
    }
}
