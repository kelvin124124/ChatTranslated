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

        private const string DefaultContentType = "application/json";
        private static readonly Regex LibreTranslateRegex = new Regex(@"""translatedText""\s*:\s*""(.*?)""");
        private static readonly Regex GPTRegex = new Regex(@"\*\*\*\[TRANSLATED\]\*\*\*([\s\S]*?)\*\*\*\[/TRANSLATED\]\*\*\*");

        public static async Task Translate(string sender, string message)
        {
            string translatedText = message;

            switch (Service.configuration.SelectedMode)
            {
                case Mode.LibreTranslate:
                    translatedText = await Translate(message, LibreTranslate);
                    break;
                case Mode.GPT3_Proxy:
                    translatedText = await Translate(message, ProxyTranslate);
                    break;
                case Mode.OpenAI_API:
                    translatedText = await Translate(message, OpenAITranslate);
                    break;
            }

            Service.mainWindow.PrintToOutput($"{sender}: {translatedText}");

            if (Service.configuration.ChatIntergration && translatedText.Length<50)
            {
                Plugin.OutputChatLine($"{sender}: {message} || {translatedText}");
            }
        }

        private static async Task<string> Translate(string message, Func<string, Task<string>> translateFunc)
        {
            int attempt = 0;
            while (attempt < MAX_ATTEMPTS)
            {
                try
                {
                    return await translateFunc(message);
                }
                catch (Exception ex)
                {
                    Service.pluginLog.Warning($"Translation Error: {ex.Message}");
                    await Task.Delay(WAIT_TIME);
                    attempt++;
                }
            }
            Service.pluginLog.Warning("Translation failed after retries.");
            return message;
        }

        private static async Task<string> LibreTranslate(string message)
        {
            var requestData = new
            {
                q = message,
                source = "auto",
                target = "en",
                format = "text",
                api_key = LIBRETRANSLATE_API_KEY
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, DefaultContentType);
            var response = await HttpClient.PostAsync(LIBRETRANSLATE_API, content).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Request to LibreTranslate API failed with status code: {response.StatusCode}");
            }

            string jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (LibreTranslateRegex.IsMatch(jsonResponse))
            {
                string match = LibreTranslateRegex.Match(jsonResponse).Groups[1].Value;
                return match;
            }
            else 
            { 
                return jsonResponse.ToString(); 
            }
        }

        private static async Task<string> ProxyTranslate(string message)
        {
            var requestData = new
            {
                model = PROXY_MODEL,
                max_tokens = 500,
                messages = new[]
                {
                    new { role = "system", content = PROMPT },
                    new { role = "user", content = message }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, DefaultContentType);
            var request = new HttpRequestMessage(HttpMethod.Post, PROXY_API)
            {
                Content = content,
                Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {PROXY_API_KEY}" } }
            };

            var response = await HttpClient.SendAsync(request).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Request to Proxy API failed with status code: {response.StatusCode}\n" +
                    $"{response}");
            }

            string jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (LibreTranslateRegex.IsMatch(jsonResponse))
            {
                string match = LibreTranslateRegex.Match(jsonResponse).Groups[1].Value;
                return match;
            }
            else
            {
                return jsonResponse.ToString();
            }
        }


        private static async Task<string> OpenAITranslate(string message)
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
    }
}
