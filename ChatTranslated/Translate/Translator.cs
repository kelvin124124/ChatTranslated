using ChatTranslated.Utils;
using Dalamud.Networking.Http;
using GTranslate.Translators;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace ChatTranslated.Translate
{
    internal static class Translator
    {
        internal static readonly HttpClient HttpClient = new(new SocketsHttpHandler
        {
            ConnectCallback = new HappyEyeballsCallback().ConnectCallback,
            AutomaticDecompression = DecompressionMethods.All
        })
        {
            DefaultRequestVersion = HttpVersion.Version30,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
            Timeout = TimeSpan.FromSeconds(10)
        };

        public static readonly GoogleTranslator GTranslator = new(HttpClient);
        public static readonly BingTranslator BingTranslator = new(HttpClient);

        public static async Task<(string, Configuration.TranslationMode?)> Translate(string text, string targetLanguage)
        {
            text = ChatHandler.Sanitize(text);
            if (string.IsNullOrWhiteSpace(text)) return (text, null);

            return Service.configuration.SelectedTranslationEngine switch
            {
                Configuration.TranslationEngine.DeepL => await DeeplsTranslate.Translate(text, targetLanguage),
                Configuration.TranslationEngine.LLM => (Service.configuration.LLM_Provider == 0) ?
                    await LLMProxyTranslate.Translate(text, targetLanguage) :
                    await OpenAITranslate.Translate(text, targetLanguage),
                _ => (text, null)
            };
        }

        public static async Task<(string, Configuration.TranslationMode?)> Translate(string text, string targetLanguage, Configuration.TranslationMode translationMode)
        {
            text = ChatHandler.Sanitize(text);
            if (string.IsNullOrWhiteSpace(text)) return (text, null);

            return translationMode switch
            {
                Configuration.TranslationMode.MachineTranslate => await MachineTranslate.Translate(text, targetLanguage),
                Configuration.TranslationMode.DeepL => await DeeplsTranslate.Translate(text, targetLanguage),
                Configuration.TranslationMode.OpenAI => await OpenAITranslate.Translate(text, targetLanguage),
                Configuration.TranslationMode.LLMProxy => await LLMProxyTranslate.Translate(text, targetLanguage),
                _ => (text, null)
            };
        }

        public static async Task<string> DetermineLanguage(string messageText)
        {
            try
            {
                var language = await GTranslator.DetectLanguageAsync(messageText);
                Service.pluginLog.Debug($"{messageText}\n -> language: {language.Name}");
#if DEBUG
                Plugin.OutputChatLine($"{messageText}\n -> language: {language.Name}");
#endif
                return language.Name;
            }
            catch (Exception gEx)
            {
                Service.pluginLog.Warning($"Google Translate failed to detect language. {gEx}");
                try
                {
                    var language = await BingTranslator.DetectLanguageAsync(messageText);
                    Service.pluginLog.Debug($"{messageText}\n -> language: {language.Name}");
                    return language.Name;
                }
                catch (Exception bEx)
                {
                    Service.pluginLog.Warning($"Bing Translate failed to detect language. {bEx}");
                    return "unknown";
                }
            }
        }
    }
}
