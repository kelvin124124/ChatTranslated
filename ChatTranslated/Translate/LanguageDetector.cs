using ChatTranslated.Utils;
using Dalamud.Game.Text;
using Lingua;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LinguaLD = Lingua.LanguageDetector;

namespace ChatTranslated.Translate;

internal static class LanguageDetector
{
    private static volatile LinguaLD? _detector;
    private static readonly Lock _buildLock = new();

    private static readonly string[] NgramFiles =
        ["unigrams.json.br", "bigrams.json.br", "trigrams.json.br", "quadrigrams.json.br", "fivegrams.json.br"];

    private const string ModelBaseUrl =
        "https://raw.githubusercontent.com/searchpioneer/lingua-dotnet/1.0.5/src/Lingua/LanguageModels";

    // Core languages shipped with the plugin
    private static readonly HashSet<string> ShippedIsoCodes = ["en", "ja", "de", "fr", "zh", "ko", "es"];

    // display name, Lingua enum, ISO 639-1 code
    private static readonly (string Name, Language Lang, string Iso)[] LanguageTable =
    [
        ("English",               Language.English,    "en"),
        ("Japanese",              Language.Japanese,   "ja"),
        ("German",                Language.German,     "de"),
        ("French",                Language.French,     "fr"),
        ("Chinese (Simplified)",  Language.Chinese,    "zh"),
        ("Chinese (Traditional)", Language.Chinese,    "zh"),
        ("Korean",                Language.Korean,     "ko"),
        ("Spanish",               Language.Spanish,    "es"),
        ("Arabic",                Language.Arabic,     "ar"),
        ("Bulgarian",             Language.Bulgarian,  "bg"),
        ("Czech",                 Language.Czech,      "cs"),
        ("Danish",                Language.Danish,     "da"),
        ("Dutch",                 Language.Dutch,      "nl"),
        ("Estonian",              Language.Estonian,   "et"),
        ("Finnish",               Language.Finnish,    "fi"),
        ("Greek",                 Language.Greek,      "el"),
        ("Hungarian",             Language.Hungarian,  "hu"),
        ("Indonesian",            Language.Indonesian, "id"),
        ("Italian",               Language.Italian,    "it"),
        ("Latvian",               Language.Latvian,    "lv"),
        ("Lithuanian",            Language.Lithuanian, "lt"),
        ("Norwegian Bokmal",      Language.Bokmal,     "nb"),
        ("Polish",                Language.Polish,     "pl"),
        ("Portuguese",            Language.Portuguese, "pt"),
        ("Romanian",              Language.Romanian,   "ro"),
        ("Russian",               Language.Russian,    "ru"),
        ("Slovak",                Language.Slovak,     "sk"),
        ("Slovenian",             Language.Slovene,    "sl"),
        ("Swedish",               Language.Swedish,    "sv"),
        ("Turkish",               Language.Turkish,    "tr"),
        ("Ukrainian",             Language.Ukrainian,  "uk"),
    ];

    internal static readonly Dictionary<string, Language> NameToLingua =
        LanguageTable.ToDictionary(e => e.Name, e => e.Lang);

    internal static readonly Dictionary<string, string> NameToIsoCode =
        LanguageTable.ToDictionary(e => e.Name, e => e.Iso);

    private static readonly Dictionary<Language, string> LinguaToIso =
        LanguageTable.GroupBy(e => e.Lang).ToDictionary(g => g.Key, g => g.First().Iso);

    private static readonly Dictionary<XivChatType, (long Tick, string? Iso)> _lastChannelDetection = [];

    // Returns Lingua's top detected language as (confidence score, ISO 639-1 code).
    // Returns (0.0, null) for undetectable text (emoji, numbers, Language.Unknown).
    internal static (double Score, string? Iso) GetLinguaResult(string text)
    {
        var detector = _detector;
        if (detector == null) return (0.0, null);

        var top = detector.ComputeLanguageConfidenceValues(text).FirstOrDefault();
        if (top.Key == Language.Unknown) return (0.0, null);
        LinguaToIso.TryGetValue(top.Key, out var iso);
        return (top.Value, iso);
    }

    // Returns true if the ISO 639-1 code corresponds to one of the user's known languages.
    internal static bool IsKnownIsoCode(string? isoCode)
    {
        if (isoCode == null) return false;
        var known = Service.configuration.KnownLanguages;
        return LanguageTable.Any(e => e.Iso == isoCode && known.Contains(e.Name));
    }

    // Returns true if the text is detected as one of the user's known languages.
    // Returns true for undetectable text (emoji, numbers).
    public static bool IsKnownLanguageOrMeaningless(string text)
    {
        var detector = _detector;
        if (detector == null)
        {
            Service.pluginLog.Warning("Lingua detector not initialized.");
            return true;
        }
        var top = detector.DetectLanguageOf(text);
        Service.pluginLog.Debug($"Lingua raw detection for '{text}': {top}");

        if (top == Language.Unknown)
        {
            Service.pluginLog.Debug($"Lingua: low confidence or undetectable → skip: {text}");
            return true;
        }

        LinguaToIso.TryGetValue(top, out var iso);
        bool isKnown = IsKnownIsoCode(iso);
        Service.pluginLog.Debug($"{text}\n → Lingua: {top}, known: {isKnown}");
        return isKnown;
    }

