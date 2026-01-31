using ChatTranslated.Localization;
using ChatTranslated.Translate;
using Dalamud.Bindings.ImGui;
using System;
using System.Linq;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Windows.ConfigTabs;

public class LanguagesTab
{
    private readonly string[] supportedLanguages =
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
        DrawSourceLangSelection(configuration);
        ImGui.Separator();
        DrawTargetLangSelection(configuration);
    }

    private void DrawSourceLangSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Resources.SourceLang);
        ImGui.SameLine();

        int selectedMode = (int)configuration.SelectedLanguageSelectionMode;
        string[] modeNames = Enum.GetNames<LanguageSelectionMode>();
        string[] localizedModes = [.. modeNames.Select(mode => Resources.ResourceManager.GetString(mode, Resources.Culture) ?? mode)];

        if (ImGui.Combo("##LanguageSelectionModeCombo", ref selectedMode, localizedModes, modeNames.Length))
        {
            configuration.SelectedLanguageSelectionMode = (LanguageSelectionMode)selectedMode;
            configuration.Save();
        }

        switch (configuration.SelectedLanguageSelectionMode)
        {
            case LanguageSelectionMode.Default:
                ImGui.TextUnformatted(Resources.DefaultFilteringExplaination);
                break;
            case LanguageSelectionMode.CustomLanguages:
                if (ImGui.CollapsingHeader(Resources.SourceLangSelection, ImGuiTreeNodeFlags.None))
                {
                    foreach (string language in supportedLanguages)
                    {
                        bool isSelected = configuration.SelectedSourceLanguages.Contains(language);
                        if (ImGui.Checkbox(Resources.ResourceManager.GetString(language, Resources.Culture) ?? language, ref isSelected))
                        {
                            if (isSelected)
                            {
                                if (!configuration.SelectedSourceLanguages.Contains(language))
                                    configuration.SelectedSourceLanguages.Add(language);
                            }
                            else
                            {
                                configuration.SelectedSourceLanguages.RemoveAll(lang => lang == language);
                            }
                            configuration.Save();
                        }
                    }
                }
                break;
            case LanguageSelectionMode.AllLanguages:
                ImGui.TextUnformatted(Resources.TranslateAllExplaination);
                break;
        }
    }

    private void DrawTargetLangSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Resources.TargetLang);
        ImGui.SameLine();

        int currentIndex = Array.IndexOf(supportedLanguages, configuration.SelectedTargetLanguage);
        if (currentIndex == -1) currentIndex = 0;

        string[] localizedLanguages = [.. supportedLanguages.Select(lang => Resources.ResourceManager.GetString(lang, Resources.Culture) ?? lang)];

        if (configuration.UseCustomLanguage) ImGui.BeginDisabled();
        if (ImGui.Combo("##targetLanguage", ref currentIndex, localizedLanguages, supportedLanguages.Length))
        {
            configuration.SelectedTargetLanguage = supportedLanguages[currentIndex];
            TranslationHandler.ClearTranslationCache();
            configuration.Save();
        }
        if (configuration.UseCustomLanguage) ImGui.EndDisabled();

        if (ImGui.CollapsingHeader(Resources.CustomTargetLanguageHeader, ImGuiTreeNodeFlags.None))
        {
            ImGui.TextUnformatted(Resources.TargetLang);
            ImGui.SameLine();
            ImGui.InputText("##targetLanguageInput", ref configuration.CustomTargetLanguage, 50);
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
            ImGui.SameLine();
            if (ImGui.Button(Resources.Apply + "###ApplyCustomLanguage"))
            {
                if (GTranslate.Language.TryGetLanguage(configuration.CustomTargetLanguage, out var lang))
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
            if (ImGui.Checkbox(Resources.UseCustomTargetLanguage, ref configuration.UseCustomLanguage))
            {
                TranslationHandler.ClearTranslationCache();
                configuration.Save();
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
