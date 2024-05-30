using ChatTranslated.Utils;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace ChatTranslated.Localization
{
    public static class StringExtensions
    {
        public static string GetLocalization(this string originalString, string? language = null)
        {
            try
            {
                return LocManager.GetLocalization(originalString, language);
            }
            catch
            {
                return originalString;
            }
        }
    }

    internal class LocManager
    {
        private static readonly ResourceManager ResourceManager = new("ChatTranslated.Localization.LocStr.Resources", Assembly.GetExecutingAssembly());
        private static CultureInfo CultureInfo = new("en-US");

        public static void LoadLocalization()
        {
            string selectedLanguage = Service.configuration.SelectedPluginLanguage;

            if (selectedLanguage == "English")
            {
                CultureInfo = new CultureInfo("en-US");
                return;
            }

            string locale = selectedLanguage switch
            {
                "German" => "de-DE",
                "Spanish" => "es-ES",
                "French" => "fr-FR",
                "Japanese" => "ja-JP",
                "Chinese (Simplified)" => "zh-CN",
                "Chinese (Traditional)" => "zh-TW",
                _ => "unknown"
            };

            if (locale != "unknown")
            {
                CultureInfo = new CultureInfo(locale);
            }
        }

        private static CultureInfo GetCultureInfo(string language)
        {
            return language switch
            {
                "English" => new CultureInfo("en-US"),
                "German" => new CultureInfo("de-DE"),
                "Spanish" => new CultureInfo("es-ES"),
                "French" => new CultureInfo("fr-FR"),
                "Japanese" => new CultureInfo("ja-JP"),
                "Chinese (Simplified)" => new CultureInfo("zh-CN"),
                "Chinese (Traditional)" => new CultureInfo("zh-TW"),
                _ => CultureInfo // Default to the loaded localization
            };
        }

        public static string GetLocalization(string originalString, string? language = null)
        {
            CultureInfo cultureInfo = language == null ? CultureInfo : GetCultureInfo(language);
            if (language == "English" || (language == null && CultureInfo.Name == "en-US"))
                return originalString;
            else
                return ResourceManager.GetString(originalString, cultureInfo) ?? originalString;
        }
    }
}