    internal static async Task<(double Reliability, string? Iso)> ComputeReliabilityAsync(string text, XivChatType channel)
    {
        var (confidence, linguaIso) = await Task.Run(() => GetLinguaResult(text));
        double lengthFactor = Math.Clamp(text.Length / 15.0, 0.0, 1.0);  // length of incoming text, hiagher is better
        double channelBoost = GetChannelBoost(channel, linguaIso); // +decay if Google agrees with Lingua, -decay if disagrees
        double reliability = Math.Clamp((confidence * lengthFactor) + (channelBoost * (1.0 - lengthFactor) * 0.5), 0.0, 1.0);

        Service.pluginLog.Debug($"Confidence for '{text}': {reliability:F2} (lingua={confidence:F2} [{linguaIso ?? "?"}], length={lengthFactor:F2}, channelBoost={channelBoost:F2})");
        return (reliability, linguaIso);
    }

    private static double GetChannelBoost(XivChatType channel, string? linguaIso)
    {
        if (linguaIso == null) return 0.0;
        if (!_lastChannelDetection.TryGetValue(channel, out var cache) || cache.Iso == null) return 0.0;

        double elapsedMin = (Environment.TickCount64 - cache.Tick) / 60_000.0;
        if (elapsedMin >= 5) { _lastChannelDetection.Remove(channel); return 0.0; }

        double decay = Math.Exp(-elapsedMin * Math.Log(2) / 2.5);
        return cache.Iso == linguaIso ? decay : -decay;
    }

    internal static void UpdateChannelCache(XivChatType channel, string? iso)
        => _lastChannelDetection[channel] = (Environment.TickCount64, iso);

    internal static async Task<string?> DetectIsoAsync(string text)
    {
        try
        {
            var lang = await MachineTranslate.GTranslator.DetectLanguageAsync(text).ConfigureAwait(false);
            UpdateChannelCache(XivChatType.None, lang.ISO6391);
            return lang.ISO6391;
        }
        catch (Exception ex)
        {
            Service.pluginLog.Warning($"Google language detection failed: {ex.Message}");
            return null;
        }
    }

    public static async Task RebuildDetectorAsync()
    {
        try
        {
            var config = Service.configuration;
            var languageSet = new HashSet<Language>();

            // Add user's known languages
            foreach (var langName in config.KnownLanguages)
            {
                if (NameToLingua.TryGetValue(langName, out var linguaLang))
                    languageSet.Add(linguaLang);
            }

            // Always include core shipped languages
            foreach (var entry in LanguageTable)
                if (ShippedIsoCodes.Contains(entry.Iso))
                    languageSet.Add(entry.Lang);

            // Download any missing models
            await DownloadMissingModelsAsync(config.KnownLanguages).ConfigureAwait(false);

            lock (_buildLock)
            {
                var old = _detector;
                _detector = LanguageDetectorBuilder
                    .FromLanguages([.. languageSet])
                    .WithMinimumRelativeDistance(0.2)
                    .WithLanguageModelsDirectory(GetModelsDirectory())
                    .WithPreloadedLanguageModels()
                    .Build();
                old?.UnloadLanguageModels();
            }

            Service.pluginLog.Info($"Lingua detector rebuilt with {languageSet.Count} languages.");
        }
        catch (Exception ex)
        {
            Service.pluginLog.Error(ex, "Failed to build Lingua detector.");
        }
    }

    private static string GetModelsDirectory() =>
        Path.Combine(Service.pluginInterface.AssemblyLocation.DirectoryName!, "Lingua", "LanguageModels");

    private static async Task DownloadMissingModelsAsync(List<string> knownLanguages)
    {
        var modelsDir = GetModelsDirectory();
        foreach (var langName in knownLanguages)
        {
            if (!NameToIsoCode.TryGetValue(langName, out var isoCode) || ShippedIsoCodes.Contains(isoCode))
                continue;

            var langDir = Path.Combine(modelsDir, isoCode);
            if (Directory.Exists(langDir) && Directory.EnumerateFiles(langDir).Any())
                continue;

            await DownloadLanguageModelAsync(isoCode, langDir);
        }
    }

    private static async Task DownloadLanguageModelAsync(string isoCode, string langDir)
    {
        Service.pluginLog.Info($"Downloading language model for '{isoCode}'...");
        Directory.CreateDirectory(langDir);

        foreach (var ngramFile in NgramFiles)
        {
            var url = $"{ModelBaseUrl}/{isoCode}/{ngramFile}";
            var filePath = Path.Combine(langDir, ngramFile);

            try
            {
                using var response = await TranslationHandler.HttpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    continue;

                var bytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(filePath, bytes);
            }
            catch (HttpRequestException ex)
            {
                Service.pluginLog.Warning($"Failed to download {url}: {ex.Message}");
            }
        }

        Service.pluginLog.Info($"Language model for '{isoCode}' downloaded.");
    }

    public static void Dispose()
    {
        lock (_buildLock)
        {
            _detector?.UnloadLanguageModels();
            _detector = null;
        }
    }
}
