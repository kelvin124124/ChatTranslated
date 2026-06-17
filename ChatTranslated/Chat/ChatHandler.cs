using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatTranslated.Chat;

internal partial class ChatHandler
{

    public ChatHandler()
    {
        Service.chatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose() => Service.chatGui.ChatMessage -= OnChatMessage;

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (message.IsHandled) return;
        HandleChatMessage(message.LogKind, message.Sender, message.Message);
    }

    private async void HandleChatMessage(XivChatType type, SeString sender, SeString message)
    {
        try
        {
            if (!Service.configuration.Enabled || sender.TextValue.Contains("[CT]") || !Service.configuration.SelectedChatTypes.Contains(type))
                return;

            if (!Service.configuration.EnabledInDuty && Service.condition[ConditionFlag.BoundByDuty])
                return;

            var playerPayload = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
            string playerName = playerPayload?.PlayerName ?? sender.ToString();
            string localPlayerName = Service.playerState.CharacterName.ToString();
            if (type == XivChatType.TellOutgoing)
                playerName = localPlayerName;

            // comment for debugging
            if (!string.IsNullOrEmpty(localPlayerName) && playerName.EndsWith(localPlayerName))
            {
                Service.mainWindow.PrintToOutput($"{playerName}: {message.TextValue}");
                return;
            }

            var chatMessage = new Message(playerName, MessageSource.Chat, message, type);

            if (IsFilteredMessage(playerName, chatMessage.CleanedContent))
            {
                Service.mainWindow.PrintToOutput($"{playerName}: {message.TextValue}");
                return;
            }

            if (PhraseFilter.TryFilter(chatMessage, out var detectedIso))
                return;

            string? iso = detectedIso ?? await DetectLanguageAsync(chatMessage, type);

            bool shouldTranslate = iso != null
                && LanguageDetector.ValidIsoCodes.Contains(iso)
                && !LanguageDetector.IsTargetIsoCode(iso)
                && (Service.configuration.SelectedLanguageSelectionMode == Configuration.LanguageSelectionMode.Inclusive
                    ? LanguageDetector.IsSourceLanguageIsoCode(iso)
                    : !LanguageDetector.IsKnownIsoCode(iso));

            if (!shouldTranslate)
            {
                Service.mainWindow.PrintToOutput($"{chatMessage.Sender}: {chatMessage.CleanedContent}");
                return;
            }

            if (chatMessage.TranslatedContent == null)
            {
                chatMessage.Context = GetChatMessageContext();
                await TranslationHandler.TranslateMessage(chatMessage);
            }

            OutputMessage(chatMessage, type);
        }
        catch (Exception ex)
        {
            Service.pluginLog.Error(ex, "Error processing chat message.");
        }
    }

    private static Task<string?> DetectLanguageAsync(Message message, XivChatType type) =>
        Service.configuration.SelectedDetectionSource == Configuration.DetectionSource.Online
            ? DetectOnlineWithFallback(message, type)
            : DetectLocalAsync(message, type);

    private static async Task<string?> DetectLocalAsync(Message message, XivChatType type)
    {
        var (reliability, linguaIso) = await LanguageDetector.ComputeReliabilityAsync(
            message.CleanedContent, type, PhraseFilter.HasEnToken(message.CleanedContent));
        return reliability >= 0.40 ? linguaIso : await LanguageDetector.DetectIsoAsync(message) ?? linguaIso;
    }

    private static async Task<string?> DetectOnlineWithFallback(Message message, XivChatType type)
    {
        var iso = await OnlineLanguageDetector.DetectIsoAsync(message.CleanedContent)
               ?? (await Task.Run(() => LanguageDetector.GetLinguaResult(message.CleanedContent))).Iso;
        LanguageDetector.UpdateChannelCache(type, iso);
        return iso;
    }

    [GeneratedRegex(@"^<se\.\d+>$")]
    private static partial Regex SoundMacroRegex();

    [GeneratedRegex(@"^\s*https?://\S+(\s+https?://\S+)*\s*$")]
    private static partial Regex PureLinkRegex();

    [GeneratedRegex(@"[\u3400-\u4DBF\u4E00-\u9FFF]|\p{L}{2,}")]
    private static partial Regex HasTranslatableContentRegex();

    private readonly Dictionary<string, int> lastMessageTime = [];

    private bool IsFilteredMessage(string sender, string message)
    {
        var trimmed = message.Trim();
        bool isMacro = IsMacroMessage(sender);

        if (trimmed.Length < 2)
        {
            Service.pluginLog.Debug("Filtered: short/empty.");
            return true;
        }
        if (!HasTranslatableContentRegex().IsMatch(trimmed))
        {
            Service.pluginLog.Debug("Filtered: no translatable content.");
            return true;
        }
        if (PureLinkRegex().IsMatch(trimmed))
        {
            Service.pluginLog.Debug("Filtered: pure link.");
            return true;
        }
        if (SoundMacroRegex().IsMatch(trimmed))
        {
            Service.pluginLog.Debug("Filtered: sound macro.");
            return true;
        }
        if (isMacro)
        {
            Service.pluginLog.Debug("Filtered: macro spam.");
            return true;
        }

        return false;
    }

