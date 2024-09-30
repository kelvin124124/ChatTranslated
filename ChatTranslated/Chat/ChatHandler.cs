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
            if (isHandled)
                return;

            if (!Service.configuration.Enabled || sender.TextValue.Contains("[CT]") || !Service.configuration.SelectedChatTypes.Contains(type))
                return;

            if (!Service.configuration.EnabledInDuty && Service.condition[ConditionFlag.BoundByDuty])
                return;

            var playerPayload = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
            string playerName = Sanitize(playerPayload?.PlayerName ?? sender.ToString());
            string localPlayerName = Sanitize(Service.clientState.LocalPlayer?.Name.ToString() ?? string.Empty);
            if (type == XivChatType.TellOutgoing)
                playerName = localPlayerName;

            if (playerName == localPlayerName)
            {
                Service.mainWindow.PrintToOutput($"{playerName}: {message.TextValue}");
                return;
            }

            if (IsFilteredMessage(playerName, message.TextValue))
            {
                Service.mainWindow.PrintToOutput($"{playerName}: {message}");
                return;
            }

            var chatMessage = new Message(playerName, MessageSource.Chat, message, type);

            switch (Service.configuration.SelectedLanguageSelectionMode)
            {
                case Configuration.LanguageSelectionMode.Default:
                    if (ChatRegex.NonEnglishRegex().IsMatch(chatMessage.CleanedContent) && !IsJPFilteredMessage(chatMessage))
                        Task.Run(() => TranslationHandler.TranslateChat(chatMessage));
                    break;
                case Configuration.LanguageSelectionMode.CustomLanguages:
                    Task.Run(() => TranslationHandler.DetermineLangAndTranslate(chatMessage));
                    break;
                case Configuration.LanguageSelectionMode.AllLanguages:
                    Task.Run(() => TranslationHandler.TranslateChat(chatMessage));
                    break;
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

                TranslationHandler.OutputTranslation(chatMessage);
                return true;
            }
            if (ChatRegex.JPByeRegex().IsMatch(chatMessage.CleanedContent))
            {
                chatMessage.TranslatedContent = Resources.GGstr;

                TranslationHandler.OutputTranslation(chatMessage);
                return true;
            }
            if (ChatRegex.JPDomaRegex().IsMatch(chatMessage.CleanedContent))
            {
                chatMessage.TranslatedContent = Resources.DomaStr;

                TranslationHandler.OutputTranslation(chatMessage);
                return true;
            }

            return false;
        }

        public static string Sanitize(string input) => ChatRegex.SpecialCharacterRegex().Replace(input, " ");

        public void Dispose() => Service.chatGui.ChatMessage -= OnChatMessage;
    }
}
