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
            GPT4Beta,
            OpenAIAPI
        }
        public Mode SelectedMode { get; set; } = Mode.LibreTranslate;

        public int maxAttempts = 3;
        public int waitTime = 200;

        internal string SERVER = "https://translate.kelpcc.com/translate";
        internal string SERVER_SECRET = "";
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
