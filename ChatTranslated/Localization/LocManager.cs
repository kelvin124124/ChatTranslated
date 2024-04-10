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
        private static ResourceManager ResourceManager = new ResourceManager("ChatTranslated.Resources", Assembly.GetExecutingAssembly());
        private static CultureInfo CultureInfo = CultureInfo.CurrentCulture;

        public static void LoadLocalization()
        {
            string selectedLanguage = Service.configuration.SelectedPluginLanguage;

            if (selectedLanguage == "English")
                return;

            string langCode = selectedLanguage switch
            {
                "German" => "de",
                "Spanish" => "es",
                "French" => "fr",
                "Japanese" => "ja",
                "Korean" => "ko",
                "Chinese (Simplified)" => "zh-Hans",
                "Chinese (Traditional)" => "zh-Hant",
                _ => "unknown"
            };

            if (langCode != "unknown")
                CultureInfo = new CultureInfo(langCode);
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
