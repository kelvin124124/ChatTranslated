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
        [GeneratedRegex(@"^\uE040\u0020?.*\u0020?\uE041$")]
        private static partial Regex AutoTranslateRegex();

        [GeneratedRegex(@"[\uE000-\uF8FF]+")]
        private static partial Regex SpecialCharacterRegex();

        [GeneratedRegex(@"[^\u0020-\u007E\uFF01-\uFF5E]+")]
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
            if (!Service.configuration.Enabled || sender.TextValue.Contains("[CT]") || !Service.configuration.ChatTypes.Contains(type))
                return;

            if (!Service.configuration.EnabledInDuty && Service.condition[ConditionFlag.BoundByDuty])
                return;

            var playerPayload = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
            string playerName = Sanitize(playerPayload?.PlayerName ?? sender.ToString());
            if (type == XivChatType.TellOutgoing)
                playerName = Sanitize(Service.clientState.LocalPlayer?.Name.ToString() ?? string.Empty);

            if (playerName == Sanitize(Service.clientState.LocalPlayer?.Name.ToString() ?? string.Empty))
            {
                if (Service.configuration.SendChatToDB == true)
                {
                    SeString _message = message;
                    Task.Run(() => ChatStore.SendToDB(_message.TextValue));
                }
                return;
            }

            string messageText = Regex.Replace(message.TextValue, @"\uE040(.*?)\uE041", string.Empty);
            string? filterReason = MessageFilter(playerName, messageText);
            if (filterReason != null)
            {
                Service.pluginLog.Debug($"Message filtered: {filterReason}");
                Service.mainWindow.PrintToOutput($"{playerName}: {message.TextValue}");
                return;
            }

            ProcessMessage(playerName, Sanitize(message.TextValue), type);
        }

        private string? MessageFilter(string playerName, string message)
        {
            if (AutoTranslateRegex().IsMatch(message))
                return "Auto-translate messages.";
            else if (IsMacroMessage(playerName))
                return "Macro messages.";
            else if (Sanitize(message.Trim()).Length < 2)
                return "Single character or empty message.";

            return null;
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

        private static void ProcessMessage(string playerName, string message, XivChatType type)
        {
            // Process Eng character messages if configured
            string messageText = Regex.Replace(message.TextValue, @"\uE040(.*?)\uE041", string.Empty);
            if (!NonEnglishRegex().IsMatch(messageText))
            {
                // Eng character detected
                if (Service.configuration.TranslateEn)
                {
                    Task.Run(() => Translator.TranslateChat(playerName, message, type));
                }
                else if (Service.configuration.TranslateFr || Service.configuration.TranslateDe)
                {
                    Task.Run(() => Translator.TranslateFrDeChat(playerName, message, type));
                }
                return;
            }

            // Likely Japanese -> Filter specific Japanese messages
            string? filteredMessage = JapaneseFilter(message);
            if (filteredMessage != null)
            {
                OutputTranslation(type, playerName, $"{message} || {filteredMessage}", "Japanese greeting message filtered.");
                return;
            }

            Task.Run(() => Translator.TranslateChat(playerName, message, type));
        }

        private static string? JapaneseFilter(string message)
        {
            if (JPWelcomeRegex().IsMatch(message))
                return "Let's do it!";
            else if (JPByeRegex().IsMatch(message))
                return "Good game!";
            else if (JPDomaRegex().IsMatch(message))
                return "It's okay!";

            return null;
        }

        public static void OutputTranslation(XivChatType type, string playerName, string message, string? logmessage = null)
        {
            Service.mainWindow.PrintToOutput($"{playerName}: {message}");
            if (logmessage != null)
                Service.pluginLog.Debug(logmessage);
            if (Service.configuration.ChatIntegration)
                Plugin.OutputChatLine(playerName, message, type);
        }

        public static string Sanitize(string input)
        {
            return SpecialCharacterRegex().Replace(input, string.Empty);
        }

        public void Dispose() => Service.chatGui.ChatMessage -= OnChatMessage;
    }
}
