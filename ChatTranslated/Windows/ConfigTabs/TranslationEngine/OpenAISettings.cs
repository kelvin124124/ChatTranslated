using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Bindings.ImGui;
using System;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;

namespace ChatTranslated.Windows.ConfigTabs.TranslationEngine;

public static class OpenAISettings
{
    private static string OpenAIApiKeyInput = Service.configuration.OpenAI_API_Key;
    private static bool? OpenAIApiKeyValid = null;

    private static readonly string[] OpenAIModels =
    [
        "gpt-5-mini", "gpt-5",
        "gpt-4.1-mini", "gpt-4.1",
        "gpt-4o-mini", "gpt-4o"
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
        int currentModelIndex = Array.IndexOf(OpenAIModels, configuration.OpenAI_Model);
        if (currentModelIndex == -1) currentModelIndex = 0;

        if (ImGui.Combo("##OpenAIModel", ref currentModelIndex, OpenAIModels, OpenAIModels.Length))
        {
            configuration.OpenAI_Model = OpenAIModels[currentModelIndex];
            TranslationHandler.ClearTranslationCache();
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.TextUnformatted(Resources.OpenAIPriceEstimation);
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1, 0, 0, 1), Resources.APIKeyWarn);

        ImGui.Spacing();
        ImGui.Separator();

        // Advanced Settings
        CustomPromptEditor.DrawCollapsible(configuration);
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
