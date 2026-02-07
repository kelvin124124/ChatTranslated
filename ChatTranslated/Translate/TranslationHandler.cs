using ChatTranslated.Chat;
using ChatTranslated.Utils;
using Dalamud.Networking.Http;
using Dalamud.Utility;
using GTranslate.Translators;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
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

    public static readonly GoogleTranslator GTranslator = new(HttpClient);
    public static readonly BingTranslator BingTranslator = new(HttpClient);
    public static readonly YandexTranslator YTranslator = new(HttpClient);

    private const int MAX_CACHE_SIZE = 120;
    public static readonly ConcurrentDictionary<string, string> TranslationCache = [];

    public static async Task<Message> TranslateMessage(Message message, string targetLanguage = null!)
    {
        targetLanguage ??= Service.configuration.SelectedTargetLanguage;

        if (string.IsNullOrWhiteSpace(message.CleanedContent)) return message;

        if (TranslationCache.TryGetValue(message.OriginalText, out var cachedTranslation))
        {
            message.TranslatedContent = cachedTranslation;
            return message;
        }

        var (translatedText, mode) = Service.configuration.SelectedTranslationEngine switch
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

        if (!translatedText.IsNullOrWhitespace()
            && message.Source != MessageSource.MainWindow
            && message.TranslationMode != Configuration.TranslationMode.MachineTranslate)
        {
            if (TranslationCache.Count >= MAX_CACHE_SIZE)
            {
                TranslationCache.Clear();
            }

            TranslationCache.TryAdd(message.OriginalText, translatedText);
        }

        return message;
    }

    public static async Task<string> DetermineLanguage(string messageText)
    {
        Func<Task<string>>[] translators =
        [
            async () => (await YTranslator.DetectLanguageAsync(messageText)).Name,
            async () => (await GTranslator.DetectLanguageAsync(messageText)).Name,
            async () => (await BingTranslator.DetectLanguageAsync(messageText)).Name
        ];

        foreach (var translator in translators)
        {
            try
            {
                var language = await translator();
                if (language.IsNullOrEmpty())
                {
                    throw new Exception($"Language detection failed.");
                }
                Service.pluginLog.Debug($"{messageText}\n -> language: {language}");
                return language;
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"Failed to detect language: {ex.Message}");
            }
        }

        Service.pluginLog.Error("All language detection attempts failed.");
        return "unknown";
    }

    public static void ClearTranslationCache() => TranslationCache.Clear();
}
