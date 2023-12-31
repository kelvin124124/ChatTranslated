using ChatTranslated.Utils;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private string inputText = "";

    public ConfigWindow(Plugin plugin) : base(
        "Chat Translated config window",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(360, 160);
        SizeCondition = ImGuiCond.Always;

        configuration = Service.configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Enabled
        bool _ChatIntergration = configuration.ChatIntergration;
        if (ImGui.Checkbox("Chat Intergration", ref _ChatIntergration))
        {
            configuration.ChatIntergration = _ChatIntergration;
            configuration.Save();
        }

        // Mode selection
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Mode");
        ImGui.SameLine();

        int selectedMode = (int)configuration.SelectedMode;

        if (ImGui.Combo("##ModeCombo", ref selectedMode, Enum.GetNames(typeof(Mode)), 3))
        {
            configuration.SelectedMode = (Mode)selectedMode;
            configuration.Save();
        }

        // Credentials
        ImGui.AlignTextToFramePadding();
        ImGui.Text($"Current server url: {Service.configuration.LIBRETRANSLATE_API}");
        ImGui.InputText("##Server", ref inputText, 100);
        ImGui.SameLine();
        if (ImGui.Button("Save"))
        {
            Service.configuration.LIBRETRANSLATE_API = inputText;
            Service.pluginLog.Information($"Server set to {inputText}");
            inputText = ""; // Clear the input field after sending
        }
    }
}
