using ChatTranslated.Utils;
using Lingua;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ChatTranslated.Translate;

internal static class LinguaDetector
{
    private static volatile LanguageDetector? _detector;
    private static readonly Lock _buildLock = new();

    private static readonly string[] NgramFiles =
        ["unigrams.json.br", "bigrams.json.br", "trigrams.json.br", "quadrigrams.json.br", "fivegrams.json.br"];

    private const string ModelBaseUrl =
        "https://raw.githubusercontent.com/searchpioneer/lingua-dotnet/1.0.5/src/Lingua/LanguageModels";

    // Core languages shipped with the plugin
    private static readonly HashSet<string> ShippedIsoCodes = ["en", "ja", "de", "fr", "zh", "ko", "es"];

    internal static readonly Dictionary<string, Language> NameToLingua = new()
    {
        ["English"] = Language.English,
        ["Japanese"] = Language.Japanese,
        ["German"] = Language.German,
        ["French"] = Language.French,
        ["Chinese (Simplified)"] = Language.Chinese,
        ["Chinese (Traditional)"] = Language.Chinese,
        ["Korean"] = Language.Korean,
        ["Spanish"] = Language.Spanish,
        ["Arabic"] = Language.Arabic,
        ["Bulgarian"] = Language.Bulgarian,
        ["Czech"] = Language.Czech,
        ["Danish"] = Language.Danish,
        ["Dutch"] = Language.Dutch,
        ["Estonian"] = Language.Estonian,
        ["Finnish"] = Language.Finnish,
        ["Greek"] = Language.Greek,
        ["Hungarian"] = Language.Hungarian,
        ["Indonesian"] = Language.Indonesian,
        ["Italian"] = Language.Italian,
        ["Latvian"] = Language.Latvian,
        ["Lithuanian"] = Language.Lithuanian,
        ["Norwegian Bokmal"] = Language.Bokmal,
        ["Polish"] = Language.Polish,
        ["Portuguese"] = Language.Portuguese,
        ["Romanian"] = Language.Romanian,
        ["Russian"] = Language.Russian,
        ["Slovak"] = Language.Slovak,
        ["Slovenian"] = Language.Slovene,
        ["Swedish"] = Language.Swedish,
        ["Turkish"] = Language.Turkish,
        ["Ukrainian"] = Language.Ukrainian,
    };

    private static readonly Dictionary<Language, string> LinguaToName = new()
    {
        [Language.English] = "English",
        [Language.Japanese] = "Japanese",
        [Language.German] = "German",
        [Language.French] = "French",
        [Language.Chinese] = "Chinese",
        [Language.Korean] = "Korean",
        [Language.Spanish] = "Spanish",
        [Language.Arabic] = "Arabic",
        [Language.Bulgarian] = "Bulgarian",
        [Language.Czech] = "Czech",
        [Language.Danish] = "Danish",
        [Language.Dutch] = "Dutch",
        [Language.Estonian] = "Estonian",
        [Language.Finnish] = "Finnish",
        [Language.Greek] = "Greek",
        [Language.Hungarian] = "Hungarian",
        [Language.Indonesian] = "Indonesian",
        [Language.Italian] = "Italian",
        [Language.Latvian] = "Latvian",
        [Language.Lithuanian] = "Lithuanian",
        [Language.Bokmal] = "Norwegian Bokmal",
        [Language.Polish] = "Polish",
        [Language.Portuguese] = "Portuguese",
        [Language.Romanian] = "Romanian",
        [Language.Russian] = "Russian",
        [Language.Slovak] = "Slovak",
        [Language.Slovene] = "Slovenian",
        [Language.Swedish] = "Swedish",
        [Language.Turkish] = "Turkish",
        [Language.Ukrainian] = "Ukrainian",
    };

