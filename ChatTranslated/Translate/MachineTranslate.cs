using ChatTranslated.Utils;
using GTranslate.Translators;
using System;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Translate
{
    internal static class MachineTranslate
    {
        public static GoogleTranslator GTranslator = new(Translator.HttpClient);
        public static BingTranslator BingTranslator = new(Translator.HttpClient);

        public static async Task<(string, TranslationMode?)> Translate(string text, string targetLanguage)
        {
            try
            {
                var result = await GTranslator.TranslateAsync(text, targetLanguage).ConfigureAwait(false);
                return (result.Translation, TranslationMode.MachineTranslate);
            }
            catch (Exception GTex)
            {
                Service.pluginLog.Warning($"Google Translate failed to translate. Falling back to Bing Translate.\n{GTex.Message}");
                try
                {
                    var result = await BingTranslator.TranslateAsync(text, targetLanguage).ConfigureAwait(false);
                    return (result.Translation, TranslationMode.MachineTranslate);
                }
                catch (Exception BTex)
                {
                    Service.pluginLog.Error($"Bing Translate failed to translate. Returning original text.\n{BTex.Message}");
                    return (text, null);
                }
            }
        }
    }
}
