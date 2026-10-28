using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;

namespace ChatTranslated.Windows.ConfigTabs.TranslationEngineTabs;

public static class LLMSettings
{
    public static void DrawContextSettings(Configuration configuration)
    {
        if (ImGui.Checkbox(Resources.UseContext, ref configuration.UseContext))
            configuration.Save();

        if (configuration.UseContext)
        {
            using var indent = ImRaii.PushIndent(20f, scaled: false);
            ImGui.TextUnformatted(Resources.ChatContextExplanation);
        }
    }

    public static void DrawProviderSelection(Configuration configuration)
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

    internal static void DrawKeyStatus(bool valid)
    {
        ImGui.SameLine();
        ImGui.TextColored(valid ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1), valid ? "✓ valid" : "✗ invalid");
    }

    // true when an authenticated GET to the endpoint succeeds
    internal static async Task<bool> IsApiKeyValid(string apiKey, string endpoint)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint)
            {
                Headers = { { "Authorization", $"Bearer {apiKey}" } }
            };
            using var response = await TranslationHandler.HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            Service.pluginLog.Information("API Key validation successful.");
            return true;
        }
        catch
        {
            Service.pluginLog.Warning("API Key validation failed.");
            return false;
        }
    }
}
