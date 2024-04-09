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

            // get player name
            var playerPayload = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
            string playerName = Sanitize(playerPayload?.PlayerName ?? sender.ToString());
            if (type == XivChatType.TellOutgoing)
                playerName = Sanitize(Service.clientState.LocalPlayer?.Name.ToString() ?? string.Empty);

            string _message = message.TextValue;

            // check if message is from self
            if (playerName == Sanitize(Service.clientState.LocalPlayer?.Name.ToString() ?? string.Empty))
            {
                if (Service.configuration.SendChatToDB == true)
                {
                    Task.Run(() => ChatStore.SendToDB(_message));
                }
                return;
            }

            switch (Service.configuration.SelectedLanguageSelectionMode)
            {
                case Configuration.LanguageSelectionMode.Default:
                    string messageText = Sanitize(AutoTranslateRegex().Replace(_message, string.Empty));
                    if (NonEnglishRegex().IsMatch(messageText))
                        if (!isFilteredMessage(playerName, messageText))
                            if (!isJPFilteredMessage(type, playerName, _message))
                                Task.Run(() => TranslationHandler.TranslateChat(type, playerName, _message));
                    break;
                case Configuration.LanguageSelectionMode.CustomLanguages:
                    Task.Run(() => TranslationHandler.DetermineLangAndTranslate(type, playerName, _message));
                    break;
                case Configuration.LanguageSelectionMode.AllLanguages:
                    Task.Run(() => TranslationHandler.TranslateChat(type, playerName, _message));
                    break;
            }

            return;
        }

        private bool isFilteredMessage(string playerName, SeString message)
        {
            string? filterReason = null;
            if (message.TextValue.Trim().Length < 2)
                filterReason = "Single character or empty message.";
            else if (IsMacroMessage(playerName))
                filterReason = "Macro messages.";

            if (filterReason != null)
            {
                Service.pluginLog.Debug($"Message filtered: {filterReason}");
                Service.mainWindow.PrintToOutput($"{playerName}: {message.TextValue}");
                return true;
            }
            else
                return false;
        }

        private static bool isJPFilteredMessage(XivChatType type, string sender, string message)
        {
            string messageText = Sanitize(AutoTranslateRegex().Replace(message, string.Empty));

            if (JPWelcomeRegex().IsMatch(messageText))
            {
                TranslationHandler.OutputTranslation(type, sender, $"{message} || Let's do it!");
                return true;
            }
            else if (JPByeRegex().IsMatch(messageText))
            {
                TranslationHandler.OutputTranslation(type, sender, $"{message} || Good game!");
                return true;
            }
            else if (JPDomaRegex().IsMatch(messageText))
            {
                TranslationHandler.OutputTranslation(type, sender, $"{message} || It's okay!");
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

        public static string Sanitize(string input)
        {
            return SpecialCharacterRegex().Replace(input, string.Empty);
        }

        public void Dispose() => Service.chatGui.ChatMessage -= OnChatMessage;
    }
}
