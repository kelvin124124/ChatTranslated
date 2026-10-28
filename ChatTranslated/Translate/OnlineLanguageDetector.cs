using ChatTranslated.Utils;
using GTranslate.Translators;
using System;
using System.Threading.Tasks;

namespace ChatTranslated.Translate;

internal static class OnlineLanguageDetector
{
    // tried in order until one answers
    private static readonly ITranslator[] Detectors =
        [MachineTranslate.YTranslator, MachineTranslate.G2Translator, MachineTranslate.MicrosoftTranslator];

    public static async Task<string?> DetectIsoAsync(string text)
    {
        foreach (var detector in Detectors)
        {
            try
            {
                var iso = (await detector.DetectLanguageAsync(text).ConfigureAwait(false))?.ISO6391?.Split('-')[0].ToLowerInvariant();
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
