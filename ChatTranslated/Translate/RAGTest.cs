using ChatTranslated.Utils;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatTranslated.Translate
{
    internal class RAGTest
    {
        private const string BaseUrl = "https://api.dify.ai/v1";

        public static async Task<string> Translate(string message, string targetLanguage)
        {
            var client = new HttpClient();
            string ApiKey = Service.configuration.Experimental_API_Key;
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");

            var inputs = new
            {
                targetLanguage = targetLanguage,
                message = message
            };

            var requestBody = new
            {
                inputs = inputs,
                response_mode = "blocking",
                user = "dev"
            };

            Plugin.OutputChatLine($"Requesting experimental translation for message: {message}");
            Plugin.OutputChatLine($"Request body: {JsonSerializer.Serialize(requestBody)}");

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{BaseUrl}/completion-messages", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            Service.pluginLog.Info($"Response: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                var result = JsonDocument.Parse(responseContent);
                var answer = result.RootElement.GetProperty("answer").GetString();
                return answer ?? "answer is null";
            }
            else
            {
                throw new Exception($"API request failed with status code: {response.StatusCode}. Error message: {responseContent}");
            }
        }
    }
}
