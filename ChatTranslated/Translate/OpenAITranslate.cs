using ChatTranslated.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatTranslated.Translate
{
    internal static class OpenAITranslate
    {
        private const string DefaultContentType = "application/json";

        public static async Task<string> Translate(string message, string targetLanguage)
        {
            if (!Regex.IsMatch(Service.configuration.OpenAI_API_Key, @"^sk-[a-zA-Z0-9]{32,}$"))
            {
                Service.pluginLog.Warning("OpenAI API Key is invalid. Please check your configuration. Falling back to machine translation.");
                return await MachineTranslate.Translate(message, targetLanguage);
            }

            var prompt = $"Translate the following FFXIV chat message into {targetLanguage}. If you encounter any in-game terms, keep them in their original form. Output only the translated text in a single line.\nMessage to translate: {message}";
            var requestData = new
            {
                model = "gpt-3.5-turbo",
                temperature = 0.6,
                max_tokens = Math.Min(Math.Max(message.Length * 2, 20), 150),
                messages = new[]
                {
                    new { role = "system", content = prompt },
                    new { role = "user", content = message }
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
                return !string.IsNullOrWhiteSpace(translated) ? translated : throw new Exception("Translation not found in the expected JSON structure.");
            }
            catch
            {
                Service.pluginLog.Warning("OpenAI Translate failed to translate. Falling back to machine translation.");
                return await MachineTranslate.Translate(message, targetLanguage);
            }
        }
    }
}
