using ChatTranslated.Translate;
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
    internal static class RAG
    {
        // TODO: use full text search for short texts

        private const string DefaultContentType = "application/json";
        private static readonly string EmbeddingsArchivePath = Path.Join(Service.pluginInterface.AssemblyLocation.DirectoryName, "Utils", "embeddings", "embeddings.zip");
        private const string OpenAIEmbeddingsEndpoint = "https://api.openai.com/v1/embeddings";
        private const string EmbeddingModel = "text-embedding-3-large";

        private static readonly List<KnowledgeItem> KnowledgeBase = new List<KnowledgeItem>();

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

        public static IReadOnlyList<string>? GetTopResults(float[] query, int topK = 3, float minScore = 0.3f)
        {
            if (!IsValidApiKey(Service.configuration.OpenAI_API_Key))
            {
                Service.pluginLog.Warning("OpenAI API Key is invalid. Please check your configuration.");
                return null;
            }
            try
            {
                var queryNorm = CalculateL2Norm(query);
                var resultsWithScores = KnowledgeBase
                    .Select(doc => (doc.Content, Similarity: ComputeCosineSimilarity(doc.Embedding, query, doc.EmbeddingNorm, queryNorm)))
                    .Where(result => result.Similarity >= minScore)
                    .OrderByDescending(result => result.Similarity)
                    .Take(topK)
                    .ToList();

                var results = resultsWithScores.Select(r => r.Content).ToArray();
#if DEBUG
                Service.pluginLog.Information($"RAG: Found {results.Length} results above the minimum score threshold.");
                Service.pluginLog.Information($"RAG: Top results: {string.Join(", ", results)}, scores: {string.Join(", ", resultsWithScores.Select(r => r.Similarity))}");
#endif
                return (results.Length > 0) ? results : null;
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"RAG: Failed to process query.\n{ex.Message}");
                return null;
            }
        }

        public static async Task<float[]> GenerateEmbedding(string text)
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

            return embeddingData.EnumerateArray().Select(e => e.GetSingle()).ToArray();
        }

        private static bool IsValidApiKey(string apiKey) =>
            Regex.IsMatch(apiKey, @"^sk-[a-zA-Z0-9\-_]{32,}$");

        private static double ComputeCosineSimilarity(float[] a, float[] b, double aNorm, double bNorm)
        {
            double dotProduct = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
            }
            return dotProduct / (aNorm * bNorm);
        }

        internal static double CalculateL2Norm(float[] vector)
        {
            double sumOfSquares = 0;
            for (int i = 0; i < vector.Length; i++)
            {
                sumOfSquares += vector[i] * vector[i];
            }
            return Math.Sqrt(sumOfSquares);
        }
    }

    internal readonly struct KnowledgeItem
    {
        public string Content { get; }
        public float[] Embedding { get; }
        public double EmbeddingNorm { get; }

        public KnowledgeItem(string content, float[] embedding)
        {
            Content = content;
            Embedding = embedding;
            EmbeddingNorm = RAG.CalculateL2Norm(embedding);
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
