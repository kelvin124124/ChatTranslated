using ChatTranslated.Utils;
using Dalamud.Configuration;
using Dalamud.Game.Text;
using System;
using System.Collections.Generic;

namespace ChatTranslated;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 8;

    public enum TranslationEngine
    {
        DeepL,
        LLM
    }

    public enum TranslationMode
    {
        MachineTranslate,
        DeepL,
        OpenAI,
        LLMProxy
    }

    public enum DetectionSource
    {
        Local,
        Online
    }

    public enum LanguageSelectionMode
    {
        Inclusive,
        Exclusive
    }

    public TranslationEngine SelectedTranslationEngine { get; set; } = TranslationEngine.DeepL;
    public DetectionSource SelectedDetectionSource { get; set; } = DetectionSource.Online;
    public LanguageSelectionMode SelectedLanguageSelectionMode { get; set; } = LanguageSelectionMode.Inclusive;

    public List<string> KnownLanguages { get; set; } = [];
    public List<string> SelectedSourceLanguages { get; set; } = [];

    public bool ShowedWizard { get; set; } = false;

    public string SelectedTargetLanguage { get; set; } = "English";
    public string SelectedMainWindowTargetLanguage { get; set; } = "Japanese";
    public string SelectedPluginLanguage { get; set; } = "English";

    // toggles are fields so the config UI can bind them by ref
    public bool Enabled = true;
    public bool ChatIntegration = true;
    public bool ChatIntegration_HideOriginal = false;
    public bool ChatIntegration_ShowColoredText = false;
    public bool ChatIntegration_UseEchoChannel = false;
    public bool EnabledInDuty = false;

    public string CustomTargetLanguage = "";
    public bool UseCustomLanguage = false;

    public string EffectiveTargetLanguage =>
        UseCustomLanguage && !string.IsNullOrWhiteSpace(CustomTargetLanguage)
            ? CustomTargetLanguage
            : SelectedTargetLanguage;

    public List<XivChatType> SelectedChatTypes { get; set; } = null!;

    public short LLM_Provider { get; set; } = 0;
    public string OpenAI_API_Key { get; set; } = "sk-YOUR-API-KEY";
    public string OpenAI_Model { get; set; } = "gpt-5-mini";
    public string DeepL_API_Key { get; set; } = "YOUR-API-KEY:fx";
    public string Proxy_Url { get; set; } = "https://cfv6.kelpcc.com";
    public string Proxy_API_Key { get; set; } = "YOUR-API-KEY";
    public string LLM_API_Key { get; set; } = "YOUR-API-KEY";
    public string LLM_API_endpoint { get; set; } = "https://openrouter.ai/api/v1/chat/completions";
    public string LLM_Model { get; set; } = "google/gemini-2.0-flash-001";
    public bool UseContext = true;
    public bool UseCustomPrompt = false;
    public string LLM_CustomPrompt { get; set; } = "";

    public bool EnableTranslationCache = true;

    public void Save()
    {
        Service.pluginInterface.SavePluginConfig(this);
    }
}
