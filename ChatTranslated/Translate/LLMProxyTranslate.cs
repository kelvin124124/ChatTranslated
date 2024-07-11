using ChatTranslated.Utils;
using Dalamud.Utility;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Translate
{
    internal static class LLMProxyTranslate
    {
        private const string DefaultContentType = "application/json";
        private static readonly string? Cfv2 = ReadSecret("ChatTranslated.Resources.cfv2.secret").Replace("\n", string.Empty);

        private static string ReadSecret(string resourceName)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            return stream == null ? "" : new StreamReader(stream).ReadToEnd();
        }

        public static async Task<(string, TranslationMode?)> Translate(string message, string targetLanguage)
        {
#if DEBUG
            string Cfv2 = Service.configuration.Proxy_API_Key;
#else
            if (string.IsNullOrEmpty(Cfv2))
            {
                Service.pluginLog.Warning("LLMProxy API key not found. Falling back to machine translate.");
                return await MachineTranslate.Translate(message, targetLanguage);
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
                var response = await Translator.HttpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var translated = JObject.Parse(responseBody)["translated"]?.ToString().Trim();

                if (translated.IsNullOrWhitespace() || translated == "{}")
                {
                    throw new Exception("Translation not found in the expected JSON structure.");
                }

                return (translated.Replace("\n", string.Empty), TranslationMode.LLMProxy);
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"LLMProxy Translate failed to translate. Falling back to machine translate.\n{ex.Message}");
                return await MachineTranslate.Translate(message, targetLanguage);
            }
        }
    }
}
