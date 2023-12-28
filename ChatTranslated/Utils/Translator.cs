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
            switch (Service.configuration.SelectedMode)
            {
                case Configuration.Mode.LibreTranslate:
                    await LibreTranslate(sender, message, Service.configuration.SERVER);
                    return;
                case Configuration.Mode.GPT4Beta:
                    await ServerTranslate(sender, message);
                    return;
                case Configuration.Mode.OpenAIAPI:
                    await OpenAITranslate(sender, message);
                    return;
            }
        }

        private async Task LibreTranslate(string sender, string message, string SERVER)
        {
            string apiUrl = SERVER;
            var client = new HttpClient();

            var requestData = new
            {
                q = message,
                source = "ja",
                target = "en",
                format = "text",
                api_key = ""
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(apiUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var match = Regex.Match(jsonResponse, @"""translatedText""\s*:\s*""(.*?)""", RegexOptions.Compiled).Groups[1].Value.ToString();
                    Service.mainWindow.PrintToOutput($"{sender}: {match}");
                    return;
                }
                Service.pluginLog.Warning("Error: API request failed");
                Service.mainWindow.PrintToOutput($"Error: API request failed\n{sender}: {message}");
                return;
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"Error: {ex.Message}");
                Service.mainWindow.PrintToOutput($"Error: {ex.Message}\n{sender}: {message}");
                return;
            }
        }

        private async Task ServerTranslate(string sender, string message)
        {
            Service.mainWindow.PrintToOutput($"Error: Not yet supprted.\n{sender}: {message}");
            return;
        }

        private async Task OpenAITranslate(string sender, string message)
        {
            Service.mainWindow.PrintToOutput($"Error: Not yet supprted.\n{sender}: {message}");
            return;
        }

        public void Dispose()
        {

        }
    }
}
