using ChatTranslated.Utils;
using LanguageDetection;
using System;

namespace ChatTranslated.Translate
{
    public static class StringExtensions
    {
        public static string GetLanguage(this string inputstring)
        {
            try
            {
                return LangDetector.DetectLanguage(inputstring);
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"Error detecting language: {ex.Message}");
                return "unknown";
            }
        }
    }

    public static class LangDetector
    {
        public static readonly LanguageDetectorSettings settings = new LanguageDetectorSettings()
        {
            RandomSeed = 1,
            ConvergenceThreshold = 0.9,
            MaxIterations = 50,
        };
        public static readonly LanguageDetector detector = new(settings);

        // supported languages = ["English", "Japanese", "German", "French", "Korean", "Chinese", "Spanish"]
        // cannot distinguish between simplified and traditional Chinese

        public static string DetectLanguage(string inputstring)
        {
            detector.AddAllLanguages();
            string langcode = detector.Detect(inputstring) ?? "unknown";
            string language = langcode switch
            {
                "eng" => "English",
                "jpn" => "Japanese",
                "ger" => "German",
                "fre" => "French",
                "kor" => "Korean",
                "chi" => "Chinese",
                "spa" => "Spanish",
                _ => "unknown"
            };
            return language;
        }
    }
}
