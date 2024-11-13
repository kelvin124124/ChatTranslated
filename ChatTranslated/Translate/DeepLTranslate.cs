using ChatTranslated.Utils;
using Dalamud.Utility;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
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
                "Chinese (Simplified)" => "ZH",
                "Chinese (Traditional)" => "ZH",
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

    // Based on DeepLX: https://github.com/OwO-Network/DeepLX
    internal static class DeeplsTranslate
    {
        private static readonly Random Random = new();
        private const string BaseUrl = "https://www2.deepl.com/jsonrpc";
        private const string ClientInfo = "chrome-extension,1.28.0";

        public static async Task<(string, TranslationMode?)> Translate(string message, string targetLanguage)
        {
            if (!DeepLTranslate.TryGetLanguageCode(targetLanguage, out string? langCode))
            {
                return ("Target language not supported by DeepL.", null);
            }

            var id = ((ulong)Random.Next(8300000, 8400000) * 1000) + 1;
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var iCount = message.Count(c => c == 'i');
            var adjustedTimestamp = iCount == 0 ? timestamp : timestamp - (timestamp % (iCount + 1)) + iCount + 1;

            var requestBody = new
            {
                jsonrpc = "2.0",
                method = "LMT_handle_jobs",
                @params = new
                {
                    commonJobParams = new
                    {
                        mode = "translate",
                        regionalVariant = targetLanguage switch
                        {
                            "Chinese (Simplified)" => "ZH-HANS",
                            "Chinese (Traditional)" => "ZH-HANT",
                            _ => default
                        }
                    },
                    lang = new
                    {
                        source_lang_computed = "auto",
                        target_lang = langCode
                    },
                    jobs = new[]
                    {
                        new
                        {
                            kind = "default",
                            preferred_num_beams = 4,
                            raw_en_context_before = Array.Empty<string>(),
                            raw_en_context_after = Array.Empty<string>(),
                            sentences = new[]
                            {
                                new { prefix = "", text = message, id = 1 }
                            }
                        }
                    },
                    priority = 1,
                    timestamp = adjustedTimestamp
                },
                id
            };

            var postDataJson = JsonSerializer.Serialize(requestBody);
            postDataJson = postDataJson.Replace("\"method\":\"", (id + 5) % 29 == 0 || (id + 3) % 13 == 0 ? "\"method\" : \"" : "\"method\": \"");

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}?client={ClientInfo}&method=LMT_handle_jobs")
            {
                Content = new StringContent(postDataJson, Encoding.UTF8, "application/json")
            };

            SetHeaders(request);

            try
            {
                var response = await Translator.HttpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var jsonDoc = JsonDocument.Parse(jsonResponse);
                var translated = jsonDoc.RootElement
                    .GetProperty("result")
                    .GetProperty("translations")[0]
                    .GetProperty("beams")[0]
                    .GetProperty("sentences")[0]
                    .GetProperty("text")
                    .GetString();

                if (translated.IsNullOrWhitespace())
                {
                    throw new Exception("Translation not found in the expected JSON structure.");
                }

                return (translated, TranslationMode.DeepL);
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"DeeplsTranslate failed to translate. Falling back to DeepL API / machine translation.\n{ex.Message}");
                if (Service.configuration.DeepL_API_Key != "YOUR-API-KEY:fx")
                    return await DeepLTranslate.Translate(message, targetLanguage);
                else
                    return await MachineTranslate.Translate(message, targetLanguage);
            }
        }

        private static void SetHeaders(HttpRequestMessage request)
        {
            var headers = new Dictionary<string, string>
        {
            { "Accept", "*/*" },
            { "Accept-Language", "en-US,en;q=0.9,zh-CN;q=0.8,zh-TW;q=0.7,zh-HK;q=0.6,zh;q=0.5" },
            { "Authorization", "None" },
            { "Cache-Control", "no-cache" },
            { "DNT", "1" },
            { "Origin", "chrome-extension://cofdbpoegempjloogbagkncekinflcnj" },
            { "Pragma", "no-cache" },
            { "Priority", "u=1, i" },
            { "Referer", "https://www.deepl.com/" },
            { "Sec-Fetch-Dest", "empty" },
            { "Sec-Fetch-Mode", "cors" },
            { "Sec-Fetch-Site", "none" },
            { "Sec-GPC", "1" },
            { "User-Agent", "DeepLBrowserExtension/1.28.0 Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36" }
        };
            foreach (var header in headers) request.Headers.Add(header.Key, header.Value);
        }
    }
}
