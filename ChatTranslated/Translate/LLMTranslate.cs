using ChatTranslated.Chat;
using ChatTranslated.Utils;
using Dalamud.Utility;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Translate;

internal static partial class OpenAITranslate
{
    [GeneratedRegex(@"#### Translation\s*\n(.+)$", RegexOptions.Singleline)]
    private static partial Regex TranslationSectionRegex();

    [GeneratedRegex(@"\s*CONTEXT\s*\(Use if relevant\)\s*:.*$", RegexOptions.Singleline)]
    private static partial Regex EchoedContextRegex();

    public static async Task<(string, TranslationMode?)> Translate(Message message, string targetLanguage
        , string baseUrl = "https://api.openai.com/v1/chat/completions", string model = "gpt-5-mini", string? apiKey = null)
    {
        apiKey ??= Service.configuration.OpenAI_API_Key;

        var prompt = Service.configuration.UseCustomPrompt
            ? BuildCustomPrompt(targetLanguage, message.Context)
            : BuildPrompt(targetLanguage, message.Context);

        var userMsg = $"Translate to: {targetLanguage}\n#### Original Text\n{message.OriginalText}";
        var requestData = new
        {
            model,
            temperature = 0.6,
            max_tokens = Math.Max(prompt.Length, 80),
            messages = new[]
            {
                new { role = "system", content = prompt },
                new { role = "user", content = userMsg }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json"),
            Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {apiKey}" } }
        };

        try
        {
            var response = await TranslationHandler.HttpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            var translated = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim();

            if (translated.IsNullOrWhitespace())
            {
                throw new Exception("Translation not found in the expected structure.");
            }

            if (translated == message.OriginalText)
            {
                Service.pluginLog.Warning("Message was not translated. Falling back to machine translate.");
                return await MachineTranslate.Translate(message.OriginalText, targetLanguage);
            }

            var translationMatch = TranslationSectionRegex().Match(translated);
            translated = translationMatch.Success ? translationMatch.Groups[1].Value.Trim() : translated;

            // Strip any echoed CONTEXT block that the LLM may have repeated from the prompt
            translated = EchoedContextRegex().Replace(translated, string.Empty).Trim();

            return (translated, TranslationMode.OpenAI);
        }
        catch (Exception ex)
        {
            Service.pluginLog.Warning($"OpenAI Translate failed to translate. Falling back to machine translation.\n{ex.Message}");
            return await MachineTranslate.Translate(message.OriginalText, targetLanguage);
        }
    }

    public static string BuildPrompt(string targetLanguage, string? context)
    {
        var prompt = """
            You are a precise translator for FFXIV game content.

            TRANSLATION RULES:
            1. Be mindful of FFXIV-specific terms, but translate all content appropriately
            2. Preserve all formatting and tone.

            OUTPUT RULES:
            1. First, in a "#### Reasoning" section, BRIEFLY identify FFXIV-specific terms and their meanings
            2. Your response must then include "#### Translation"
            3. Write only the translated text after this header
            4. If the original text is already in target language, return it WITHOUT modification.

            Example response format:
            #### Reasoning
            [BRIEF analysis and translation process]

            #### Translation
            [Only the translated text goes here]
            """;

        if (Service.configuration.UseContext && context != null)
        {
            prompt += $"""

                CONTEXT (Use if relevant):
                <context>
                {context}
                </context>
                """;
        }

        return prompt;
    }

    public static string BuildCustomPrompt(string targetLanguage, string? context)
    {
        string prompt = Service.configuration.LLM_CustomPrompt.Replace("{targetLanguage}", targetLanguage);

        if (Service.configuration.UseContext && context != null)
        {
            prompt += $"""

                CONTEXT (Use if relevant):
                <context>
                {context}
                </context>
                """;
        }

        return prompt;
    }
}

internal static class OpenAICompatible
{
    public static Task<(string, TranslationMode?)> Translate(Message message, string targetLanguage) =>
        OpenAITranslate.Translate(message, targetLanguage,
            Service.configuration.LLM_API_endpoint, Service.configuration.LLM_Model, Service.configuration.LLM_API_Key);
}

internal static class LLMProxyTranslate
{
    private static readonly string? Cfv5 = ReadSecret("ChatTranslated.Resources.cfv5.secret").Replace("\n", string.Empty);

    private static string ReadSecret(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream == null) return "";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static async Task<(string, TranslationMode?)> Translate(Message message, string targetLanguage)
    {
#if DEBUG
        string Cfv5 = Service.configuration.Proxy_API_Key;
#else
        if (string.IsNullOrEmpty(Cfv5))
        {
            Service.pluginLog.Warning("LLMProxy API key not found. Falling back to machine translate.");
            return await MachineTranslate.Translate(message.OriginalText, targetLanguage);
        }
#endif

        if (!Service.configuration.UseContext) message.Context = "null";

        var requestData = new { targetLanguage, message = message.OriginalText, context = message.Context };
        using var request = new HttpRequestMessage(HttpMethod.Post, Service.configuration.Proxy_Url)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", Cfv5);

        try
        {
            var response = await TranslationHandler.HttpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));

            var translated = jsonDoc.RootElement.GetProperty("translated").GetString()?.Trim();

            if (translated.IsNullOrWhitespace())
            {
                throw new Exception("Translation not found in the expected structure.");
            }

            if (translated == message.OriginalText)
            {
                Service.pluginLog.Warning("Message was not translated. Falling back to machine translate.");
                return await MachineTranslate.Translate(message.OriginalText, targetLanguage);
            }

            if (jsonDoc.RootElement.TryGetProperty("responseTime", out var responseTime))
                Service.pluginLog.Info($"Request processed in: {responseTime}");

            return (translated.Replace("\n", string.Empty), TranslationMode.LLMProxy);
        }
        catch (Exception ex)
        {
            Service.pluginLog.Warning($"LLMProxy Translate failed to translate. Falling back to machine translate.\n{ex.Message}");
            return await MachineTranslate.Translate(message.OriginalText, targetLanguage);
        }
    }
}
