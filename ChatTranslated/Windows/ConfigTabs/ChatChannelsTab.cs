using ChatTranslated.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using System.Collections.Generic;

namespace ChatTranslated.Windows.ConfigTabs;

public class ChatChannelsTab
{
    public static readonly List<XivChatType> genericChatTypes =
    [
        XivChatType.Say,
        XivChatType.Shout,
        XivChatType.TellIncoming,
        XivChatType.Party,
        XivChatType.Alliance,
        XivChatType.FreeCompany,
        XivChatType.NoviceNetwork,
        XivChatType.Yell,
        XivChatType.CrossParty,
        XivChatType.PvPTeam
    ];

    public static readonly List<XivChatType> lsChatTypes =
    [
        XivChatType.Ls1,
        XivChatType.Ls2,
        XivChatType.Ls3,
        XivChatType.Ls4,
        XivChatType.Ls5,
        XivChatType.Ls6,
        XivChatType.Ls7,
        XivChatType.Ls8
    ];

    public static readonly List<XivChatType> cwlsChatTypes =
    [
        XivChatType.CrossLinkShell1,
        XivChatType.CrossLinkShell2,
        XivChatType.CrossLinkShell3,
        XivChatType.CrossLinkShell4,
        XivChatType.CrossLinkShell5,
        XivChatType.CrossLinkShell6,
        XivChatType.CrossLinkShell7,
        XivChatType.CrossLinkShell8
    ];

    public void Draw(Configuration configuration)
    {
        ImGui.BeginTabBar("ChatChannelTabs");

        if (ImGui.BeginTabItem(Resources.GenericChannels))
        {
            DrawChatTypes(genericChatTypes, configuration);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("LS"))
        {
            DrawChatTypes(lsChatTypes, configuration);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("CWLS"))
        {
            DrawChatTypes(cwlsChatTypes, configuration);
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private static void DrawChatTypes(IEnumerable<XivChatType> chatTypes, Configuration configuration)
    {
        foreach (var type in chatTypes)
        {
            var typeEnabled = configuration.SelectedChatTypes.Contains(type);
            if (ImGui.Checkbox(Resources.ResourceManager.GetString(type.ToString(), Resources.Culture) ?? type.ToString(), ref typeEnabled))
            {
                if (typeEnabled && !configuration.SelectedChatTypes.Contains(type))
                {
                    configuration.SelectedChatTypes.Add(type);
                }
                else if (!typeEnabled)
                {
                    configuration.SelectedChatTypes.RemoveAll(t => t == type);
                }
                configuration.Save();
            }
        }
    }
}
