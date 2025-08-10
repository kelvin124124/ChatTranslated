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
using System.Threading.Tasks;

namespace ChatTranslated.Chat
{
    internal partial class ChatHandler
    {
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
            if (!Service.configuration.Enabled || sender.TextValue.Contains("[CT]") || !Service.configuration.SelectedChatTypes.Contains(type))
                return;

            if (!Service.configuration.EnabledInDuty && Service.condition[ConditionFlag.BoundByDuty])
                return;

            var playerPayload = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
            string playerName = playerPayload?.PlayerName ?? sender.ToString();
            string localPlayerName = Service.clientState.LocalPlayer?.Name.ToString() ?? string.Empty;
            if (type == XivChatType.TellOutgoing)
                playerName = localPlayerName;

            if (playerName.EndsWith(localPlayerName))
            {
                Service.mainWindow.PrintToOutput($"{playerName}: {message.TextValue}");
                return;
            }

            var chatMessage = new Message(playerName, MessageSource.Chat, message, type);
            chatMessage.Context = GetChatMessageContext();

            if (IsFilteredMessage(playerName, chatMessage.CleanedContent) || IsJPFilteredMessage(chatMessage))
            {
                Service.mainWindow.PrintToOutput($"{playerName}: {message.TextValue}");
                return;
            }

            bool needsTranslation = Service.configuration.SelectedLanguageSelectionMode switch
            {
                Configuration.LanguageSelectionMode.Default => ChatRegex.NonEnglishRegex().IsMatch(chatMessage.CleanedContent),
                Configuration.LanguageSelectionMode.CustomLanguages => await IsCustomSourceLanguage(chatMessage),
                Configuration.LanguageSelectionMode.AllLanguages => true,
                _ => false
            };

            if (needsTranslation)
            {
                await TranslationHandler.TranslateMessage(chatMessage);
                OutputMessage(chatMessage, type);
            }
            else
            {
                Service.mainWindow.PrintToOutput($"{chatMessage.Sender}: {chatMessage.CleanedContent}");
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
                var stack = new Stack<string>();

                if (Service.condition[ConditionFlag.BoundByDuty])
                    stack.Push("\nIn instanced area: true");
                if (Service.condition[ConditionFlag.InCombat])
                    stack.Push("\nIn combat: true");

                int lines = 0;
                for (int i = payloads.Count - 1; i >= 0 && lines < 15; i--)
                {
                    switch (payloads[i])
                    {
                        case TextPayload text when !string.IsNullOrEmpty(text.Text):
                            int newLines = text.Text.Count(c => c == '\r');
                            if (lines + newLines <= 15)
                            {
                                stack.Push(text.Text);
                                lines += newLines;
                            }
                            break;

                        case PlayerPayload player:
                            stack.Push(player.PlayerName);
                            // keep decrementing i to skip over any UI payloads
                            while (--i >= 0 && payloads[i] is UIForegroundPayload or UIGlowPayload or RawPayload) { }
                            continue;

                        case ItemPayload:
                            stack.Push("[Item]");
                            while (--i >= 0 && payloads[i] is UIForegroundPayload or UIGlowPayload or RawPayload) { }
                            continue;

                        case QuestPayload:
                            stack.Push("[Quest]");
                            while (--i >= 0 && payloads[i] is UIForegroundPayload or UIGlowPayload or RawPayload) { }
                            continue;

                        case MapLinkPayload:
                            stack.Push("[Map]");
                            while (--i >= 0 && payloads[i] is UIForegroundPayload or UIGlowPayload or RawPayload) { }
                            continue;

                        case StatusPayload:
                            stack.Push("[Status]");
                            while (--i >= 0 && payloads[i] is UIForegroundPayload or UIGlowPayload or RawPayload) { }
                            continue;

                        case PartyFinderPayload:
                            stack.Push("[PF]");
                            while (--i >= 0 && payloads[i] is UIForegroundPayload or UIGlowPayload or RawPayload) { }
                            continue;
                    }
                }

                return ChatRegex.AutoTranslateRegex().Replace(string.Concat(stack), string.Empty);
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

        private static async Task<bool> IsCustomSourceLanguage(Message chatMessage)
        {
            var language = await TranslationHandler.DetermineLanguage(chatMessage.CleanedContent);
            return Service.configuration.SelectedSourceLanguages.Contains(language);
        }

        internal static void OutputMessage(Message chatMessage, XivChatType type = XivChatType.Say)
        {
            if (chatMessage.OriginalContent.TextValue == chatMessage.TranslatedContent
                && chatMessage.Source != MessageSource.MainWindow) // no need to output if translation is the same
            {
                Service.pluginLog.Info("Translation is the same as original. Skipping output.");
                return;
            }

            string outputStr = Service.configuration.ChatIntegration_HideOriginal
                ? chatMessage.TranslatedContent!
                : $"{chatMessage.OriginalContent.TextValue} || {chatMessage.TranslatedContent}";

            Service.mainWindow.PrintToOutput($"{chatMessage.Sender}: {outputStr}");

            if (Service.configuration.ChatIntegration)
            {
                Plugin.OutputChatLine(type, chatMessage.Sender, outputStr);
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

        public static string Sanitize(string input) => ChatRegex.SpecialCharacterRegex().Replace(input, "*");

        public void Dispose() => Service.chatGui.ChatMessage -= OnChatMessage;
    }
}
