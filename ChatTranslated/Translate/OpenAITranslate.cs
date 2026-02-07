using ChatTranslated.Chat;
using ChatTranslated.Utils;
using Dalamud.Utility;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Translate;

internal static partial class OpenAITranslate
{
    private const string DefaultContentType = "application/json";

    [GeneratedRegex(@"#### Translation\s*\n(.+)$", RegexOptions.Singleline)]
    private static partial Regex TranslationSectionRegex();

    public static async Task<(string, TranslationMode?)> Translate(Message message, string targetLanguage
        , string baseUrl = "https://api.openai.com/v1/chat/completions", string model = "gpt-5-mini", string? apiKey = null)
    {
        if (apiKey == null)
        {
            if (!Service.configuration.OpenAI_API_Key.IsNullOrWhitespace())
            {
                apiKey = Service.configuration.OpenAI_API_Key;
            }
            else
            {
                Service.pluginLog.Warning("OpenAI API Key is invalid. Please check your configuration. Falling back to machine translation.");
                return await MachineTranslate.Translate(message.OriginalText, targetLanguage);
            }
        }

        var prompt = Service.configuration.UseCustomPrompt
            ? BuildCustomPrompt(targetLanguage, message.Context)
            : BuildPrompt(targetLanguage, message.Context);

        int promptLength = prompt.Length;
        var userMsg = $"Translate to: {targetLanguage}\n#### Original Text\n{message.OriginalText}";
        var requestData = new
        {
            model,
            temperature = 0.6,
            max_tokens = Math.Max(promptLength, 80),
            messages = new[]
            {
                new { role = "system", content = prompt },
                new { role = "user", content = userMsg }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, DefaultContentType),
            Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {apiKey}" } }
        };

        try
        {
            var response = await TranslationHandler.HttpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var translated = JObject.Parse(jsonResponse)["choices"]?[0]?["message"]?["content"]?.ToString().Trim();

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
        var prompt = $"""
            You are a precise translator for FFXIV game content into {targetLanguage}.

            TRANSLATION RULES:
            1. Be mindful of FFXIV-specific terms, but translate all content appropriately
            2. Preserve all formatting, including spaces and punctuation
            3. Maintain the exact meaning and tone of the original text

            OUTPUT RULES:
            1. First, in a "#### Reasoning" section, briefly:
               - Identify any FFXIV-specific terms and their meanings
               - Consider multiple possible translations
               - Explain your final translation choice
            2. Your response must then include "#### Translation".
            3. Write only the translated text after this header.
            4. Do not include the original text.
            5. Do not add any explanations or notes after the translation.

            Example response format:
            #### Reasoning
            {{Your analysis and translation process}}

            #### Translation
            {{Only the translated text goes here}}
            """;

        if (Service.configuration.UseContext && context != null)
        {
            prompt += $"""


                CONTEXT:
                Use the following context information if relevant (provided in XML tags):
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


                CONTEXT:
                Use the following context information if relevant (provided in XML tags):
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
    public static async Task<(string, TranslationMode?)> Translate(Message message, string targetLanguage)
    {
        return await OpenAITranslate.Translate(message, targetLanguage,
            Service.configuration.LLM_API_endpoint, Service.configuration.LLM_Model, Service.configuration.LLM_API_Key);
    }
}
