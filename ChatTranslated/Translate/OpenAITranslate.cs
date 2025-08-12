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

namespace ChatTranslated.Translate
{
    internal static class OpenAITranslate
    {
        private const string DefaultContentType = "application/json";

        public static async Task<(string, TranslationMode?)> Translate(Message message, string targetLanguage
            , string baseUrl = "https://api.openai.com/v1/chat/completions", string model = "gpt-4o-mini", string? apiKey = null)
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
                    return await MachineTranslate.Translate(message.OriginalContent.TextValue, targetLanguage);
                }
            }

            var prompt = BuildPrompt(Service.configuration.SelectedTargetLanguage, message.Context);
            int promptLength = prompt.Length;
            var userMsg = $"Translate to: {Service.configuration.SelectedTargetLanguage}\n#### Original Text\n{message.OriginalContent.TextValue}";
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

                if (translated == message.OriginalContent.TextValue)
                {
                    Service.pluginLog.Warning("Message was not translated. Falling back to machine translate.");
                    return await MachineTranslate.Translate(message.OriginalContent.TextValue, targetLanguage);
                }

                var translationMatch = Regex.Match(translated, @"#### Translation\s*\n(.+)$", RegexOptions.Singleline);
                translated = translationMatch.Success ? translationMatch.Groups[1].Value.Trim() : translated;

                return (translated, TranslationMode.OpenAI);
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"OpenAI Translate failed to translate. Falling back to machine translation.\n{ex.Message}");
                return await MachineTranslate.Translate(message.OriginalContent.TextValue, targetLanguage);
            }
        }

        public static string BuildPrompt(string targetLanguage, string? context)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"You are a precise translator for FFXIV game content into {targetLanguage}.\n");

            sb.AppendLine("TRANSLATION RULES:");
            sb.AppendLine("1. Be mindful of FFXIV-specific terms, but translate all content appropriately");
            sb.AppendLine("2. Preserve all formatting, including spaces and punctuation");
            sb.AppendLine("3. Maintain the exact meaning and tone of the original text\n");

            sb.AppendLine("OUTPUT RULES:");
            sb.AppendLine("1. First, in a \"#### Reasoning\" section, briefly:");
            sb.AppendLine("   - Identify any FFXIV-specific terms and their meanings");
            sb.AppendLine("   - Consider multiple possible translations");
            sb.AppendLine("   - Explain your final translation choice");
            sb.AppendLine("2. Your response must then include \"#### Translation\".");
            sb.AppendLine("3. Write only the translated text after this header.");
            sb.AppendLine("4. Do not include the original text.");
            sb.AppendLine("5. Do not add any explanations or notes after the translation.\n");

            sb.AppendLine("Example response format:");
            sb.AppendLine("#### Reasoning");
            sb.AppendLine("{Your analysis and translation process}");
            sb.AppendLine("");
            sb.AppendLine("#### Translation");
            sb.AppendLine("{Only the translated text goes here}");

            if (Service.configuration.UseContext && context != null)
            {
                sb.AppendLine("\nCONTEXT:");
                sb.AppendLine("Use the following context information if relevant (provided in XML tags):");
                sb.AppendLine("<context>");
                sb.AppendLine(context);
                sb.AppendLine("</context>");
            }

            return sb.ToString();
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
}