    private static readonly Dictionary<string, string> NameToIsoCode = new()
    {
        ["English"] = "en",
        ["Japanese"] = "ja",
        ["German"] = "de",
        ["French"] = "fr",
        ["Chinese (Simplified)"] = "zh",
        ["Chinese (Traditional)"] = "zh",
        ["Korean"] = "ko",
        ["Spanish"] = "es",
        ["Arabic"] = "ar",
        ["Bulgarian"] = "bg",
        ["Czech"] = "cs",
        ["Danish"] = "da",
        ["Dutch"] = "nl",
        ["Estonian"] = "et",
        ["Finnish"] = "fi",
        ["Greek"] = "el",
        ["Hungarian"] = "hu",
        ["Indonesian"] = "id",
        ["Italian"] = "it",
        ["Latvian"] = "lv",
        ["Lithuanian"] = "lt",
        ["Norwegian Bokmal"] = "nb",
        ["Polish"] = "pl",
        ["Portuguese"] = "pt",
        ["Romanian"] = "ro",
        ["Russian"] = "ru",
        ["Slovak"] = "sk",
        ["Slovenian"] = "sl",
        ["Swedish"] = "sv",
        ["Turkish"] = "tr",
        ["Ukrainian"] = "uk",
    };

    // Returns true if the text is detected as one of the user's known languages.
    // Returns true for undetectable text (emoji, numbers).
    public static bool IsKnownLanguage(string text)
    {
        var detector = _detector;
        if (detector == null)
        {
            Service.pluginLog.Warning("Lingua detector not initialized.");
            return true;
        }

        var confidenceValues = detector.ComputeLanguageConfidenceValues(text);
        var top = confidenceValues.FirstOrDefault();
        var detected = top.Key;
        var confidence = top.Value;

        Service.pluginLog.Debug($"Lingua raw detection for '{text}': {detected} ({confidence:P0})");

        if (confidence < 0.4)
        {
            Service.pluginLog.Debug($"Lingua: low confidence ({confidence:P0}) → skip: {text}");
            return true;
        }

        if (detected == Language.Unknown)
        {
            Service.pluginLog.Debug($"Lingua: undetectable → skip: {text}");
            return true;
        }

        if (!LinguaToName.TryGetValue(detected, out var detectedName))
        {
            Service.pluginLog.Debug($"Lingua detected {detected} but no mapping found.");
            return false;
        }

        var knownLanguages = Service.configuration.KnownLanguages;
        bool isKnown = detected == Language.Chinese
            ? knownLanguages.Contains("Chinese (Simplified)") || knownLanguages.Contains("Chinese (Traditional)")
            : knownLanguages.Contains(detectedName);

        Service.pluginLog.Debug($"{text}\n → Lingua: {detectedName}, known: {isKnown}");
        return isKnown;
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

            // Always include core shipped languages for meaningful relative distance
            foreach (var lang in ShippedIsoCodes)
            { 
                var coreLang = NameToIsoCode.First(kv => kv.Value == lang).Key;
                if (NameToLingua.TryGetValue(coreLang, out var linguaLang))
                    languageSet.Add(linguaLang);
            }

            // Check any missing models and download them
            await EnsureModelsAvailableAsync(config.KnownLanguages).ConfigureAwait(false);

            var languageModelsDir = GetModelsDirectory();

            lock (_buildLock)
            {
                var old = _detector;
                _detector = LanguageDetectorBuilder
                    .FromLanguages([.. languageSet])
                    .WithMinimumRelativeDistance(0.2)
                    .WithLanguageModelsDirectory(languageModelsDir)
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

    private static string GetModelsDirectory()
    {
        return Path.Combine(
            Service.pluginInterface.AssemblyLocation.DirectoryName!, "Lingua", "LanguageModels");
    }

    private static async Task EnsureModelsAvailableAsync(List<string> knownLanguages)
    {
        var modelsDir = GetModelsDirectory();
        foreach (var langName in knownLanguages)
        {
            if (!NameToIsoCode.TryGetValue(langName, out var isoCode))
                continue;

            if (ShippedIsoCodes.Contains(isoCode))
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
