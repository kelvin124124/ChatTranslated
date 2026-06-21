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

    private const int MaxContextLines = 10;

    public unsafe string GetChatMessageContext()
    {
        try
        {
            var panel = Service.gameGui.GetAddonByName($"ChatLogPanel_{GetActiveChatLogPanel()}");
            if (panel == 0) return string.Empty;

            var payloads = SeString.Parse((byte*)((AddonChatLogPanel*)panel.Address)->ChatText->GetText()).Payloads;

            // The panel renders each message as plain text — sender names and links already
            // appear once as TextPayloads. Messages are separated by '\r'; long ones are
            // soft-wrapped with a NewLinePayload followed by indent padding to strip.
            var sb = new StringBuilder();
            bool afterWrap = false;
            foreach (var payload in payloads)
            {
                switch (payload)
                {
                    case NewLinePayload:
                        afterWrap = true;
                        break;
                    // Tag interactive links; their display name/coords follow as the next TextPayload.
                    case ItemPayload:
                        sb.Append("[Item] ");
                        break;
                    case MapLinkPayload:
                        sb.Append("[Map] ");
                        break;
                    case QuestPayload:
                        sb.Append("[Quest] ");
                        break;
                    case StatusPayload:
                        sb.Append("[Status] ");
                        break;
                    case TextPayload { Text: { } text }:
                        sb.Append(afterWrap ? text.TrimStart() : text);
                        afterWrap = false;
                        break;
                }
            }

            var lines = PrivateUseRegex().Replace(sb.ToString(), string.Empty)
                .Split('\r', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var context = string.Join('\n', lines.TakeLast(MaxContextLines).Select(CollapseOwnEcho));

            if (Service.condition[ConditionFlag.BoundByDuty]) context += "\nIn instanced area: true";
            if (Service.condition[ConditionFlag.InCombat]) context += "\nIn combat: true";

            return context;
        }
        catch (Exception ex)
        {
            Service.pluginLog.Error(ex, "Failed to read chat panel.");
            return string.Empty;
        }
    }

    // Our own translations are echoed back as "[CT] Sender: original || translated";
    // keep only the translation so context isn't bloated with duplicated originals.
    private static string CollapseOwnEcho(string line)
    {
        if (!line.StartsWith("[CT] ", StringComparison.Ordinal)) return line;
        var colon = line.IndexOf(':');
        var sep = line.IndexOf(" || ", StringComparison.Ordinal);
        return colon >= 0 && sep > colon ? $"{line[..colon]}: {line[(sep + 4)..]}" : line;
    }

    [GeneratedRegex(@"[\uE000-\uF8FF]+")]
    private static partial Regex PrivateUseRegex();

    public unsafe nint GetActiveChatLogPanel()
    {
        var addon = (AddonChatLog*)Service.gameGui.GetAddonByName("ChatLog").Address;
        return addon == null ? 0 : addon->TabIndex;
    }
}
