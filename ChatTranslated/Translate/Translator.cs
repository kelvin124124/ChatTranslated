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
            Timeout = TimeSpan.FromSeconds(20)
        };

        public static readonly GoogleTranslator GTranslator = new(HttpClient);
        public static readonly BingTranslator BingTranslator = new(HttpClient);
        public static readonly YandexTranslator YTranslator = new(HttpClient);

        public static readonly Dictionary<string, string> TranslationCache = [];

        public static async Task<Message> TranslateMessage(Message message, string targetLanguage = null!)
        {
            targetLanguage ??= Service.configuration.SelectedTargetLanguage;

            if (string.IsNullOrWhiteSpace(message.CleanedContent)) return message;

            if (TranslationCache.TryGetValue(message.OriginalContent.TextValue, out var cachedTranslation))
            {
                message.TranslatedContent = cachedTranslation;
                return message;
            }

            string translatedText;
            Configuration.TranslationMode? mode;

            (translatedText, mode) = Service.configuration.SelectedTranslationEngine switch
            {
                Configuration.TranslationEngine.DeepL => await DeeplsTranslate.Translate(message.OriginalContent.TextValue, targetLanguage),
                Configuration.TranslationEngine.LLM => Service.configuration.LLM_Provider switch
                {
                    0 => await LLMProxyTranslate.Translate(message, targetLanguage),
                    1 => await OpenAITranslate.Translate(message, targetLanguage),
                    2 => await OpenAICompatible.Translate(message, targetLanguage),
                    _ => (message.OriginalContent.TextValue, null)
                },
                _ => (message.OriginalContent.TextValue, null)
            };

            message.TranslatedContent = translatedText;
            message.translationMode = mode;

            if (!translatedText.IsNullOrWhitespace()
                && message.Source != MessageSource.MainWindow
                && message.translationMode != Configuration.TranslationMode.MachineTranslate)
            {
                TranslationCache[message.OriginalContent.TextValue] = translatedText;
            }

            return message;
        }

        public static async Task<string> DetermineLanguage(string messageText)
        {
            var translators = new Func<Task<string>>[]
            {
                async () => (await YTranslator.DetectLanguageAsync(messageText)).Name,
                async () => (await GTranslator.DetectLanguageAsync(messageText)).Name,
                async () => (await BingTranslator.DetectLanguageAsync(messageText)).Name
            };

            foreach (var translator in translators)
            {
                try
                {
                    var language = await translator();
                    if (language.IsNullOrEmpty())
                    {
                        throw new Exception($"Language detection failed.");
                    }
                    Service.pluginLog.Debug($"{messageText}\n -> language: {language}");
                    return language;
                }
                catch (Exception ex)
                {
                    Service.pluginLog.Warning($"Failed to detect language: {ex.Message}");
                }
            }

            Service.pluginLog.Error("All language detection attempts failed.");
            return "unknown";
        }

        public static void ClearTranslationCache() => TranslationCache.Clear();
    }
}
