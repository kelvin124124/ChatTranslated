using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatTranslated.Utils
{
    internal class ChatHandler
    {
        private static readonly Regex AutoTranslateRegex = new Regex(@"^\uE040\u0020?.*\u0020?\uE041$", RegexOptions.Compiled);
        private static readonly Regex SpecialCharacterRegex = new Regex(@"[\uE000-\uF8FF]+", RegexOptions.Compiled);
        private static readonly Regex NonEnglishRegex = new Regex(@"[^\u0020-\u007E\uFF01-\uFF5E]+", RegexOptions.Compiled);
        private static readonly Regex JPWelcomeRegex = new Regex(@"^よろしくお(願|ねが)いします[\u3002\uFF01!]*", RegexOptions.Compiled);
        private static readonly Regex JPByeRegex = new Regex(@"^お疲れ様でした[\u3002\uFF01!]*", RegexOptions.Compiled);
        private static readonly Regex JPDomaRegex = new Regex(@"\b(どまい?|ドマ|どんまい)(です)?[\u3002\uFF01!]*\b", RegexOptions.Compiled);

        private readonly Dictionary<string, DateTime> lastMessageTime = new Dictionary<string, DateTime>();

        public ChatHandler()
        {
            Service.chatGui.ChatMessage += OnChatMessage;
        }

        private void OnChatMessage(XivChatType type, uint _, ref SeString sender, ref SeString message, ref bool _1)
        {
            if (sender.TextValue.Contains("[CT]") || !Service.configuration.ChatTypes.Contains(type))
                return;

            string playerName = Sanitize(sender.ToString());
            if (type == XivChatType.TellOutgoing)
                playerName = Sanitize(Service.clientState.LocalPlayer!.Name.ToString() ?? string.Empty);

            if (playerName == Sanitize(Service.clientState.LocalPlayer!.Name.ToString() ?? string.Empty))
            {
                if (Service.configuration.SendChatToDB == true)
                {
                    SeString _message = message;
                    Task.Run(() => ChatStore.SendToDB(_message.TextValue));
                }
                return;
            }

            string? filterReason = MessageFilter(playerName, message.TextValue);
            if (filterReason != null)
            {
                Service.pluginLog.Info($"Message filtered: {filterReason}");
                return;
            }

            ProcessMessage(playerName, Sanitize(message.TextValue), type);
        }

        private string? MessageFilter(string playerName, string message)
        {
            if (AutoTranslateRegex.IsMatch(message))
                return "Auto-translate messages.";
            else if (IsMacroMessage(playerName))
                return "Macro messages.";
            else if (message.Trim().Length == 1)
                return "Single character message.";

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
            if (!NonEnglishRegex.IsMatch(message))
            {
                // Eng character detected
                if (Service.configuration.TranslateEn)
                {
                    Task.Run(() => Translator.TranslateChat(playerName, message, type));
                }
                else if (Service.configuration.TranslateFrDe)
                {
                    Task.Run(() => Translator.TranslateFrDeChat(playerName, message, type));
                }
                return;
            }

            // Likely Japanese -> Filter specific Japanese messages
            string? filteredMessage = JapaneseFilter(message);
            if (filteredMessage != null)
            {
                OutputTranslation(type, playerName, filteredMessage, "Japanese greeting message filtered.");
                return;
            }

            Task.Run(() => Translator.TranslateChat(playerName, message, type));
        }

        private static string? JapaneseFilter(string message)
        {
            if (JPWelcomeRegex.IsMatch(message))
                return "Let's do it!";
            else if (JPByeRegex.IsMatch(message))
                return "Good game!";
            else if (JPDomaRegex.IsMatch(message))
                return "It's okay!";

            return null;
        }

        public static void OutputTranslation(XivChatType type, string playerName, string message, string? logmessage)
        {
            Service.mainWindow.PrintToOutput($"{playerName}: {message}");
            if (logmessage != null)
                Service.pluginLog.Debug(logmessage);
            if (Service.configuration.ChatIntegration)
                Plugin.OutputChatLine(playerName, message, type);
        }

        public static string Sanitize(string input)
        {
            return SpecialCharacterRegex.Replace(input, "");
        }

        public void Dispose() => Service.chatGui.ChatMessage -= OnChatMessage;
    }
}
