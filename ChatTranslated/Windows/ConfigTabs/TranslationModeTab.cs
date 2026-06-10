using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Windows.ConfigTabs.TranslationEngineTabs;
using Dalamud.Bindings.ImGui;
using System;

namespace ChatTranslated.Windows.ConfigTabs;

public class TranslationModeTab
{
    public void Draw(Configuration configuration)
    {
        DrawEngineSelection(configuration);
        DrawCacheToggle(configuration);

        ImGui.Separator();
        ImGui.Spacing();

        DrawFallbackChain(configuration);

        ImGui.Separator();
        ImGui.Spacing();

        switch (configuration.SelectedTranslationEngine)
        {
            case Configuration.TranslationEngine.DeepL:
                ImGui.TextWrapped(Resources.DeepLExplanation);
                ImGui.Separator();
                ImGui.Spacing();
                DeepLSettings.Draw(configuration);
                break;

            case Configuration.TranslationEngine.LLM:
                ImGui.TextWrapped(Resources.LLM_Explanation);
                ImGui.Separator();
                ImGui.Spacing();
                DrawLLMConfiguration(configuration);
                break;
        }
    }

    internal static void DrawEngineSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Resources.TranslationEngine);
        ImGui.SameLine();

        int selectedEngine = (int)configuration.SelectedTranslationEngine;
        string[] engineNames = Enum.GetNames<Configuration.TranslationEngine>();

        if (ImGui.Combo("##TranslationEngineCombo", ref selectedEngine, engineNames, engineNames.Length))
        {
            configuration.SelectedTranslationEngine = (Configuration.TranslationEngine)selectedEngine;
            TranslationHandler.ClearTranslationCache();
            configuration.Save();
        }
    }

    internal static void DrawCacheToggle(Configuration configuration)
    {
        var enableCache = configuration.EnableTranslationCache;
        if (ImGui.Checkbox("Cache translations", ref enableCache))
        {
            configuration.EnableTranslationCache = enableCache;
            configuration.Save();
            if (!enableCache)
                TranslationHandler.ClearTranslationCache();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Reuse previous translations for identical messages (up to 120 entries, persisted to disk).\nDisable to always re-translate.");

        ImGui.SameLine();
        if (ImGui.Button("Clear cache"))
            TranslationHandler.ClearTranslationCache();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Remove all cached translations.");
    }

    internal static void DrawFallbackChain(Configuration configuration)
    {
        ImGui.TextUnformatted("Fallback chain");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When the selected engine fails, these providers are tried in order, top to bottom.\n" +
                             "Providers without an API key are skipped. Custom target languages always use machine translation.");

        var chain = configuration.FallbackProviders;
        var primary = TranslationHandler.GetPrimaryProvider(configuration);

        ImGui.TextDisabled($"1. {GetProviderName(primary)} (selected engine)");

        int moveUp = -1, moveDown = -1, remove = -1;
        for (int i = 0; i < chain.Count; i++)
        {
            var provider = chain[i];
            ImGui.PushID(i);

            ImGui.BeginDisabled(i == 0);
            if (ImGui.ArrowButton("##up", ImGuiDir.Up)) moveUp = i;
            ImGui.EndDisabled();

            ImGui.SameLine();
            ImGui.BeginDisabled(i == chain.Count - 1);
            if (ImGui.ArrowButton("##down", ImGuiDir.Down)) moveDown = i;
            ImGui.EndDisabled();

            ImGui.SameLine();
            if (ImGui.Button("X")) remove = i;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Remove from the fallback chain.");

            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted($"{i + 2}. {GetProviderName(provider)}");

            if (provider == primary)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("(selected engine - skipped)");
            }
            else if (!TranslationHandler.IsProviderConfigured(provider))
            {
                ImGui.SameLine();
                ImGui.TextDisabled("(no API key - skipped)");
            }

            ImGui.PopID();
        }

        if (moveUp > 0)
        {
            (chain[moveUp - 1], chain[moveUp]) = (chain[moveUp], chain[moveUp - 1]);
            configuration.Save();
        }
        else if (moveDown >= 0 && moveDown < chain.Count - 1)
        {
            (chain[moveDown + 1], chain[moveDown]) = (chain[moveDown], chain[moveDown + 1]);
            configuration.Save();
        }
        else if (remove >= 0)
        {
            chain.RemoveAt(remove);
            configuration.Save();
        }

        var available = Array.FindAll(Enum.GetValues<Configuration.TranslationProvider>(), p => !chain.Contains(p));
        if (available.Length > 0)
        {
            ImGui.SetNextItemWidth(220);
            if (ImGui.BeginCombo("##AddFallbackProvider", "Add fallback provider..."))
            {
                foreach (var provider in available)
                {
                    if (ImGui.Selectable(GetProviderName(provider)))
                    {
                        chain.Add(provider);
                        configuration.Save();
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();
        }

        if (ImGui.Button("Reset to default"))
        {
            configuration.FallbackProviders = [.. Configuration.DefaultFallbackProviders];
            configuration.Save();
        }
    }

    internal static string GetProviderName(Configuration.TranslationProvider provider) => provider switch
    {
        Configuration.TranslationProvider.DeepL => "DeepL (Free)",
        Configuration.TranslationProvider.DeepL_API => "DeepL API",
        Configuration.TranslationProvider.LLMProxy => "LLM Proxy",
        Configuration.TranslationProvider.OpenAI => "OpenAI",
        Configuration.TranslationProvider.OpenAICompatible => "OpenAI-compatible API",
        Configuration.TranslationProvider.GoogleTranslate => "Google Translate",
        Configuration.TranslationProvider.BingTranslate => "Bing Translate",
        Configuration.TranslationProvider.YandexTranslate => "Yandex Translate",
        _ => provider.ToString()
    };

    internal static void DrawLLMConfiguration(Configuration configuration)
    {
        LLMSettings.DrawContextSettings(configuration);

        ImGui.Separator();
        ImGui.Spacing();

        LLMSettings.DrawProviderSelection(configuration);

        ImGui.Separator();
        ImGui.Spacing();

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
