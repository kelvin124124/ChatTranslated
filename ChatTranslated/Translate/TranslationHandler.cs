using ChatTranslated.Chat;
using ChatTranslated.Utils;
using Dalamud.Game.Text;
using Dalamud.Utility;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatTranslated.Translate
{
    internal partial class TranslationHandler
    {
        public static readonly Dictionary<string, string> TranslationCache = [];

        internal static async Task DetermineLangAndTranslate(Message chatMessage)
        {
            var language = await Translator.DetermineLanguage(chatMessage.CleanedContent);
            if (Service.configuration.SelectedSourceLanguages.Contains(language))
            {
                await TranslateChat(chatMessage);
            }
            else
            {
                Service.mainWindow.PrintToOutput($"{chatMessage.Sender}: {chatMessage.OriginalContent}");
            }
        }

        public static async Task TranslateChat(Message chatMessage)
        {
            chatMessage.TranslatedContent = await TranslateMessage(chatMessage.OriginalContent.TextValue, Service.configuration.SelectedTargetLanguage, true);
            OutputTranslation(chatMessage);
        }

        public static async Task TranslateMainWindowMessage(Message message)
        {
            message.TranslatedContent = await TranslateMessage(message.OriginalContent.TextValue, Service.configuration.SelectedMainWindowTargetLanguage);

            var reversedTranslationResult = await Translator.Translate(message.TranslatedContent,
                Service.configuration.SelectedPluginLanguage, Configuration.TranslationMode.MachineTranslate);
            var reversedTranslation = reversedTranslationResult.Item1;
            Service.mainWindow.PrintToOutput($"Translation: {message.TranslatedContent} " +
                $"|| Original: {message.OriginalContent} " +
                $"|| Reverse Translation: {reversedTranslation}");
        }

        public static async Task TranslatePFMessage(Message PFmessage)
        {
            PFmessage.TranslatedContent = await TranslateMessage(PFmessage.OriginalContent.TextValue, Service.configuration.SelectedTargetLanguage, true);
            OutputTranslation(PFmessage);
        }

        private static async Task<string> TranslateMessage(string message, string targetLanguage, bool cache = false)
        {
            if (TranslationCache.TryGetValue(message, out var cachedTranslation))
            {
                return cachedTranslation;
            }

            if (Service.configuration.UseCustomLanguage && !Service.configuration.CustomTargetLanguage.IsNullOrEmpty())
            {
                var (result, _) = await Translator.Translate(message, Service.configuration.CustomTargetLanguage, Configuration.TranslationMode.MachineTranslate);
                return result;
            }

            var (translatedText, mode) = await Translator.Translate(message, targetLanguage);
            if (cache && !translatedText.IsNullOrWhitespace() && mode != Configuration.TranslationMode.MachineTranslate)
            {
                TranslationCache[message] = translatedText;
            }

#if DEBUG
            Plugin.OutputChatLine($"Mode: {mode}, Target language: {targetLanguage}");
#endif

            return translatedText;
        }

        public static void OutputTranslation(Message chatMessage)
        {
            Service.mainWindow.PrintToOutput($"{chatMessage.Sender}: {chatMessage.TranslatedContent}");

            if (Service.configuration.ChatIntegration)
            {
                string outputStr = Service.configuration.ChatIntegration_HideOriginal
                    ? chatMessage.TranslatedContent!
                    : $"{chatMessage.OriginalContent.TextValue} || {chatMessage.TranslatedContent}";

                Plugin.OutputChatLine(chatMessage.Type ?? XivChatType.Say, chatMessage.Sender, outputStr);
            }
        }

        public static void ClearTranslationCache() => TranslationCache.Clear();
    }
}
