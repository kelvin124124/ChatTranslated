using ChatTranslated.Utils;
using Dalamud.Networking.Http;
using GTranslate.Translators;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

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

        public static GoogleTranslator GTranslator = new(HttpClient);
        public static BingTranslator BingTranslator = new(HttpClient);

        public static async Task<string> Translate(string text, string targetLanguage, TranslationMode? translationMode = null)
        {
            text = ChatHandler.Sanitize(text);
            if (string.IsNullOrWhiteSpace(text)) return text;

            var mode = translationMode ?? Service.configuration.SelectedTranslationMode;

            return mode switch
            {
                Configuration.TranslationMode.MachineTranslate => await MachineTranslate.Translate(text, targetLanguage),
                Configuration.TranslationMode.DeepL_API => await DeepLTranslate.Translate(text, targetLanguage),
                Configuration.TranslationMode.OpenAI_API => await OpenAITranslate.Translate(text, targetLanguage),
                Configuration.TranslationMode.LLMProxy => await LLMProxyTranslate.Translate(text, targetLanguage),
                _ => text
            };
        }

        public static async Task<string> DetermineLanguage(string messageText)
        {
            string? langStr = null;
            try
            {
                var language = await Translator.GTranslator.DetectLanguageAsync(messageText);
                Service.pluginLog.Debug($"{messageText}\n -> language: {language.Name}");
#if DEBUG
                Plugin.OutputChatLine($"{messageText}\n -> language: {language.Name}");
#endif
                langStr = language.Name;
            }
            catch (Exception GTex)
            {
                Service.pluginLog.Warning($"Google Translate failed to detect language. {GTex}");
                try
                {
                    var language = await Translator.BingTranslator.DetectLanguageAsync(messageText);
                    Service.pluginLog.Debug($"{messageText}\n -> language: {language.Name}");
                    langStr = language.Name;
                }
                catch (Exception BTex)
                {
                    Service.pluginLog.Warning($"Bing Translate failed to detect language. {BTex}");
                }
            }
            return langStr ?? "unknown";
        }
    }
}
