using ChatTranslated.Utils;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatTranslated.Translate
{
    internal static class DeeplsTranslate
    {
        private static readonly Random Random = new Random();
        public static async Task<string> Translate(string message, string targetLanguage)
        {
            string postData = PreparePostData(targetLanguage, message);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://www2.deepl.com/jsonrpc")
            {
                Content = new StringContent(postData, Encoding.UTF8, "application/json")
            };
            try
            {
                var response = await Translator.HttpClient.SendAsync(request);
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                Service.pluginLog.Warning("DeeplsTranslate failed to translate. Falling back to machine translation.");
                return await MachineTranslate.Translate(message, targetLanguage);
            }
        }

        private static string PreparePostData(string targetLang, string text)
        {
            ulong id = (ulong)Random.Next(8300000, 8400000) * 1000 + 1;
            var postData = new
            {
                jsonrpc = "2.0",
                method = "LMT_handle_texts",
                @params = new
                {
                    splitting = "newlines",
                    lang = new { source_lang_user_selected = "auto", target_lang = targetLang },
                    texts = new[] { new { text } },
                    timestamp = GetTimeStamp(text)
                },
                id,
                timestamp = GetTimeStamp(text),
            };

            string postDataJson = JsonSerializer.Serialize(postData);
            postDataJson = postDataJson.Replace("\"method\":\"", (id + 5) % 29 == 0 || (id + 3) % 13 == 0 ? "\"method\" : \"" : "\"method\": \"");

            return postDataJson;
        }

        private static long GetTimeStamp(string text)
        {
            var iCount = text.Count(c => c == 'i');
            long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return iCount == 0 ? ts : ts - ts % (iCount + 1) + iCount + 1;
        }
    }
}
