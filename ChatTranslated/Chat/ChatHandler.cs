using ChatTranslated.Localization;
using ChatTranslated.Translate;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatTranslated.Utils
{
    internal partial class ChatHandler
    {
        [GeneratedRegex(@"\uE040\u0020(.*?)\u0020\uE041")]
        public static partial Regex AutoTranslateRegex();

        [GeneratedRegex(@"[\uE000-\uF8FF]+")]
        private static partial Regex SpecialCharacterRegex();

        [GeneratedRegex(@"(?<![\u0020-\u007E\u2000-\u21FF\u3000-\u303F\uFF10-\uFF5A])[^(\u0020-\u007E\u2000-\u21FF\u2501\u3000-\u303F\uFF10-\uFF5A)]{2,}(?![\u0020-\u007E\u2000-\u21FF\u3000-\u303F\uFF10-\uFF5A])")]
        private static partial Regex NonEnglishRegex();

        [GeneratedRegex(@"^よろしくお(願|ねが)いします[\u3002\uFF01!]*")]
        private static partial Regex JPWelcomeRegex();

        [GeneratedRegex(@"^お疲れ様でした[\u3002\uFF01!]*")]
        private static partial Regex JPByeRegex();

        [GeneratedRegex(@"\b(どまい?|ドマ|どんまい)(です)?[\u3002\uFF01!]*\b")]
        private static partial Regex JPDomaRegex();

        private readonly Dictionary<string, DateTime> lastMessageTime = [];

        public ChatHandler()
        {
            Service.chatGui.ChatMessage += OnChatMessage;
        }

        private void OnChatMessage(XivChatType type, uint _, ref SeString sender, ref SeString message, ref bool _1)
        {
            if (!Service.configuration.Enabled || sender.TextValue.Contains("[CT]") || !Service.configuration.SelectedChatTypes.Contains(type))
                return;

            if (!Service.configuration.EnabledInDuty && Service.condition[ConditionFlag.BoundByDuty])
                return;

            var playerPayload = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
            string playerName = Sanitize(playerPayload?.PlayerName ?? sender.ToString());
            if (type == XivChatType.TellOutgoing)
                playerName = Sanitize(Service.clientState.LocalPlayer?.Name.ToString() ?? string.Empty);

            if (playerName == Sanitize(Service.clientState.LocalPlayer?.Name.ToString() ?? string.Empty))
            {
                Service.mainWindow.PrintToOutput($"{playerName}: {message.TextValue}");
                if (Service.configuration.SendChatToDB)
                {
                    string _messageText = RemoveNonTextPayloads(message);
                    Task.Run(() => ChatStore.SendToDB(_messageText));
                }
                return;
            }

            string messageText = RemoveNonTextPayloads(message);
            if (IsFilteredMessage(playerName, messageText))
            {
                Service.mainWindow.PrintToOutput($"{playerName}: {message.TextValue}");
                return;
            }

            var capturedMessage = message;
            switch (Service.configuration.SelectedLanguageSelectionMode)
            {
                case Configuration.LanguageSelectionMode.Default:
                    if (NonEnglishRegex().IsMatch(messageText) && !IsJPFilteredMessage(type, playerName, messageText))
                        Task.Run(() => TranslationHandler.TranslateChat(type, playerName, capturedMessage.TextValue));
                    break;
                case Configuration.LanguageSelectionMode.CustomLanguages:
                    Task.Run(() => TranslationHandler.DetermineLangAndTranslate(type, playerName, capturedMessage));
                    break;
                case Configuration.LanguageSelectionMode.AllLanguages:
                    Task.Run(() => TranslationHandler.TranslateChat(type, playerName, capturedMessage.TextValue));
                    break;
            }
        }

        public static string RemoveNonTextPayloads(SeString inputMsg)
        {
            var message = new SeString(new List<Payload>());
            for (int i = 0; i < inputMsg.Payloads.Count; i++)
            {
                var payload = inputMsg.Payloads[i];
                switch (payload)
                {
                    case TextPayload textPayload:
                        message.Payloads.Add(textPayload);
                        break;
                    case PlayerPayload _:
                        i += 2;
                        break;
                    case ItemPayload _:
                    case QuestPayload _:
                    case MapLinkPayload _:
                        i += 7;
                        break;
                    case StatusPayload _:
                        i += 10;
                        break;
                    case PartyFinderPayload _:
                        i += 6;
                        break;
                }
            }
            return Sanitize(AutoTranslateRegex().Replace(message.TextValue, string.Empty));
        }

        private bool IsFilteredMessage(string playerName, string messageText)
        {
            if (messageText.Trim().Length < 2 || IsMacroMessage(playerName))
            {
                Service.pluginLog.Debug("Message filtered: " + (messageText.Trim().Length < 2 ? "Single character or empty message." : "Macro messages."));
                return true;
            }
            return false;
        }

        private static bool IsJPFilteredMessage(XivChatType type, string sender, string message)
        {
            string messageText = Sanitize(AutoTranslateRegex().Replace(message, string.Empty));

            if (JPWelcomeRegex().IsMatch(messageText))
            {
                TranslationHandler.OutputTranslation(type, sender, $"{message} || " + Resources.WelcomeStr);
                return true;
            }
            if (JPByeRegex().IsMatch(messageText))
            {
                TranslationHandler.OutputTranslation(type, sender, $"{message} || " + Resources.GGstr);
                return true;
            }
            if (JPDomaRegex().IsMatch(messageText))
            {
                TranslationHandler.OutputTranslation(type, sender, $"{message} || " + Resources.DomaStr);
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

        public static string Sanitize(string input) => SpecialCharacterRegex().Replace(input, " ");

        public void Dispose() => Service.chatGui.ChatMessage -= OnChatMessage;
    }
}
