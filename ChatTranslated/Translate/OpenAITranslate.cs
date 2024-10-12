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

        public static async Task<(string, TranslationMode?)> Translate(Message message, string targetLanguage)
        {
            if (!Regex.IsMatch(Service.configuration.OpenAI_API_Key, @"^sk-[a-zA-Z0-9\-_]{32,}$", RegexOptions.Compiled))
            {
                Service.pluginLog.Warning("OpenAI API Key is invalid. Please check your configuration. Falling back to machine translation.");
                return await MachineTranslate.Translate(message.OriginalContent.TextValue, targetLanguage);
            }

            string? knowledge = null;
            int messageLength = message.OriginalContent.TextValue.Length;
            if (messageLength <= 5 || !Service.configuration.OpenAI_UseRAG)
            {
                Service.pluginLog.Information("Skipping RAG.");
            }
            else
            {
                var queryEmbeddings = await RAG.GenerateEmbedding(message.OriginalContent.TextValue);
                var topResults = RAG.GetTopResults(queryEmbeddings);

#if DEBUG
                if (topResults != null && topResults.Count > 0)
                {
                    Service.pluginLog.Information("Top results from RAG:");
                    foreach (var result in topResults)
                    {
                        var firstSentence = result.Split('\n')[0];
                        Service.pluginLog.Information(firstSentence + "\n...");
                    }
                }
                else
                {
                    Service.pluginLog.Information("No results above the minimum score threshold.");
                }
#endif

                knowledge = (topResults != null) ?
                    string.Join("\n", topResults) : null;
            }

            var prompt = BuildPrompt(Service.configuration.SelectedTargetLanguage, message.Context, knowledge);
            var requestData = new
            {
                model = "gpt-4o-mini",
                temperature = 0.6,
                max_tokens = Math.Min(Math.Max(messageLength * 2, 20), 175),
                messages = new[]
                {
                    new { role = "system", content = prompt },
                    new { role = "user", content = message.OriginalContent.TextValue }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, DefaultContentType),
                Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {Service.configuration.OpenAI_API_Key}" } }
            };

            try
            {
                var response = await Translator.HttpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var translated = JObject.Parse(jsonResponse)["choices"]?[0]?["message"]?["content"]?.ToString().Trim();

                if (translated.IsNullOrWhitespace())
                {
                    throw new Exception("Translation not found in the expected JSON structure.");
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

        public static string BuildPrompt(string targetLanguage, string? context, string? knowledge)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"Translate the following FFXIV message into {targetLanguage}.");
            sb.AppendLine("If you encounter any in-game terms, keep them in their original form.");
            sb.AppendLine("Maintain the original format without omitting any information.");
            sb.AppendLine("Do not provide any breakdown or explanation. Use the following format.");
            sb.AppendLine("#### Original Text");
            sb.AppendLine("{Message to translate}");
            sb.AppendLine("#### Translation");
            sb.AppendLine("{Translated message}\n");

            if (knowledge != null)
            {
                sb.AppendLine("Use the following texts as your learned knowledge, inside <knowledge></knowledge> XML tags.");
                sb.AppendLine("<knowledge>");
                sb.AppendLine(knowledge);
                sb.AppendLine("</knowledge>");
                sb.AppendLine("Avoid mentioning that you obtained the information from the texts.\n");
            }

            if (context != null)
            {
                sb.AppendLine("Some context info is provided, inside <context></context> XML tags. They may or may not be useful.");
                sb.AppendLine("<context>");
                sb.AppendLine(context);
                sb.AppendLine("</context>");
            }

#if DEBUG
            Service.pluginLog.Information(sb.ToString());
#endif

            return sb.ToString();
        }
    }
}
