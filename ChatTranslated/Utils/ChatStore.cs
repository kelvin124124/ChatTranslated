using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ChatTranslated.Utils
{
    internal class ChatStore
    {
        public static async Task SendToDB(string message)
        {
            // wrap auto translated texts
            message = message.Replace("\uE040", "{[").Replace("\uE041", "]}");
            message = ChatHandler.Sanitize(message);

            var pbData = new { sentence = message, client = "chat_translated" };
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(pbData);
            var data = new StringContent(json, Encoding.UTF8, "application/json");

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("http://ffdb.sapphosound.com");
                await client.PostAsync("/api/collections/ff_sentences/records", data);
            }
        }
    }
}
