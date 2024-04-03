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

    private readonly string[] languages = ["English", "Japanese", "German", "French", "Korean", "Chinese (Simplified)", "Chinese (Traditional)", "Spanish"];
    private string apiKeyInput = Service.configuration.OpenAI_API_Key;
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
        XivChatType.PvPTeam
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
        XivChatType.Ls8
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
        Size = new Vector2(450, 500);
        configuration = Service.configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        bool _ChatIntegration = configuration.ChatIntegration;
        bool _Enabled = configuration.Enabled;
        bool _EnabledInDuty = configuration.EnabledInDuty;
        bool _TranslateFr = configuration.TranslateFr;
        bool _TranslateDe = configuration.TranslateDe;
        bool _TranslateEn = configuration.TranslateEn;
        bool _SendChatToDB = configuration.SendChatToDB;
        bool _BetterTranslation = configuration.BetterTranslation;

        // Enabled
        if (ImGui.Checkbox("Enabled", ref _Enabled))
        {
            configuration.Enabled = _Enabled;
            configuration.Save();
        }

        // Chat Integration
        if (ImGui.Checkbox("Chat Integration", ref _ChatIntegration))
        {
            configuration.ChatIntegration = _ChatIntegration;
            configuration.Save();
        }

        // Enable in duties
        if (ImGui.Checkbox("Enable in duties", ref _EnabledInDuty))
        {
            configuration.EnabledInDuty = _EnabledInDuty;
            configuration.Save();
        }

        ImGui.Separator();

        // Translate language selection
        if (ImGui.Checkbox("Translate French", ref _TranslateFr))
        {
            configuration.TranslateFr = _TranslateFr;
            configuration.Save();
        }
        if (ImGui.Checkbox("Translate German", ref _TranslateDe))
        {
            configuration.TranslateDe = _TranslateDe;
            configuration.Save();
        }
        if (ImGui.Checkbox("Translate all languages", ref _TranslateEn))
        {
            configuration.TranslateEn = _TranslateEn;
            configuration.Save();
        }
        ImGui.Text("    Note: Plugin translates all messages with non-English characters \n         by default.\n" +
                   "    Using these options make translations slower.");

        // Send chat to DB option
        if (ImGui.Checkbox("Send chat to DB", ref _SendChatToDB))
        {
            configuration.SendChatToDB = _SendChatToDB;
            configuration.Save();
        }
        ImGui.Text("    Collect outgoing chat messages to improve translations.\n" +
                   "    Personal identifiers and sensitive info will be removed before use.");

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
            var typeEnabled = configuration.ChatTypes.Contains(type);
            if (ImGui.Checkbox(type.ToString(), ref typeEnabled))
            {
                if (typeEnabled)
                {
                    if (!configuration.ChatTypes.Contains(type))
                        configuration.ChatTypes.Add(type);
                }
                else
                {
                    configuration.ChatTypes.Remove(type);
                }

                configuration.Save();
            }
        }

        // Target language selection
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Translate to");
        ImGui.SameLine();

        int currentLanguageIndex = Array.IndexOf(languages, Service.configuration.SelectedChatLanguage);
        if (currentLanguageIndex == -1) currentLanguageIndex = 0;

        if (ImGui.Combo("##LanguageCombo", ref currentLanguageIndex, languages, languages.Length))
        {
            configuration.SelectedChatLanguage = languages[currentLanguageIndex];
            Translator.TranslationCache = [];
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
            Translator.TranslationCache = [];
            configuration.Save();
        }

        // GPTProxy description
        if (configuration.SelectedMode == Mode.GPTProxy)
        {
            ImGui.Text("Free GPT-3.5-turbo translation service provided by the dev,\nsubject to availability.");
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
                    configuration.OpenAI_API_Key = apiKeyInput;
                    Translator.TranslationCache = [];
                    configuration.Save();
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
                    configuration.OpenAI_API_Key = apiKeyInput;
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

            // Better translation
            if (ImGui.Checkbox("Better translation", ref _BetterTranslation))
            {
                configuration.BetterTranslation = _BetterTranslation;
                configuration.Save();
            }
            // Tooltip explaining better translation option
            ImGui.SameLine();
            ImGui.TextDisabled("?");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("Use GPT-4-Turbo and more detailed prompt." +
                    "\nPrice estimation:" +
                    "\nNormal mode: <$0.1/month" +
                    "\nBetter Translation: $2");
                ImGui.EndTooltip();
            }

            ImGui.TextColored(new Vector4(1, 0, 0, 1),
                "Warning: " +
                "\nAPI key stored as plain text in plugin configuration, " +
                "\nany malware or third party plugins may have access to \nthe key.");
        }
    }
}
