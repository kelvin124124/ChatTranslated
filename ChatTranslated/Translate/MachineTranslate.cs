using ChatTranslated.Utils;
using GTranslate.Translators;
using System;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Translate;

internal static class MachineTranslate
{
    public static readonly GoogleTranslator2 G2Translator = new(TranslationHandler.HttpClient);
    public static readonly MicrosoftTranslator MicrosoftTranslator = new(TranslationHandler.HttpClient);
    public static readonly YandexTranslator YTranslator = new(TranslationHandler.HttpClient);

    // tried in order until one returns a real translation
    private static readonly ITranslator[] Translators = [MicrosoftTranslator, G2Translator];

    public static async Task<(string, TranslationMode?)> Translate(string text, string targetLanguage)
    {
        foreach (var translator in Translators)
        {
            try
            {
                var result = await translator.TranslateAsync(text, targetLanguage).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(result.Translation) && result.Translation != text)
                    return (result.Translation, TranslationMode.MachineTranslate);
                Service.pluginLog.Warning($"{translator.Name} returned an invalid translation.");
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"{translator.Name} failed.\n{ex.Message}");
            }
        }

        return (text, null);
    }
}
