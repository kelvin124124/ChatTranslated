using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Bindings.ImGui;
using System;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Windows.ConfigTabs;

public class TranslationModeTab
{
    private static string DeepLApiKeyInput = Service.configuration.DeepL_API_Key;
    private static string OpenAIApiKeyInput = Service.configuration.OpenAI_API_Key;
    private static string LLMApiEndpointInput = Service.configuration.LLM_API_endpoint;
    private static string LLMApiKeyInput = Service.configuration.LLM_API_Key;
    private static string LLMModelInput = Service.configuration.LLM_Model;
    private static string OpenAICustomPromptInput = Service.configuration.OpenAI_CustomPrompt;
    private static string LLMCustomPromptInput = Service.configuration.LLM_CustomPrompt;

#if DEBUG
    private static string ProxyBaseUrl = Service.configuration.Proxy_Url;
    private static string ProxyApiKeyInput = Service.configuration.Proxy_API_Key;
#endif

    private static bool? OpenAIApiKeyValid = null;
    private static bool? LLMApiKeyValid = null;

    private static readonly string[] OpenAIModels =
    [
        "gpt-5-mini", "gpt-5",
        "gpt-4.1-mini", "gpt-4.1",
        "gpt-4o-mini", "gpt-4o"
    ];

    public void Draw(Configuration configuration)
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

        switch (configuration.SelectedTranslationEngine)
        {
            case TranslationEngine.DeepL:
                ImGui.TextWrapped(Resources.DeepLExplanation);

                ImGui.Separator();
                DrawDeepLSettings(configuration);

                break;

            case TranslationEngine.LLM:
                ImGui.TextUnformatted(Resources.LLM_Explanation);

                ImGui.Spacing();
                DrawContextSettings(configuration);

                ImGui.Spacing();
                DrawProviderSelection(configuration);
                switch (configuration.LLM_Provider)
                {
                    case 0:
                        ImGui.TextUnformatted(Resources.LLM_Proxy_Explanation);
#if DEBUG
                        ImGui.Separator();
                        DrawLLMProxyDebugSettings(configuration);
#endif
                        break;
                    case 1:
                        ImGui.TextUnformatted(Resources.OpenAIAPIExplanation);
                        ImGui.Separator();
                        DrawOpenAISettings(configuration);
                        break;
                    case 2:
                        ImGui.TextUnformatted(Resources.OpenAICompatibleExplanation);
                        ImGui.Separator();
                        DrawLLMSettings(configuration);
                        break;
                }
                break;
        }
    }

    private static void DrawDeepLSettings(Configuration configuration)
    {
        ImGui.TextUnformatted(Resources.DeepLAPIKey);
        ImGui.InputText("##APIKey", ref DeepLApiKeyInput, 100);

        ImGui.SameLine();
        if (ImGui.Button(Resources.Apply + "###DeepL_API_Key"))
        {
            configuration.DeepL_API_Key = DeepLApiKeyInput;
            configuration.Save();

            Plugin.OutputChatLine($"DeepL API Key {configuration.DeepL_API_Key[..12]}... saved successfully."); // only output part of the key
        }
    }

    private static void DrawProviderSelection(Configuration configuration)
    {
        ImGui.TextUnformatted(Resources.LLMProvider + ":");
        ImGui.SameLine();
        int selectedProvider = configuration.LLM_Provider;

        float posX = ImGui.GetCursorPosX() + 20;

        ImGui.SetCursorPosX(posX);
        if (ImGui.RadioButton("LLM Proxy", ref selectedProvider, 0))
        {
            configuration.LLM_Provider = 0;
            configuration.Save();
        }
        ImGui.SetCursorPosX(posX);
        if (ImGui.RadioButton("OpenAI", ref selectedProvider, 1))
        {
            configuration.LLM_Provider = 1;
            configuration.Save();
        }
        ImGui.SetCursorPosX(posX);
        if (ImGui.RadioButton("OpenAI-compatible API", ref selectedProvider, 2))
        {
            configuration.LLM_Provider = 2;
            configuration.Save();
        }
    }

    private static void DrawContextSettings(Configuration configuration)
    {
        bool _UseContext = configuration.UseContext;

        if (ImGui.Checkbox(Resources.UseContext, ref _UseContext))
        {
            configuration.UseContext = _UseContext;
            configuration.Save();
        }

        if (configuration.UseContext)
        {
            ImGui.Indent(20);
            ImGui.TextUnformatted(Resources.ChatContextExplanation);
            ImGui.Unindent(20);
        }
    }

#if DEBUG
    private static void DrawLLMProxyDebugSettings(Configuration configuration)
    {
        ImGui.TextUnformatted("Proxy Url");
        ImGui.InputText("##APIBaseUrl", ref ProxyBaseUrl, 100);
        ImGui.SameLine();
        if (ImGui.Button("Save###Proxy_Url"))
        {
            configuration.Proxy_Url = ProxyBaseUrl;
            configuration.Save();
            Plugin.OutputChatLine($"Proxy Url {configuration.Proxy_Url} saved successfully.");
        }

        ImGui.TextUnformatted("Proxy API Key");
        ImGui.InputText("##APIKey", ref ProxyApiKeyInput, 100);
        ImGui.SameLine();
        if (ImGui.Button("Save###Proxy_API_Key"))
        {
            configuration.Proxy_API_Key = ProxyApiKeyInput;
            configuration.Save();
            Plugin.OutputChatLine($"Proxy API Key {configuration.Proxy_API_Key} saved successfully.");
        }
    }
