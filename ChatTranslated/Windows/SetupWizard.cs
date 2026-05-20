using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using ChatTranslated.Windows.ConfigTabs;
using ChatTranslated.Windows.ConfigTabs.TranslationEngineTabs;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Linq;
using System.Numerics;

namespace ChatTranslated.Windows;

public class SetupWizard : Window
{
    private const int TotalSteps = 6;

    private int step;
    private bool knownLanguagesChanged;

    public SetupWizard(Plugin plugin) : base(
        Resources.Wizard_Title + "##ChatTranslatedSetupWizard",
        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking)
    {
        Size = new Vector2(560, 460);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void OnOpen()
    {
        step = 1;
        knownLanguagesChanged = false;
    }

    public override void Draw()
    {
        var configuration = Service.configuration;

        ImGui.TextDisabled(string.Format(Resources.Wizard_Step_Format, step, TotalSteps));
        ImGui.Separator();

        if (ImGui.BeginChild("WizardBody", new Vector2(-1, -36), true))
        {
            switch (step)
            {
                case 1: DrawWelcome(); break;
                case 2: DrawTargetLanguage(configuration); break;
                case 3: DrawDetectionConfig(configuration); break;
                case 4: DrawLanguagesList(configuration); break;
                case 5: DrawEngine(configuration); break;
                case 6: DrawFinish(); break;
            }
        }
        ImGui.EndChild();

        ImGui.Separator();
        DrawFooter(configuration);
    }

    private static void DrawWelcome()
    {
        ImGui.TextWrapped(Resources.Wizard_Welcome_Header);
        ImGui.Spacing();
        ImGui.TextWrapped(Resources.Wizard_Welcome_Body);
    }

    private static void DrawTargetLanguage(Configuration configuration)
    {
        ImGui.TextUnformatted(Resources.Wizard_TargetLanguage_Header);
        ImGui.Spacing();
        ImGui.TextWrapped(Resources.Wizard_TargetLanguage_Body);
        ImGui.Spacing();

        int currentIndex = Array.IndexOf(LanguagesTab.SupportedLanguages, configuration.SelectedTargetLanguage);
        if (currentIndex == -1) currentIndex = 0;
        string[] localized = [.. LanguagesTab.SupportedLanguages.Select(l => Resources.ResourceManager.GetString(l, Resources.Culture) ?? l)];

        ImGui.SetNextItemWidth(280);
        if (ImGui.Combo("##wizardTargetLang", ref currentIndex, localized, LanguagesTab.SupportedLanguages.Length))
        {
            configuration.SelectedTargetLanguage = LanguagesTab.SupportedLanguages[currentIndex];
            TranslationHandler.ClearTranslationCache();
            configuration.Save();
        }
    }

    private static void DrawDetectionConfig(Configuration configuration)
    {
        ImGui.TextUnformatted(Resources.Wizard_Behavior_Header);
        ImGui.Spacing();
        ImGui.TextWrapped(Resources.Wizard_Behavior_Body);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        int source = (int)configuration.SelectedDetectionSource;
        if (DrawLabeledCombo(Resources.DetectionSource_Label, "##wizardDetectionSource",
                [Resources.DetectionSource_Local, Resources.DetectionSource_Online], ref source))
        {
            configuration.SelectedDetectionSource = (Configuration.DetectionSource)source;
            configuration.Save();
        }
        ImGui.Indent(20);
        FormattedText.Draw(configuration.SelectedDetectionSource == Configuration.DetectionSource.Online
            ? Resources.DetectionSource_Online_Help
            : Resources.DetectionSource_Local_Help);
        ImGui.Unindent(20);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        int mode = (int)configuration.SelectedLanguageSelectionMode;
        if (DrawLabeledCombo(Resources.SelectionMode_Label, "##wizardSelectionMode",
                [Resources.SelectionMode_Inclusive, Resources.SelectionMode_Exclusive], ref mode))
        {
            configuration.SelectedLanguageSelectionMode = (Configuration.LanguageSelectionMode)mode;
            configuration.Save();
        }
        ImGui.Indent(20);
        FormattedText.Draw(configuration.SelectedLanguageSelectionMode == Configuration.LanguageSelectionMode.Inclusive
            ? Resources.SelectionMode_Inclusive_Help
            : Resources.SelectionMode_Exclusive_Help);
        ImGui.Unindent(20);
    }

    private static bool DrawLabeledCombo(string label, string id, string[] options, ref int selected)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(280);
        return ImGui.Combo(id, ref selected, options, options.Length);
    }

