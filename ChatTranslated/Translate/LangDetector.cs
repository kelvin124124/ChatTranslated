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
        public static readonly LanguageDetector detector = GetLanguageDetector();

        public static LanguageDetector GetLanguageDetector()
        {
            LanguageDetector detector = new();
            detector.AddAllLanguages();
            detector.ConvergenceThreshold = 0.9;
            detector.MaxIterations = 50;
            return detector;
        }

        // supported languages = ["English", "Japanese", "German", "French", "Korean", "Chinese", "Spanish"]
        // cannot distinguish between simplified and traditional Chinese

        public static string DetectLanguage(string inputstring)
        {
            detector.AddAllLanguages();
            string langcode = detector.Detect(inputstring);
            string language = langcode switch
            {
                "eng" => "English",
                "jpn" => "Japanese",
                "deu" => "German",
                "fra" => "French",
                "kor" => "Korean",
                "zho" => "Chinese",
                "spa" => "Spanish",
                _ => "unknown"
            };
            return language;
        }
    }
}
