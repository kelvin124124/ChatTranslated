using ChatTranslated.Localization;
using ChatTranslated.Translate;
using ChatTranslated.Utils;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatTranslated.Chat;

internal partial class ChatHandler
{
    private readonly Dictionary<string, int> lastMessageTime = [];
    private readonly Dictionary<XivChatType, (long Tick, string? Iso)> _lastChannelDetection = [];

    public ChatHandler()
    {
        Service.chatGui.ChatMessage += OnChatMessage;
    }

    private void OnChatMessage(XivChatType type, int _, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (isHandled) return;
        HandleChatMessage(type, sender, message);
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
            string localPlayerName = Service.playerState.CharacterName.ToString() ?? string.Empty;
            if (type == XivChatType.TellOutgoing)
                playerName = localPlayerName;

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

            if (IsJPFilteredMessage(chatMessage))
                return;

            // compute confidence score for Lingua's reliability on this message
            var    (linguaScore, linguaIso) = await Task.Run(() => LinguaDetector.GetLinguaResult(chatMessage.CleanedContent));
            double lengthFactor  = Math.Clamp(chatMessage.CleanedContent.Length / 20.0, 0.0, 1.0);
            double channelBoost  = GetChannelBoost(type, linguaIso); // +decay if Google agrees with Lingua, -decay if it disagrees
            double confidence    = Math.Min(1.0, linguaScore * (0.5 + 0.5 * lengthFactor) + channelBoost * 0.5);

            Service.pluginLog.Debug($"Confidence for '{chatMessage.CleanedContent}': {confidence:F2} (lingua={linguaScore:F2} [{linguaIso ?? "?"}], length={lengthFactor:F2}, channelBoost={channelBoost:F2})");

            if (confidence >= 0.65)
            {
                // High: Lingua probably correct; skip Google detection; act on result
                if (!LinguaDetector.IsKnownIsoCode(linguaIso))
                    Service.mainWindow.PrintToOutput($"{chatMessage.Sender}: {chatMessage.CleanedContent}");
                else
                {
                    chatMessage.Context = GetChatMessageContext();
                    await TranslationHandler.TranslateMessage(chatMessage);
                    OutputMessage(chatMessage, type);
                }
            }
            else if (confidence >= 0.35)
            {
                // Medium: Google detect -> translate only if needed, update channel cache
                string? iso = await TranslationHandler.DetectIsoAsync(chatMessage.CleanedContent);
                _lastChannelDetection[type] = (Environment.TickCount64, iso);
                if (LinguaDetector.IsKnownIsoCode(iso))
                {
                    Service.mainWindow.PrintToOutput($"{chatMessage.Sender}: {chatMessage.CleanedContent}");
                }
                else
                {
                    chatMessage.Context = GetChatMessageContext();
                    await TranslationHandler.TranslateMessage(chatMessage);
                    OutputMessage(chatMessage, type);
                }
            }
            else
            {
                // Low: translate and detect in parallel; discard if Google says known
                chatMessage.Context = GetChatMessageContext();
                var translateTask = TranslationHandler.TranslateMessage(chatMessage);
                var detectTask    = TranslationHandler.DetectIsoAsync(chatMessage.CleanedContent);
                await Task.WhenAll(translateTask, detectTask);

                _lastChannelDetection[type] = (Environment.TickCount64, detectTask.Result);
                if (LinguaDetector.IsKnownIsoCode(detectTask.Result))
                {
                    Service.mainWindow.PrintToOutput($"{chatMessage.Sender}: {chatMessage.CleanedContent}");
                }
                else
                    OutputMessage(chatMessage, type);
            }
        }
        catch (Exception ex)
        {
            Service.pluginLog.Error(ex, "Error processing chat message.");
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

            for (int i = Math.Max(0, payloads.Count - 300); i < payloads.Count; i++)
            {
                switch (payloads[i])
                {
                    case TextPayload textPayload:
                        sb.Append(textPayload.Text);
                        break;

                    case PlayerPayload playerPayload:
                        sb.Append($"[{playerPayload.PlayerName}]");
                        i += 2;
                        break;

                    case ItemPayload:
                        sb.Append("[Item] ");
                        i += 5;
                        break;

                    case QuestPayload:
                        sb.Append("[Quest] ");
                        i += 7;
                        break;

                    case MapLinkPayload:
                        sb.Append("[Map] ");
                        i += 7;
                        break;

                    case StatusPayload:
                        sb.Append("[Status] ");
                        i += 10;
                        break;

                    case PartyFinderPayload: // does not need to be tagged
                        i += 6;
                        break;

                    case AutoTranslatePayload:
                        i += 2;
                        break;
                }
            }

            var lines = sb.ToString()
                .Split('\r')
                .TakeLast(15)
                .Select(line => line.Trim())
                .ToList();

            if (Service.condition[ConditionFlag.BoundByDuty])
                lines.Add("In instanced area: true");
            if (Service.condition[ConditionFlag.InCombat])
                lines.Add("In combat: true");

            return string.Join('\n', lines);
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

    internal static void OutputMessage(Message chatMessage, XivChatType type = XivChatType.Say)
    {
        if (chatMessage.OriginalText == chatMessage.TranslatedContent
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

    private bool IsFilteredMessage(string sender, string message)
    {
        if (message.Trim().Length < 2 || IsMacroMessage(sender))
        {
            Service.pluginLog.Debug("Message filtered: " + (message.Trim().Length < 2
                ? "Single character or empty message."
                : "Macro messages."));
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
                .Where(kv => now - kv.Value > 10000) // 10s
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in keysToRemove)
                lastMessageTime.Remove(key);
        }

        if (lastMessageTime.TryGetValue(playerName, out int lastMsgTime) && now - lastMsgTime < 650) // 0.65s
        {
            lastMessageTime[playerName] = now;
            return true;
        }
        lastMessageTime[playerName] = now;
        return false;
    }

    private static bool IsJPFilteredMessage(Message chatMessage)
    {
        if (ChatRegex.JPWelcomeRegex().IsMatch(chatMessage.CleanedContent))
        {
            chatMessage.TranslatedContent = Resources.WelcomeStr;

            OutputMessage(chatMessage, chatMessage.Type);
            return true;
        }
        if (ChatRegex.JPByeRegex().IsMatch(chatMessage.CleanedContent))
        {
            chatMessage.TranslatedContent = Resources.GGstr;

            OutputMessage(chatMessage, chatMessage.Type);
            return true;
        }
        if (ChatRegex.JPDomaRegex().IsMatch(chatMessage.CleanedContent))
        {
            chatMessage.TranslatedContent = Resources.DomaStr;

            OutputMessage(chatMessage, chatMessage.Type);
            return true;
        }

        return false;
    }

    // Returns a decayed boost in [-1, +1]: positive if Google's cached detection agrees with Lingua, negative if it disagrees.
    private double GetChannelBoost(XivChatType channel, string? linguaIso)
    {
        if (linguaIso == null) return 0.0;
        if (!_lastChannelDetection.TryGetValue(channel, out var cache) || cache.Iso == null) return 0.0;

        double elapsedMin = (Environment.TickCount64 - cache.Tick) / 60_000.0;
        if (elapsedMin >= 5) { _lastChannelDetection.Remove(channel); return 0.0; }

        double decay = Math.Exp(-elapsedMin * Math.Log(2) / 2.5);
        return cache.Iso == linguaIso ? decay : -decay;
    }

    public void Dispose() => Service.chatGui.ChatMessage -= OnChatMessage;
}
