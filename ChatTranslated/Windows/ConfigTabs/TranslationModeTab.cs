using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Windows.ConfigTabs.TranslationEngineTabs;
using Dalamud.Bindings.ImGui;
using System;

namespace ChatTranslated.Windows.ConfigTabs;

public class TranslationModeTab
{
    public void Draw(Configuration configuration)
    {
        DrawEngineSelection(configuration);
        DrawCacheToggle(configuration);

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
                DrawLLMConfiguration(configuration);
                break;
        }
    }

    internal static void DrawEngineSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Resources.TranslationEngine);
        ImGui.SameLine();

        int selectedEngine = (int)configuration.SelectedTranslationEngine;
        string[] engineNames = Enum.GetNames<Configuration.TranslationEngine>();

        if (ImGui.Combo("##TranslationEngineCombo", ref selectedEngine, engineNames, engineNames.Length))
        {
            configuration.SelectedTranslationEngine = (Configuration.TranslationEngine)selectedEngine;
            TranslationHandler.ClearTranslationCache();
            configuration.Save();
        }
    }

    internal static void DrawCacheToggle(Configuration configuration)
    {
        var enableCache = configuration.EnableTranslationCache;
        if (ImGui.Checkbox("Cache translations", ref enableCache))
        {
            configuration.EnableTranslationCache = enableCache;
            configuration.Save();
            if (!enableCache)
                TranslationHandler.ClearTranslationCache();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Reuse previous translations for identical messages (up to 120 entries, persisted to disk).\nDisable to always re-translate.");
    }

    internal static void DrawLLMConfiguration(Configuration configuration)
    {
        LLMSettings.DrawContextSettings(configuration);

        ImGui.Separator();
        ImGui.Spacing();

        LLMSettings.DrawProviderSelection(configuration);

        ImGui.Separator();
        ImGui.Spacing();

        switch (configuration.LLM_Provider)
        {
            case 0:
                LLMProxySettings.Draw(configuration);
                break;
            case 1:
                OpenAISettings.Draw(configuration);
                break;
            case 2:
                OpenAICompatibleSettings.Draw(configuration);
                break;
        }
    }
}
