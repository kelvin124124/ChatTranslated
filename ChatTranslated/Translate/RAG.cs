using ChatTranslated.Utils;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatTranslated.Translate
{
    internal static class RAGSystem
    {
        private const string DefaultContentType = "application/json";
        private static List<(string Content, Vector<float> Embedding)> KnowledgeBase;

        public static void Initialize(List<(string Content, float[] Embedding)> knowledgeBase)
        {
            // TODO: import knowledge base from file
            KnowledgeBase = knowledgeBase
                .Select(item => (item.Content, Vector<float>.Build.DenseOfArray(item.Embedding)))
                .ToList();
        }

        public static async Task<List<string>> GetTopResults(string query, int topK = 3)
        {
            if (!Regex.IsMatch(Service.configuration.OpenAI_API_Key, @"^sk-[a-zA-Z0-9\-_]{32,}$"))
            {
                Service.pluginLog.Warning("OpenAI API Key is invalid. Please check your configuration.");
                return new List<string>();
            }

            try
            {
                var queryEmbedding = Vector<float>.Build.DenseOfArray(await GenerateEmbedding(query));
                return KnowledgeBase
                    .OrderByDescending(doc => doc.Embedding.DotProduct(queryEmbedding) / (doc.Embedding.L2Norm() * queryEmbedding.L2Norm()))
                    .Take(topK)
                    .Select(doc => doc.Content)
                    .ToList();
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"RAG: failed to process query.\n{ex.Message}");
                return new List<string>();
            }
        }

        private static async Task<float[]> GenerateEmbedding(string text)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings")
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    input = text,
                    model = "text-embedding-ada-002"
                }), Encoding.UTF8, DefaultContentType),
                Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {Service.configuration.OpenAI_API_Key}" } }
            };

            using var response = await Translator.HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var jsonResponse = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(jsonResponse);
            var embeddingElement = document.RootElement.GetProperty("data")[0].GetProperty("embedding");
            return embeddingElement.EnumerateArray().Select(e => e.GetSingle()).ToArray();
        }
    }
}
