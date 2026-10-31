using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Bindings.ImGui;
using System;
using System.Net.Http;
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
        if (OpenAIApiKeyValid.HasValue)
        {
            ImGui.SameLine();
            ImGui.TextColored(OpenAIApiKeyValid.Value ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                             OpenAIApiKeyValid.Value ? "✓ valid" : "✗ invalid");
        }
        else
        {
            _ = ValidateApiKey(OpenAIApiKeyInput);
        }

        ImGui.InputText("##APIKey", ref OpenAIApiKeyInput, 200);
        ImGui.SameLine();
        if (ImGui.Button(Resources.Apply + "###OpenAI_API_Key"))
        {
            configuration.OpenAI_API_Key = OpenAIApiKeyInput;
            configuration.Save();
            Plugin.OutputChatLine($"OpenAI API Key {configuration.OpenAI_API_Key[..12]}... saved successfully.");
            _ = ValidateApiKey(OpenAIApiKeyInput);
        }

        ImGui.Spacing();

        // Model Selection
        ImGui.TextUnformatted("Model");
        // a model saved before the list changed stays selectable, so the combo shows what is really used
        string[] models = Array.IndexOf(OpenAIModels, configuration.OpenAI_Model) >= 0
            ? OpenAIModels
            : [configuration.OpenAI_Model, .. OpenAIModels];
        int currentModelIndex = Array.IndexOf(models, configuration.OpenAI_Model);

        if (ImGui.Combo("##OpenAIModel", ref currentModelIndex, models, models.Length))
        {
            configuration.OpenAI_Model = models[currentModelIndex];
            TranslationHandler.ClearTranslationCache();
            configuration.Save();
        }

        if (OpenAITranslate.IsReasoningModel(configuration.OpenAI_Model))
        {
            ImGui.TextUnformatted("Reasoning effort");
            int effort = Array.IndexOf(OpenAITranslate.ReasoningEfforts, configuration.OpenAI_ReasoningEffort);
            if (effort < 0)
                effort = Array.IndexOf(OpenAITranslate.ReasoningEfforts, "low");

            if (ImGui.SliderInt("##OpenAIReasoningEffort", ref effort, 0, OpenAITranslate.ReasoningEfforts.Length - 1,
                    OpenAITranslate.ReasoningEfforts[effort]))
                configuration.OpenAI_ReasoningEffort = OpenAITranslate.ReasoningEfforts[effort];

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                TranslationHandler.ClearTranslationCache();
                configuration.Save();
            }
        }

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1, 0, 0, 1), Resources.APIKeyWarn);

        ImGui.Spacing();
        ImGui.Separator();

        // Advanced Settings
        CustomPromptEditor.DrawSettings(configuration);
    }

    private static async Task ValidateApiKey(string apiKey, string endpoint = "https://api.openai.com/v1/models")
    {
        OpenAIApiKeyValid = false;

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "sk-YOUR-API-KEY") return;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint)
            {
                Headers = { { "Authorization", $"Bearer {apiKey}" } }
            };

            var response = await TranslationHandler.HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            OpenAIApiKeyValid = true;
            Service.pluginLog.Information($"API Key validation successful.");
        }
        catch
        {
            Service.pluginLog.Warning($"API Key validation failed.");
        }
    }
}
