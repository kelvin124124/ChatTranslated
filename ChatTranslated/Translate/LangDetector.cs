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
        public static readonly LanguageDetector detector = new();

        // supported languages = ["English", "Japanese", "German", "French", "Korean", "Chinese", "Spanish"]
        // cannot distinguish between simplified and traditional Chinese

        public static string DetectLanguage(string inputstring)
        {
            detector.AddAllLanguages();
            string langcode = detector.Detect(inputstring) ?? "unknown";
            Service.pluginLog.Debug($"LangCode: {langcode}");
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
