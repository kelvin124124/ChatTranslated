using Dalamud.Game.Text;
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

        public static async Task TranslateChat(string sender, string message, XivChatType type = XivChatType.Say)
        {
            string translatedText = await TranslateMessage(message, Service.configuration.SelectedChatLanguage);
            Service.mainWindow.PrintToOutput($"{sender}: {translatedText}");

            if (Service.configuration.ChatIntegration && translatedText.Length < 500)
            {
                Plugin.OutputChatLine(sender, $"{message} || {translatedText}", type);
            }
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
                case Mode.MachineTranslate:
                    return await MachineTranslate(message, targetLanguage);
                case Mode.OpenAI_API:
                    return await OpenAITranslate(message, targetLanguage);
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

        private static async Task<string> OpenAITranslate(string message, string targetLanguage)
        {
            if (string.IsNullOrEmpty(Service.configuration.OpenAI_API_Key))
            {
                Service.pluginLog.Warning("Warn: API key is null, falling back to machine translate.");
                return await MachineTranslate(message, targetLanguage);
            }

            var requestData = new
            {
                model = MODEL,
                max_tokens = 300,
                messages = new[]
                {
                    new
                    {
                        role = "system", content =
                            $"Translate this MMORPG chat message from FFXIV to {targetLanguage}."
                    },
                    new { role = "user", content = message }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, DefaultContentType);
            var request = new HttpRequestMessage(HttpMethod.Post, OPENAI_API)
            {
                Content = content,
                Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {Service.configuration.OpenAI_API_Key}" } }
            };

            try
            {
                var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var translated = JObject.Parse(jsonResponse)["choices"]![0]!["message"]!["content"]!.ToString().Trim();
                return translated;
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"Warn: {ex.Message}, falling back to machine translate.");
                return await MachineTranslate(message, targetLanguage);
            }
        }
    }
}