    private bool IsMacroMessage(string playerName)
    {
        int now = Environment.TickCount;

        if (lastMessageTime.Count > 20)
        {
            var keysToRemove = lastMessageTime
                .Where(kv => now - kv.Value > 3000) // 3s
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in keysToRemove)
                lastMessageTime.Remove(key);
        }

        bool isMacro = lastMessageTime.TryGetValue(playerName, out int lastMsgTime) && now - lastMsgTime < 650;
        lastMessageTime[playerName] = now;
        return isMacro;
    }

    internal static void OutputMessage(Message chatMessage, XivChatType type = XivChatType.Say)
    {
        if (PhraseFilter.Normalize(chatMessage.OriginalText) == PhraseFilter.Normalize(chatMessage.TranslatedContent!)
            && chatMessage.Source != MessageSource.MainWindow) // no need to output if translation is the same
        {
            Service.pluginLog.Info("Translation is the same as original. Skipping output.");
            return;
        }

        string outputStr = Service.configuration.ChatIntegration_HideOriginal
            ? chatMessage.TranslatedContent!
            : $"{chatMessage.OriginalText} || {chatMessage.TranslatedContent}";

        Service.mainWindow.PrintToOutput($"{chatMessage.Sender}: {outputStr}");

        if (Service.configuration.ChatIntegration)
        {
            var outputType = Service.configuration.ChatIntegration_UseEchoChannel ? XivChatType.Echo : type;
            Plugin.OutputChatLine(outputType, chatMessage.Sender, outputStr);
        }
    }

    public unsafe string GetChatMessageContext()
    {
        var x = GetActiveChatLogPanel();
        try
        {
            var chatLogPanelPtr = Service.gameGui.GetAddonByName($"ChatLogPanel_{x}");
            if (chatLogPanelPtr == 0) return string.Empty;

            var payloads = SeString.Parse((byte*)((AddonChatLogPanel*)chatLogPanelPtr.Address)->ChatText->GetText()).Payloads;
            var sb = new StringBuilder();
            bool inLink = false;
            // ChatLogPanel re-emits the name+world as plain text after the link; strip the echoed name.
            string? pendingName = null;

            for (int i = Math.Max(0, payloads.Count - 300); i < payloads.Count; i++)
            {
                switch (payloads[i])
                {
                    case TextPayload textPayload when !inLink:
                        var text = textPayload.Text ?? string.Empty;
                        if (pendingName != null)
                        {
                            if (text.StartsWith(pendingName)) text = text[pendingName.Length..];
                            pendingName = null;
                        }
                        sb.Append(text);
                        break;

                    case PlayerPayload playerPayload:
                        sb.Append($"[{playerPayload.PlayerName}]");
                        inLink = true;
                        pendingName = playerPayload.PlayerName;
                        break;

                    case ItemPayload itemPayload:
                        sb.Append($"[Item] {itemPayload.DisplayName}");
                        inLink = true;
                        pendingName = null;
                        break;

                    case QuestPayload questPayload:
                        sb.Append($"[Quest] {questPayload.Quest.ToString()}");
                        inLink = true;
                        pendingName = null;
                        break;

                    case MapLinkPayload mapLinkPayload:
                        sb.Append($"[Map] {mapLinkPayload.ToString()}");
                        inLink = true;
                        pendingName = null;
                        break;

                    case StatusPayload statusPayload:
                        sb.Append($"[Status] {statusPayload.ToString()}");
                        inLink = true;
                        pendingName = null;
                        break;

                    case PartyFinderPayload: // does not need to be tagged
                        inLink = true;
                        pendingName = null;
                        break;

                    case RawPayload when inLink: // link terminator (0x27 0x03); keep pendingName
                        inLink = false;
                        break;

                    case AutoTranslatePayload:
                        i += 2; // self-contained, no link terminator
                        pendingName = null;
                        break;
                }
            }

            var lines = sb.ToString().Split('\r');
            sb.Clear();

            for (int j = Math.Max(0, lines.Length - 10); j < lines.Length; j++)  // Max ctx lines: 10
            {
                var line = lines[j].Trim();

                // Our "[CT]" echo lines repeat the original ("orig || translated"); keep only the translation.
                if (line.Contains("[CT]"))
                {
                    var sep = line.IndexOf(" || ");
                    if (sep >= 0)
                    {
                        var tagEnd = line.IndexOf(']');
                        var tag = tagEnd >= 0 ? line[..(tagEnd + 1)] : "[CT]";
                        line = $"{tag}: {line[(sep + 4)..].Trim()}";
                    }
                }

                if (sb.Length > 0) sb.Append('\n');
                sb.Append(line);
            }

            if (Service.condition[ConditionFlag.BoundByDuty])
                sb.Append("\nIn instanced area: true");
            if (Service.condition[ConditionFlag.InCombat])
                sb.Append("\nIn combat: true");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            Service.pluginLog.Error(ex, "Failed to read chat panel.");
            return string.Empty;
        }
    }

    public unsafe nint GetActiveChatLogPanel()
    {
        var addon = (AddonChatLog*)Service.gameGui.GetAddonByName("ChatLog").Address;
        return addon == null ? 0 : addon->TabIndex;
    }
}
