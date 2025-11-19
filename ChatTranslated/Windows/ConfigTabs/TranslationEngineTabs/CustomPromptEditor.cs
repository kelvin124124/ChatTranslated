using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace ChatTranslated.Windows.ConfigTabs.TranslationEngineTabs;

public static class CustomPromptEditor
{
    private static string CustomPromptInput = GetCustomPromptInput();

    public static void DrawSettings(Configuration configuration)
    {
        bool _UseCustomPrompt = configuration.UseCustomPrompt;

        if (ImGui.Checkbox(Resources.UseCustomPrompt + "##UseCustomPromptCheckbox", ref _UseCustomPrompt))
        {
            configuration.UseCustomPrompt = _UseCustomPrompt;
            configuration.Save();
        }

        if (configuration.UseCustomPrompt)
        {
            DrawEditor(configuration);
        }
    }

    public static void DrawCollapsible(Configuration configuration)
    {
        if (ImGui.CollapsingHeader(Resources.UseCustomPrompt))
        {
            DrawSettings(configuration);
        }
    }

    private static void DrawEditor(Configuration configuration)
    {
        ImGui.TextUnformatted(Resources.CustomSystemPrompt);

        ImGui.SameLine();
        ImGui.TextDisabled("?");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Resources.CustomPromptTooltip);
        }

        ImGui.InputTextMultiline("##CustomPrompt", ref CustomPromptInput, 10000, new Vector2(-1, 200));

        if (ImGui.Button("Apply##CustomPromptApply"))
        {
            configuration.LLM_CustomPrompt = CustomPromptInput;
            configuration.Save();

            TranslationHandler.ClearTranslationCache();
            Plugin.OutputChatLine("Custom prompt saved successfully.");
        }

        ImGui.SameLine();
        if (ImGui.Button("Reset to Default##CustomPromptReset"))
        {
            configuration.LLM_CustomPrompt = OpenAITranslate.BuildPrompt("{targetLanguage}", null);
            configuration.Save();

            CustomPromptInput = configuration.LLM_CustomPrompt;

            TranslationHandler.ClearTranslationCache();
            Plugin.OutputChatLine("Prompt reset to default.");
        }
    }

    private static string GetCustomPromptInput()
    {
        if (string.IsNullOrWhiteSpace(Service.configuration.LLM_CustomPrompt))
        {
            return OpenAITranslate.BuildPrompt("{targetLanguage}", null);
        }
        return Service.configuration.LLM_CustomPrompt;
    }
}
