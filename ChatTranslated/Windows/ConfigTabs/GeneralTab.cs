using ChatTranslated.Localization;
using ChatTranslated.Utils;
using Dalamud.Bindings.ImGui;
using System;
using System.Globalization;
using System.Linq;

namespace ChatTranslated.Windows.ConfigTabs;

public class GeneralTab
{
    private readonly string[] supportedDisplayLanguages =
    ["English", "Japanese", "German", "French", "Chinese (Simplified)", "Chinese (Traditional)", "Korean", "Spanish"];

    public void Draw(Configuration configuration)
    {
        DrawPluginToggles(configuration);
        ImGui.Separator();
        DrawChatIntegrationSettings(configuration);
        ImGui.Separator();
        DrawPluginLangSelection(configuration);

#if DEBUG
        ImGui.Separator();
        if (ImGui.Button("Magic button"))
        {
            var str = Service.chatHandler?.GetChatMessageContext();
            Service.pluginLog.Warning(str!);
        }
#endif
    }

    private static void DrawPluginToggles(Configuration configuration)
    {
        bool _Enabled = configuration.Enabled;
        bool _EnabledInDuty = configuration.EnabledInDuty;

        if (ImGui.Checkbox(Resources.EnablePlugin, ref _Enabled))
        {
            configuration.Enabled = _Enabled;
            configuration.Save();
        }

        if (ImGui.Checkbox(Resources.EnableInDuties, ref _EnabledInDuty))
        {
            configuration.EnabledInDuty = _EnabledInDuty;
            configuration.Save();
        }
    }

    private static void DrawChatIntegrationSettings(Configuration configuration)
    {
        bool _ChatIntegration = configuration.ChatIntegration;
        bool _ChatIntegration_HideOriginal = configuration.ChatIntegration_HideOriginal;
        bool _ChatIntegration_ShowColoredText = configuration.ChatIntegration_ShowColoredText;
        bool _ChatIntegration_UseEchoChannel = configuration.ChatIntegration_UseEchoChannel;

        if (ImGui.Checkbox(Resources.ChatIntegration, ref _ChatIntegration))
        {
            configuration.ChatIntegration = _ChatIntegration;
            configuration.Save();
        }

        if (configuration.ChatIntegration)
        {
            ImGui.Indent(20);

            if (ImGui.Checkbox(Resources.ChatIntegration_HideOriginal, ref _ChatIntegration_HideOriginal))
            {
                configuration.ChatIntegration_HideOriginal = _ChatIntegration_HideOriginal;
                configuration.Save();
            }

            if (ImGui.Checkbox(Resources.ChatIntegration_ShowColoredText, ref _ChatIntegration_ShowColoredText))
            {
                configuration.ChatIntegration_ShowColoredText = _ChatIntegration_ShowColoredText;
                configuration.Save();
            }

            if (ImGui.Checkbox(Resources.ChatIntegration_UseEchoChannel, ref _ChatIntegration_UseEchoChannel))
            {
                configuration.ChatIntegration_UseEchoChannel = _ChatIntegration_UseEchoChannel;
                configuration.Save();
            }

            ImGui.Unindent(20);
        }
    }

    private void DrawPluginLangSelection(Configuration configuration)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Resources.PluginUILanguage);

        string currentSelection = configuration.SelectedPluginLanguage;

        int currentIndex = Array.IndexOf(supportedDisplayLanguages, currentSelection);
        if (currentIndex == -1) currentIndex = 0;

        string[] localizedSupportedDisplayLanguages = [.. supportedDisplayLanguages.Select(lang => Resources.ResourceManager.GetString(lang, Resources.Culture) ?? lang)];
        if (ImGui.Combo("##pluginLanguage", ref currentIndex, localizedSupportedDisplayLanguages, supportedDisplayLanguages.Length))
        {
            configuration.SelectedPluginLanguage = supportedDisplayLanguages[currentIndex];
            configuration.Save();
            SetLanguageCulture(configuration.SelectedPluginLanguage);
        }

        if (ImGui.Button("Help with localization!"))
        {
            Dalamud.Utility.Util.OpenLink("https://hosted.weblate.org/projects/chattranslated/");
        }
    }

    internal static void SetLanguageCulture(string langName)
    {
        string langCode = langName switch
        {
            "English" => "en-US",
            "German" => "de-DE",
            "Spanish" => "es-ES",
            "French" => "fr-FR",
            "Japanese" => "ja-JP",
            "Chinese (Simplified)" => "zh-CN",
            "Chinese (Traditional)" => "zh-TW",
            "Korean" => "ko-KR",
            _ => "en-US"
        };
        Resources.Culture = new CultureInfo(langCode);
    }
}
