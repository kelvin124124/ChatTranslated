using ChatTranslated.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using System.Collections.Generic;

namespace ChatTranslated.Windows.ConfigTabs;

public class ChatTab
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
        DrawChatIntegrationSettings(configuration);

        ImGui.Separator();
        ImGui.Spacing();

        DrawChatChannelSelection(configuration);
    }

    private static void DrawChatIntegrationSettings(Configuration configuration)
    {
        bool _ChatIntegration = configuration.ChatIntegration;
        bool _ChatIntegration_HideOriginal = configuration.ChatIntegration_HideOriginal;
        bool _ChatIntegration_ShowColoredText = configuration.ChatIntegration_ShowColoredText;

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

            ImGui.Unindent(20);
        }
    }

    private static void DrawChatChannelSelection(Configuration configuration)
    {
        ImGui.TextUnformatted(Resources.Chat_Channels);
        ImGui.Spacing();

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
