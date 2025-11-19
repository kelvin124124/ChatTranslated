using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Windows.ConfigTabs.TranslationEngine;
using Dalamud.Bindings.ImGui;
using System;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Windows.ConfigTabs;

/// <summary>
/// Main coordinator for translation engine configuration UI.
/// Delegates to engine-specific settings components in the TranslationEngine subfolder.
/// </summary>
public class TranslationEngineTab
{
    public void Draw(Configuration configuration)
    {
        DrawEngineSelection(configuration);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Delegate to engine-specific settings
        switch (configuration.SelectedTranslationEngine)
        {
            case TranslationEngine.DeepL:
                DeepLSettings.Draw(configuration);
                break;

            case TranslationEngine.LLM:
                DrawLLMConfiguration(configuration);
                break;
        }
    }

    private static void DrawEngineSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Resources.TranslationEngine);
        ImGui.SameLine();

        int selectedEngine = (int)configuration.SelectedTranslationEngine;
        string[] engineNames = Enum.GetNames<TranslationEngine>();

        if (ImGui.Combo("##TranslationEngineCombo", ref selectedEngine, engineNames, engineNames.Length))
        {
            configuration.SelectedTranslationEngine = (TranslationEngine)selectedEngine;
            TranslationHandler.ClearTranslationCache();
            configuration.Save();
        }
    }

    private static void DrawLLMConfiguration(Configuration configuration)
    {
        ImGui.TextUnformatted(Resources.LLM_Explanation);
        ImGui.Spacing();

        // Common LLM settings
        LLMCommonSettings.DrawContextSettings(configuration);
        ImGui.Spacing();

        LLMCommonSettings.DrawProviderSelection(configuration);
        ImGui.Spacing();

        // Provider-specific settings
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
