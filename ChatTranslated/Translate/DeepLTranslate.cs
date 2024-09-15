using ChatTranslated.Utils;
using Dalamud.Utility;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Translate
{
    internal static class DeepLTranslate
    {
        private const string DefaultContentType = "application/json";

        public static async Task<(string, TranslationMode?)> Translate(string text, string targetLanguage)
        {
            if (TryGetLanguageCode(targetLanguage, out var languageCode))
            {
                var requestBody = new { text = new[] { text }, target_lang = languageCode, context = "FFXIV, MMORPG" };
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api-free.deepl.com/v2/translate")
                {
                    Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, DefaultContentType),
                    Headers = { { HttpRequestHeader.Authorization.ToString(), $"DeepL-Auth-Key {Service.configuration.DeepL_API_Key}" } }
                };

                try
                {
                    var response = await Translator.HttpClient.SendAsync(requestMessage).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var translated = JObject.Parse(jsonResponse)["translations"]?[0]?["text"]?.ToString().Trim();

                    if (translated.IsNullOrWhitespace())
                    {
                        throw new Exception("Translation not found in the expected JSON structure.");
                    }

                    return (translated, TranslationMode.DeepL);
                }
                catch (Exception ex)
                {
                    Service.pluginLog.Warning($"DeepL Translate failed to translate. Falling back to machine translation.\n{ex.Message}");
                    return await MachineTranslate.Translate(text, targetLanguage);
                }
            }
            return ("Target language not supported by DeepL.", null);
        }

        internal static bool TryGetLanguageCode(string language, out string? languageCode)
        {
            languageCode = language switch
            {
                "English" => "EN",
                "Japanese" => "JA",
                "German" => "DE",
                "French" => "FR",
                "Chinese (Simplified)" => "ZH-HANS",
                "Chinese (Traditional)" => "ZH-HANT",
                "Korean" => "KO",
                "Spanish" => "ES",
                "Arabic" => "AR",
                "Bulgarian" => "BG",
                "Czech" => "CS",
                "Danish" => "DA",
                "Dutch" => "NL",
                "Estonian" => "ET",
                "Finnish" => "FI",
                "Greek" => "EL",
                "Hungarian" => "HU",
                "Indonesian" => "ID",
                "Italian" => "IT",
                "Latvian" => "LV",
                "Lithuanian" => "LT",
                "Norwegian Bokmal" => "NB",
                "Polish" => "PL",
                "Portuguese" => "PT",
                "Romanian" => "RO",
                "Russian" => "RU",
                "Slovak" => "SK",
                "Slovenian" => "SL",
                "Swedish" => "SV",
                "Turkish" => "TR",
                "Ukrainian" => "UK",
                _ => null
            };
            return !string.IsNullOrEmpty(languageCode);
        }
    }
}
