using ChatTranslated.Localization;
using ChatTranslated.Utils;
using ChatTranslated.Windows.ConfigTabs;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace ChatTranslated.Windows;

public class ConfigWindow : Window
{
    private static int CurrentTab = 0;

    private readonly GeneralTab generalTab = new();
    private readonly LanguagesTab languagesTab = new();
    private readonly ChatChannelsTab chatChannelsTab = new();
    private readonly TranslationModeTab translationModeTab = new();

    public ConfigWindow() : base(
        "ChatTranslated config window",
        ImGuiWindowFlags.AlwaysAutoResize)
    {
        Size = new Vector2(700, 340);
    }

    public override void Draw()
    {
        Configuration configuration = Service.configuration;
        string[] tabs = [Resources.General, Resources.Languages, Resources.ChatChannels, Resources.Translation_Mode];

        ImGui.Columns(2, "ConfigColumns", false);
        ImGui.SetColumnWidth(0, 200);

        using (var tabsBox = ImRaii.Child("TabsBox", new Vector2(190, 300), true))
        {
            if (tabsBox)
            {
                for (var i = 0; i < tabs.Length; i++)
                {
                    if (ImGui.Selectable(tabs[i], CurrentTab == i))
                        CurrentTab = i;
                }
            }
        }

        ImGui.NextColumn();

        using (var configBox = ImRaii.Child("ConfigBox", new Vector2(0, 300), true))
        {
            if (configBox)
            {
                switch (CurrentTab)
                {
                    case 0: generalTab.Draw(configuration); break;
                    case 1: languagesTab.Draw(configuration); break;
                    case 2: chatChannelsTab.Draw(configuration); break;
                    case 3: translationModeTab.Draw(configuration); break;
                }
            }
        }

        ImGui.Columns(1);
    }
}
