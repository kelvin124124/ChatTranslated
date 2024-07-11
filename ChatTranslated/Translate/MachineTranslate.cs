using ChatTranslated.Utils;
using GTranslate.Translators;
using System;
using System.Threading.Tasks;

namespace ChatTranslated.Translate
{
    internal static class MachineTranslate
    {
        public static GoogleTranslator GTranslator = new(Translator.HttpClient);
        public static BingTranslator BingTranslator = new(Translator.HttpClient);

        public static async Task<string> Translate(string text, string targetLanguage)
        {
            try
            {
                var result = await GTranslator.TranslateAsync(text, targetLanguage).ConfigureAwait(false);
                return result.Translation;
            }
            catch(Exception GTex)
            {
                Service.pluginLog.Warning($"Google Translate failed to translate. Falling back to Bing Translate.\n{GTex.Message}");
                try
                {
                    var result = await BingTranslator.TranslateAsync(text, targetLanguage).ConfigureAwait(false);
                    return result.Translation;
                }
                catch (Exception BTex)
                {
                    Service.pluginLog.Error($"Bing Translate failed to translate. Returning original text.\n{BTex.Message}");
                    return text;
                }
            }
        }
    }
}
