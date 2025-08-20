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
                [Configuration.MachineTranslationEngine.Bing, Configuration.MachineTranslationEngine.Google];

            foreach (var engineType in priorityOrder)
            {
                dynamic translator = engineType switch
                {
                    Configuration.MachineTranslationEngine.Bing => BingTranslator,
                    Configuration.MachineTranslationEngine.Google => GTranslator,
                    _ => null
                };

                if (translator == null) continue;

                try
                {
                    var result = await translator.TranslateAsync(text, targetLanguage).ConfigureAwait(false);
                    string resultText = result.Translation;

                    if (string.IsNullOrWhiteSpace(resultText) || resultText == text)
                        throw new Exception($"{translator.Name} Translate returned an invalid translation.");

                    return (resultText, TranslationMode.MachineTranslate);
                }
                catch (Exception ex)
                {
                    Service.pluginLog.Warning($"{translator.Name} failed.\n{ex.Message}");
                }
            }

            return (text, null);
        }
    }
}
