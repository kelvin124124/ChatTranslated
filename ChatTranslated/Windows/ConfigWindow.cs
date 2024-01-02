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
    private string apiKeyInput = "";

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
            UpdateApiKeyInput();
            configuration.Save();
        }

        // API Key Input
        ImGui.Text("API Key");
        ImGui.InputText("##APIKey", ref apiKeyInput, 256);
        ImGui.SameLine();
        if (ImGui.Button("Save")) SaveApiKey();
    }

    private void UpdateApiKeyInput()
    {
        switch (configuration.SelectedMode)
        {
            case Mode.LibreTranslate:
                apiKeyInput = LIBRETRANSLATE_API_KEY;
                break;
            case Mode.GPT3_Proxy:
                apiKeyInput = PROXY_API_KEY;
                break;
            case Mode.OpenAI_API:
                apiKeyInput = OPENAI_API_KEY;
                break;
        }
    }
    private void SaveApiKey()
    {
        switch (configuration.SelectedMode)
        {
            case Mode.LibreTranslate:
                LIBRETRANSLATE_API_KEY = apiKeyInput;
                break;
            case Mode.GPT3_Proxy:
                PROXY_API_KEY = apiKeyInput;
                break;
            case Mode.OpenAI_API:
                OPENAI_API_KEY = apiKeyInput;
                break;
        }
        configuration.Save();
    }
}
