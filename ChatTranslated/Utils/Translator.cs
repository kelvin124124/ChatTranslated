using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatTranslated.Utils
{
    internal class Translator
    {
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        private const string DefaultContentType = "application/json";
        private static readonly Regex LibreTranslateRegex = new Regex(@"""translatedText""\s*:\s*""(.*?)""");
        private static readonly Regex GPTRegex = new Regex(@"\*\*\*\[TRANSLATED\]\*\*\*([\s\S]*?)\*\*\*\[/TRANSLATED\]\*\*\*");


        public static async Task Translate(string sender, string message, Configuration config)
        {
            string translatedText = message;

            switch (config.SelectedMode)
            {
                case Configuration.Mode.LibreTranslate:
                    translatedText = await Translate(message, config, LibreTranslate);
                    break;
                case Configuration.Mode.GPT3_Proxy:
                    translatedText = await Translate(message, config, ProxyTranslate);
                    break;
                case Configuration.Mode.OpenAI_API:
                    translatedText = await Translate(message, config, OpenAITranslate);
                    break;
            }

            Service.mainWindow.PrintToOutput($"{sender}: {translatedText}");
            if (config.ChatIntergration)
            {
                Plugin.OutputChatLine($"{sender}: {message} || {translatedText}");
            }
        }

        private static async Task<string> Translate(string message, Configuration config,
            Func<string, Configuration, Task<string>> translateFunc)
        {
            int attempt = 0;
            while (attempt < config.maxAttempts)
            {
                try
                {
                    return await translateFunc(message, config);
                }
                catch (Exception ex)
                {
                    Service.pluginLog.Warning($"Translation Error: {ex.Message}");
                    await Task.Delay(config.waitTime);
                    attempt++;
                }
            }

            Service.pluginLog.Warning("Translation failed after retries.");
            return message;
        }

        private static async Task<string> LibreTranslate(string message, Configuration config)
        {
            var requestData = new
            {
                q = message,
                source = "auto",
                target = "en",
                format = "text",
                api_key = config.LIBRETRANSLATE_API_KEY
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, DefaultContentType);
            var response = await HttpClient.PostAsync(config.LIBRETRANSLATE_API, content).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Request to LibreTranslate API failed with status code: {response.StatusCode}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var match = LibreTranslateRegex.Match(jsonResponse).Groups[1].Value;

            Service.pluginLog.Information($"LibreTranslate - Match: {match}");
            return match;
        }

        private static async Task<string> ProxyTranslate(string message, Configuration config)
        {
            var requestData = new
            {
                model = "gpt-3.5-turbo",
                max_tokens = 500,
                messages = new[]
                {
                    new 
                    {
                        role = "system",
                        content = "This is a message from an MMORPG chat (FFXIV). Please follow these steps:\n" +
                                    "1. Rephrase the message for clarity while preserving game-specific terms.\n" +
                                    "2. Identify the language of the message.\n" +
                                    "3. If not in English, translate the message to English.\n" +
                                    "4. Provide the final, translated text enclosed between ***[TRANSLATED]*** and ***[/TRANSLATED]***."
                    },
                    new { role = "user", content = message }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, DefaultContentType);
            var request = new HttpRequestMessage(HttpMethod.Post, config.PROXY_API)
            {
                Content = content,
                Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {config.PROXY_API_KEY}" } }
            };

            var response = await HttpClient.SendAsync(request).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Request to Proxy API failed with status code: {response.StatusCode}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var match = GPTRegex.Match(jsonResponse).Groups[1].Value.Trim();

            return match;
        }


        private static async Task<string> OpenAITranslate(string message, Configuration config)
        {
            var requestData = new
            {
                model = "gpt-4-1106-preview",
                max_tokens = 500,
                messages = new[]
                {
                    new 
                    {
                        role = "system",
                        content = "This is a message from an MMORPG chat (FFXIV). Please follow these steps:\n" +
                                    "1. Rephrase the message for clarity while preserving game-specific terms.\n" +
                                    "2. Identify the language of the message.\n" +
                                    "3. If not in English, translate the message to English.\n" +
                                    "4. Provide the final, translated text enclosed between ***[TRANSLATED]*** and ***[/TRANSLATED]***."
                    },
                    new { role = "user", content = message }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, DefaultContentType);
            var request = new HttpRequestMessage(HttpMethod.Post, config.OPENAI_API)
            {
                Content = content,
                Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {config.OPENAI_API_KEY}" } }
            };

            var response = await HttpClient.SendAsync(request).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Request to OpenAI API failed with status code: {response.StatusCode}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var match = GPTRegex.Match(jsonResponse).Groups[1].Value.Trim();

            return match;
        }
    }
}
