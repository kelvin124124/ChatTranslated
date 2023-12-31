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
        public bool ChatIntergration { get; set; } = false;

        public int maxAttempts = 3;
        public int waitTime = 200;

        internal string LIBRETRANSLATE_API = "https://translate.kelpcc.com/translate";
        internal string LIBRETRANSLATE_API_KEY = "";
        internal string PROXY_API = "https://api.pawan.krd/v1/chat/completions";
        internal string PROXY_API_KEY = "pk-YOUR_API_KEY";
        internal string OPENAI_API = "https://api.openai.com/v1/chat/completions";
        internal string OPENAI_API_KEY = "sk-YOUR_API_KEY";

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
