using ChatTranslated.Utils;
using Dalamud.Configuration;
using Dalamud.Game.Text;
using System;
using System.Collections.Generic;

namespace ChatTranslated
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 4;
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
        public TranslationEngine SelectedTranslationEngine { get; set; } = TranslationEngine.DeepL;

        public enum LanguageSelectionMode
        {
            Default,
            CustomLanguages,
            AllLanguages
        }
        public LanguageSelectionMode SelectedLanguageSelectionMode { get; set; } = LanguageSelectionMode.Default;

        public List<string> SelectedSourceLanguages { get; set; } = [];

        public string SelectedTargetLanguage { get; set; } = "English";
        public string SelectedMainWindowTargetLanguage { get; set; } = "Japanese";
        public string SelectedPluginLanguage { get; set; } = "English";

        public bool Enabled { get; set; } = true;
        public bool ChatIntegration { get; set; } = true;
        public bool EnabledInDuty { get; set; } = false;
        public bool SendChatToDB { get; set; } = false;

        public string CustomTargetLanguage = "";
        public bool UseCustomLanguage = false;

        public List<XivChatType> SelectedChatTypes { get; set; } =
        [
            XivChatType.Say, XivChatType.Shout, XivChatType.TellIncoming, XivChatType.Party,
            XivChatType.Alliance, XivChatType.FreeCompany, XivChatType.NoviceNetwork,
            XivChatType.Yell, XivChatType.CrossParty, XivChatType.PvPTeam,
        ];

        public short LLM_Provider { get; set; } = 0;
        public string OpenAI_API_Key { get; set; } = "sk-YOUR-API-KEY";
        public string DeepL_API_Key { get; set; } = "YOUR-API-KEY:fx";
        public string Proxy_Url { get; set; } = "https://cfv3.kelpcc.com";
        public string Proxy_API_Key { get; set; } = "YOUR-API-KEY";

        public void Save()
        {
            Service.pluginInterface?.SavePluginConfig(this);
        }
    }
}
