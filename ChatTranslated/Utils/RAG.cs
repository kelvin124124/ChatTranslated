using ChatTranslated.Translate;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatTranslated.Utils
{
    // TODO: take keyword search into consideration
    internal static class RAG
    {
        private const string DefaultContentType = "application/json";
        private static readonly string EmbeddingsArchivePath = Path.Join(Service.pluginInterface.AssemblyLocation.DirectoryName, "Utils", "embeddings", "embeddings.zip");
        private const string OpenAIEmbeddingsEndpoint = "https://api.openai.com/v1/embeddings";
        private const string EmbeddingModel = "text-embedding-3-large";

        private static readonly List<KnowledgeItem> KnowledgeBase = [];

        static RAG()
        {
            Initialize();
        }

        public static void Initialize()
        {
            using var archive = ZipFile.OpenRead(EmbeddingsArchivePath);
            foreach (var entry in archive.Entries.Where(e => e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                using var stream = entry.Open();
                var embeddingsData = JsonSerializer.Deserialize<EmbeddingsData>(stream);
                if (embeddingsData is null)
                {
                    Service.pluginLog.Warning($"RAG: Failed to deserialize embeddings data from {entry.FullName}");
                    continue;
                }

                KnowledgeBase.AddRange(embeddingsData.Embeddings.Select(item => new KnowledgeItem(item.chunk, item.embedding)));
            }

            Service.pluginLog.Information($"RAG: Initialized knowledge base with {KnowledgeBase.Count} entries.");
        }

        public static IReadOnlyList<string>? GetTopResults(Vector<float> query, int topK = 3, float minScore = 0.3f)
        {
            if (!IsValidApiKey(Service.configuration.OpenAI_API_Key))
            {
                Service.pluginLog.Warning("OpenAI API Key is invalid. Please check your configuration.");
                return null;
            }
            try
            {
                var queryNorm = query.L2Norm();
                var results = KnowledgeBase
                    .Select(doc => (doc.Content, Similarity: ComputeCosineSimilarity(doc.Embedding, query, doc.EmbeddingNorm, queryNorm)))
                    .Where(result => result.Similarity >= minScore)
                    .OrderByDescending(result => result.Similarity)
                    .Take(topK)
                    .Select(result => result.Content)
                    .ToArray();

                return (results.Length > 0) ?
                    results : null;
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"RAG: Failed to process query.\n{ex.Message}");
                return null;
            }
        }

        public static async Task<Vector<float>> GenerateEmbedding(string text)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, OpenAIEmbeddingsEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { input = text, model = EmbeddingModel }), System.Text.Encoding.UTF8, DefaultContentType),
                Headers = { { "Authorization", $"Bearer {Service.configuration.OpenAI_API_Key}" } }
            };

            using var response = await Translator.HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            using var responseStream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(responseStream);

            var embeddingData = document.RootElement.GetProperty("data")[0].GetProperty("embedding");

            return Vector<float>.Build.DenseOfEnumerable(embeddingData.EnumerateArray().Select(e => e.GetSingle()));
        }

        private static bool IsValidApiKey(string apiKey) =>
            Regex.IsMatch(apiKey, @"^sk-[a-zA-Z0-9\-_]{32,}$");

        private static double ComputeCosineSimilarity(Vector<float> a, Vector<float> b, double aNorm, double bNorm) =>
            a.DotProduct(b) / (aNorm * bNorm);
    }

    internal readonly struct KnowledgeItem
    {
        public string Content { get; }
        public Vector<float> Embedding { get; }
        public double EmbeddingNorm { get; }

        public KnowledgeItem(string content, float[] embedding)
        {
            Content = content;
            Embedding = Vector<float>.Build.Dense(embedding);
            EmbeddingNorm = Embedding.L2Norm();
        }
    }

    internal class EmbeddingsData
    {
        public DateTime Generated { get; set; }
        public List<EmbeddingItem> Embeddings { get; set; } = null!;
    }

    internal class EmbeddingItem
    {
        public List<string> keywords { get; set; } = null!;
        public string chunk { get; set; } = null!;
        public float[] embedding { get; set; } = null!;
    }
}
