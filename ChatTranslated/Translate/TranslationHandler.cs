using ChatTranslated.Chat;
using ChatTranslated.Utils;
using Dalamud.Networking.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ChatTranslated.Translate;

internal static class TranslationHandler
{
    internal static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        ConnectCallback = new HappyEyeballsCallback().ConnectCallback,
        AutomaticDecompression = DecompressionMethods.All
    })
    {
        DefaultRequestVersion = HttpVersion.Version30,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        Timeout = TimeSpan.FromSeconds(20)
    };

    // optional request fields are left out instead of sent as null
    private static readonly JsonSerializerOptions RequestJsonOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    // POSTs a JSON body and returns the parsed JSON response; throws on a non-success status.
    internal static async Task<JsonElement> PostJsonAsync(string url, object body, Action<HttpRequestHeaders>? setHeaders = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, RequestJsonOptions), Encoding.UTF8, "application/json")
        };
        setHeaders?.Invoke(request.Headers);

        using var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
    }

    private const int MAX_CACHE_SIZE = 120;

    // insertion order doubles as recency: a hit moves its entry to the end, eviction takes the front
    private static readonly OrderedDictionary<string, string> TranslationCache = [];
    private static readonly Lock TranslationCacheLock = new();

    private static string CacheFilePath =>
        Path.Combine(Service.pluginInterface.ConfigDirectory.FullName, "translation_cache.json");

    public static void LoadCache()
    {
        try
        {
            var path = CacheFilePath;
            if (!File.Exists(path)) return;

            var entries = JsonSerializer.Deserialize<List<string[]>>(File.ReadAllText(path));
            if (entries == null) return;

            lock (TranslationCacheLock)
            {
                TranslationCache.Clear();
                // only keep the most recent MAX_CACHE_SIZE entries
                foreach (var pair in entries.TakeLast(MAX_CACHE_SIZE))
                {
                    if (pair.Length == 2)
                        TranslationCache.TryAdd(pair[0], pair[1]);
                }
            }

            Service.pluginLog.Information($"[TranslationHandler] Loaded {TranslationCache.Count} cache entries.");
        }
        catch (Exception ex)
        {
            Service.pluginLog.Warning($"[TranslationHandler] Failed to load cache: {ex.Message}");
        }
    }

    public static void SaveCache()
    {
        try
        {
            List<string[]> snapshot;
            lock (TranslationCacheLock)
                snapshot = [.. TranslationCache.Select(kv => new[] { kv.Key, kv.Value })];

            var path = CacheFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(snapshot));
        }
        catch (Exception ex)
        {
            Service.pluginLog.Warning($"[TranslationHandler] Failed to save cache: {ex.Message}");
        }
    }

    public static async Task<Message> TranslateMessage(Message message, string? targetLanguage = null)
    {
        targetLanguage ??= Service.configuration.EffectiveTargetLanguage;

        // entries are keyed by text alone and the cache is cleared whenever the target language changes,
        // so only translations into the configured language may read or write it
        var cacheEnabled = Service.configuration.EnableTranslationCache
            && message.Source != MessageSource.MainWindow
            && targetLanguage == Service.configuration.EffectiveTargetLanguage;

        if (cacheEnabled)
        {
            lock (TranslationCacheLock)
            {
                if (TranslationCache.Remove(message.OriginalText, out var cached))
                {
                    TranslationCache.Add(message.OriginalText, cached);
                    message.TranslatedContent = cached;
                    return message;
                }
            }
        }

        var (translatedText, mode) = Service.configuration.UseCustomLanguage
            ? await MachineTranslate.Translate(message.OriginalText, targetLanguage)
            : Service.configuration.SelectedTranslationEngine switch
            {
                Configuration.TranslationEngine.DeepL => await DeeplsTranslate.Translate(message.OriginalText, targetLanguage),
                Configuration.TranslationEngine.LLM => Service.configuration.LLM_Provider switch
                {
                    0 => await LLMProxyTranslate.Translate(message, targetLanguage),
                    1 => await OpenAITranslate.Translate(message, targetLanguage, model: Service.configuration.OpenAI_Model),
                    2 => await OpenAICompatible.Translate(message, targetLanguage),
                    _ => (message.OriginalText, null)
                },
                _ => (message.OriginalText, null)
            };

        message.TranslatedContent = translatedText;
        message.TranslationMode = mode;

        // a null mode means every translator failed; don't remember that
        if (cacheEnabled && mode is not (null or Configuration.TranslationMode.MachineTranslate))
        {
            bool added;
            lock (TranslationCacheLock)
            {
                added = TranslationCache.TryAdd(message.OriginalText, translatedText);
                if (added && TranslationCache.Count > MAX_CACHE_SIZE)
                    TranslationCache.RemoveAt(0);
            }

            if (added)
                _ = Task.Run(SaveCache);
        }

        return message;
    }

    public static void ClearTranslationCache()
    {
        lock (TranslationCacheLock)
            TranslationCache.Clear();

        _ = Task.Run(SaveCache);
    }
}
