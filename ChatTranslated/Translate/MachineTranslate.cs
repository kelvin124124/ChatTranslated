using ChatTranslated.Utils;
using GTranslate.Translators;
using System;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Translate;

internal static class MachineTranslate
{
    private static readonly Lazy<GoogleTranslator2> LazyG2Translator = new(() => new GoogleTranslator2(TranslationHandler.HttpClient));
    private static readonly Lazy<MicrosoftTranslator> LazyMicrosoftTranslator = new(() => new MicrosoftTranslator(TranslationHandler.HttpClient));
    private static readonly Lazy<YandexTranslator> LazyYTranslator = new(() => new YandexTranslator(TranslationHandler.HttpClient));
    public static GoogleTranslator2 G2Translator => LazyG2Translator.Value;
    public static MicrosoftTranslator MicrosoftTranslator => LazyMicrosoftTranslator.Value;
    public static YandexTranslator YTranslator => LazyYTranslator.Value;

    public static async Task<(string, TranslationMode?)> Translate(string text, string targetLanguage)
    {
        // Try Microsoft first, then Google2 as fallback
        try
        {
            var result = await MicrosoftTranslator.TranslateAsync(text, targetLanguage).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(result.Translation) && result.Translation != text)
                return (result.Translation, TranslationMode.MachineTranslate);
            Service.pluginLog.Warning("Microsoft Translate returned an invalid translation.");
        }
        catch (Exception ex)
        {
            Service.pluginLog.Warning($"Microsoft failed.\n{ex.Message}");
        }

        try
        {
            var result = await G2Translator.TranslateAsync(text, targetLanguage).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(result.Translation) && result.Translation != text)
                return (result.Translation, TranslationMode.MachineTranslate);
            Service.pluginLog.Warning("Google2 Translate returned an invalid translation.");
        }
        catch (Exception ex)
        {
            Service.pluginLog.Warning($"Google2 failed.\n{ex.Message}");
        }

        return (text, null);
    }
}
