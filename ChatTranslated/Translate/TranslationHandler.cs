using ChatTranslated.Utils;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatTranslated.Translate
{
    internal partial class TranslationHandler
    {
        public static Dictionary<string, string> TranslationCache = [];

        internal static async Task DetermineLangAndTranslate(XivChatType type, string sender, SeString message)
        {
            string language = await DetermineLanguage(message);
            if (Service.configuration.SelectedSourceLanguages.Contains(language))
                await TranslateChat(type, sender, message.TextValue);
            else
                Service.mainWindow.PrintToOutput($"{sender}: {message.TextValue}");
        }

        public static async Task<string> DetermineLanguage(SeString message)
        {
            string? langStr = null;
            string messageText = ChatHandler.RemoveNonTextPayloads(message);
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
            return (langStr ?? "unknown");
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
            {
                string reversedTranslation = await Translator.Translate(translatedText, Service.configuration.SelectedPluginLanguage, Configuration.TranslationMode.MachineTranslate);
                Service.mainWindow.PrintToOutput($"Translation: {translatedText}\nReverse Translation: {reversedTranslation}");
            }
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
