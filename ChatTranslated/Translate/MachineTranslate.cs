using ChatTranslated.Utils;
using GTranslate.Translators;
using System;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Translate
{
    internal static class MachineTranslate
    {
        private static readonly Lazy<GoogleTranslator> LazyGTranslator = new(() => new GoogleTranslator(TranslationHandler.HttpClient));
        private static readonly Lazy<BingTranslator> LazyBingTranslator = new(() => new BingTranslator(TranslationHandler.HttpClient));
        public static GoogleTranslator GTranslator => LazyGTranslator.Value;
        public static BingTranslator BingTranslator => LazyBingTranslator.Value;

        public static async Task<(string, TranslationMode?)> Translate(string text, string targetLanguage)
        {
            // Get the configured priority order of machine translation engines
            var priorityOrder = Service.configuration.MachineTranslationPriority ?? 
                [Configuration.MachineTranslationEngine.DeepL, Configuration.MachineTranslationEngine.Google, Configuration.MachineTranslationEngine.Bing];

            foreach (var engineType in priorityOrder)
            {
                try
                {
                    string resultText;
                    
                    switch (engineType)
                    {
                        case Configuration.MachineTranslationEngine.DeepL:
                            // Use DeepL translation - try DeeplsTranslate first, then fallback to DeepL API
                            var (deeplResult, deeplMode) = await DeeplsTranslate.Translate(text, targetLanguage);
                            if (deeplMode != null)
                            {
                                return (deeplResult, TranslationMode.MachineTranslate);
                            }
                            continue; // If DeepL fails, try next engine
                            
                        case Configuration.MachineTranslationEngine.Bing:
                            var bingResult = await BingTranslator.TranslateAsync(text, targetLanguage).ConfigureAwait(false);
                            resultText = bingResult.Translation;
                            break;
                            
                        case Configuration.MachineTranslationEngine.Google:
                            var googleResult = await GTranslator.TranslateAsync(text, targetLanguage).ConfigureAwait(false);
                            resultText = googleResult.Translation;
                            break;
                            
                        default:
                            continue;
                    }

                    if (string.IsNullOrWhiteSpace(resultText) || resultText == text)
                        throw new Exception($"{engineType} Translate returned an invalid translation.");

                    return (resultText, TranslationMode.MachineTranslate);
                }
                catch (Exception ex)
                {
                    Service.pluginLog.Warning($"{engineType} failed.\n{ex.Message}");
                }
            }

            return (text, null);
        }
    }
}
