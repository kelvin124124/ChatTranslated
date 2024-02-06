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

        public List<XivChatType> genericChatTypes = new List<XivChatType>
        {
            XivChatType.Say, XivChatType.Shout, XivChatType.TellIncoming, XivChatType.Party,
            XivChatType.Alliance, XivChatType.FreeCompany, XivChatType.NoviceNetwork,
            XivChatType.Yell, XivChatType.CrossParty, XivChatType.PvPTeam,
        };
        public List<XivChatType> lsChatTypes = new List<XivChatType>
        {
            XivChatType.Ls1, XivChatType.Ls2, XivChatType.Ls3,
            XivChatType.Ls4, XivChatType.Ls5, XivChatType.Ls6, XivChatType.Ls7,
            XivChatType.Ls8,
        };
        public List<XivChatType> cwlsChatTypes = new List<XivChatType>
        {
            XivChatType.CrossLinkShell1, XivChatType.CrossLinkShell2, XivChatType.CrossLinkShell3,
            XivChatType.CrossLinkShell4, XivChatType.CrossLinkShell5, XivChatType.CrossLinkShell6,
            XivChatType.CrossLinkShell7, XivChatType.CrossLinkShell8
        };
        public ICollection<XivChatType> ChatTypes { get; set; } = new List<XivChatType>
        {
            XivChatType.Say, XivChatType.Shout, XivChatType.TellIncoming, XivChatType.Party,
            XivChatType.Alliance, XivChatType.FreeCompany, XivChatType.NoviceNetwork,
            XivChatType.Yell, XivChatType.CrossParty, XivChatType.PvPTeam,
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
