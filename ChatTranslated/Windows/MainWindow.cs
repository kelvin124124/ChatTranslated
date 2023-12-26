using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace ChatTranslated.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    // temporary solution
    private string outputText = ""; // Holds the text for the output field
    private string inputText = "";  // Holds the text for the input field

    public MainWindow(Plugin plugin) : base(
        "My Amazing Window", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public override void Draw()
    {
        // Output text field
        ImGui.BeginChild("outputField", new Vector2(-1, -50), false, ImGuiWindowFlags.HorizontalScrollbar);
        ImGui.TextUnformatted(outputText);
        ImGui.EndChild();

        // Input text field with send button
        ImGui.AlignTextToFramePadding();
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

    public void PrintToOutput(string text)
    {
        // Append the given text to the output field
        outputText += text;
    }
}
