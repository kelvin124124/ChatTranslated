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
    [System.Text.RegularExpressions.GeneratedRegex(@"^<se\.\d+>$")]
    private static partial System.Text.RegularExpressions.Regex SoundMacroRegex();

    private readonly Dictionary<string, int> lastMessageTime = [];

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
            string localPlayerName = Service.playerState.CharacterName.ToString();
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

            if (PhraseFilter.TryFilter(chatMessage, out var detectedIso))
                return;

            string? iso = detectedIso;

            // skip detection when phrase filter already identified the language
            if (iso == null)
            {
                // low reliability: translate and detect in parallel, drops when detected as known language
                // mid reliability: consult google, then act accordingly
                // high reliability: act accordingly
                bool hasEnTokens = PhraseFilter.HasEnToken(chatMessage.CleanedContent);
                var (reliability, linguaIso) = await LanguageDetector.ComputeReliabilityAsync(chatMessage.CleanedContent, type, hasEnTokens);
                iso = linguaIso;

                if (reliability < 0.25)
                {
                    chatMessage.Context = GetChatMessageContext();
                    var t = TranslationHandler.TranslateMessage(chatMessage);
                    var d = LanguageDetector.DetectIsoAsync(chatMessage);
                    await Task.WhenAll(t, d);
                    iso = d.Result ?? linguaIso;
                }
                else if (reliability < 0.40)
                {
                    iso = await LanguageDetector.DetectIsoAsync(chatMessage) ?? linguaIso;
                }
            }

            // emoticons usually classified to rare languages in Google translate
            // if iso not in supported languages, drop the message to avoid mistranslations
            if (iso == null || !LanguageDetector.ValidIsoCodes.Contains(iso) || LanguageDetector.IsKnownIsoCode(iso))
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

            var lines = sb.ToString().Split('\r');
            sb.Clear();

            for (int j = Math.Max(0, lines.Length - 10); j < lines.Length; j++)  // Max ctx lines: 10
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(lines[j].Trim());
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

    private bool IsFilteredMessage(string sender, string message)
    {
        if (message.Trim().Length < 2)
        {
            Service.pluginLog.Debug("Message filtered: Single character or empty message.");
            return true;
        }
        if (SoundMacroRegex().IsMatch(message.Trim()))
        {
            Service.pluginLog.Debug("Message filtered: Sound macro.");
            return true;
        }
        if (IsMacroMessage(sender))
        {
            Service.pluginLog.Debug("Message filtered: Macro messages.");
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

    public void Dispose() => Service.chatGui.ChatMessage -= OnChatMessage;
}
