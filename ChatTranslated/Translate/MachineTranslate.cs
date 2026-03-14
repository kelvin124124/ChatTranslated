using ChatTranslated.Utils;
using GTranslate.Translators;
using System;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Translate;

internal static class MachineTranslate
{
    private static readonly Lazy<GoogleTranslator> LazyGTranslator = new(() => new GoogleTranslator(TranslationHandler.HttpClient));
    private static readonly Lazy<BingTranslator> LazyBingTranslator = new(() => new BingTranslator(TranslationHandler.HttpClient));
    public static GoogleTranslator GTranslator => LazyGTranslator.Value;
    public static BingTranslator BingTranslator => LazyBingTranslator.Value;

    public static async Task<(string, TranslationMode?)> Translate(string text, string targetLanguage)
    {
        // Try Bing first, then Google as fallback
        try
        {
            var result = await BingTranslator.TranslateAsync(text, targetLanguage).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(result.Translation) && result.Translation != text)
                return (result.Translation, TranslationMode.MachineTranslate);
            Service.pluginLog.Warning("Bing Translate returned an invalid translation.");
        }
        catch (Exception ex)
        {
            Service.pluginLog.Warning($"Bing failed.\n{ex.Message}");
        }

        try
        {
            var result = await GTranslator.TranslateAsync(text, targetLanguage).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(result.Translation) && result.Translation != text)
                return (result.Translation, TranslationMode.MachineTranslate);
            Service.pluginLog.Warning("Google Translate returned an invalid translation.");
        }
        catch (Exception ex)
        {
            Service.pluginLog.Warning($"Google failed.\n{ex.Message}");
        }

        return (text, null);
    }
}
