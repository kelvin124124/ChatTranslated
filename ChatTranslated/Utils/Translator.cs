using GTranslate.Results;
using GTranslate.Translators;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Utils
{
    internal class Translator
    {
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly GoogleTranslator GTranslator = new GoogleTranslator();

        private const string DefaultContentType = "application/json";
        private static readonly Regex GPTRegex = new Regex(@"\[TRANSLATED\]\n?([\s\S]*?)\n?\[/TRANSLATED\]", RegexOptions.Compiled);

        public static async Task Translate(string sender, string message)
        {
            string translatedText = message;

            switch (Service.configuration.SelectedMode)
            {
                case Mode.MachineTranslate:
                    translatedText = await MachineTranslate(message);
                    break;
                case Mode.OpenAI_API:
                    translatedText = await OpenAITranslate(message);
                    break;
            }

            Service.mainWindow.PrintToOutput($"{sender}: {translatedText}");

            if (Service.configuration.ChatIntergration && translatedText.Length < 500)
            {
                Plugin.OutputChatLine($"{sender}: {message} || {translatedText}");
            }
        }

        private static async Task<string> MachineTranslate(string message)
        {
            var result = await GTranslator.TranslateAsync(message, "English");
            return result.Translation;
        }

        private static async Task<string> OpenAITranslate(string message)
        {
            int attempt = 0;
            while (attempt < 3)
            {
                var requestData = new
                {
                    model = MODEL,
                    max_tokens = 500,
                    messages = new[]
                    {
                        new { role = "system", content = PROMPT },
                        new { role = "user", content = message }
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, DefaultContentType);
                var request = new HttpRequestMessage(HttpMethod.Post, OPENAI_API)
                {
                    Content = content,
                    Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {OPENAI_API_KEY}" } }
                };

                try
                {
                    var response = await HttpClient.SendAsync(request).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException($"Request to Proxy API failed with status code: {response.StatusCode}\n" +
                            $"{response}");
                    }

                    var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var match = GPTRegex.Match(jsonResponse).Groups[1].Value.Trim();

                    return match;
                }
                catch (Exception ex)
                {
                    Service.pluginLog.Warning($"Translation Error: {ex.Message}");
                    await Task.Delay(500);
                    attempt++;
                }
            }
            Service.pluginLog.Warning("Translation failed after retries.");
            return message;
        }
    }
}
