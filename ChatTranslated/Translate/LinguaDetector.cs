using ChatTranslated.Utils;
using Lingua;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ChatTranslated.Translate;

internal static class LinguaDetector
{
    private static volatile LanguageDetector? _detector;
    private static readonly Lock _buildLock = new();

    private static readonly Dictionary<string, Language> NameToLingua = new()
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
        [Language.Chinese] = "Chinese (Simplified)",
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

    public static string DetectLanguage(string text)
    {
        var detector = _detector;
        if (detector == null)
        {
            Service.pluginLog.Warning("Lingua detector not initialized.");
            return "unknown";
        }

        var detected = detector.DetectLanguageOf(text);
        if (detected == Language.Unknown)
        {
            Service.pluginLog.Debug($"Lingua: unable to confidently detect language for: {text}");
            return "unknown";
        }

        if (detected == Language.Chinese)
        {
            var selectedSources = Service.configuration.SelectedSourceLanguages;
            if (selectedSources.Contains("Chinese (Traditional)") && !selectedSources.Contains("Chinese (Simplified)"))
                return "Chinese (Traditional)";
            return "Chinese (Simplified)";
        }

        if (LinguaToName.TryGetValue(detected, out var name))
        {
            Service.pluginLog.Debug($"{text}\n -> language (Lingua): {name}");
            return name;
        }

        Service.pluginLog.Debug($"Lingua detected {detected} but no mapping found.");
        return detected.ToString();
    }

    public static Task RebuildDetectorAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var config = Service.configuration;
                var languageSet = new HashSet<Language>();

                foreach (var langName in config.SelectedSourceLanguages)
                {
                    if (NameToLingua.TryGetValue(langName, out var linguaLang))
                        languageSet.Add(linguaLang);
                }

                if (NameToLingua.TryGetValue(config.SelectedTargetLanguage, out var targetLang))
                    languageSet.Add(targetLang);

                // add languages so RelativeDistance filtering works
                if (languageSet.Count < 2)
                {
                    foreach (var lang in NameToLingua.Values.Take(8))
                        languageSet.Add(lang);
                }

                var languageModelsDir = Path.Combine(
                    Service.pluginInterface.AssemblyLocation.DirectoryName!, "Lingua", "LanguageModels");

                lock (_buildLock)
                {
                    var old = _detector;
                    _detector = LanguageDetectorBuilder
                        .FromLanguages([.. languageSet])
                        .WithMinimumRelativeDistance(0.1)
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
        });
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
