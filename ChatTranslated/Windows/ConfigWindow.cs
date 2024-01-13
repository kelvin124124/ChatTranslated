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
    private string apiKeyInput = Translator.OPENAI_API_KEY ?? "sk-YOUR-API-KEY";

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

        if (ImGui.Combo("##ModeCombo", ref selectedMode, Enum.GetNames(typeof(Mode)), 2))
        {
            configuration.SelectedMode = (Mode)selectedMode;
            configuration.Save();
        }

        // API Key Input
        if (configuration.SelectedMode == Mode.OpenAI_API)
        {
            ImGui.Text("OpenAI API Key ");
            ImGui.InputText("##APIKey", ref apiKeyInput, 256);
            ImGui.SameLine();
            if (ImGui.Button("Apply"))
            {
                Translator.OPENAI_API_KEY = apiKeyInput;
            }

            ImGui.TextColored(new Vector4(1, 0, 0, 1), 
                "Warning: API key is not saved and must be re-entered after reboot for safety reasons.");
        }
    }
}
