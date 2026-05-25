using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Bindings.ImGui;
using GTranslate;
using System;
using System.Linq;
using System.Numerics;

namespace ChatTranslated.Windows.ConfigTabs;

public class LanguagesTab
{
    internal static readonly string[] SupportedLanguages =
    ["English", "Japanese", "German", "French",
        "Chinese (Simplified)", "Chinese (Traditional)",
        "Korean", "Spanish", "Arabic", "Bulgarian",
        "Czech", "Danish", "Dutch", "Estonian",
        "Finnish", "Greek", "Hungarian", "Indonesian",
        "Italian", "Latvian", "Lithuanian", "Norwegian Bokmal",
        "Polish", "Portuguese", "Romanian", "Russian", "Slovak",
        "Slovenian", "Swedish", "Turkish", "Ukrainian"];

    public void Draw(Configuration configuration)
    {
        DrawDetectionSourceSelection(configuration);
        ImGui.Separator();
        DrawSelectionModeSelection(configuration);
        ImGui.Separator();
        DrawLanguageListSelection(configuration);
        ImGui.Separator();
        DrawTargetLangSelection(configuration);
    }

    private static void DrawDetectionSourceSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Resources.DetectionSource_Label);
        ImGui.SameLine();

        int selected = (int)configuration.SelectedDetectionSource;
        string[] names = [Resources.DetectionSource_Local, Resources.DetectionSource_Online];

        if (ImGui.Combo("##DetectionSourceCombo", ref selected, names, names.Length))
        {
            configuration.SelectedDetectionSource = (Configuration.DetectionSource)selected;
            configuration.Save();
        }

        ImGui.TextDisabled(FormattedText.Strip(configuration.SelectedDetectionSource == Configuration.DetectionSource.Online
            ? Resources.DetectionSource_Online_Help
            : Resources.DetectionSource_Local_Help));
    }

    private static void DrawSelectionModeSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Resources.SelectionMode_Label);
        ImGui.SameLine();

        int selected = (int)configuration.SelectedLanguageSelectionMode;
        string[] names = [Resources.SelectionMode_Inclusive, Resources.SelectionMode_Exclusive];

        if (ImGui.Combo("##SelectionModeCombo", ref selected, names, names.Length))
        {
            configuration.SelectedLanguageSelectionMode = (Configuration.LanguageSelectionMode)selected;
            configuration.Save();
        }

        ImGui.TextDisabled(FormattedText.Strip(configuration.SelectedLanguageSelectionMode == Configuration.LanguageSelectionMode.Inclusive
            ? Resources.SelectionMode_Inclusive_Help
            : Resources.SelectionMode_Exclusive_Help));
    }

    private void DrawLanguageListSelection(Configuration configuration)
    {
        bool isInclusive = configuration.SelectedLanguageSelectionMode == Configuration.LanguageSelectionMode.Inclusive;
        string header = isInclusive ? Resources.SourceLanguages_Header : Resources.My_Languages;
        string explanation = isInclusive ? Resources.SourceLanguages_Explanation : Resources.My_Languages_Explanation;
        var bound = isInclusive ? configuration.SelectedSourceLanguages : configuration.KnownLanguages;
        string idSuffix = isInclusive ? "##sourceLangSelection" : "##KnownLanguagesSelection";
        string checkboxSuffix = isInclusive ? "##source" : "##known";

        if (ImGui.CollapsingHeader(header + idSuffix, ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.BeginChild("languageList" + idSuffix, new Vector2(0, 7 * ImGui.GetFrameHeightWithSpacing()), true);
            foreach (string language in SupportedLanguages)
            {
                bool isSelected = bound.Contains(language);
                if (ImGui.Checkbox(language + checkboxSuffix, ref isSelected))
                {
                    if (isSelected && !bound.Contains(language))
                        bound.Add(language);
                    else if (!isSelected)
                        bound.RemoveAll(lang => lang == language);
                    configuration.Save();
                    if (!isInclusive)
                        _ = LanguageDetector.RebuildDetectorAsync();
                }
            }
            ImGui.EndChild();
        }

        ImGui.TextDisabled(explanation);
    }

    private void DrawTargetLangSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Resources.TargetLang);
        ImGui.SameLine();

        int currentIndex = Array.IndexOf(SupportedLanguages, configuration.SelectedTargetLanguage);
        if (currentIndex == -1) currentIndex = 0;

        string[] localizedLanguages = [.. SupportedLanguages.Select(lang => Resources.ResourceManager.GetString(lang, Resources.Culture) ?? lang)];

        if (configuration.UseCustomLanguage) ImGui.BeginDisabled();
        if (ImGui.Combo("##targetLanguage", ref currentIndex, localizedLanguages, 7))
        {
            configuration.SelectedTargetLanguage = SupportedLanguages[currentIndex];
            TranslationHandler.ClearTranslationCache();
            configuration.Save();
        }
        if (configuration.UseCustomLanguage) ImGui.EndDisabled();

        if (ImGui.CollapsingHeader(Resources.CustomTargetLanguageHeader, ImGuiTreeNodeFlags.None))
        {
            if (ImGui.Checkbox(Resources.UseCustomTargetLanguage, ref configuration.UseCustomLanguage))
            {
                TranslationHandler.ClearTranslationCache();
                configuration.Save();
            }

            if (configuration.UseCustomLanguage)
            {
                ImGui.Indent(20);

                ImGui.InputText("##targetLanguageInput", ref configuration.CustomTargetLanguage, 50);
                ImGui.SameLine();
                if (ImGui.Button(Resources.Apply + "###ApplyCustomLanguage"))
                {
                    if (Language.TryGetLanguage(configuration.CustomTargetLanguage, out var lang))
                    {
                        Plugin.OutputChatLine("Language applied successfully.");
                        TranslationHandler.ClearTranslationCache();
                        configuration.Save();
                    }
                    else
                    {
                        configuration.CustomTargetLanguage = "";
                        Plugin.OutputChatLine("Invalid language.");
                    }
                }
                ImGui.SameLine();
                ImGui.TextDisabled("?");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(Resources.CustomTargetLanguageExplanation);
                }

                if (ImGui.Button(Resources.SeeLanguageList))
                {
                    Dalamud.Utility.Util.OpenLink("https://github.com/d4n3436/GTranslate/blob/master/src/GTranslate/LanguageDictionary.cs#L164");
                }

                ImGui.Unindent(20);
            }
        }

        ImGui.TextDisabled("Regarding unsupported (=) characters");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Unsupported characters can be rendered if Dalamud font is switch to anything except the default game font " +
                "\nThis only fixes texts in plugin windows, unsupported characters in chat UI will still be rendered as  =");
        }
    }
}
