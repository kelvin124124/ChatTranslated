using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatTranslated.Utils
{
    internal class Translator : IDisposable
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        
        private const string DefaultContentType = "application/json";

        private static readonly Regex TranslatedRegex = new Regex(@"""translatedText""\s*:\s*""(.*?)""", RegexOptions.Compiled);

        public async Task Translate(string sender, string message)
        {
            string translatedText = message;

            switch (Service.configuration.SelectedMode)
            {
                case Configuration.Mode.LibreTranslate:
                    translatedText = await LibreTranslate(message).ConfigureAwait(false);
                    break;
                case Configuration.Mode.GPT4Beta:
                    translatedText = ServerTranslate(message);
                    break;
                case Configuration.Mode.OpenAIAPI:
                    translatedText = OpenAITranslate(message);
                    break;
            }

            Service.mainWindow.PrintToOutput($"{sender}: {translatedText}");
        }

        private async Task<string> LibreTranslate(string message)
        {
            Service.pluginLog.Information($"LibreTranslate - Message: {message}");

            var requestData = new
            {
                q = message,
                source = "auto",
                target = "en",
                format = "text",
                api_key = ""
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, DefaultContentType);

            int attempt = 0;

            while (attempt < Service.configuration.maxAttempts)
            {
                try
                {
                    var response = await HttpClient.PostAsync(Service.configuration.SERVER, content).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var match = TranslatedRegex.Match(jsonResponse).Groups[1].Value;

                        Service.pluginLog.Information($"LibreTranslate - Match: {match}");
                        return match;
                    }
                }
                catch (Exception ex)
                {
                    Service.pluginLog.Warning($"LibreTranslate - Error: {ex.Message}");
                }

                attempt++;
            }

            Service.pluginLog.Warning("LibreTranslate - Warning: API request failed");
            return message; // Return original message if all retries fail
        }


        private string ServerTranslate(string message)
        {
            Service.mainWindow.PrintToOutput("ServerTranslate - Error: Not yet supported.");
            return message;
        }

        private string OpenAITranslate(string message)
        {
            Service.mainWindow.PrintToOutput("OpenAITranslate - Error: Not yet supported.");
            return message;
        }

        public void Dispose()
        {
            HttpClient?.Dispose();
        }
    }
}
