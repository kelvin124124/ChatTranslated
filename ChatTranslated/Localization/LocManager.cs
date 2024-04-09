using ChatTranslated.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

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
        private static Dictionary<string, string>? Localizations;

        public static void LoadLocalizations()
        {
            if (Service.configuration.SelectedPluginLanguage == "Englsih")
            {
                Localizations = null;
                return;
            }

            string langCode = Service.configuration.SelectedPluginLanguage switch
            {
                "German" => "de",
                "Spanish" => "es",
                "French" => "fr",
                "Japanese" => "ja",
                "Korean" => "ko",
                "Chinese (Simplified)" => "zh-cn",
                "Chinese (Traditional)" => "zh-tw",
                _ => "unknown"
            };

            string LocalizationFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LocStr", $"{langCode}.json");

            if (!File.Exists(LocalizationFilePath))
            {
                Service.pluginLog.Warning($"Localization file not found: {LocalizationFilePath}");
                Localizations = new Dictionary<string, string>();
                return;
            }

            try
            {
                var jsonContent = File.ReadAllText(LocalizationFilePath);
                Localizations = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent) ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"Error loading localizations: {ex.Message}");
                Localizations = new Dictionary<string, string>();
            }
        }

        public static string GetLocalization(string originalString)
        {
            if (Localizations == null)
            {
                return originalString;
            }
            else if (Localizations.TryGetValue(originalString, out var localizedString))
            {
                return localizedString;
            }
            else
            {
                Service.pluginLog.Warning($"Localization not found for: {originalString}");
                return originalString;
            }
        }
    }
}
