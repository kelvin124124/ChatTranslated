using ChatTranslated.Utils;
using Dalamud.Game.Text;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatTranslated.Translate
{
    internal partial class TranslationHandler
    {
        public static Dictionary<string, string> TranslationCache = [];

        internal static async Task DetermineLangAndTranslate(XivChatType type, string sender, string message)
        {
            string messageText = ChatHandler.Sanitize(ChatHandler.AutoTranslateRegex().Replace(message, string.Empty));
            try
            {
                var language = await Translator.GTranslator.DetectLanguageAsync(messageText);
                if (Service.configuration.SelectedSourceLanguages.Contains(language.Name))
                {
                    await TranslateChat(type, sender, message);
                }
            }
            catch (Exception GTex)
            {
                Service.pluginLog.Warning($"Google Translate failed to detect language. {GTex}");
                try
                {
                    var language = await Translator.BingTranslator.DetectLanguageAsync(messageText);
                    Service.pluginLog.Debug($"language: {language.Name}");
                    if (Service.configuration.SelectedSourceLanguages.Contains(language.Name))
                    {
                        await TranslateChat(type, sender, message);
                    }
                }
                catch (Exception BTex)
                {
                    Service.pluginLog.Warning($"Bing Translate failed to detect language. {BTex}");
                }
            }
        }

        public static async Task TranslateChat(XivChatType type, string sender, string message)
        {
            string translatedText = await TranslateMessage(message, Service.configuration.SelectedTargetLanguage);
            if (translatedText.IsNullOrWhitespace())
                return;
            else
                OutputTranslation(type, sender, $"{message} || {translatedText}");
        }

        public static async Task TranslateMainWindowMessage(string message)
        {
            string translatedText = await TranslateMessage(message, Service.configuration.SelectedMainWindowTargetLanguage);
            if (translatedText.IsNullOrWhitespace())
                return;
            else
                Service.mainWindow.PrintToOutput($"Translation: {translatedText}");
        }

        public static async Task TranslatePFMessage(string message)
        {
            string translatedText = await TranslateMessage(message, Service.configuration.SelectedTargetLanguage);
            if (translatedText.IsNullOrWhitespace())
                return;
            else
                OutputTranslation(XivChatType.Say, "PF", $"{message} || {translatedText}");
        }

        private static async Task<string> TranslateMessage(string message, string targetLanguage)
        {
            // try get translation from cache
            if (!TranslationCache.TryGetValue(message, out string? translatedText))
            {
                // Translate message if not in cache
                translatedText = await Translator.Translate(message, targetLanguage);
                TranslationCache[message] = translatedText;
            }
            return translatedText;
        }

        public static void OutputTranslation(XivChatType type, string sender, string message)
        {
            Service.mainWindow.PrintToOutput($"{sender}: {message}");
            if (Service.configuration.ChatIntegration)
                Plugin.OutputChatLine(type, sender, $"{message}");
        }

        public static void ClearTranslationCache()
        {
            TranslationCache.Clear();
        }
    }
}
