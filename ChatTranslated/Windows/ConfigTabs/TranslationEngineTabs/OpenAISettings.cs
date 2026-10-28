using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace ChatTranslated.Windows.ConfigTabs.TranslationEngineTabs;

public static class OpenAISettings
{
    private static string OpenAIApiKeyInput = Service.configuration.OpenAI_API_Key;
    private static bool? OpenAIApiKeyValid = null;

    private static readonly string[] OpenAIModels =
    [
        "gpt-5.5", "gpt-5.5-mini", "gpt-5.5-nano",
        "gpt-5.4", "gpt-5.4-mini", "gpt-5.4-nano",
        "gpt-5", "gpt-5-mini", "gpt-5-nano",
        "gpt-4.1", "gpt-4.1-mini", "gpt-4.1-nano",
        "gpt-4o", "gpt-4o-mini"
    ];  // why so many???

    public static void Draw(Configuration configuration)
    {
        ImGui.TextUnformatted(Resources.OpenAIAPIExplanation);
        ImGui.Spacing();

        // API Configuration
        ImGui.TextUnformatted(Resources.OpenAIAPIKey);
        if (OpenAIApiKeyValid is { } valid)
            LLMSettings.DrawKeyStatus(valid);
        else
            _ = ValidateApiKey(OpenAIApiKeyInput);

        ImGui.InputText("##APIKey", ref OpenAIApiKeyInput, 200);
        ImGui.SameLine();
        if (ImGui.Button(Resources.Apply + "###OpenAI_API_Key"))
        {
            configuration.OpenAI_API_Key = OpenAIApiKeyInput;
            configuration.Save();
            Plugin.OutputChatLine($"OpenAI API Key {configuration.OpenAI_API_Key[..Math.Min(configuration.OpenAI_API_Key.Length, 12)]}... saved successfully.");
            _ = ValidateApiKey(OpenAIApiKeyInput);
        }

        ImGui.Spacing();

        // Model Selection
        ImGui.TextUnformatted("Model");
        int currentModelIndex = Array.IndexOf(OpenAIModels, configuration.OpenAI_Model);
        if (currentModelIndex == -1) currentModelIndex = 0;

        if (ImGui.Combo("##OpenAIModel", ref currentModelIndex, OpenAIModels, OpenAIModels.Length))
        {
            configuration.OpenAI_Model = OpenAIModels[currentModelIndex];
            TranslationHandler.ClearTranslationCache();
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1, 0, 0, 1), Resources.APIKeyWarn);

        ImGui.Spacing();
        ImGui.Separator();

        // Advanced Settings
        CustomPromptEditor.DrawSettings(configuration);
    }

    private static async Task ValidateApiKey(string apiKey)
    {
        OpenAIApiKeyValid = false;

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "sk-YOUR-API-KEY") return;

        OpenAIApiKeyValid = await LLMSettings.IsApiKeyValid(apiKey, "https://api.openai.com/v1/models");
    }
}
