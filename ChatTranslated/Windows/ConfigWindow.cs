using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using GTranslate;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Windows;

public class ConfigWindow : Window
{
    private readonly string[] supportedDetectedLanguages =
    ["English", "Japanese", "German", "French", "Chinese (Simplified)", "Chinese (Traditional)", "Korean", "Spanish"];
    private readonly string[] supportedLanguages =
    ["English", "Japanese", "German", "French", "Chinese (Simplified)", "Chinese (Traditional)", "Korean", "Spanish"];

    public static readonly HashSet<XivChatType> genericChatTypes =
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
    public static readonly HashSet<XivChatType> lsChatTypes =
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
    public static readonly HashSet<XivChatType> cwlsChatTypes =
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
        Size = new Vector2(600, 340);
    }

    private static string DeepLApiKeyInput = Service.configuration.DeepL_API_Key;
    private static string OpenAIApiKeyInput = Service.configuration.OpenAI_API_Key;

#if DEBUG
    private static string ProxyBaseUrl = Service.configuration.Proxy_Url;
    private static string ProxyApiKeyInput = Service.configuration.Proxy_API_Key;
#endif

    private static short CurrentTab = 0;
    public override void Draw()
    {
        Configuration configuration = Service.configuration;
        string[] tabs = ["General", "Languages", "Chat Channels", "Translation Mode"];

        ImGui.Columns(2, "ConfigColumns", false);
        ImGui.SetColumnWidth(0, 200); // Increased width for the tab box

        // Left column: Tab selection box
        if (ImGui.BeginChild("TabsBox", new Vector2(190, 300), true))
        {
            for (short i = 0; i < tabs.Length; i++)
            {
                if (ImGui.Selectable(tabs[i], CurrentTab == i))
                {
                    CurrentTab = i;
                }
            }
        }
        ImGui.EndChild();

        ImGui.NextColumn();

        // Right column: Configuration items box
        if (ImGui.BeginChild("ConfigBox", new Vector2(0, 300), true)) // 0 width means "stretch to fill"
        {
            switch (CurrentTab)
            {
                case 0:
                    DrawGenericSettigns(configuration);
                    ImGui.Separator();
                    DrawPluginLangSelection(configuration);
                    break;
                case 1:
                    DrawSourceLangSelection(configuration);
                    ImGui.Separator();
                    DrawTargetLangSelection(configuration);
                    break;
                case 2:
                    DrawChatChannelSelection(configuration);
                    break;
                case 3:
                    DrawEngineSelection(configuration);
                    break;
            }
        }
        ImGui.EndChild();

        ImGui.Columns(1);
    }

    private static void DrawGenericSettigns(Configuration configuration)
    {
        bool _Enabled = configuration.Enabled;
        bool _ChatIntegration = configuration.ChatIntegration;
        bool _EnabledInDuty = configuration.EnabledInDuty;
        bool _SendChatToDB = configuration.SendChatToDB;

        // Enabled
        if (ImGui.Checkbox(Resources.EnablePlugin, ref _Enabled))
        {
            configuration.Enabled = _Enabled;
            configuration.Save();
        }

        // Chat Integration
        if (ImGui.Checkbox(Resources.ChatIntegration, ref _ChatIntegration))
        {
            configuration.ChatIntegration = _ChatIntegration;
            configuration.Save();
        }

        // Enable in duties
        if (ImGui.Checkbox(Resources.EnableInDuties, ref _EnabledInDuty))
        {
            configuration.EnabledInDuty = _EnabledInDuty;
            configuration.Save();
        }

        // Send chat to DB
        if (ImGui.Checkbox(Resources.SendChat, ref _SendChatToDB))
        {
            configuration.SendChatToDB = _SendChatToDB;
        }
        ImGui.TextUnformatted("    " + Resources.SendChatExplaination);
        ImGui.TextUnformatted("    " + Resources.SendChatDisclaimer);
    }

    private void DrawPluginLangSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Resources.PluginLanguage);
        ImGui.SameLine();

        string currentSelection = configuration.SelectedPluginLanguage;

        int currentIndex = Array.IndexOf(supportedLanguages, currentSelection);
        if (currentIndex == -1) currentIndex = 0; // Fallback to the first item if not found.

        ImGui.SameLine();
        if (ImGui.Button("Help with localization!"))
        {
            Process.Start(new ProcessStartInfo { FileName = "https://crowdin.com/project/chattranslated", UseShellExecute = true });
        }

        string[] localizedSupportedLanguages = supportedLanguages.Select(lang => Resources.ResourceManager.GetString(lang, Resources.Culture) ?? lang).ToArray();
        if (ImGui.Combo("##pluginLanguage", ref currentIndex, localizedSupportedLanguages, supportedLanguages.Length))
        {
            configuration.SelectedPluginLanguage = supportedLanguages[currentIndex];
            configuration.Save();
            SetLanguageCulture(configuration.SelectedPluginLanguage);
        }
    }

    internal static void SetLanguageCulture(string langName)
    {
        string langCode = langName switch
        {
            "English" => "en-US",
            "German" => "de-DE",
            "Spanish" => "es-ES",
            "French" => "fr-FR",
            "Japanese" => "ja-JP",
            "Chinese (Simplified)" => "zh-CN",
            "Chinese (Traditional)" => "zh-TW",
            "Korean" => "ko-KR",
            _ => "en-US"
        };
        Resources.Culture = new CultureInfo(langCode);
    }

    private static void DrawChatChannelSelection(Configuration configuration)
    {
        ImGui.BeginTabBar("ChatChannelTabs");

        if (ImGui.BeginTabItem(Resources.GenericChannels))
        {
            DrawChatTypeGroup(genericChatTypes, configuration);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("LS"))
        {
            DrawChatTypeGroup(lsChatTypes, configuration);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("CWLS"))
        {
            DrawChatTypeGroup(cwlsChatTypes, configuration);
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
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
        if (ImGui.Checkbox(Resources.ResourceManager.GetString(type.ToString(), Resources.Culture) ?? type.ToString(), ref typeEnabled))
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
        ImGui.TextUnformatted(Resources.SourceLang);
        ImGui.SameLine();

        int selectedLanguageSelectionMode = (int)configuration.SelectedLanguageSelectionMode;

        string[] languageSelectionModeNames = Enum.GetNames(typeof(LanguageSelectionMode));
        string[] localizedlanguageSelectionModes = languageSelectionModeNames.Select(mode => Resources.ResourceManager.GetString(mode, Resources.Culture) ?? mode).ToArray();

        if (ImGui.Combo("##LanguageSelectionModeCombo", ref selectedLanguageSelectionMode, localizedlanguageSelectionModes, languageSelectionModeNames.Length))
        {
            configuration.SelectedLanguageSelectionMode = (LanguageSelectionMode)selectedLanguageSelectionMode;
            configuration.Save();
        }

        if (configuration.SelectedLanguageSelectionMode == LanguageSelectionMode.Default)
        {
            ImGui.TextUnformatted(Resources.DefaultFilteringExplaination);
        }
        else if (configuration.SelectedLanguageSelectionMode == LanguageSelectionMode.CustomLanguages)
        {
            if (ImGui.CollapsingHeader(Resources.SourceLangSelection, ImGuiTreeNodeFlags.None))
            {
                // checkbox list
                foreach (string language in supportedDetectedLanguages)
                {
                    bool isSelected = configuration.SelectedSourceLanguages.Contains(language);
                    if (ImGui.Checkbox(Resources.ResourceManager.GetString(language, Resources.Culture) ?? language, ref isSelected))
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
            ImGui.TextUnformatted(Resources.TranslateAllExplaination);
        }
    }

    private void DrawTargetLangSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Resources.TargetLang);
        ImGui.SameLine();

        string currentSelection = configuration.SelectedTargetLanguage;

        int currentIndex = Array.IndexOf(supportedLanguages, currentSelection);
        if (currentIndex == -1) currentIndex = 0; // Fallback to the first item if not found.

        string[] localizedSupportedLanguages = supportedLanguages.Select(lang => Resources.ResourceManager.GetString(lang, Resources.Culture) ?? lang).ToArray();
        if (configuration.UseCustomLanguage) ImGui.BeginDisabled();
        if (ImGui.Combo("##targetLanguage", ref currentIndex, localizedSupportedLanguages, supportedLanguages.Length))
        {
            configuration.SelectedTargetLanguage = supportedLanguages[currentIndex];
            TranslationHandler.ClearTranslationCache();
            configuration.Save();
        }
        if (configuration.UseCustomLanguage) ImGui.EndDisabled();

        // custom target language
        if (ImGui.CollapsingHeader(Resources.CustomTargetLanguageHeader, ImGuiTreeNodeFlags.None))
        {
            ImGui.TextUnformatted(Resources.TargetLang);
            ImGui.SameLine();
            ImGui.InputText("##targetLanguageInput", ref configuration.CustomTargetLanguage, 50);
            ImGui.SameLine();
            ImGui.TextDisabled("?");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Resources.CustomTargetLanguageExplanation);
            }

            if (ImGui.Button(Resources.SeeLanguageList))
            {
                Process.Start(new ProcessStartInfo { FileName = "https://github.com/d4n3436/GTranslate/blob/master/src/GTranslate/LanguageDictionary.cs#L148", UseShellExecute = true });
            }
            ImGui.SameLine();
            if (ImGui.Button(Resources.Apply + "###ApplyCustomLanguage"))
            {
                if (Language.TryGetLanguage(configuration.CustomTargetLanguage, out var lang))
                {
                    Plugin.OutputChatLine("Language applied successfully.");
                    TranslationHandler.ClearTranslationCache();
                    configuration.Save();
                }
                else
                {
                    configuration.CustomTargetLanguage = "";
                    Plugin.OutputChatLine("Invalid language.");
                }
            }
            ImGui.SameLine();
            if (ImGui.Checkbox(Resources.UseCustomTargetLanguage, ref configuration.UseCustomLanguage))
            {
                TranslationHandler.ClearTranslationCache();
                configuration.Save();
            }
        }

        // tooltip explaining unsupported characters [do not localize]
        ImGui.TextDisabled("Regarding unsupported (=) characters");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Unsupported characters can be rendered if Dalamud font is switch to anything except the default game font " +
                "\nThis only fixes texts in plugin windows, unsupported characters in chat UI will still be rendered as  =");
        }
    }

    private static void DrawEngineSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Resources.TranslationEngine);
        ImGui.SameLine();

        int selectedTranslationEngine = (int)configuration.SelectedTranslationEngine;

        string[] translationEngineNames = Enum.GetNames(typeof(TranslationEngine));

        // update index when adding new modes
        if (ImGui.Combo("##TranslationEngineCombo", ref selectedTranslationEngine, translationEngineNames, translationEngineNames.Length))
        {
            configuration.SelectedTranslationEngine = (TranslationEngine)selectedTranslationEngine;
            TranslationHandler.ClearTranslationCache();
            configuration.Save();
        }

        switch (configuration.SelectedTranslationEngine)
        {
            case TranslationEngine.DeepL:
                ImGui.TextWrapped(Resources.DeepLExplanation);
                ImGui.Separator();
                DrawDeepLSettings(configuration);
                break;
            case TranslationEngine.LLM:
                DrawProviderSelection(configuration);
                if (configuration.LLM_Provider == 0)
                {
                    ImGui.TextUnformatted(Resources.LLM_Explanation);
#if DEBUG
                    ImGui.Separator();
                    DrawLLMProxySettings(configuration);
#endif
                }
                else
                {
                    ImGui.TextUnformatted("GPT-4o-mini\nMay be restricted if you live in unsupported region.");
                    ImGui.Separator();
                    DrawOpenAISettings(configuration);
                }
                break;
            default:
                break;
        }
    }

    private static void DrawDeepLSettings(Configuration configuration)
    {
        ImGui.TextUnformatted(Resources.DeepLAPIKey);
        ImGui.InputText("##APIKey", ref DeepLApiKeyInput, 100);
        ImGui.SameLine();
        if (ImGui.Button(Resources.Apply + "###DeepL_API_Key"))
        {
            configuration.DeepL_API_Key = DeepLApiKeyInput;
            configuration.Save();
        }
    }

    private static void DrawProviderSelection(Configuration configuration)
    {
        ImGui.TextUnformatted(Resources.LLMProvider + ":");
        ImGui.SameLine();

        int selectedProvider = configuration.LLM_Provider;
        if (ImGui.RadioButton("LLM Proxy", ref selectedProvider, 0))
        {
            configuration.LLM_Provider = 0;
            configuration.Save();
        }
        ImGui.SameLine();
        if (ImGui.RadioButton("OpenAI", ref selectedProvider, 1))
        {
            configuration.LLM_Provider = 1;
            configuration.Save();
        }
    }

