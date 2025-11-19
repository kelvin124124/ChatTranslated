using ChatTranslated.Localization;
using ChatTranslated.Utils;
using Dalamud.Bindings.ImGui;
using System;
using System.Globalization;
using System.Linq;

namespace ChatTranslated.Windows.ConfigTabs;

public class GeneralTab
{
    private readonly string[] supportedDisplayLanguages =
    ["English", "Japanese", "German", "French", "Chinese (Simplified)", "Chinese (Traditional)", "Korean", "Spanish"];

    public void Draw(Configuration configuration)
    {
        DrawCoreSettings(configuration);
        ImGui.Separator();
        DrawPluginLangSelection(configuration);
    }

    private static void DrawCoreSettings(Configuration configuration)
    {
        bool _Enabled = configuration.Enabled;
        bool _EnabledInDuty = configuration.EnabledInDuty;

        if (ImGui.Checkbox(Resources.EnablePlugin, ref _Enabled))
        {
            configuration.Enabled = _Enabled;
            configuration.Save();
        }

        if (ImGui.Checkbox(Resources.EnableInDuties, ref _EnabledInDuty))
        {
            configuration.EnabledInDuty = _EnabledInDuty;
            configuration.Save();
        }

#if DEBUG
        ImGui.Separator();
        if (ImGui.Button("Magic button"))
        {
            var str = Service.chatHandler?.GetChatMessageContext();
            Service.pluginLog.Warning(str!);
        }
#endif
    }

    private void DrawPluginLangSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Resources.PluginUILanguage);
        ImGui.SameLine();

        string currentSelection = configuration.SelectedPluginLanguage;

        int currentIndex = Array.IndexOf(supportedDisplayLanguages, currentSelection);
        if (currentIndex == -1) currentIndex = 0;

        ImGui.SameLine();
        if (ImGui.Button("Help with localization!"))
        {
            Dalamud.Utility.Util.OpenLink("https://hosted.weblate.org/projects/chattranslated/");
        }

        string[] localizedSupportedDisplayLanguages = [.. supportedDisplayLanguages.Select(lang => Resources.ResourceManager.GetString(lang, Resources.Culture) ?? lang)];
        if (ImGui.Combo("##pluginLanguage", ref currentIndex, localizedSupportedDisplayLanguages, supportedDisplayLanguages.Length))
        {
            configuration.SelectedPluginLanguage = supportedDisplayLanguages[currentIndex];
            configuration.Save();
            SetLanguageCulture(configuration.SelectedPluginLanguage);
        }
    }

    internal static void SetLanguageCulture(string langName)
    {
        string langCode = langName switch
        {
            "English" => "en-US",
            "German" => "de-DE",
            "Spanish" => "es-ES",
            "French" => "fr-FR",
            "Japanese" => "ja-JP",
            "Chinese (Simplified)" => "zh-CN",
            "Chinese (Traditional)" => "zh-TW",
            "Korean" => "ko-KR",
            _ => "en-US"
        };
        Resources.Culture = new CultureInfo(langCode);
    }
}
