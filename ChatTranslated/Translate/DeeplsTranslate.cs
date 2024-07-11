using ChatTranslated.Utils;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatTranslated.Translate
{
    internal static class DeeplsTranslate
    {
        private static readonly Random Random = new();
        public static async Task<string> Translate(string message, string langCode)
        {
            string postData = PreparePostData(langCode, message);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://www2.deepl.com/jsonrpc")
            {
                Content = new StringContent(postData, Encoding.UTF8, "application/json")
            };
            SetHeaders(request);

            try
            {
                var response = await Translator.HttpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                // Extract the translated text
                var jsonDoc = JsonDocument.Parse(jsonResponse);
                var translated = jsonDoc.RootElement
                    .GetProperty("result")
                    .GetProperty("texts")[0]
                    .GetProperty("text")
                    .GetString();

                if (translated.IsNullOrWhitespace())
                {
                    throw new Exception("Translation not found in the expected JSON structure.");
                }

                if (Service.configuration.SelectedTargetLanguage == "Chinese (Traditional)")
                    return await MachineTranslate.Translate(translated, "Chinese (Traditional)");
                else
                    return translated;
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"DeeplsTranslate failed to translate. Falling back to DeepL API / machine translation.\n{ex.Message}");
                if (Service.configuration.DeepL_API_Key != "YOUR-API-KEY:fx") // fallback to official DeepL API if the key is not the default
                    return await DeepLTranslate.Translate(message, langCode);
                else
                    return await MachineTranslate.Translate(message, langCode);
            }
        }

        private static string PreparePostData(string targetLang, string text)
        {
            ulong id = ((ulong)Random.Next(8300000, 8400000) * 1000) + 1;
            var postData = new
            {
                jsonrpc = "2.0",
                method = "LMT_handle_texts",
                @params = new
                {
                    splitting = "newlines",
                    lang = new { source_lang_user_selected = "auto", target_lang = targetLang },
                    texts = new[] { new { text } },
                    timestamp = GetTimeStamp(text)
                },
                id,
                timestamp = GetTimeStamp(text),
            };

            string postDataJson = JsonSerializer.Serialize(postData);
            postDataJson = postDataJson.Replace("\"method\":\"", (id + 5) % 29 == 0 || (id + 3) % 13 == 0 ? "\"method\" : \"" : "\"method\": \"");

            return postDataJson;
        }

        private static long GetTimeStamp(string text)
        {
            var iCount = text.Count(c => c == 'i');
            long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return iCount == 0 ? ts : ts - (ts % (iCount + 1)) + iCount + 1;
        }

        private static void SetHeaders(HttpRequestMessage request)
        {
            var headers = new Dictionary<string, string>
            {
                { "Accept", "*/*" },
                { "x-app-os-name", "iOS" },
                { "x-app-os-version", "16.3.0" },
                { "Accept-Language", "en-US,en;q=0.9" },
                { "Accept-Encoding", "gzip, deflate, br" },
                { "x-app-device", "iPhone13,2" },
                { "User-Agent", "DeepL-iOS/2.9.1 iOS 16.3.0 (iPhone13,2)" },
                { "x-app-build", "510265" },
                { "x-app-version", "2.9.1" },
                { "Connection", "keep-alive" }
            };
            foreach (var header in headers) request.Headers.Add(header.Key, header.Value);
        }
    }
}