    private void DrawLanguagesList(Configuration configuration)
    {
        bool isInclusive = configuration.SelectedLanguageSelectionMode == Configuration.LanguageSelectionMode.Inclusive;
        string header = isInclusive ? Resources.Wizard_Languages_Inclusive_Header : Resources.Wizard_Languages_Exclusive_Header;
        string body = isInclusive ? Resources.Wizard_Languages_Inclusive_Body : Resources.Wizard_Languages_Exclusive_Body;
        var bound = isInclusive ? configuration.SelectedSourceLanguages : configuration.KnownLanguages;
        string idSuffix = isInclusive ? "##wizardSrcLang" : "##wizardKnownLang";

        ImGui.TextUnformatted(header);
        ImGui.Spacing();
        ImGui.TextWrapped(body);
        ImGui.Spacing();

        if (ImGui.BeginChild("WizardLanguageList", new Vector2(-1, 0), true))
        {
            ImGui.Columns(2, "WizardLangCols", false);
            foreach (var language in LanguagesTab.SupportedLanguages)
            {
                bool isSelected = bound.Contains(language);
                string label = Resources.ResourceManager.GetString(language, Resources.Culture) ?? language;
                if (ImGui.Checkbox(label + idSuffix + language, ref isSelected))
                {
                    if (isSelected && !bound.Contains(language))
                        bound.Add(language);
                    else if (!isSelected)
                        bound.RemoveAll(l => l == language);

                    configuration.Save();
                    if (!isInclusive) knownLanguagesChanged = true;
                }
                ImGui.NextColumn();
            }
            ImGui.Columns(1);
        }
        ImGui.EndChild();
    }

    private static void DrawEngine(Configuration configuration)
    {
        ImGui.TextUnformatted(Resources.Wizard_Engine_Header);

        TranslationModeTab.DrawEngineSelection(configuration);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        switch (configuration.SelectedTranslationEngine)
        {
            case Configuration.TranslationEngine.DeepL:
                ImGui.TextWrapped(Resources.DeepLExplanation);
                ImGui.Separator();
                ImGui.Spacing();
                DeepLSettings.Draw(configuration);
                break;
            case Configuration.TranslationEngine.LLM:
                ImGui.TextWrapped(Resources.LLM_Explanation);
                ImGui.Separator();
                ImGui.Spacing();
                TranslationModeTab.DrawLLMConfiguration(configuration);
                break;
        }
    }

    private static void DrawFinish()
    {
        ImGui.TextUnformatted(Resources.Wizard_Finish_Header);
        ImGui.Spacing();
        ImGui.TextWrapped(Resources.Wizard_Finish_Body);
    }

    private void DrawFooter(Configuration configuration)
    {
        if (step > 1)
        {
            if (ImGui.Button(Resources.Wizard_Back + "##wizardBack", new Vector2(80, 0)))
                step--;
            ImGui.SameLine();
        }

        if (step < TotalSteps)
        {
            if (ImGui.Button(Resources.Wizard_Skip + "##wizardSkip", new Vector2(80, 0)))
            {
                Complete(configuration);
                return;
            }
        }

        string rightLabel = step < TotalSteps ? Resources.Wizard_Next : Resources.Wizard_Finish;
        float rightButtonWidth = 80;
        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - rightButtonWidth);
        if (ImGui.Button(rightLabel + "##wizardAdvance", new Vector2(rightButtonWidth, 0)))
        {
            if (step < TotalSteps) step++;
            else Complete(configuration);
        }
    }

    private void Complete(Configuration configuration)
    {
        configuration.ShowedWizard = true;
        configuration.Save();

        if (knownLanguagesChanged)
            _ = LanguageDetector.RebuildDetectorAsync();

        IsOpen = false;
    }
}
