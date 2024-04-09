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
        public int Version { get; set; } = 0;
        public enum TranslationMode
        {
            MachineTranslate,
            DeepL_API,
            OpenAI_API,
            LLMProxy
        }
        public TranslationMode SelectedTranslationMode { get; set; } = TranslationMode.MachineTranslate;

        public enum LanguageSelectionMode
        {
            Default,
            CustomLanguages,
            AllLanguages
        }
        public LanguageSelectionMode SelectedLanguageSelectionMode { get; set; } = LanguageSelectionMode.Default;

        public ICollection<string> SelectedSourceLanguages { get; set; } =
        ["English", "Japanese", "German", "French", "Korean", "Chinese (Simplified)", "Chinese (Traditional)", "Spanish"];

        public string SelectedTargetLanguage = "English";
        public string SelectedMainWindowTargetLanguage = "Japanese";
        public string SelectedPluginLanguage = "English";

        public bool Enabled { get; set; } = true;
        public bool ChatIntegration { get; set; } = true;
        public bool EnabledInDuty { get; set; } = false;
        public bool SendChatToDB { get; set; } = false;

        public ICollection<XivChatType> SelectedChatTypes { get; set; } =
        [
            XivChatType.Say, XivChatType.Shout, XivChatType.TellIncoming, XivChatType.Party,
            XivChatType.Alliance, XivChatType.FreeCompany, XivChatType.NoviceNetwork,
            XivChatType.Yell, XivChatType.CrossParty, XivChatType.PvPTeam,
        ];

        public string OpenAI_API_Key { get; set; } = "sk-YOUR-API-KEY";
        public string DeepL_API_Key { get; set; } = "YOUR-API-KEY:fx";
        public bool openaiWarned { get; set; } = false;
        public bool BetterTranslation { get; set; } = false;
        public string ProxyRegion { get; set; } = "US";

        public void Save()
        {
            Service.pluginInterface?.SavePluginConfig(this);
        }
    }
}
