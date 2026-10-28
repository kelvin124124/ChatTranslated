using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Globalization;
using System.Linq;

namespace ChatTranslated.Windows.ConfigTabs;

public class GeneralTab
{
    // plugin UI languages and their resource cultures
    private static readonly (string Name, string Culture)[] DisplayLanguages =
    [
        ("English", "en-US"),
        ("Japanese", "ja-JP"),
        ("German", "de-DE"),
        ("French", "fr-FR"),
        ("Chinese (Simplified)", "zh-CN"),
        ("Chinese (Traditional)", "zh-TW"),
        ("Korean", "ko-KR"),
        ("Spanish", "es-ES"),
    ];

#if DEBUG
    private static string testInput = "";
#endif

    public void Draw(Configuration configuration)
    {
        DrawPluginToggles(configuration);
        ImGui.Separator();
        DrawChatIntegrationSettings(configuration);
        ImGui.Separator();
        DrawPluginLangSelection(configuration);

#if DEBUG
        ImGui.Separator();

        // input field for test text
        ImGui.InputText("##TestInput", ref testInput);

        // send for language detection
        if (ImGui.Button("Magic button"))
            Service.pluginLog.Info($"IsKnownLanguage returned: {LanguageDetector.IsKnownLanguageOrMeaningless(testInput)} for input: {testInput}");
#endif
    }

    private static void DrawPluginToggles(Configuration configuration)
    {
        if (ImGui.Checkbox(Resources.EnablePlugin, ref configuration.Enabled))
            configuration.Save();

        if (ImGui.Checkbox(Resources.EnableInDuties, ref configuration.EnabledInDuty))
            configuration.Save();
    }

    private static void DrawChatIntegrationSettings(Configuration configuration)
    {
        if (ImGui.Checkbox(Resources.ChatIntegration, ref configuration.ChatIntegration))
            configuration.Save();

        if (!configuration.ChatIntegration)
            return;

        using var indent = ImRaii.PushIndent(20f, scaled: false);

        if (ImGui.Checkbox(Resources.ChatIntegration_HideOriginal, ref configuration.ChatIntegration_HideOriginal))
            configuration.Save();

        if (ImGui.Checkbox(Resources.ChatIntegration_ShowColoredText, ref configuration.ChatIntegration_ShowColoredText))
            configuration.Save();

        if (ImGui.Checkbox(Resources.ChatIntegration_UseEchoChannel, ref configuration.ChatIntegration_UseEchoChannel))
            configuration.Save();
    }

    private static void DrawPluginLangSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Resources.PluginUILanguage);

        int currentIndex = Math.Max(Array.FindIndex(DisplayLanguages, l => l.Name == configuration.SelectedPluginLanguage), 0);
        string[] localizedNames = [.. DisplayLanguages.Select(l => Loc.Get(l.Name))];

        if (ImGui.Combo("##pluginLanguage", ref currentIndex, localizedNames, localizedNames.Length))
        {
            configuration.SelectedPluginLanguage = DisplayLanguages[currentIndex].Name;
            configuration.Save();
            SetLanguageCulture(configuration.SelectedPluginLanguage);
        }

        if (ImGui.Button("Help with localization!"))
            Dalamud.Utility.Util.OpenLink("https://hosted.weblate.org/projects/chattranslated/");
    }

    internal static void SetLanguageCulture(string langName)
    {
        var culture = Array.Find(DisplayLanguages, l => l.Name == langName).Culture ?? "en-US";
        Resources.Culture = new CultureInfo(culture);
    }
}
