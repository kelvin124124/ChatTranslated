using ChatTranslated.Utils;
using Dalamud.Networking.Http;
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
        private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
        {
            ConnectCallback = new HappyEyeballsCallback().ConnectCallback,
            AutomaticDecompression = DecompressionMethods.All
        })
        {
            DefaultRequestVersion = HttpVersion.Version30,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,

            Timeout = TimeSpan.FromSeconds(10)
        };

        public static GoogleTranslator2 GTranslator = new(HttpClient);
        public static BingTranslator BingTranslator = new(HttpClient);

        private const string DefaultContentType = "application/json";
        private static readonly string? Cfv2 = ReadSecret("ChatTranslated.Resources.cfv2.secret").Replace("\n", string.Empty);

        private static string ReadSecret(string resourceName)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            return stream == null ? "" : new StreamReader(stream).ReadToEnd();
        }

        public static async Task<string> Translate(string text, string targetLanguage, TranslationMode? translationMode = null)
        {
            text = ChatHandler.Sanitize(text);
            if (string.IsNullOrWhiteSpace(text)) return text;

            var mode = translationMode ?? Service.configuration.SelectedTranslationMode;

            return mode switch
            {
                Configuration.TranslationMode.MachineTranslate => await MachineTranslate(text, targetLanguage),
                Configuration.TranslationMode.DeepL_API => await DeepLTranslate(text, targetLanguage),
                Configuration.TranslationMode.OpenAI_API => await OpenAITranslate(text, targetLanguage),
                Configuration.TranslationMode.LLMProxy => await LLMProxyTranslate(text, targetLanguage),
                _ => text
            };
        }

        private static async Task<string> MachineTranslate(string text, string targetLanguage)
        {
            try
            {
                var result = await GTranslator.TranslateAsync(text, targetLanguage);
                return result.Translation;
            }
            catch
            {
                try
                {
                    var result = await BingTranslator.TranslateAsync(text, targetLanguage);
                    return result.Translation;
                }
                catch
                {
                    return text;
                }
            }
        }

        public static async Task<string> DeepLTranslate(string text, string targetLanguage)
        {
            if (TryGetLanguageCode(targetLanguage, out var languageCode))
            {
                var requestBody = new { text = new[] { text }, target_lang = languageCode, context = "FFXIV, MMORPG" };
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api-free.deepl.com/v2/translate")
                {
                    Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, DefaultContentType),
                    Headers = { { HttpRequestHeader.Authorization.ToString(), $"DeepL-Auth-Key {Service.configuration.DeepL_API_Key}" } }
                };

                try
                {
                    var response = await HttpClient.SendAsync(requestMessage).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var translated = JObject.Parse(jsonResponse)["translations"]?[0]?["text"]?.ToString().Trim();
                    return !string.IsNullOrWhiteSpace(translated)
                        ? (targetLanguage == "Chinese (Traditional)" ? await MachineTranslate(translated, "Chinese (Traditional)") : translated)
                        : throw new Exception("Translation not found in the expected JSON structure.");
                }
                catch
                {
                    return await MachineTranslate(text, targetLanguage);
                }
            }
            return "Target language not supported by DeepL.";
        }

        public static bool TryGetLanguageCode(string language, out string? languageCode)
        {
            languageCode = language switch
            {
                "English" => "EN-GB",
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
#if DEBUG
            string Cfv2 = Service.configuration.Proxy_API_Key;
#else
            if (string.IsNullOrEmpty(Cfv2))
            {
                return await MachineTranslate(message, targetLanguage);
            }
#endif

            var requestData = new { regionCode = Service.configuration.ProxyRegion, targetLanguage, message };
            var request = new HttpRequestMessage(HttpMethod.Post, "https://cfv2.kelpcc.com")
            {
                Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, DefaultContentType)
            };
            request.Headers.Add("x-api-key", Cfv2);

            try
            {
                var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();
                var translated = JObject.Parse(responseBody)["translated"]?.ToString().Trim();
                return !string.IsNullOrWhiteSpace(translated) && translated != "{}" ? translated.Replace("\n", string.Empty) : throw new Exception("Translation not found in the expected JSON structure.");
            }
            catch
            {
                return await MachineTranslate(message, targetLanguage);
            }
        }

        private static async Task<string> OpenAITranslate(string message, string targetLanguage)
        {
            if (!Regex.IsMatch(Service.configuration.OpenAI_API_Key, @"^sk-[a-zA-Z0-9]{32,}$"))
            {
                return await MachineTranslate(message, targetLanguage);
            }

            var prmopt = $"Translate FF14 chat to {targetLanguage}, keep context and jargon. Return original if emojis / meaningless. Guess if unknown.\nOutput plain text.";
            var requestData = new
            {
                model = "gpt-3.5-turbo",
                temperature = 0.6,
                max_tokens = Math.Min(Math.Max(message.Length * 2, 20), 150),
                messages = new[]
                {
                    new { role = "system", content = prmopt },
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
                var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var translated = JObject.Parse(jsonResponse)["choices"]?[0]?["message"]?["content"]?.ToString().Trim();
                return !string.IsNullOrWhiteSpace(translated) ? translated : throw new Exception("Translation not found in the expected JSON structure.");
            }
            catch
            {
                return await MachineTranslate(message, targetLanguage);
            }
        }
    }
}
