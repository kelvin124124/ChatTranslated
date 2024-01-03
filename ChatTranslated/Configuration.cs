using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

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

        public bool ChatIntergration { get; set; } = true;

        internal static string OPENAI_API = "https://api.openai.com/v1/chat/completions";
        internal static string? OPENAI_API_KEY;

        // gpt-3.5-turbo or gpt-4 or gpt-4-turbo (VERY expensive)
        internal static string MODEL = "gpt-3.5-turbo";
        internal static string PROMPT = "Process this MMORPG chat message from FFXIV:\n" +
            "1. Determine the language.\n" +
            "2. Translate into English.\n" +
            "3. Enclose the translation in [TRANSLATED] AND [/TRANSLATED].";

        // the below exist just to make saving less cumbersome
        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface!.SavePluginConfig(this);
        }
    }
}
