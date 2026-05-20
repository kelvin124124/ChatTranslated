using ChatTranslated.Utils;
using GTranslate;
using GTranslate.Translators;
using System;
using System.Threading.Tasks;

namespace ChatTranslated.Translate;

internal static class OnlineLanguageDetector
{
    private static readonly Lazy<YandexTranslator> LazyYTranslator =
        new(() => new YandexTranslator(TranslationHandler.HttpClient));
    public static YandexTranslator YTranslator => LazyYTranslator.Value;

    public static async Task<string?> DetectIsoAsync(string text)
    {
        Func<Task<Language>>[] detectors =
        [
            () => YTranslator.DetectLanguageAsync(text),
            () => MachineTranslate.GTranslator.DetectLanguageAsync(text),
            () => MachineTranslate.BingTranslator.DetectLanguageAsync(text),
        ];

        foreach (var detect in detectors)
        {
            try
            {
                var iso = (await detect().ConfigureAwait(false))?.ISO6391?.Split('-')[0].ToLowerInvariant();
                if (!string.IsNullOrEmpty(iso))
                {
                    Service.pluginLog.Debug($"Online detect → {iso}");
                    return iso;
                }
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"Online language detection failed: {ex.Message}");
            }
        }

        return null;
    }
}
