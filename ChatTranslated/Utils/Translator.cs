using Dalamud.Game.Text;
using Dalamud.Networking.Http;
using GTranslate.Translators;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatTranslated.Utils
{
    internal class Translator
    {
        private static readonly HttpClient HttpClient =
            new HttpClient(new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                ConnectCallback = new HappyEyeballsCallback().ConnectCallback,
            })
            { Timeout = TimeSpan.FromSeconds(20) };

        private static readonly GoogleTranslator2 GTranslator = new GoogleTranslator2(HttpClient);
        private static readonly BingTranslator BingTranslator = new BingTranslator(HttpClient);

        private const string DefaultContentType = "application/json";
        private static readonly string? ChatFunction_key = ReadSecret("ChatTranslated.Resources.ChatFunctionKey.secret").Replace("\n", string.Empty);

        public static Dictionary<string, string> TranslationCache = new Dictionary<string, string>();

        private static string ReadSecret(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return "";

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static async Task TranslateChat(string sender, string message, XivChatType type = XivChatType.Say)
        {
            // try get translation from cache
            if (!TranslationCache.TryGetValue(message, out string? translatedText))
            {
                // Translate message if not in cache
                translatedText = await TranslateMessage(message, Service.configuration.SelectedChatLanguage);
                TranslationCache[message] = translatedText;
            }

            ChatHandler.OutputTranslation(type, sender, $"{message} || {translatedText}");
        }

        public static async Task TranslateMainWindow(string message)
        {
            string translatedText = await TranslateMessage(message, Service.configuration.SelectedMainWindowLanguage);
            Service.mainWindow.PrintToOutput($"Translation: {translatedText}");
        }

        public static async Task TranslateFrDeChat(string sender, string message, XivChatType type = XivChatType.Say)
        {
            try
            {
                var language = await GTranslator.DetectLanguageAsync(message);
                Service.pluginLog.Debug($"language: {language.Name}");
                if (language.Name == "French" || language.Name == "German")
                {
                    await TranslateChat(sender, message, type);
                }
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"Warn: Failed to detect language. {ex}");
            }
        }

        private static async Task<string> TranslateMessage(string message, string targetLanguage)
        {
            switch (Service.configuration.SelectedMode)
            {
                case Configuration.Mode.MachineTranslate:
                    return await MachineTranslate(message, targetLanguage);
                case Configuration.Mode.OpenAI_API:
                    return await OpenAITranslate(message, targetLanguage);
                case Configuration.Mode.GPTProxy:
                    return await GPTProxyTranslate(message, targetLanguage);
                default:
                    Service.pluginLog.Warning("Warn: Unknown translation mode.");
                    return message;
            }
        }

        private static async Task<string> MachineTranslate(string message, string targetLanguage)
        {
            try
            {
                var result = await GTranslator.TranslateAsync(message, targetLanguage);
                return result.Translation;
            }
            catch (Exception GTex)
            {
                Service.pluginLog.Info($"Google Translate: {GTex.Message}, falling back to Bing Translate.");
                try
                {
                    var result = await BingTranslator.TranslateAsync(message, targetLanguage);
                    return result.Translation;
                }
                catch (Exception BTex)
                {
                    Service.pluginLog.Error($"Error: {BTex.Message}, both translators failed, returning original message.");
                    return message;
                }
            }
        }

        public static async Task<string> GPTProxyTranslate(string message, string targetLanguage)
        {
            if (string.IsNullOrEmpty(ChatFunction_key))
            {
                Service.pluginLog.Warning("Warn: GPTProxyTranslate - api key empty.");
                return await MachineTranslate(message, targetLanguage);
            }

            var requestData = new
            {
                message,
                targetLanguage
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, "https://jbtzl5pwoe.execute-api.us-east-2.amazonaws.com/default/ChatFunction")
            {
                Content = content
            };

            request.Headers.Add("x-api-key", ChatFunction_key);

            try
            {
                var response = await HttpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync();

                var jsonParsed = JObject.Parse(jsonResponse);
                var translated = jsonParsed["choices"]?[0]?["message"]?["content"]?.ToString().Trim();

                if (!string.IsNullOrEmpty(translated))
                    return translated;
                else
                    throw new Exception("Translation not found in the expected JSON structure.");
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"Error during proxy translation: {ex.Message}");
                return await MachineTranslate(message, targetLanguage);
            }
        }

        private static async Task<string> OpenAITranslate(string message, string targetLanguage)
        {
            if (!Regex.IsMatch(Service.configuration.OpenAI_API_Key, @"^sk-[a-zA-Z0-9]{32,}$"))
            {
                Service.pluginLog.Warning("Warn: Incorrect API key format, falling back to machine translate.");
                return await MachineTranslate(message, targetLanguage);
            }

            string StandardPrompt = $"TRANSLATE FFXIV chat to {targetLanguage}:";
            string PerfectPrompt = $"TRANSLATE FFXIV chat to {targetLanguage}:";
            //string PerfectPrompt = PhrasePerfectPrompt();

            var requestData = new
            {
                model = Service.configuration.PerfectTranslation ? "gpt-4-turbo" : "gpt-3.5-turbo",
                temperature = 0.6,
                max_tokens = Math.Min(Math.Max(message.Length * 2, 20), 150),
                messages = new[]
                {
                    new
                    {
                        role = "system", content =
                            Service.configuration.PerfectTranslation? PerfectPrompt : StandardPrompt
                    },
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
                var response = await HttpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var translated = JObject.Parse(jsonResponse)["choices"]![0]!["message"]!["content"]!.ToString().Trim();

                if (!string.IsNullOrEmpty(translated))
                    return translated;
                else
                    throw new Exception("Translation not found in the expected JSON structure.");
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"Warn: {ex.Message}, falling back to machine translate.");
                return await MachineTranslate(message, targetLanguage);
            }
        }
        private static string PhrasePerfectPrompt()
        {
            // TODO: Implement phrase perfect prompt
            // context: cached message in 120 seconds with the same type (including self)
            string context = "I'm a professional translator, and I'm translating a conversation between two people who are speaking in a language I don't understand. I'm translating their conversation into English.";
            // instruction: Cosplay prompt, example output, point to note like ignore emojis / meaningless sentences
            string instruction = "";
            // knowledge: detect payload (item / map) & player instance / location, fetch knowledge from db (if any)
            string knowledge = "";

            // return organized prompt
            return "";
        }
    }
}
