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
        private static readonly HttpClient HttpClient =
        new(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = new HappyEyeballsCallback().ConnectCallback,
        })
        { Timeout = TimeSpan.FromSeconds(10) };

        public static GoogleTranslator2 GTranslator = new(HttpClient);
        public static BingTranslator BingTranslator = new(HttpClient);

        private const string DefaultContentType = "application/json";
        private static readonly string? Cfv2 = ReadSecret("ChatTranslated.Resources.cfv2.secret").Replace("\n", string.Empty);

        private static string ReadSecret(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return "";

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static async Task<string> Translate(string text, string targetLanguage, TranslationMode? translationMode = null)
        {
            text = ChatHandler.Sanitize(text);
            if (string.IsNullOrWhiteSpace(text))
                return text;

            switch ((translationMode != null) ? translationMode : Service.configuration.SelectedTranslationMode)
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
                Service.pluginLog.Info($"Exception in Google Translate: {GTex.Message}.");
                try
                {
                    var result = await BingTranslator.TranslateAsync(text, targetLanguage);
                    return result.Translation;
                }
                catch (Exception BTex)
                {
                    Service.pluginLog.Info($"Exception in Bing Translate: {BTex.Message}.");
                    return text;
                }
            }
        }

        public static async Task<string> DeepLTranslate(string text, string targetLanguage)
        {
            if (TryGetLanguageCode(targetLanguage, out var languageCode))
            {
                var requestBody = new
                {
                    text = new string[] { text },
                    target_lang = languageCode,
                    context = "FFXIV, MMORPG"
                };

                string jsonContent = JsonSerializer.Serialize(requestBody);
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api-free.deepl.com/v2/translate")
                {
                    Content = new StringContent(jsonContent, Encoding.UTF8, "application/json"),
                    Headers = { { HttpRequestHeader.Authorization.ToString(), $"DeepL-Auth-Key {Service.configuration.DeepL_API_Key}" } }
                };

                try
                {
                    var response = await HttpClient.SendAsync(requestMessage).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    string jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    var translated = JObject.Parse(jsonResponse)["translations"]?[0]?["text"]?.ToString().Trim();

                    if (!string.IsNullOrWhiteSpace(translated))
                    {
                        if (targetLanguage == "Chinese (Traditional)")
                            return await MachineTranslate(translated, "Chinese (Traditional)");
                        else
                            return translated;
                    }
                    else
                        throw new Exception("Translation not found in the expected JSON structure.");
                }
                catch (Exception DLex)
                {
                    Service.pluginLog.Info($"DeepL Translate: {DLex.Message}, falling back to Google Translate.");
                    return await MachineTranslate(text, targetLanguage);
                }
            }
            else
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
                Service.pluginLog.Warning("LLMProxyTranslate - api key empty.");
                return await MachineTranslate(message, targetLanguage);
            }
#endif
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

            request.Headers.Add("x-api-key", Cfv2);

            try
            {
                var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
                Service.pluginLog.Debug(response.Content.ToString() ?? "Proxy: HTTP response is null");

                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                Service.pluginLog.Debug($"responseBody = {responseBody}");

                // Attempt to parse JSON from the responseBody
                var jObject = JObject.Parse(responseBody);

                // Extract translation based on the updated JSON structure
                string? translated = jObject["translated"]?.ToString().Trim();

                if (!string.IsNullOrWhiteSpace(translated))
                    return translated;
                else
                    throw new Exception("Translation not found in the expected JSON structure.");
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"Error during proxy translation: {ex.Message}.");
                return await MachineTranslate(message, targetLanguage);
            }
        }

        private static async Task<string> OpenAITranslate(string message, string targetLanguage)
        {
            if (!Regex.IsMatch(Service.configuration.OpenAI_API_Key, @"^sk-[a-zA-Z0-9]{32,}$"))
            {
                Service.pluginLog.Warning("Incorrect API key format, falling back to machine translate.");
                return await MachineTranslate(message, targetLanguage);
            }

            string prmopt = $"Translate FF14 chat to {targetLanguage}, keep context and jargon. Return original if emojis / meaningless. Guess if unknown." +
                $"\nOutput plain text.";

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

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, DefaultContentType);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
            {
                Content = content,
                Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {Service.configuration.OpenAI_API_Key}" } }
            };

            try
            {
                var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var translated = JObject.Parse(jsonResponse)["choices"]?[0]?["message"]?["content"]?.ToString().Trim();

                if (!string.IsNullOrWhiteSpace(translated))
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
