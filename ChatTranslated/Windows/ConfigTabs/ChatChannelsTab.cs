using ChatTranslated.Localization;
using ChatTranslated.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Utility.Raii;
using System;
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
        XivChatType.PvPTeam,
        XivChatType.CustomEmote
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
        using var tabBar = ImRaii.TabBar("ChatChannelTabs");
        if (!tabBar) return;

        DrawTab(Resources.GenericChannels, genericChatTypes, configuration);
        DrawTab("LS", lsChatTypes, configuration);
        DrawTab("CWLS", cwlsChatTypes, configuration);
    }

    private static void DrawTab(string label, List<XivChatType> chatTypes, Configuration configuration)
    {
        using var tab = ImRaii.TabItem(label);
        if (tab) DrawChatTypesColumns(chatTypes, configuration);
    }

    private static void DrawChatTypesColumns(List<XivChatType> chatTypes, Configuration configuration)
    {
        int rows = (int)Math.Ceiling(chatTypes.Count / 2.0);

        ImGui.Columns(2, "ChatTypeColumns", false);
        for (int i = 0; i < chatTypes.Count; i++)
        {
            if (i == rows) ImGui.NextColumn();
            DrawChatTypeCheckbox(chatTypes[i], configuration);
        }
        ImGui.Columns(1);
    }

    private static void DrawChatTypeCheckbox(XivChatType type, Configuration configuration)
    {
        var typeEnabled = configuration.SelectedChatTypes.Contains(type);
        if (ImGui.Checkbox(Loc.Get(type.ToString()), ref typeEnabled))
        {
            if (typeEnabled && !configuration.SelectedChatTypes.Contains(type))
                configuration.SelectedChatTypes.Add(type);
            else if (!typeEnabled)
                configuration.SelectedChatTypes.RemoveAll(t => t == type);
            configuration.Save();
        }
    }
}
