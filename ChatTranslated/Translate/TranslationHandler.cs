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

        var (translatedText, mode) = await RunTranslationChain(message, targetLanguage);

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

    private static async Task<(string, Configuration.TranslationMode?)> RunTranslationChain(Message message, string targetLanguage)
    {
        var configuration = Service.configuration;

        // Custom target languages are only reliably supported by machine translators.
        if (configuration.UseCustomLanguage)
            return await MachineTranslate.Translate(message.OriginalText, targetLanguage);

        var chain = new List<Configuration.TranslationProvider> { GetPrimaryProvider(configuration) };
        foreach (var provider in configuration.FallbackProviders)
        {
            if (!chain.Contains(provider))
                chain.Add(provider);
        }

        foreach (var provider in chain)
        {
            if (!IsProviderConfigured(provider))
            {
                Service.pluginLog.Debug($"[TranslationHandler] Skipping {provider}: no API key configured.");
                continue;
            }

            var (translated, mode) = await TranslateWith(provider, message, targetLanguage).ConfigureAwait(false);
            if (mode != null)
                return (translated, mode);

            Service.pluginLog.Warning($"[TranslationHandler] {provider} failed, trying next provider in the fallback chain.");
        }

        return (message.OriginalText, null);
    }

    private static Task<(string, Configuration.TranslationMode?)> TranslateWith(
        Configuration.TranslationProvider provider, Message message, string targetLanguage) => provider switch
    {
        Configuration.TranslationProvider.DeepL => DeeplsTranslate.Translate(message.OriginalText, targetLanguage),
        Configuration.TranslationProvider.DeepL_API => DeepLTranslate.Translate(message.OriginalText, targetLanguage),
        Configuration.TranslationProvider.LLMProxy => LLMProxyTranslate.Translate(message, targetLanguage),
        Configuration.TranslationProvider.OpenAI => OpenAITranslate.Translate(message, targetLanguage, model: Service.configuration.OpenAI_Model),
        Configuration.TranslationProvider.OpenAICompatible => OpenAICompatible.Translate(message, targetLanguage),
        Configuration.TranslationProvider.GoogleTranslate => MachineTranslate.TranslateWith(MachineTranslate.GTranslator, message.OriginalText, targetLanguage),
        Configuration.TranslationProvider.BingTranslate => MachineTranslate.TranslateWith(MachineTranslate.BingTranslator, message.OriginalText, targetLanguage),
        Configuration.TranslationProvider.YandexTranslate => MachineTranslate.TranslateWith(MachineTranslate.YTranslator, message.OriginalText, targetLanguage),
        _ => Task.FromResult((message.OriginalText, (Configuration.TranslationMode?)null))
    };

    internal static Configuration.TranslationProvider GetPrimaryProvider(Configuration configuration) =>
        configuration.SelectedTranslationEngine switch
        {
            Configuration.TranslationEngine.LLM => configuration.LLM_Provider switch
            {
                1 => Configuration.TranslationProvider.OpenAI,
                2 => Configuration.TranslationProvider.OpenAICompatible,
                _ => Configuration.TranslationProvider.LLMProxy
            },
            _ => Configuration.TranslationProvider.DeepL
        };

    internal static bool IsProviderConfigured(Configuration.TranslationProvider provider) => provider switch
    {
        Configuration.TranslationProvider.DeepL_API =>
            !string.IsNullOrWhiteSpace(Service.configuration.DeepL_API_Key) && Service.configuration.DeepL_API_Key != "YOUR-API-KEY:fx",
        Configuration.TranslationProvider.OpenAI =>
            !string.IsNullOrWhiteSpace(Service.configuration.OpenAI_API_Key) && Service.configuration.OpenAI_API_Key != "sk-YOUR-API-KEY",
        Configuration.TranslationProvider.OpenAICompatible =>
            !string.IsNullOrWhiteSpace(Service.configuration.LLM_API_Key) && Service.configuration.LLM_API_Key != "YOUR-API-KEY",
        _ => true
    };

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
