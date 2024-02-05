using GTranslate.Translators;
using Newtonsoft.Json.Linq;
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
        private static readonly GoogleTranslator2 GTranslator = new GoogleTranslator2(HttpClient);
        private static readonly BingTranslator BingTranslator = new BingTranslator(HttpClient);


        private const string DefaultContentType = "application/json";
        private static readonly Regex GPTRegex = new Regex(@"\[TRANSLATED\]\n*([\s\S]*?)\n*\[/TRANSLATED\]", RegexOptions.Compiled);

        public static async Task Translate(string sender, string message, ushort color = 1)
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
                Plugin.OutputChatLine($"{sender}: {message} || {translatedText}", color);
            }
        }

        public static void TranslateFrDe(string sender, string message, ushort color = 1)
        {
            Service.pluginLog.Info($"TranslatFrDe: {message}");
            try
            {
                string language = GTranslator.DetectLanguageAsync(message).Result.Name;
                Service.pluginLog.Info($"language: {language}");
                if (language == "French" || language == "German")
                {
                    _ = Task.Run(() => Translate(sender, message, color));
                }
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"Warn: Failed to detect language. {ex}");
            }
            return;
        }

        private static async Task<string> MachineTranslate(string message)
        {
            try
            {
                var result = await GTranslator.TranslateAsync(message, "English");
                return result.Translation;
            }
            catch (Exception GTex)
            {
                Service.pluginLog.Info($"Google Translate: {GTex.Message}, falling back to Bing Translate.");
                try
                {
                    var result = await BingTranslator.TranslateAsync(message, "English");
                    return result.Translation;
                }
                catch (Exception BTex)
                {
                    Service.pluginLog.Error($"Error: {BTex.Message}, both translator failed, returning original message.");
                    return message;
                }
            }
        }

        private static async Task<string> OpenAITranslate(string message)
        {
            if (OPENAI_API_KEY != null)
            {
                var requestData = new
                {
                    model = MODEL,
                    max_tokens = 800,
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
                        throw new HttpRequestException($"Request to API failed with status code: {response.StatusCode}\n" +
                            $"{response}");
                    }

                    var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var match = GPTRegex.Match(JObject.Parse(jsonResponse)["choices"]![0]!["message"]!["content"]!.ToString()).Groups[1].Value.Trim();

                    return match;
                }
                catch (Exception ex)
                {
                    Service.pluginLog.Warning($"Warn: {ex.Message}, falling back to machine translate.");
                }
            }
            else
                Service.pluginLog.Warning("Warn: API key is null, falling back to machine translate.");

            string str = await MachineTranslate(message);
            return str;
        }
    }
}
