using ChatTranslated.Chat;
using ChatTranslated.Utils;
using Dalamud.Utility;

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Translate;

internal static partial class OpenAITranslate
{
    [GeneratedRegex(@"#### Translation\s*\n(.+)$", RegexOptions.Singleline)]
    private static partial Regex TranslationSectionRegex();

    private const string OpenAIUrl = "https://api.openai.com/v1/chat/completions";

    // /v1/models lists everything the key can reach: image, audio and realtime models, dated snapshots and
    // Responses-only pro models. Keep plain chat model names, "gpt-<version>" plus at most one tier word.
    [GeneratedRegex(@"^gpt-\d+(\.\d+)?o?(-(?!(pro|codex|cyber|preview|audio|realtime|search|transcribe|tts|image)$)[a-z]+)?$")]
    private static partial Regex ChatModelRegex();

    // Chat models this key can use, newest first; null when the key is rejected or the request fails.
    // Retired models drop out of /v1/models, so the list never needs maintaining by hand.
    public static async Task<string[]?> FetchModelsAsync(string apiKey)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models")
            {
                Headers = { { "Authorization", $"Bearer {apiKey}" } }
            };
            using var response = await TranslationHandler.HttpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
            return [.. json.GetProperty("data").EnumerateArray()
                .Select(model => (Id: model.GetProperty("id").GetString() ?? "", Created: model.TryGetProperty("created", out var created) ? created.GetInt64() : 0))
                .Where(model => ChatModelRegex().IsMatch(model.Id))
                .OrderByDescending(model => model.Created)
                .Select(model => model.Id)];
        }
        catch (Exception ex)
        {
            Service.pluginLog.Warning($"Failed to fetch OpenAI models: {ex.Message}");
            return null;
        }
    }

    public static async Task<(string, TranslationMode?)> Translate(Message message, string targetLanguage
        , string baseUrl = OpenAIUrl, string model = "gpt-6-luna", string? apiKey = null)
    {
        apiKey ??= Service.configuration.OpenAI_API_Key;

        var prompt = Service.configuration.UseCustomPrompt
            ? BuildCustomPrompt(targetLanguage, message.Context)
            : BuildPrompt(message.Context);

        // OpenAI rejects max_tokens on current models, and its reasoning models (everything after gpt-4)
        // reject any temperature but the default. They also spend part of the token budget on hidden
        // reasoning, so give them headroom.
        var isOpenAI = baseUrl == OpenAIUrl;
        var isReasoningModel = isOpenAI && !model.StartsWith("gpt-4", StringComparison.Ordinal);
        var maxTokens = Math.Max(prompt.Length, 80) + (isReasoningModel ? 1024 : 0);

        var userMsg = $"Translate to: {targetLanguage}\n#### Original Text\n{message.OriginalText}";
        var requestData = new
        {
            model,
            temperature = isReasoningModel ? (double?)null : 0.6,
            reasoning_effort = isReasoningModel ? "low" : null,
            max_tokens = isOpenAI ? (int?)null : maxTokens,
            max_completion_tokens = isOpenAI ? maxTokens : (int?)null,
            messages = new[]
            {
                new { role = "system", content = prompt },
                new { role = "user", content = userMsg }
            }
        };

        try
        {
            var json = await TranslationHandler.PostJsonAsync(baseUrl, requestData,
                headers => headers.Add("Authorization", $"Bearer {apiKey}")).ConfigureAwait(false);
            var translated = json
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim();

            if (translated.IsNullOrWhitespace())
                throw new Exception("Translation not found in the expected structure.");

            if (translated == message.OriginalText)
            {
                Service.pluginLog.Warning("Message was not translated. Falling back to machine translate.");
                return await MachineTranslate.Translate(message.OriginalText, targetLanguage);
            }

            var translationMatch = TranslationSectionRegex().Match(translated);
            translated = translationMatch.Success ? translationMatch.Groups[1].Value.Trim() : translated;

            // LLM likely generated slop
            if (translated.Split('\n').Length >= 4)
            {
                var trimmed = translated.Split("\n\n", 2)[0].Trim();  // take the first part and call it a day
                if (!trimmed.IsNullOrWhitespace())
                    translated = trimmed;
            }

            return (translated, TranslationMode.OpenAI);
        }
        catch (Exception ex)
        {
            Service.pluginLog.Warning($"OpenAI Translate failed to translate. Falling back to machine translation.\n{ex.Message}");
            return await MachineTranslate.Translate(message.OriginalText, targetLanguage);
        }
    }

    // Target language is communicated via the user message ("Translate to: ..."), not the system prompt.
    public const string DefaultPrompt = """
        You are a precise translator for FFXIV game content.

        TRANSLATION RULES:
        1. Be mindful of FFXIV-specific terms, but translate all content appropriately
        2. Preserve all formatting and tone.

        OUTPUT RULES:
        1. First, in a "#### Reasoning" section, BRIEFLY identify FFXIV-specific terms and their meanings
        2. Your response must then include "#### Translation"
        3. Write only the translated text after this header
        4. If the original text is already in target language, return it WITHOUT modification. Do not expand abbreviations or slang. Ignore typos.

        Example response format:
        #### Reasoning
        [BRIEF analysis and translation process]

        #### Translation
        [Only the translated text goes here]
        """;

    public static string BuildPrompt(string? context) => AppendContext(DefaultPrompt, context);

    public static string BuildCustomPrompt(string targetLanguage, string? context)
    {
        string prompt = Service.configuration.LLM_CustomPrompt.Replace("{targetLanguage}", targetLanguage);
        return AppendContext(prompt, context);
    }

    // chat messages only carry context when UseContext is on; Party Finder and IPC bring their own
    private static string AppendContext(string prompt, string? context)
    {
        if (string.IsNullOrEmpty(context) || context == "null") return prompt;
        return prompt + $"""

            CONTEXT (Use if relevant):
            "[CT]" lines are translations.
            <context>
            {context}
            </context>
            """;
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

        var requestData = new
        {
            targetLanguage,
            message = message.OriginalText,
            // the proxy requires a context string; "null" means none
            context = message.Context ?? "null",
            // lets the proxy pick the Party Finder glossary for the listing's duty
            dutyId = message.DutyId
        };

        try
        {
            var json = await TranslationHandler.PostJsonAsync(Service.configuration.Proxy_Url, requestData,
                headers => headers.Add("x-api-key", Cfv5)).ConfigureAwait(false);

            var translated = json.GetProperty("translated").GetString()?.Trim();

            if (translated.IsNullOrWhitespace())
                throw new Exception("Translation not found in the expected structure.");

            if (translated == message.OriginalText)
            {
                Service.pluginLog.Warning("Message was not translated. Falling back to machine translate.");
                return await MachineTranslate.Translate(message.OriginalText, targetLanguage);
            }

            if (json.TryGetProperty("responseTime", out var responseTime))
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
