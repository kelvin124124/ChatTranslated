using ChatTranslated.Utils;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using System.Threading.Tasks;

namespace ChatTranslated.Translate
{
    internal partial class TranslationHandler
    {
        internal static async Task DetermineLangAndTranslate(XivChatType type, string sender, SeString message)
        {
            string messageText = ChatHandler.RemoveNonTextPayloads(message);
            string language = await Translator.DetermineLanguage(messageText);
            if (Service.configuration.SelectedSourceLanguages.Contains(language))
                await TranslateChat(type, sender, message.TextValue);
            else
                Service.mainWindow.PrintToOutput($"{sender}: {message.TextValue}");
        }

        public static async Task TranslateChat(XivChatType type, string sender, string message)
        {
            string translatedText = await TranslateMessage(message, Service.configuration.SelectedTargetLanguage);
            if (!translatedText.IsNullOrWhitespace())
                OutputTranslation(type, sender, $"{message} || {translatedText}");
        }

        public static async Task TranslateMainWindowMessage(string message)
        {
            string translatedText = await TranslateMessage(message, Service.configuration.SelectedMainWindowTargetLanguage);
            if (!translatedText.IsNullOrWhitespace())
            {
                string reversedTranslation = await Translator.Translate(translatedText, Service.configuration.SelectedPluginLanguage, Configuration.TranslationMode.MachineTranslate);
                Service.mainWindow.PrintToOutput($"Translation: {translatedText} || Original: {message} || Reverse Translation: {reversedTranslation}");
            }
        }

        public static async Task TranslatePFMessage(string message)
        {
            string translatedText = await TranslateMessage(message, Service.configuration.SelectedTargetLanguage);
            if (!translatedText.IsNullOrWhitespace())
                OutputTranslation(XivChatType.Say, "PF", $"{message} || {translatedText}");
        }

        // call translator
        private static async Task<string> TranslateMessage(string message, string targetLanguage)
        {
            string translatedText;

            if (Service.configuration.UseCustomLanguage && !Service.configuration.CustomTargetLanguage.IsNullOrEmpty())
            {
                translatedText = await Translator.Translate(message, Service.configuration.CustomTargetLanguage, Configuration.TranslationMode.MachineTranslate);
            }
            else
            {
                translatedText = await Translator.Translate(message, targetLanguage);
            }

            return translatedText;
        }

        public static void OutputTranslation(XivChatType type, string sender, string message)
        {
            Service.mainWindow.PrintToOutput($"{sender}: {message}");
            if (Service.configuration.ChatIntegration)
                Plugin.OutputChatLine(type, sender, message);
        }
    }
}
