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
            LibreTranslate,
            GPT3_Proxy,
            OpenAI_API
        }
        public Mode SelectedMode { get; set; } = Mode.LibreTranslate;

        public bool ChatIntergration { get; set; } = true;

        internal static int MAX_ATTEMPTS = 3;
        internal static int WAIT_TIME = 500;

        internal static string LIBRETRANSLATE_API = "https://translate.kelpcc.com/translate";
        internal static string LIBRETRANSLATE_API_KEY = "";

        internal static string PROXY_API = "https://api.pawan.krd/v1/chat/completions";
        internal static string PROXY_API_KEY = "pk-YOUR_API_KEY";
        internal static string OPENAI_API = "https://api.openai.com/v1/chat/completions";
        internal static string OPENAI_API_KEY = "sk-YOUR_API_KEY";

        internal static string MODEL = "gpt-3.5-turbo";
        internal static string PROMPT = "Process this MMORPG chat message from FFXIV:\n" +
            "1. Determine the language.\n" +
            "2. Translate into English.\n" +
            "3. Enclose the translation in ***[TRANSLATED]*** and ***[/TRANSLATED]***.";

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
