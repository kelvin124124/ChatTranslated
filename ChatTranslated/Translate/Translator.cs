using ChatTranslated.Chat;
using ChatTranslated.Utils;
using Dalamud.Networking.Http;
using Dalamud.Utility;
using GTranslate.Translators;
using System;
using System.Collections.Generic;
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

        public static readonly Dictionary<string, string> TranslationCache = [];

        public static async Task<Message> TranslateMessage(Message message, string targetLanguage = null!)
        {
            targetLanguage ??= Service.configuration.SelectedTargetLanguage;

            if (string.IsNullOrWhiteSpace(message.CleanedContent)) return message;

            if (TranslationCache.TryGetValue(message.CleanedContent, out var cachedTranslation))
            {
                message.TranslatedContent = cachedTranslation;
                return message;
            }

            string translatedText;
            Configuration.TranslationMode? mode;

            (translatedText, mode) = Service.configuration.SelectedTranslationEngine switch
            {
                Configuration.TranslationEngine.DeepL => await DeeplsTranslate.Translate(message.CleanedContent, targetLanguage),
                Configuration.TranslationEngine.LLM => Service.configuration.LLM_Provider switch
                {
                    0 => await LLMProxyTranslate.Translate(message.CleanedContent, targetLanguage),
                    1 => await OpenAITranslate.Translate(message.CleanedContent, targetLanguage),
                    2 => await OpenAICompatible.Translate(message.CleanedContent, targetLanguage),
                    _ => (message.CleanedContent, null)
                },
                _ => (message.CleanedContent, null)
            };

            message.TranslatedContent = translatedText;
            message.translationMode = mode;

            if (!translatedText.IsNullOrWhitespace() 
                && message.Source != MessageSource.MainWindow 
                && message.translationMode != Configuration.TranslationMode.MachineTranslate)
            {
                TranslationCache[message.CleanedContent] = translatedText;
            }

            return message;
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

        public static void ClearTranslationCache() => TranslationCache.Clear();
    }
}
