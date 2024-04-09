using ChatTranslated.Utils;
using Dalamud.Networking.Http;
using Dalamud.Utility;
using GTranslate.Translators;
using Newtonsoft.Json.Linq;
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

namespace ChatTranslated.Translate
{
    internal class Translator
    {
        private static readonly HttpClient HttpClient =
        new(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = new HappyEyeballsCallback().ConnectCallback,
        })
        { Timeout = TimeSpan.FromSeconds(10) };

        public static GoogleTranslator2 GTranslator = new(HttpClient);
        public static DeepL.Translator DeepLtranslator = new(DeepL_API_Key);

        private const string DefaultContentType = "application/json";
        private static readonly string? cfv2 = ReadSecret("ChatTranslated.Resources.cfv2.secret").Replace("\n", string.Empty);

        private static string ReadSecret(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return "";

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }


        public static async Task<string> Translate(string text, string targetLanguage)
        {
            text = ChatHandler.Sanitize(text);
            if (string.IsNullOrWhiteSpace(text))
                return text;

            switch (Service.configuration.SelectedTranslationMode)
            {
                case Configuration.TranslationMode.MachineTranslate:
                    return await MachineTranslate(text, targetLanguage);
                case Configuration.TranslationMode.DeepL_API:
                    return await DeepLTranslate(text, targetLanguage);
                case Configuration.TranslationMode.OpenAI_API:
                    return await OpenAITranslate(text, targetLanguage);
                case Configuration.TranslationMode.LLMProxy:
                    return await LLMProxyTranslate(text, targetLanguage);
                default:
                    Service.pluginLog.Warning("Unknown translation mode.");
                    return text;
            }
        }

        private static async Task<string> MachineTranslate(string text, string targetLanguage)
        {
            try
            {
                var result = await GTranslator.TranslateAsync(text, targetLanguage);
                return result.Translation;
            }
            catch (Exception GTex)
            {
                Service.pluginLog.Info($"Google Translate: {GTex.Message}.");
                return text;
            }
        }

        private static async Task<string> DeepLTranslate(string text, string targetLanguage)
        {
            if (TryGetLanguageCode(targetLanguage, out var languageCode))
            {
                try
                {
                    var result = await DeepLtranslator.TranslateTextAsync(text, null, languageCode!);
                    return result.Text;
                }
                catch (Exception DLex)
                {
                    Service.pluginLog.Info($"DeepL Translate: {DLex.Message}, falling back to Google Translate.");
                    return await MachineTranslate(text, targetLanguage);
                }
            }
            else return "Target language not supported by DeepL.";
        }

        public static bool TryGetLanguageCode(string language, out string? languageCode)
        {
            languageCode = language switch
            {
                "English" => "EN",
                "Japanese" => "JA",
                "German" => "DE",
                "French" => "FR",
                "Korean" => "KO",
                "Chinese (Simplified)" => "ZH",
                "Chinese (Traditional)" => "ZH",
                "Spanish" => "ES",
                _ => null
            };

            return !string.IsNullOrEmpty(languageCode);
        }

        public static async Task<string> LLMProxyTranslate(string message, string targetLanguage)
        {
            //if (string.IsNullOrEmpty(cfv2))
            //{
            //    Service.pluginLog.Warning("LLMProxyTranslate - api key empty.");
            //    return await MachineTranslate(message, targetLanguage);
            //}

            string regionCode = Service.configuration.ProxyRegion;

            var requestData = new
            {
                regionCode,
                targetLanguage,
                message
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, "https://cfv2.kelpcc.com")
            {
                Content = content
            };

            request.Headers.Add("x-api-key", cfv2);

            try
            {
                var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                Service.pluginLog.Info($"responseBody = {responseBody}");

                using JsonDocument doc = JsonDocument.Parse(responseBody);
                string? translated = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();

                Service.pluginLog.Info($"translated = {translated}");

                if (translated.IsNullOrWhitespace())
                {
                    throw new Exception("Translation not found in the expected JSON structure.");
                }
                else
                    return translated;
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"Error during proxy translation: {ex.Message}");
                return await MachineTranslate(message, targetLanguage);
            }
        }

        private static async Task<string> OpenAITranslate(string message, string targetLanguage)
        {
            if (!Regex.IsMatch(OpenAI_API_Key, @"^sk-[a-zA-Z0-9]{32,}$"))
            {
                Service.pluginLog.Warning("Incorrect API key format, falling back to machine translate.");
                return await MachineTranslate(message, targetLanguage);
            }

            string StandardPrompt = $"Translate FF14 chat to {targetLanguage}";
            string PerfectPrompt = $"Translate FF14 chat to {targetLanguage}, keep context and jargon. Return original if emojis / meaningless. Guess if unknown." +
                $"\nOutput plain text.";

            var requestData = new
            {
                model = Service.configuration.BetterTranslation ? "gpt-4-turbo-preview" : "gpt-3.5-turbo",
                temperature = 0.6,
                max_tokens = Math.Min(Math.Max(message.Length * 2, 20), 150),
                messages = new[]
                {
                    new
                    {
                        role = "system", content =
                            Service.configuration.BetterTranslation ? PerfectPrompt : StandardPrompt
                    },
                    new { role = "user", content = message }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, DefaultContentType);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
            {
                Content = content,
                Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {OpenAI_API_Key}" } }
            };

            try
            {
                var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var translated = JObject.Parse(jsonResponse)["choices"]?[0]?["message"]?["content"]?.ToString().Trim();

                if (!string.IsNullOrEmpty(translated))
                    return translated;
                else
                    throw new Exception("Translation not found in the expected JSON structure.");
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"{ex.Message}, falling back to machine translate.");
                return await MachineTranslate(message, targetLanguage);
            }
        }

    }
}
