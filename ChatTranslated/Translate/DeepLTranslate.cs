using ChatTranslated.Utils;
using Dalamud.Utility;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Translate;

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
                    formality = "undefined",
                    transcribe_as = "romanize",
                    advancedMode = false,
                    textType = "plaintext",
                    wasSpoken = false,
                    regionalVariant = targetLanguage switch
                    {
                        "Chinese (Simplified)" => "ZH-HANS",
                        "Chinese (Traditional)" => "ZH-HANT",
                        _ => default
                    }
                },
                lang = new
                {
                    source_lang_user_selected = "auto",
                    target_lang = langCode,
                    source_lang_computed = "AUTO",
                },
                jobs = new[]
                {
                    new
                    {
                        kind = "default",
                        preferred_num_beams = 4,
                        raw_en_context_before = (string[])[],
                        raw_en_context_after = (string[])[],
                        sentences = new[]
                        {
                            new { prefix = "", text = message, id = 0 }
                        }
                    }
                },
                timestamp = adjustedTimestamp
            },
            id
        };

        var postDataJson = JsonSerializer.Serialize(requestBody);
        postDataJson = postDataJson.Replace("\"method\":\"", (id + 5) % 29 == 0 || (id + 3) % 13 == 0 ? "\"method\" : \"" : "\"method\": \"");

        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl)
        {
            Content = new StringContent(postDataJson, Encoding.UTF8, "application/json")
        };

        SetHeaders(request);

        try
        {
            var response = await TranslationHandler.HttpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var contentEncoding = response.Content.Headers.ContentEncoding;
            if (contentEncoding.Contains("gzip", StringComparer.OrdinalIgnoreCase))
            {
                using var gzipStream = new System.IO.Compression.GZipStream(responseStream, System.IO.Compression.CompressionMode.Decompress);
                using var streamReader = new System.IO.StreamReader(gzipStream, Encoding.UTF8);
                postDataJson = await streamReader.ReadToEndAsync().ConfigureAwait(false);
            }
            else if (contentEncoding.Contains("deflate", StringComparer.OrdinalIgnoreCase))
            {
                using var deflateStream = new System.IO.Compression.DeflateStream(responseStream, System.IO.Compression.CompressionMode.Decompress);
                using var streamReader = new System.IO.StreamReader(deflateStream, Encoding.UTF8);
                postDataJson = await streamReader.ReadToEndAsync().ConfigureAwait(false);
            }
            else if (contentEncoding.Contains("br", StringComparer.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Brotli encoding is not supported.");
            }
            else
            {
                postDataJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }


            using var jsonDoc = JsonDocument.Parse(postDataJson);
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

            if (translated == message)
            {
                throw new Exception("Translation is the same as the original text.");
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
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

        var headers = new Dictionary<string, string>
        {
            { "Accept-Language", "en-US,en;q=0.9" },
            { "Accept-Encoding", "gzip, deflate" }, // removed br and zstd for simplicity
            { "Origin", "https://www.deepl.com" },
            { "Referer", "https://www.deepl.com/" },
            { "Sec-Fetch-Dest", "empty" },
            { "Sec-Fetch-Mode", "cors" },
            { "Sec-Fetch-Site", "same-site" },
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36 Edg/141.0.0.0" },
            { "Content-Type", "application/json" }
        };

        foreach (var header in headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }
}
