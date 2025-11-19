using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Bindings.ImGui;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;

namespace ChatTranslated.Windows.ConfigTabs.TranslationEngineTabs;

public static class OpenAICompatibleSettings
{
    private static string LLMApiEndpointInput = Service.configuration.LLM_API_endpoint;
    private static string LLMApiKeyInput = Service.configuration.LLM_API_Key;
    private static string LLMModelInput = Service.configuration.LLM_Model;
    private static bool? LLMApiKeyValid = null;

    public static void Draw(Configuration configuration)
    {
        ImGui.TextUnformatted(Resources.OpenAICompatibleExplanation);
        ImGui.Spacing();

        // API Endpoint
        ImGui.TextUnformatted(Resources.LLMApiEndpoint);
        ImGui.SameLine();
        ImGui.TextDisabled("?");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Resources.LLMApiEndpointExample);
        }

        ImGui.InputText("##APIEndpoint", ref LLMApiEndpointInput, 200);
        ImGui.SameLine();
        if (ImGui.Button(Resources.Apply + "###LLM_API_Endpoint"))
        {
            configuration.LLM_API_endpoint = LLMApiEndpointInput;
            Plugin.OutputChatLine($"LLM API Endpoint {configuration.LLM_API_endpoint} saved successfully.");
            configuration.Save();
            _ = ValidateApiKey(LLMApiKeyInput, LLMApiEndpointInput);
        }

        ImGui.Spacing();

        // API Key
        ImGui.TextUnformatted(Resources.LLMAPIKey);
        if (LLMApiKeyValid.HasValue)
        {
            ImGui.SameLine();
            ImGui.TextColored(LLMApiKeyValid.Value ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                             LLMApiKeyValid.Value ? "✓ valid" : "✗ invalid");
        }
        else
        {
            _ = ValidateApiKey(LLMApiKeyInput, LLMApiEndpointInput);
        }

        ImGui.InputText("##APIKey", ref LLMApiKeyInput, 200);
        ImGui.SameLine();
        if (ImGui.Button(Resources.Apply + "###LLM_API_Key"))
        {
            configuration.LLM_API_Key = LLMApiKeyInput;
            Plugin.OutputChatLine($"LLM API Key {configuration.LLM_API_Key[..12]}... saved successfully.");
            configuration.Save();
            _ = ValidateApiKey(LLMApiKeyInput, LLMApiEndpointInput);
        }

        ImGui.Spacing();

        // Model
        ImGui.TextUnformatted(Resources.LLMModel);
        ImGui.InputText("##Model", ref LLMModelInput, 200);
        ImGui.SameLine();
        if (ImGui.Button(Resources.Apply + "###LLM_Model"))
        {
            configuration.LLM_Model = LLMModelInput;
            Plugin.OutputChatLine($"LLM Model {configuration.LLM_Model} saved successfully.");
            configuration.Save();
            _ = ValidateApiKey(LLMApiKeyInput, LLMApiEndpointInput);
        }

        ImGui.Spacing();
        ImGui.TextUnformatted(Resources.OpenAICompatibleInfo);

        ImGui.Spacing();
        ImGui.Separator();

        // Advanced Settings
        CustomPromptEditor.DrawCollapsible(configuration);
    }

    private static async Task ValidateApiKey(string apiKey, string endpoint)
    {
        LLMApiKeyValid = false;

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(endpoint)) return;

        if (endpoint.Contains("openrouter"))
            endpoint = endpoint.TrimEnd('/').Replace("/chat/completions", "/auth/key");
        else
            endpoint = endpoint.TrimEnd('/').Replace("/chat/completions", "/models");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint)
            {
                Headers = { { "Authorization", $"Bearer {apiKey}" } }
            };

            var response = await TranslationHandler.HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            LLMApiKeyValid = true;
            Service.pluginLog.Information($"API Key validation successful.");
        }
        catch
        {
            Service.pluginLog.Warning($"API Key validation failed.");
        }
    }
}
