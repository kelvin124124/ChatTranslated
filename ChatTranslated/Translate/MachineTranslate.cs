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
    private static readonly Lazy<YandexTranslator> LazyYTranslator = new(() => new YandexTranslator(TranslationHandler.HttpClient));
    public static GoogleTranslator GTranslator => LazyGTranslator.Value;
    public static BingTranslator BingTranslator => LazyBingTranslator.Value;
    public static YandexTranslator YTranslator => LazyYTranslator.Value;

    public static async Task<(string, TranslationMode?)> Translate(string text, string targetLanguage)
    {
        // Try Google first, then Bing as fallback
        var (translated, mode) = await TranslateWith(GTranslator, text, targetLanguage).ConfigureAwait(false);
        if (mode != null)
            return (translated, mode);

        return await TranslateWith(BingTranslator, text, targetLanguage).ConfigureAwait(false);
    }

    public static async Task<(string, TranslationMode?)> TranslateWith(ITranslator translator, string text, string targetLanguage)
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

        return (text, null);
    }
}
