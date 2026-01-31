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

        public static async Task<(string, TranslationMode?)> Translate(string text, string targetLang)
        {
            var requestBody = new { text = new[] { text }, target_lang = targetLang, context = "FFXIV, MMORPG" };
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api-free.deepl.com/v2/translate")
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, DefaultContentType),
                Headers = { { HttpRequestHeader.Authorization.ToString(), $"DeepL-Auth-Key {Service.configuration.DeepL_API_Key}" } }
            };

            var response = await TranslationHandler.HttpClient.SendAsync(requestMessage).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var translated = JObject.Parse(jsonResponse)["translations"]?[0]?["text"]?.ToString().Trim();

            if (translated.IsNullOrWhitespace())
            {
                throw new Exception("Translation not found in the expected JSON structure.");
            }

            return (translated, TranslationMode.DeepL);
        }
    }
}
