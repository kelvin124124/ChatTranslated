using ChatTranslated.Utils;
using System;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace ChatTranslated.Localization
{
    public static class StringExtensions
    {
        public static string GetLocalization(this string originalString)
        {
            try
            {
                return LocManager.GetLocalization(originalString);
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"Error retrieving localization: {ex.Message}");
                return originalString;
            }
        }
    }

    internal class LocManager
    {
        private static readonly ResourceManager ResourceManager = new("ChatTranslated.Resources", Assembly.GetExecutingAssembly());
        private static CultureInfo CultureInfo = new("en-US");

        public static void LoadLocalization()
        {
            string selectedLanguage = Service.configuration.SelectedPluginLanguage;

            if (selectedLanguage == "English")
                return;

            string locale = selectedLanguage switch
            {
                "German" => "de-DE",
                "Spanish" => "es-ES",
                "French" => "fr-FR",
                "Japanese" => "ja-JP",
                "Korean" => "ko-KR",
                "Chinese (Simplified)" => "zh-CN",
                "Chinese (Traditional)" => "zh-TW",
                _ => "unknown"
            };

            if (locale != "unknown")
            {
                CultureInfo = new CultureInfo(locale);
            }
        }

        public static string GetLocalization(string originalString)
        {
            if (Service.configuration.SelectedPluginLanguage == "English")
                return originalString;
            else
                return ResourceManager.GetString(originalString, CultureInfo) ?? originalString;
        }
    }
}
