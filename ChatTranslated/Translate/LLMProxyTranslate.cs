using ChatTranslated.Chat;
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
        private static readonly string? Cfv5 = ReadSecret("ChatTranslated.Resources.cfv5.secret").Replace("\n", string.Empty);

        private static string ReadSecret(string resourceName)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            return stream == null ? "" : new StreamReader(stream).ReadToEnd();
        }

        public static async Task<(string, TranslationMode?)> Translate(Message message, string targetLanguage)
        {
#if DEBUG
            string Cfv5 = Service.configuration.Proxy_API_Key;
#else
            if (string.IsNullOrEmpty(Cfv5))
            {
                Service.pluginLog.Warning("LLMProxy API key not found. Falling back to machine translate.");
                return await MachineTranslate.Translate(message.OriginalContent.TextValue, targetLanguage);
            }
#endif

            if (!Service.configuration.UseContext)
            {
                message.Context = "null";
            }

            var requestData = new { targetLanguage, message = message.OriginalContent.TextValue, context = message.Context };
            var request = new HttpRequestMessage(HttpMethod.Post, Service.configuration.Proxy_Url)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, DefaultContentType)
            };
            request.Headers.Add("x-api-key", Cfv5);

            try
            {
                var response = await Translator.HttpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var jsonResponse = JObject.Parse(responseBody);

                var translated = jsonResponse["translated"]?.ToString().Trim();
                var responseTime = jsonResponse["responseTime"]?.ToString();

                if (translated.IsNullOrWhitespace())
                {
                    throw new Exception("Translation not found in the expected structure.");
                }

                if (translated == message.OriginalContent.TextValue)
                {
                    Service.pluginLog.Warning("Message was not translated. Falling back to machine translate.");
                    return await MachineTranslate.Translate(message.OriginalContent.TextValue, targetLanguage);
                }

                Service.pluginLog.Info($"Request processed in: {responseTime}");

                return (translated.Replace("\n", string.Empty), TranslationMode.LLMProxy);
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"LLMProxy Translate failed to translate. Falling back to machine translate.\n{ex.Message}");
                return await MachineTranslate.Translate(message.OriginalContent.TextValue, targetLanguage);
            }
        }
    }
}
