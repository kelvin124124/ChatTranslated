using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly string[] supportedDetectedLanguages =
    ["English", "Japanese", "German", "French", "Chinese (Simplified)", "Chinese (Traditional)", "Korean", "Spanish"];
    private readonly string[] supportedLanguages =
    ["English", "Japanese", "German", "French", "Chinese (Simplified)", "Chinese (Traditional)", "Korean", "Spanish"];

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
        Size = new Vector2(600, 500);
    }

    private static string DeepLApiKeyInput = Service.configuration.DeepL_API_Key;
    private static string OpenAIApiKeyInput = Service.configuration.OpenAI_API_Key;
    private static string ProxyApiKeyInput = Service.configuration.Proxy_API_Key;

    public void Dispose() { Service.fontManager.ExtendedFontHandle?.Pop(); }

    public override void Draw()
    {
        Configuration configuration = Service.configuration;

        using (Service.fontManager.ExtendedFontHandle?.Push())
        {
            DrawGenericSettigns(configuration);
            ImGui.Separator();

            DrawPluginLangSelection(configuration);
            ImGui.Separator();

            DrawChatChannelSelection(configuration);
            ImGui.Separator();

            DrawSourceLangSelection(configuration);
            ImGui.Separator();

            DrawTargetLangSelection(configuration);
            ImGui.Separator();

            DrawModeSelection(configuration);
        }
    }

    private static void DrawGenericSettigns(Configuration configuration)
    {
        bool _Enabled = configuration.Enabled;
        bool _ChatIntegration = configuration.ChatIntegration;
        bool _EnabledInDuty = configuration.EnabledInDuty;
        bool _SendChatToDB = configuration.SendChatToDB;

        // Enabled
        if (ImGui.Checkbox("Enable plugin".GetLocalization(), ref _Enabled))
        {
            configuration.Enabled = _Enabled;
            configuration.Save();
        }

        // Chat Integration
        if (ImGui.Checkbox("Chat Integration".GetLocalization(), ref _ChatIntegration))
        {
            configuration.ChatIntegration = _ChatIntegration;
            configuration.Save();
        }

        // Enable in duties
        if (ImGui.Checkbox("Enable in duties".GetLocalization(), ref _EnabledInDuty))
        {
            configuration.EnabledInDuty = _EnabledInDuty;
            configuration.Save();
        }

        // Send chat to DB
        if (ImGui.Checkbox("Send chat to DB".GetLocalization(), ref _SendChatToDB))
        {
            configuration.SendChatToDB = _SendChatToDB;
        }
        ImGui.Text("    " + "Collect outgoing chat messages to improve translations.".GetLocalization());
        ImGui.Text("    " + "Personal identifiers and sensitive info will be removed before use.".GetLocalization());
    }

    private void DrawPluginLangSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Plugin Language".GetLocalization());
        ImGui.SameLine();

        string currentSelection = configuration.SelectedPluginLanguage;

        int currentIndex = Array.IndexOf(supportedLanguages, currentSelection);
        if (currentIndex == -1) currentIndex = 0; // Fallback to the first item if not found.

        ImGui.SameLine();
        if (ImGui.Button("Help with localization!"))
        {
            Process.Start(new ProcessStartInfo { FileName = "https://crowdin.com/project/chattranslated", UseShellExecute = true });
        }

        string[] localizedSupportedLanguages = supportedLanguages.Select(lang => lang.GetLocalization()).ToArray();
        if (ImGui.Combo("##pluginLanguage", ref currentIndex, localizedSupportedLanguages, supportedLanguages.Length))
        {
            configuration.SelectedPluginLanguage = supportedLanguages[currentIndex];
            configuration.Save();
            LocManager.LoadLocalization();
        }
    }

    private static void DrawChatChannelSelection(Configuration configuration)
    {
        // Translate channel selection
        if (ImGui.CollapsingHeader("Channel Selection".GetLocalization(), ImGuiTreeNodeFlags.None))
        {
            ImGui.Columns(3, "chatTypeColumns", false);

            ImGui.SetColumnWidth(0, 200);
            ImGui.SetColumnWidth(1, 200);
            ImGui.SetColumnWidth(2, 200);

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
        if (ImGui.Checkbox(type.ToString().GetLocalization(), ref typeEnabled))
        {
            if (typeEnabled)
            {
                if (!configuration.SelectedChatTypes.Contains(type))
                    configuration.SelectedChatTypes.Add(type);
            }
            else
            {
                configuration.SelectedChatTypes.RemoveAll(t => t == type);
            }

            configuration.Save();
        }
    }

    private void DrawSourceLangSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text("What to translate".GetLocalization());
        ImGui.SameLine();

        int selectedLanguageSelectionMode = (int)configuration.SelectedLanguageSelectionMode;

        string[] languageSelectionModeNames = Enum.GetNames(typeof(LanguageSelectionMode));
        string[] localizedlanguageSelectionModes = languageSelectionModeNames.Select(mode => mode.GetLocalization()).ToArray();

        if (ImGui.Combo("##LanguageSelectionModeCombo", ref selectedLanguageSelectionMode, localizedlanguageSelectionModes, languageSelectionModeNames.Length))
        {
            configuration.SelectedLanguageSelectionMode = (LanguageSelectionMode)selectedLanguageSelectionMode;
            configuration.Save();
        }

        if (configuration.SelectedLanguageSelectionMode == LanguageSelectionMode.Default)
        {
            ImGui.Text("Recommended. Translate non-Latin based languages.\\n(Japanese, Korean, Chinese, etc.)".GetLocalization());
        }
        else if (configuration.SelectedLanguageSelectionMode == LanguageSelectionMode.CustomLanguages)
        {
            if (ImGui.CollapsingHeader("Source Language Selection".GetLocalization(), ImGuiTreeNodeFlags.None))
            {
                // checkbox list
                foreach (string language in supportedDetectedLanguages)
                {
                    bool isSelected = configuration.SelectedSourceLanguages.Contains(language);
                    if (ImGui.Checkbox(language.GetLocalization(), ref isSelected))
                    {
                        if (isSelected)
                        {
                            if (!configuration.SelectedSourceLanguages.Contains(language))
                                configuration.SelectedSourceLanguages.Add(language);
                        }
                        else
                        {
                            configuration.SelectedSourceLanguages.RemoveAll(lang => lang == language);
                        }

                        configuration.Save();
                    }
                }
            }
        }
        else if (configuration.SelectedLanguageSelectionMode == LanguageSelectionMode.AllLanguages)
        {
            ImGui.Text("Translate all incoming messages.\\n\\nDidn't find your language in language selection?\\nSend feedback from plugin installer!".GetLocalization());
        }
    }

    private void DrawTargetLangSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Translate to".GetLocalization());
        ImGui.SameLine();

        string currentSelection = configuration.SelectedTargetLanguage;

        int currentIndex = Array.IndexOf(supportedLanguages, currentSelection);
        if (currentIndex == -1) currentIndex = 0; // Fallback to the first item if not found.

        string[] localizedSupportedLanguages = supportedLanguages.Select(lang => lang.GetLocalization()).ToArray();
        if (ImGui.Combo("##targetLanguage", ref currentIndex, localizedSupportedLanguages, supportedLanguages.Length))
        {
            configuration.SelectedTargetLanguage = supportedLanguages[currentIndex];

            TranslationHandler.ClearTranslationCache();
            configuration.Save();
        }
    }

    private static void DrawModeSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text("TranslationMode".GetLocalization());
        ImGui.SameLine();

        int selectedTranslationMode = (int)configuration.SelectedTranslationMode;

        string[] translationModeNames = Enum.GetNames(typeof(TranslationMode));
        string[] localizedTranslationModes = translationModeNames.Select(mode => mode.GetLocalization()).ToArray();

        // update index when adding new modes
        if (ImGui.Combo("##TranslationModeCombo", ref selectedTranslationMode, localizedTranslationModes, translationModeNames.Length))
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
        ImGui.Text("DeepL API Key".GetLocalization());
        ImGui.InputText("##APIKey", ref DeepLApiKeyInput, 100);
        ImGui.SameLine();
        if (ImGui.Button("Apply".GetLocalization()))
        {
            configuration.DeepL_API_Key = DeepLApiKeyInput;
            TranslationHandler.ClearTranslationCache();
            configuration.Save();
        }
        ImGui.Text("Get one free from DeepL official website!".GetLocalization());
    }

    private static void DrawOpenAISettings(Configuration configuration)
    {
        ImGui.Text("OpenAI API Key".GetLocalization());
        ImGui.InputText("##APIKey", ref OpenAIApiKeyInput, 100);
        ImGui.SameLine();
        if (ImGui.Button("Apply".GetLocalization()))
        {
            if (configuration.openaiWarned)
            {
                configuration.OpenAI_API_Key = OpenAIApiKeyInput;
                TranslationHandler.ClearTranslationCache();
                configuration.Save();
            }
            else
            {
                ImGui.OpenPopup("Confirmation");
            }
        }
        ImGui.Text("Price estimation: $0.2 /month".GetLocalization());
        ImGui.NewLine();
        ImGui.TextColored(new Vector4(1, 0, 0, 1),
            "Warning: API key will be stored as plain text in plugin configuration,\\nany malware or third party plugins may have access to the key.".GetLocalization());

        // confirmation popup
        if (ImGui.BeginPopupModal("Confirmation"))
        {
            ImGui.Text("Warning: API key will be stored as plain text in plugin configuration,\\nany malware or third party plugins may have access to the key.".GetLocalization());
            ImGui.Text("Proceed?".GetLocalization());

            ImGui.Separator();

            float windowWidth = ImGui.GetWindowWidth();
            float buttonSize = ImGui.CalcTextSize("Yes").X + (ImGui.GetStyle().FramePadding.X * 2);

            ImGui.SetCursorPosX((windowWidth - (buttonSize * 2) - ImGui.GetStyle().ItemSpacing.X) * 0.5f);
            if (ImGui.Button("Yes".GetLocalization(), new Vector2(buttonSize, 0)))
            {
                configuration.openaiWarned = true;
                Service.configuration.OpenAI_API_Key = OpenAIApiKeyInput;
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();

            if (ImGui.Button("No".GetLocalization(), new Vector2(buttonSize, 0)))
            {
                OpenAIApiKeyInput = "sk-YOUR-API-KEY";
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private static void DrawLLMProxySettings(Configuration configuration)
    {
        ImGui.Text("Free Claude-Haiku translation service provided by the dev,\\nsubject to availability.".GetLocalization());
        ImGui.Text("Users from unsupported regions WILL experience higher latency.".GetLocalization());

        // select region
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Region".GetLocalization());
        ImGui.SameLine();

        string[] ProxyRegions = ["US", "EU", "HK"];
        string currentSelection = configuration.ProxyRegion;

        int currentIndex = Array.IndexOf(ProxyRegions, currentSelection);
        if (currentIndex == -1) currentIndex = 0; // Fallback to the first item if not found.

        string[] localizedRegions = ProxyRegions.Select(region => region.GetLocalization()).ToArray();
        if (ImGui.Combo("##regionCombo", ref currentIndex, localizedRegions, ProxyRegions.Length))
        {
            configuration.ProxyRegion = ProxyRegions[currentIndex];
            configuration.Save();
        }

#if DEBUG
        ImGui.Text("Proxy API Key");
        ImGui.InputText("##APIKey", ref ProxyApiKeyInput, 100);
        ImGui.SameLine();
        if (ImGui.Button("Apply"))
        {
            configuration.Proxy_API_Key = ProxyApiKeyInput;
            configuration.Save();
        }
#endif
    }
}
