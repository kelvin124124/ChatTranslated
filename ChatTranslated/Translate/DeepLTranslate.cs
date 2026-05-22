using ChatTranslated.Utils;
using Dalamud.Utility;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Translate;

internal static class DeepLTranslate
{
    public static async Task<(string, TranslationMode?)> Translate(string text, string targetLanguage)
    {
        if (!TryGetLanguageCode(targetLanguage, out var languageCode))
            return ("Target language not supported by DeepL.", null);

        var requestBody = new { text = new[] { text }, target_lang = languageCode, context = "FFXIV, MMORPG" };
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api-free.deepl.com/v2/translate")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"),
            Headers = { { "Authorization", $"DeepL-Auth-Key {Service.configuration.DeepL_API_Key}" } }
        };

        try
        {
            var response = await TranslationHandler.HttpClient.SendAsync(requestMessage).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            var translated = doc.RootElement
                .GetProperty("translations")[0]
                .GetProperty("text")
                .GetString()?.Trim();

            if (translated.IsNullOrWhitespace())
                throw new Exception("Translation not found in the expected JSON structure.");

            return (translated, TranslationMode.DeepL);
        }
        catch (Exception ex)
        {
            Service.pluginLog.Warning($"DeepL Translate failed to translate. Falling back to machine translation.\n{ex.Message}");
            return await MachineTranslate.Translate(text, targetLanguage);
        }
    }

    internal static bool TryGetLanguageCode(string language, out string? languageCode)
    {
        if (LanguageDetector.NameToIsoCode.TryGetValue(language, out var iso))
        {
            languageCode = iso.ToUpperInvariant();
            return true;
        }
        languageCode = null;
        return false;
    }
}

// Based on DeepLX: https://github.com/OwO-Network/DeepLX
internal static class DeeplsTranslate
{
    private const string BaseUrl = "https://oneshot-free.www.deepl.com/v1/translate";
    private static readonly string InstanceId = Guid.NewGuid().ToString();

    public static async Task<(string, TranslationMode?)> Translate(string message, string targetLanguage)
    {
        if (!DeepLTranslate.TryGetLanguageCode(targetLanguage, out string? langCode))
        {
            return ("Target language not supported by DeepL.", null);
        }

        var targetLang = targetLanguage switch
        {
            "Chinese (Simplified)" => "ZH-HANS",
            "Chinese (Traditional)" => "ZH-HANT",
            _ => langCode!
        };

        var requestBody = new
        {
            text = new[] { message },
            target_lang = targetLang,
            usage_type = "Translate",
            app_information = new
            {
                os = "brex_macOS",
                os_version = "brex_chrome_120.0.0.0",
                app_version = "1.86.0",
                app_build = "chrome_web_store",
                instance_id = InstanceId
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };

        SetHeaders(request);

        try
        {
            var response = await TranslationHandler.HttpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            var translated = jsonDoc.RootElement
                .GetProperty("translations")[0]
                .GetProperty("text")
                .GetString();

            if (translated.IsNullOrWhitespace())
                throw new Exception("Translation not found in the expected JSON structure.");

            if (translated == message)
                throw new Exception("Translation is the same as the original text.");

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

    private static readonly KeyValuePair<string, string>[] StaticHeaders =
    [
        new("Authorization", "None"),
        new("Accept-Language", "en-US,en;q=0.9"),
        new("Accept-Encoding", "gzip, deflate, br"),
        new("Origin", "chrome-extension://cofdbpoegempjloogbagkncekinflcnj"),
        new("Sec-Fetch-Dest", "empty"),
        new("Sec-Fetch-Mode", "cors"),
        new("Sec-Fetch-Site", "cross-site"),
        new("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"),
    ];

    private static void SetHeaders(HttpRequestMessage request)
    {
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

        foreach (var header in StaticHeaders)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
    }
}
