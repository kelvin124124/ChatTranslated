using ChatTranslated.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatTranslated.Translate
{
    internal static class DeepLTranslate
    {
        private const string DefaultContentType = "application/json";

        public static async Task<string> Translate(string text, string targetLanguage)
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
                    return !string.IsNullOrWhiteSpace(translated)
                        ? (targetLanguage == "Chinese (Traditional)" ? await MachineTranslate.Translate(translated, "Chinese (Traditional)") : translated)
                        : throw new Exception("Translation not found in the expected JSON structure.");
                }
                catch
                {
                    Service.pluginLog.Warning("DeepL Translate failed to translate. Falling back to machine translation.");
                    return await MachineTranslate.Translate(text, targetLanguage);
                }
            }
            return "Target language not supported by DeepL.";
        }

        private static bool TryGetLanguageCode(string language, out string? languageCode)
        {
            languageCode = language switch
            {
                "English" => "EN-GB",
                "Japanese" => "JA",
                "German" => "DE",
                "French" => "FR",
                "Korean" => "KO",
                "Chinese (Simplified)" => "ZH",
                "Chinese (Traditional)" => "ZH",
                "Spanish" => "ES",
                _ => null
            };
            return !string.IsNullOrEmpty(languageCode);
        }
    }
}
