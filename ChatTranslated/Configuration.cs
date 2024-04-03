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
        public enum Mode
        {
            MachineTranslate,
            OpenAI_API,
            GPTProxy
        }

        public string SelectedChatLanguage = "English";
        public string SelectedMainWindowLanguage = "Japanese";

        public Mode SelectedMode { get; set; } = Mode.MachineTranslate;
        public bool Enabled { get; set; } = true;
        public bool ChatIntegration { get; set; } = true;
        public bool EnabledInDuty { get; set; } = false;
        public bool TranslateFr { get; set; } = false;
        public bool TranslateDe { get; set; } = false;
        public bool TranslateEn { get; set; } = false;
        public bool SendChatToDB { get; set; } = false;
        public bool BetterTranslation { get; set; } = false;

        public ICollection<XivChatType> ChatTypes { get; set; } =
        [
            XivChatType.Say, XivChatType.Shout, XivChatType.TellIncoming, XivChatType.Party,
            XivChatType.Alliance, XivChatType.FreeCompany, XivChatType.NoviceNetwork,
            XivChatType.Yell, XivChatType.CrossParty, XivChatType.PvPTeam,
        ];

        public string OpenAI_API_Key { get; set; } = "sk-YOUR-API-KEY";
        public bool warned { get; set; } = false;

        public void Save()
        {
            Service.pluginInterface?.SavePluginConfig(this);
        }
    }
}
