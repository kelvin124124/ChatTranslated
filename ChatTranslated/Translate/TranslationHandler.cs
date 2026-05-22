using ChatTranslated.Chat;
using ChatTranslated.Utils;
using Dalamud.Networking.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
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

    private const int MAX_CACHE_SIZE = 120;

    private static readonly LinkedList<KeyValuePair<string, string>> TranslationCache = new();
    private static readonly Dictionary<string, LinkedListNode<KeyValuePair<string, string>>> TranslationCacheIndex = [];
    private static readonly Lock TranslationCacheLock = new();

    private static string CacheFilePath =>
        Path.Combine(Service.pluginInterface.ConfigDirectory.FullName, "translation_cache.json");

    public static void LoadCache()
    {
        try
        {
            var path = CacheFilePath;
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var entries = JsonSerializer.Deserialize<List<string[]>>(json);
            if (entries == null) return;

            // Only keep the most recent MAX_CACHE_SIZE entries
            if (entries.Count > MAX_CACHE_SIZE)
                entries = entries.GetRange(entries.Count - MAX_CACHE_SIZE, MAX_CACHE_SIZE);

            lock (TranslationCacheLock)
            {
                TranslationCache.Clear();
                TranslationCacheIndex.Clear();
                foreach (var pair in entries)
                {
                    if (pair.Length != 2 || TranslationCacheIndex.ContainsKey(pair[0])) continue;
                    var node = TranslationCache.AddLast(new KeyValuePair<string, string>(pair[0], pair[1]));
                    TranslationCacheIndex[pair[0]] = node;
                }
            }

            Service.pluginLog.Information($"[TranslationHandler] Loaded {TranslationCacheIndex.Count} cache entries.");
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
            {
                snapshot = new List<string[]>(TranslationCache.Count);
                foreach (var kv in TranslationCache)
                    snapshot.Add([kv.Key, kv.Value]);
            }

            var path = CacheFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(snapshot));
        }
        catch (Exception ex)
        {
            Service.pluginLog.Warning($"[TranslationHandler] Failed to save cache: {ex.Message}");
        }
    }

    public static async Task<Message> TranslateMessage(Message message, string targetLanguage = null!)
    {
        targetLanguage ??= Service.configuration.EffectiveTargetLanguage;

        var cacheEnabled = Service.configuration.EnableTranslationCache;

        if (cacheEnabled)
        {
            lock (TranslationCacheLock)
            {
                if (TranslationCacheIndex.TryGetValue(message.OriginalText, out var cachedNode))
                {
                    TranslationCache.Remove(cachedNode);
                    TranslationCache.AddLast(cachedNode);
                    message.TranslatedContent = cachedNode.Value.Value;
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

        if (cacheEnabled
            && message.Source != MessageSource.MainWindow
            && message.TranslationMode != Configuration.TranslationMode.MachineTranslate)
        {
            bool added;
            lock (TranslationCacheLock)
            {
                if (TranslationCacheIndex.TryGetValue(message.OriginalText, out var existingNode))
                {
                    TranslationCache.Remove(existingNode);
                    TranslationCache.AddLast(existingNode);
                    added = false;
                }
                else
                {
                    if (TranslationCacheIndex.Count >= MAX_CACHE_SIZE)
                    {
                        var oldest = TranslationCache.First!;
                        TranslationCache.RemoveFirst();
                        TranslationCacheIndex.Remove(oldest.Value.Key);
                    }

                    var node = TranslationCache.AddLast(new KeyValuePair<string, string>(message.OriginalText, translatedText));
                    TranslationCacheIndex[message.OriginalText] = node;
                    added = true;
                }
            }

            if (added)
                _ = Task.Run(SaveCache);
        }

        return message;
    }

    public static void ClearTranslationCache()
    {
        lock (TranslationCacheLock)
        {
            TranslationCache.Clear();
            TranslationCacheIndex.Clear();
        }

        _ = Task.Run(SaveCache);
    }
}
