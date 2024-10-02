using ChatTranslated.Chat;
using ChatTranslated.Localization;
using ChatTranslated.Translate;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChatTranslated.Utils
{
    internal partial class ChatHandler
    {
        private readonly Dictionary<string, DateTime> lastMessageTime = [];

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
            string playerName = Sanitize(playerPayload?.PlayerName ?? sender.ToString()).Trim();
            string localPlayerName = Sanitize(Service.clientState.LocalPlayer?.Name.ToString() ?? string.Empty).Trim();
            if (type == XivChatType.TellOutgoing)
                playerName = localPlayerName;

            Service.pluginLog.Debug($"Chat message from {playerName}: {message.TextValue}" +
                $"local player: {localPlayerName}, match: {playerName == localPlayerName}");

            if (playerName == localPlayerName)
            {
                Service.mainWindow.PrintToOutput($"{playerName}: {message.TextValue}");
                return;
            }

            var chatMessage = new Message(playerName, MessageSource.Chat, message, type);

            if (IsFilteredMessage(playerName, chatMessage.CleanedContent) || IsJPFilteredMessage(chatMessage))
            {
                OutputMessage(chatMessage);
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
                await Translator.TranslateMessage(chatMessage);
                OutputMessage(chatMessage);
            }
            else
            {
                Service.mainWindow.PrintToOutput($"{chatMessage.Sender}: {chatMessage.CleanedContent}");
            }
        }

        private async Task<bool> IsCustomSourceLanguage(Message chatMessage)
        {
            var language = await Translator.DetermineLanguage(chatMessage.CleanedContent);
            return Service.configuration.SelectedSourceLanguages.Contains(language);
        }

        internal static void OutputMessage(Message chatMessage)
        {
            Service.mainWindow.PrintToOutput($"{chatMessage.Sender}: {chatMessage.TranslatedContent ?? chatMessage.OriginalContent.TextValue}");

            if (Service.configuration.ChatIntegration)
            {
                string outputStr = Service.configuration.ChatIntegration_HideOriginal
                    ? chatMessage.TranslatedContent!
                    : $"{chatMessage.OriginalContent.TextValue} || {chatMessage.TranslatedContent}";

                Plugin.OutputChatLine(chatMessage.Type ?? XivChatType.Say, chatMessage.Sender, outputStr);
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
            var now = DateTime.Now;
            if (lastMessageTime.TryGetValue(playerName, out var lastMsgTime) && (now - lastMsgTime).TotalMilliseconds < 600)
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

                OutputMessage(chatMessage);
                return true;
            }
            if (ChatRegex.JPByeRegex().IsMatch(chatMessage.CleanedContent))
            {
                chatMessage.TranslatedContent = Resources.GGstr;

                OutputMessage(chatMessage);
                return true;
            }
            if (ChatRegex.JPDomaRegex().IsMatch(chatMessage.CleanedContent))
            {
                chatMessage.TranslatedContent = Resources.DomaStr;

                OutputMessage(chatMessage);
                return true;
            }

            return false;
        }

        public static string Sanitize(string input) => ChatRegex.SpecialCharacterRegex().Replace(input, " ");

        public void Dispose() => Service.chatGui.ChatMessage -= OnChatMessage;
    }
}
