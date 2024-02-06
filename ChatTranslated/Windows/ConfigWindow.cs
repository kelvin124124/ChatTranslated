using ChatTranslated.Utils;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private string apiKeyInput = OPENAI_API_KEY ?? "sk-YOUR-API-KEY";

    public static readonly List<XivChatType> genericChatTypes =
    [
        XivChatType.Say,
        XivChatType.Shout,
        XivChatType.TellIncoming,
        XivChatType.Party,
        XivChatType.Alliance,
        XivChatType.FreeCompany,
        XivChatType.NoviceNetwork,
        XivChatType.Yell,
        XivChatType.CrossParty,
        XivChatType.PvPTeam,
    ];
    public static readonly List<XivChatType> lsChatTypes =
    [
        XivChatType.Ls1,
        XivChatType.Ls2,
        XivChatType.Ls3,
        XivChatType.Ls4,
        XivChatType.Ls5,
        XivChatType.Ls6,
        XivChatType.Ls7,
        XivChatType.Ls8,
    ];
    public static readonly List<XivChatType> cwlsChatTypes =
    [
        XivChatType.CrossLinkShell1,
        XivChatType.CrossLinkShell2,
        XivChatType.CrossLinkShell3,
        XivChatType.CrossLinkShell4,
        XivChatType.CrossLinkShell5,
        XivChatType.CrossLinkShell6,
        XivChatType.CrossLinkShell7,
        XivChatType.CrossLinkShell8
    ];

    public ConfigWindow(Plugin plugin) : base(
        "Chat Translated config window",
        ImGuiWindowFlags.AlwaysAutoResize)
    {
        Size = new Vector2(400, 300);
        configuration = Service.configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Enabled
        bool _ChatIntegration = configuration.ChatIntegration;
        bool _TranslateFrDe = configuration.TranslateFrDe;
        if (ImGui.Checkbox("Chat Integration", ref _ChatIntegration))
        {
            configuration.ChatIntegration = _ChatIntegration;
            configuration.Save();
        }

        // Translate language selection
        if (ImGui.Checkbox("Translate French and German", ref _TranslateFrDe))
        {
            configuration.TranslateFrDe = _TranslateFrDe;
            configuration.Save();
        }
        ImGui.Text("    Note: make translations slower.");


        // Translate channel selection
        if (ImGui.CollapsingHeader("Channel Selection", ImGuiTreeNodeFlags.None))
        {
            ImGui.Columns(3, "chatTypeColumns", false);

            ImGui.SetColumnWidth(0, 125);
            ImGui.SetColumnWidth(1, 100);
            ImGui.SetColumnWidth(2, 175);

            DrawChatTypeGroup(genericChatTypes);
            ImGui.NextColumn();

            DrawChatTypeGroup(lsChatTypes);
            ImGui.NextColumn();

            DrawChatTypeGroup(cwlsChatTypes);

            ImGui.Columns(1);
        }

        void DrawChatTypeGroup(IEnumerable<XivChatType> chatTypes)
        {
            foreach (var type in chatTypes)
            {
                UpdateChannelConfig(type);
            }
        }

        void UpdateChannelConfig(XivChatType type)
        {
            var typeEnabled = Service.configuration.ChatTypes.Contains(type);
            if (ImGui.Checkbox(type.ToString(), ref typeEnabled))
            {
                if (typeEnabled)
                {
                    if (!Service.configuration.ChatTypes.Contains(type))
                        Service.configuration.ChatTypes.Add(type);
                }
                else
                {
                    if (Service.configuration.ChatTypes.Contains(type))
                        Service.configuration.ChatTypes.Remove(type);
                }

                Service.configuration.Save();
            }
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
            ImGui.InputText("##APIKey", ref apiKeyInput, 100);
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
                           "\nany malware or third party plugins may have access to the key. \nProceed?");

                ImGui.Separator();

                float windowWidth = ImGui.GetWindowWidth();
                float buttonSize = ImGui.CalcTextSize("Yes").X + (ImGui.GetStyle().FramePadding.X * 2);

                ImGui.SetCursorPosX((windowWidth - (buttonSize * 2) - ImGui.GetStyle().ItemSpacing.X) * 0.5f);
                if (ImGui.Button("Yes", new Vector2(buttonSize, 0)))
                {
                    configuration.warned = true;
                    OPENAI_API_KEY = apiKeyInput;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();

                if (ImGui.Button("No", new Vector2(buttonSize, 0)))
                {
                    apiKeyInput = "sk-YOUR-API-KEY";
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            ImGui.TextColored(new Vector4(1, 0, 0, 1),
                "Warning: " +
                "\nAPI key stored as plain text in plugin configuration, " +
                "\nany malware or third party plugins may have access to \nthe key.");
        }

    }
}
