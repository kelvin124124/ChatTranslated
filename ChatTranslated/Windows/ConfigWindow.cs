using ChatTranslated.Localization;
using ChatTranslated.Utils;
using ChatTranslated.Windows.ConfigTabs;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace ChatTranslated.Windows;

public class ConfigWindow : Window
{
    private static short CurrentTab = 0;

    private readonly GeneralTab generalTab;
    private readonly LanguagesTab languagesTab;
    private readonly ChatChannelsTab chatChannelsTab;
    private readonly TranslationModeTab translationModeTab;

    public ConfigWindow(Plugin plugin) : base(
        "ChatTranslated config window",
        ImGuiWindowFlags.AlwaysAutoResize)
    {
        Size = new Vector2(700, 340);

        generalTab = new GeneralTab();
        languagesTab = new LanguagesTab();
        chatChannelsTab = new ChatChannelsTab();
        translationModeTab = new TranslationModeTab();
    }

    public override void Draw()
    {
        Configuration configuration = Service.configuration;
        string[] tabs = [Resources.General, Resources.Languages, Resources.ChatChannels, Resources.Translation_Mode];

        ImGui.Columns(2, "ConfigColumns", false);
        ImGui.SetColumnWidth(0, 200);

        if (ImGui.BeginChild("TabsBox", new Vector2(190, 300), true))
        {
            for (short i = 0; i < tabs.Length; i++)
            {
                if (ImGui.Selectable(tabs[i], CurrentTab == i))
                {
                    CurrentTab = i;
                }
            }
        }
        ImGui.EndChild();

        ImGui.NextColumn();

        if (ImGui.BeginChild("ConfigBox", new Vector2(0, 300), true))
        {
            switch (CurrentTab)
            {
                case 0:
                    generalTab.Draw(configuration);
                    break;
                case 1:
                    languagesTab.Draw(configuration);
                    break;
                case 2:
                    chatChannelsTab.Draw(configuration);
                    break;
                case 3:
                    translationModeTab.Draw(configuration);
                    break;
            }
        }
        ImGui.EndChild();

        ImGui.Columns(1);
    }
}
