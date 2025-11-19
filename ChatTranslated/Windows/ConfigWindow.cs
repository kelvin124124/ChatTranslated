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
    private readonly TranslationModeTab translationEngineTab;
    private readonly LanguagesTab languagesTab;
    private readonly ChatTab chatTab;

    public ConfigWindow(Plugin plugin) : base(
        "Chat Translated config window",
        ImGuiWindowFlags.AlwaysAutoResize)
    {
        Size = new Vector2(700, 340);

        generalTab = new GeneralTab();
        translationEngineTab = new TranslationModeTab();
        languagesTab = new LanguagesTab();
        chatTab = new ChatTab();
    }

    public override void Draw()
    {
        Configuration configuration = Service.configuration;
        string[] tabs = [Resources.General, Resources.Translation_Engine, Resources.Languages, Resources.Chat];

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
                    translationEngineTab.Draw(configuration);
                    break;
                case 2:
                    languagesTab.Draw(configuration);
                    break;
                case 3:
                    chatTab.Draw(configuration);
                    break;
            }
        }
        ImGui.EndChild();

        ImGui.Columns(1);
    }
}
