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
            OpenAI_API
        }
        public Mode SelectedMode { get; set; } = Mode.MachineTranslate;

        public bool ChatIntegration { get; set; } = true;
        public bool TranslateFrDe { get; set; } = false;

        public ICollection<XivChatType> ChatTypes { get; set; } = new List<XivChatType>
        {
            XivChatType.CrossParty,
            XivChatType.Party,
            XivChatType.Alliance
        };

        public static string OPENAI_API = "https://api.openai.com/v1/chat/completions";
        internal static string? OPENAI_API_KEY;
        public bool warned { get; set; } = false;

        // gpt-3.5-turbo or gpt-4 / gpt-4-turbo (VERY expensive)
        internal static string MODEL = "gpt-3.5-turbo";
        internal static string PROMPT = "Process this MMORPG chat message from FFXIV:\n" +
            "1. Determine the language.\n" +
            "2. Translate into English.\n" +
            "3. Enclose the translation in [TRANSLATED] AND [/TRANSLATED].";

        public void Save()
        {
            Service.pluginInterface!.SavePluginConfig(this);
        }
    }
}
