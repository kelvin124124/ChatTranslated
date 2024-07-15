using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ChatTranslated.Utils
{
    internal class ChatStore : IDisposable
    {
        private static readonly string Client = "chat_translated_FV2";

        private static readonly HttpClient HttpClient = new()
        {
            BaseAddress = new Uri("http://ffdb.sapphosound.com"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        public static async Task SendToDB(string message)
        {
            // wrap auto translated texts
            message = message.Replace("\uE040", "{[").Replace("\uE041", "]}");
            message = ChatHandler.Sanitize(message);

            var pbData = new { sentence = message, Client };
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(pbData);
            var data = new StringContent(json, Encoding.UTF8, "application/json");

            await HttpClient.PostAsync("/api/collections/ff_sentences/records", data);
        }

        public void Dispose()
        {
            HttpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
