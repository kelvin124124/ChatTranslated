using ChatTranslated.Localization;
using ChatTranslated.Translate;
using Dalamud.Bindings.ImGui;
using GTranslate;
using System;
using System.Linq;

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
        DrawKnownLanguagesSelection(configuration);
        ImGui.Separator();
        DrawTargetLangSelection(configuration);
    }

    private void DrawKnownLanguagesSelection(Configuration configuration)
    {
        if (ImGui.CollapsingHeader(Resources.My_Languages + "##KnownLanguagesSelection", ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (string language in supportedLanguages)
            {
                bool isSelected = configuration.KnownLanguages.Contains(language);
                if (ImGui.Checkbox(language + "##known", ref isSelected))
                {
                    if (isSelected && !configuration.KnownLanguages.Contains(language))
                        configuration.KnownLanguages.Add(language);
                    else if (!isSelected)
                        configuration.KnownLanguages.RemoveAll(lang => lang == language);
                    configuration.Save();
                    _ = LanguageDetector.RebuildDetectorAsync();
                }
            }
        }

        ImGui.TextDisabled(Resources.My_Languages_Explanation);
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
