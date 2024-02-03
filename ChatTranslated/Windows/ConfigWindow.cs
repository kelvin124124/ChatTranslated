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
    private string apiKeyInput = OPENAI_API_KEY ?? "sk-YOUR-API-KEY";

    public ConfigWindow(Plugin plugin) : base(
        "Chat Translated config window",
        ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        configuration = Service.configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Enabled
        bool _ChatIntergration = configuration.ChatIntergration;
        bool _TranslateFrDe = configuration.TranslateFrDe;
        if (ImGui.Checkbox("Chat Intergration", ref _ChatIntergration))
        {
            configuration.ChatIntergration = _ChatIntergration;
            configuration.Save();
        }
        
        if (ImGui.Checkbox("Translate French and German", ref _TranslateFrDe))
        {
            configuration.TranslateFrDe = _TranslateFrDe;
            configuration.Save();
        }
        ImGui.Text("Note: make translations slower.");

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
            ImGui.InputText("##APIKey", ref apiKeyInput, 50);
            ImGui.SameLine();
            if (ImGui.Button("Apply"))
            {
                if (configuration.warned)
                {
                    // hope nothing bad happens
                    OPENAI_API_KEY = apiKeyInput;
                }
                else
                {
                    ImGui.OpenPopup("Confirmation");
                }
            }

            if (ImGui.BeginPopupModal("Confirmation"))
            {
                ImGui.Text("Warning: API key will be stored as plain text in plugin configuration, " +
                           "\nany malware or third party plugins may have access to the key. Proceed?");

                if (ImGui.Button("Yes"))
                {
                    configuration.warned = true;
                    OPENAI_API_KEY = apiKeyInput;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("No"))
                {
                    apiKeyInput = "sk-YOUR-API-KEY";
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            ImGui.TextColored(new Vector4(1, 0, 0, 1),
                "Warning: API key stored as plain text in plugin configuration, " +
                "\nany malware or third party plugins may have access to the key.");
        }

    }
}
