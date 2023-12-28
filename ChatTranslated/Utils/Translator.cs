using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatTranslated.Utils
{
    internal class Translator
    {
        public Translator() { }
        public async void Translate(string sender, string message)
        {
            message = Sanitize(message);
            Service.mainWindow.PrintToOutput($"Debug - sanitized str: {message}");
            string translatedText = "";
            switch (Service.configuration.SelectedMode)
            {
                case Configuration.Mode.LibreTranslate:
                    translatedText = await LibreTranslate(message);
                    break;
                case Configuration.Mode.GPT4Beta:
                    translatedText = await ServerTranslate(message);
                    break;
                case Configuration.Mode.OpenAIAPI:
                    translatedText = await OpenAITranslate(message);
                    break;
            }
            Service.mainWindow.PrintToOutput($"{sender}: {translatedText}");
            return;
        }

        private string Sanitize(string input)
        {
            var regex = new Regex(@"[\uE000-\uF8FF]+", RegexOptions.Compiled);
            return regex.Replace(input, "");
        }

        private async Task<string> LibreTranslate(string message)
        {
            var client = new HttpClient();

            var requestData = new
            {
                q = message,
                source = "auto",
                target = "en",
                format = "text",
                api_key = ""
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(Service.configuration.SERVER, content);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var match = Regex.Match(jsonResponse, @"""translatedText""\s*:\s*""(.*?)""", RegexOptions.Compiled).Groups[1].Value.ToString();
                    return match;
                }
                Service.pluginLog.Warning("Error: API request failed");
                return message;
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"Error: {ex.Message}");
                return message;
            }
        }

        private async Task<string> ServerTranslate(string message)
        {
            Service.mainWindow.PrintToOutput($"Error: Not yet supprted.");
            return message;
        }

        private async Task<string> OpenAITranslate(string message)
        {
            Service.mainWindow.PrintToOutput($"Error: Not yet supprted.");
            return message;
        }

        public void Dispose()
        {
            // do nothing
        }
    }
}
