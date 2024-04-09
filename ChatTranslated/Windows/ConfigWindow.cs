using ChatTranslated.Translate;
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
    private readonly string[] supportedLanguages =
    ["English", "Japanese", "German", "French", "Korean", "Chinese (Simplified)", "Chinese (Traditional)", "Spanish"];

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
    }

    public void Dispose() { }

    public override void Draw()
    {
        Configuration configuration = Service.configuration;

        DrawGenericSettigns(configuration);
        ImGui.Separator();

        DrawChatChannelSelection(configuration);
        ImGui.Separator();

        DrawSourceLangSelection(configuration);
        ImGui.Separator();

        DrawTargetLangSelection(configuration);
        ImGui.Separator();

        DrawModeSelection(configuration);
    }

    private static void DrawGenericSettigns(Configuration configuration)
    {
        bool _Enabled = configuration.Enabled;
        bool _ChatIntegration = configuration.ChatIntegration;
        bool _EnabledInDuty = configuration.EnabledInDuty;
        bool _SendChatToDB = configuration.SendChatToDB;

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

        // Send chat to DB
        if (ImGui.Checkbox("Send chat to DB", ref _SendChatToDB))
        {
            configuration.SendChatToDB = _SendChatToDB;
        }
        ImGui.Text("    Collect outgoing chat messages to improve translations.\n" +
                   "    Personal identifiers and sensitive info will be removed before use.");
    }

    private static void DrawChatChannelSelection(Configuration configuration)
    {
        // Translate channel selection
        if (ImGui.CollapsingHeader("Channel Selection", ImGuiTreeNodeFlags.None))
        {
            ImGui.Columns(3, "chatTypeColumns", false);

            ImGui.SetColumnWidth(0, 125);
            ImGui.SetColumnWidth(1, 100);
            ImGui.SetColumnWidth(2, 175);

            DrawChatTypeGroup(genericChatTypes, configuration);
            ImGui.NextColumn();

            DrawChatTypeGroup(lsChatTypes, configuration);
            ImGui.NextColumn();

            DrawChatTypeGroup(cwlsChatTypes, configuration);

            ImGui.Columns(1);
        }
    }

    private static void DrawChatTypeGroup(IEnumerable<XivChatType> chatTypes, Configuration configuration)
    {
        foreach (var type in chatTypes)
        {
            UpdateChannelConfig(type, configuration);
        }
    }

    private static void UpdateChannelConfig(XivChatType type, Configuration configuration)
    {
        var typeEnabled = configuration.SelectedChatTypes.Contains(type);
        if (ImGui.Checkbox(type.ToString(), ref typeEnabled))
        {
            if (typeEnabled)
            {
                if (!configuration.SelectedChatTypes.Contains(type))
                    configuration.SelectedChatTypes.Add(type);
            }
            else
            {
                configuration.SelectedChatTypes.Remove(type);
            }

            configuration.Save();
        }
    }

    private void DrawSourceLangSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text("What to translate");
        ImGui.SameLine();

        int selectedLanguageSelectionMode = (int)configuration.SelectedLanguageSelectionMode;

        if (ImGui.Combo("##LanguageSelectionModeCombo", ref selectedLanguageSelectionMode, Enum.GetNames(typeof(LanguageSelectionMode)), 3))
        {
            configuration.SelectedLanguageSelectionMode = (LanguageSelectionMode)selectedLanguageSelectionMode;
            configuration.Save();
        }

        if (configuration.SelectedLanguageSelectionMode == LanguageSelectionMode.Default)
        {
            ImGui.Text("Recommended. Translate non-Latin based languages.\n(Japanese, Koren, Chinese, etc.)");
        }
        else if (configuration.SelectedLanguageSelectionMode == LanguageSelectionMode.CustomLanguages)
        {
            if (ImGui.CollapsingHeader("Source Language Selection", ImGuiTreeNodeFlags.None))
            {
                // checkbox list
                foreach (string language in supportedLanguages)
                {
                    bool isSelected = configuration.SelectedSourceLanguages.Contains(language);
                    if (ImGui.Checkbox(language, ref isSelected))
                    {
                        if (isSelected)
                        {
                            if (!configuration.SelectedSourceLanguages.Contains(language))
                                configuration.SelectedSourceLanguages.Add(language);
                        }
                        else
                        {
                            configuration.SelectedSourceLanguages.Remove(language);
                        }

                        configuration.Save();
                    }
                }
            }
        }
        else if (configuration.SelectedLanguageSelectionMode == LanguageSelectionMode.AllLanguages)
        {
            ImGui.Text("Translate all incoming messages.\n\nDidn't find your language in language selection?\nSend feedback from plugin installer!");
        }
    }

    private void DrawTargetLangSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Translate to");
        ImGui.SameLine();

        string currentSelection = configuration.SelectedTargetLanguage;

        int currentIndex = Array.IndexOf(supportedLanguages, currentSelection);
        if (currentIndex == -1) currentIndex = 0; // Fallback to the first item if not found.

        if (ImGui.Combo("##targetLanguage", ref currentIndex, supportedLanguages, supportedLanguages.Length))
        {
            configuration.SelectedTargetLanguage = supportedLanguages[currentIndex];

            TranslationHandler.ClearTranslationCache();
            configuration.Save();
        }
    }

    private static void DrawModeSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text("TranslationMode");
        ImGui.SameLine();

        int selectedTranslationMode = (int)configuration.SelectedTranslationMode;

        // update index when adding new modes
        if (ImGui.Combo("##TranslationModeCombo", ref selectedTranslationMode, Enum.GetNames(typeof(TranslationMode)), 4))
        {
            configuration.SelectedTranslationMode = (TranslationMode)selectedTranslationMode;

            TranslationHandler.ClearTranslationCache();
            configuration.Save();
        }

        switch (configuration.SelectedTranslationMode)
        {
            case TranslationMode.MachineTranslate:
                // do nothing
                break;
            case TranslationMode.DeepL_API:
                DrawDeepLSettings(configuration);
                break;
            case TranslationMode.OpenAI_API:
                DrawOpenAISettings(configuration);
                break;
            case TranslationMode.LLMProxy:
                DrawLLMProxySettings(configuration);
                break;
        }
    }

    private static void DrawDeepLSettings(Configuration configuration)
    {
        string apiKeyInput = configuration.DeepL_API_Key;

        ImGui.Text("DeepL API Key ");
        ImGui.InputText("##APIKey", ref apiKeyInput, 100);
        ImGui.SameLine();
        if (ImGui.Button("Apply"))
        {
            configuration.DeepL_API_Key = apiKeyInput;
            configuration.Save();
            Translator.DeepLtranslator = new DeepL.Translator(apiKeyInput);
        }
    }

    private static void DrawOpenAISettings(Configuration configuration)
    {
        string apiKeyInput = configuration.OpenAI_API_Key;

        ImGui.Text("OpenAI API Key ");
        ImGui.InputText("##APIKey", ref apiKeyInput, 100);
        ImGui.SameLine();
        if (ImGui.Button("Apply"))
        {
            if (configuration.openaiWarned)
            {
                configuration.OpenAI_API_Key = apiKeyInput;
                TranslationHandler.ClearTranslationCache();
                configuration.Save();
            }
            else
            {
                ImGui.OpenPopup("Confirmation");
            }
        }

        bool _BetterTranslation = configuration.BetterTranslation;
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

        // confirmation popup
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
                configuration.openaiWarned = true;
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
    }

    private static void DrawLLMProxySettings(Configuration configuration)
    {
        ImGui.Text("Free Claude-Haiku translation service provided by the dev,\nsubject to availability.");
        ImGui.Text("Users from unsupported regions WILL experience higher latency.");

        // select region
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Region");
        ImGui.SameLine();

        string[] ProxyRegions = ["US", "EU"];
        string currentSelection = configuration.ProxyRegion;

        int currentIndex = Array.IndexOf(ProxyRegions, currentSelection);
        if (currentIndex == -1) currentIndex = 0; // Fallback to the first item if not found.

        if (ImGui.Combo("##regionCombo", ref currentIndex, ProxyRegions, ProxyRegions.Length))
        {
            configuration.ProxyRegion = ProxyRegions[currentIndex];
            configuration.Save();
        }
    }
}
