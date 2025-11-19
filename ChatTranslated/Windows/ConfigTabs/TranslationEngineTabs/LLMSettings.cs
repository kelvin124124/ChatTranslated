using ChatTranslated.Localization;
using Dalamud.Bindings.ImGui;

namespace ChatTranslated.Windows.ConfigTabs.TranslationEngineTabs;

public static class LLMSettings
{
    public static void DrawContextSettings(Configuration configuration)
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
}