#endif

    private static void DrawOpenAISettings(Configuration configuration)
    {
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

        ImGui.TextUnformatted(Resources.OpenAIAPIKey);
        if (OpenAIApiKeyValid.HasValue)
        {
            ImGui.SameLine();
            ImGui.TextColored(OpenAIApiKeyValid.Value ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                             OpenAIApiKeyValid.Value ? "✓ valid" : "✗ invalid");
        }
        else
        {
            _ = ValidateOpenAIKey(OpenAIApiKeyInput);
        }

        ImGui.InputText("##APIKey", ref OpenAIApiKeyInput, 200);
        ImGui.SameLine();
        if (ImGui.Button(Resources.Apply + "###OpenAI_API_Key"))
        {
            configuration.OpenAI_API_Key = OpenAIApiKeyInput;
            configuration.Save();
            Plugin.OutputChatLine($"OpenAI API Key {configuration.OpenAI_API_Key[..12]}... saved successfully."); // only output part of the key
            _ = ValidateOpenAIKey(OpenAIApiKeyInput);
        }

        ImGui.TextUnformatted(Resources.OpenAIPriceEstimation);
        ImGui.NewLine();
        ImGui.TextColored(new Vector4(1, 0, 0, 1), Resources.APIKeyWarn);

        ImGui.Separator();
        ImGui.Spacing();
        DrawCustomPromptEditor(
            "OpenAI",
            ref OpenAICustomPromptInput,
            () => Service.configuration.OpenAI_CustomPrompt,
            (value) => {
                configuration.OpenAI_CustomPrompt = value;
                OpenAICustomPromptInput = value;
                configuration.Save();
            }
        );
    }

    private static void DrawLLMSettings(Configuration configuration)
    {
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
            _ = ValidateLLMKey(LLMApiKeyInput, LLMApiEndpointInput);
        }

        ImGui.TextUnformatted(Resources.LLMAPIKey);
        if (LLMApiKeyValid.HasValue)
        {
            ImGui.SameLine();
            ImGui.TextColored(LLMApiKeyValid.Value ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                             LLMApiKeyValid.Value ? "✓ valid" : "✗ invalid");
        }
        else
        {
            _ = ValidateLLMKey(LLMApiKeyInput, LLMApiEndpointInput);
        }

        ImGui.InputText("##APIKey", ref LLMApiKeyInput, 200);
        ImGui.SameLine();
        if (ImGui.Button(Resources.Apply + "###LLM_API_Key"))
        {
            configuration.LLM_API_Key = LLMApiKeyInput;
            Plugin.OutputChatLine($"LLM API Key {configuration.LLM_API_Key[..12]}... saved successfully."); // only output part of the key
            configuration.Save();
            _ = ValidateLLMKey(LLMApiKeyInput, LLMApiEndpointInput);
        }

        ImGui.TextUnformatted(Resources.LLMModel);
        ImGui.InputText("##Model", ref LLMModelInput, 200);
        ImGui.SameLine();
        if (ImGui.Button(Resources.Apply + "###LLM_Model"))
        {
            configuration.LLM_Model = LLMModelInput;
            Plugin.OutputChatLine($"LLM Model {configuration.LLM_Model} saved successfully.");
            configuration.Save();
            _ = ValidateLLMKey(LLMApiKeyInput, LLMApiEndpointInput);
        }

        ImGui.TextUnformatted(Resources.OpenAICompatibleInfo);

        ImGui.Separator();
        ImGui.Spacing();
        DrawCustomPromptEditor(
            "LLM",
            ref LLMCustomPromptInput,
            () => Service.configuration.LLM_CustomPrompt,
            (value) => {
                configuration.LLM_CustomPrompt = value;
                LLMCustomPromptInput = value;
                configuration.Save();
            }
        );
    }

    private static void DrawCustomPromptEditor(
        string label,
        ref string promptInput,
        Func<string> getCurrentPrompt,
        Action<string> savePrompt)
    {
        ImGui.TextUnformatted($"Custom Prompt ({label})");
        ImGui.SameLine();
        ImGui.TextDisabled("?");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Customize the system prompt for translation.\n" +
                "Use {targetLanguage} as a placeholder for the target language.\n" +
                "Leave empty to use the default prompt.\n" +
                "Context (if enabled) will be appended automatically."
            );
        }

        bool isUsingCustomPrompt = !string.IsNullOrWhiteSpace(getCurrentPrompt());
        if (isUsingCustomPrompt)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0, 1, 0, 1), "(Custom)");
        }
        else
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "(Default)");
        }

        // Multi-line text input
        ImGui.InputTextMultiline($"##{label}CustomPrompt", ref promptInput, 10000, new Vector2(-1, 200));

        // Buttons row
        if (ImGui.Button($"Apply##{label}CustomPromptApply"))
        {
            savePrompt(promptInput);
            TranslationHandler.ClearTranslationCache();
            Plugin.OutputChatLine($"{label} custom prompt saved successfully.");
        }

        ImGui.SameLine();
        if (ImGui.Button($"Reset to Default##{label}CustomPromptReset"))
        {
            promptInput = "";
            savePrompt("");
            TranslationHandler.ClearTranslationCache();
            Plugin.OutputChatLine($"{label} prompt reset to default.");
        }

        ImGui.SameLine();
        if (ImGui.Button($"View Default##{label}ViewDefault"))
        {
            string defaultPrompt = OpenAITranslate.GetDefaultPrompt(Service.configuration.SelectedTargetLanguage);
            ImGui.SetClipboardText(defaultPrompt);
            Plugin.OutputChatLine($"Default prompt copied to clipboard.");
        }

        // Show a preview of what's being used
        if (!isUsingCustomPrompt)
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Using default system prompt for FFXIV translation.");
        }
    }

    private static async Task ValidateOpenAIKey(string apiKey, string endpoint = "https://api.openai.com/v1/models")
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

    private static async Task ValidateLLMKey(string apiKey, string endpoint)
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
