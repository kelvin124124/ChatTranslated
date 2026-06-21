using ChatTranslated.Utils;
using GTranslate;
using System;
using System.Threading.Tasks;

namespace ChatTranslated.Translate;

internal static class OnlineLanguageDetector
{
    public static async Task<string?> DetectIsoAsync(string text)
    {
        Func<Task<Language>>[] detectors =
        [
            () => MachineTranslate.YTranslator.DetectLanguageAsync(text),
            () => MachineTranslate.G2Translator.DetectLanguageAsync(text),
            () => MachineTranslate.MicrosoftTranslator.DetectLanguageAsync(text),
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