#if DEBUG
    private static void DrawLLMProxySettings(Configuration configuration)
    {
        ImGui.TextUnformatted("Proxy Url");
        ImGui.InputText("##APIBaseUrl", ref ProxyBaseUrl, 100);
        ImGui.SameLine();
        if (ImGui.Button("Save###Proxy_Url"))
        {
            configuration.Proxy_Url = ProxyBaseUrl;
            configuration.Save();
            Plugin.OutputChatLine($"Proxy Url {configuration.Proxy_Url} saved successfully.");
        }

        ImGui.TextUnformatted("Proxy API Key");
        ImGui.InputText("##APIKey", ref ProxyApiKeyInput, 100);
        ImGui.SameLine();
        if (ImGui.Button("Save###Proxy_API_Key"))
        {
            configuration.Proxy_API_Key = ProxyApiKeyInput;
            configuration.Save();
            Plugin.OutputChatLine($"Proxy API Key {configuration.Proxy_API_Key} saved successfully.");
        }
    }
#endif

    private static void DrawOpenAISettings(Configuration configuration)
    {
        ImGui.TextUnformatted(Resources.OpenAIAPIKey);
        ImGui.InputText("##APIKey", ref OpenAIApiKeyInput, 100);
        ImGui.SameLine();
        if (ImGui.Button(Resources.Apply + "###OpenAI_API_Key"))
        {
            configuration.OpenAI_API_Key = OpenAIApiKeyInput;
            Plugin.OutputChatLine($"OpenAI API Key {configuration.OpenAI_API_Key} saved successfully.");
            configuration.Save();
        }
        ImGui.TextUnformatted(Resources.OpenAIPriceEstimation);
        ImGui.NewLine();
        ImGui.TextColored(new Vector4(1, 0, 0, 1), Resources.APIKeyWarn);
    }
}
