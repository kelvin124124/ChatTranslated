using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Bindings.ImGui;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace ChatTranslated.Windows.ConfigTabs.TranslationEngineTabs;

public static class OpenAISettings
{
    private static string OpenAIApiKeyInput = Service.configuration.OpenAI_API_Key;
    private static bool? OpenAIApiKeyValid = null;

    // current Chat Completions models, most capable first; luna is the cheap high-volume tier
    private static readonly string[] OpenAIModels =
    [
        "gpt-6-astra", "gpt-6.1-sol", "gpt-6.1-terra", "gpt-6-luna",
        "gpt-5.6-sol", "gpt-5.6-terra", "gpt-5.6-luna"
    ];

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
        // a model saved before the list changed stays selectable, so the combo shows what is really used
        string[] models = OpenAIModels.Contains(configuration.OpenAI_Model) ? OpenAIModels : [configuration.OpenAI_Model, .. OpenAIModels];
        int currentModelIndex = Array.IndexOf(models, configuration.OpenAI_Model);

        if (ImGui.Combo("##OpenAIModel", ref currentModelIndex, models, models.Length))
        {
            configuration.OpenAI_Model = models[currentModelIndex];
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
