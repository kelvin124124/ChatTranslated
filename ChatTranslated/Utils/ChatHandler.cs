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
    internal class ChatHandler
    {
        private static readonly Regex AutoTranslateRegex = new Regex(@"^\uE040\u0020?.*\u0020?\uE041$", RegexOptions.Compiled);
        private static readonly Regex SpecialCharacterRegex = new Regex(@"[\uE000-\uF8FF]+", RegexOptions.Compiled);
        private static readonly Regex NonEnglishRegex = new Regex(@"[^\u0020-\u007E\uFF01-\uFF5E\p{S}]+", RegexOptions.Compiled);
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

            string playerName = GetPlayerName(sender, type);

            if (ShouldFilterMessage(playerName, message.TextValue, type))
                return;

            string sanitizedMessage = Sanitize(message.TextValue);

            ProcessMessage(playerName, sanitizedMessage, type);
        }

        private static string GetPlayerName(SeString sender, XivChatType type)
        {
            if (type == XivChatType.TellOutgoing && Service.clientState?.LocalPlayer != null)
                return Sanitize(Service.clientState.LocalPlayer.Name.ToString());

            var playerPayload = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
            return Sanitize(playerPayload?.PlayerName ?? sender.ToString());
        }

        private bool ShouldFilterMessage(string playerName, string message, XivChatType type)
        {
            if (IsMacroMessage(playerName))
            {
                LogAndPrint(playerName, message, $"Macro filtered.", type, includeInChat: false);
                return true;
            }

            if (AutoTranslateRegex.IsMatch(message) || playerName == Sanitize(Service.clientState?.LocalPlayer?.Name.ToString() ?? ""))
            {
                LogAndPrint(playerName, message, "Message filtered by standard rules.", type, includeInChat: false);
                return true;
            }

            if (message.Length == 1)
            {
                LogAndPrint(playerName, message, "Single character message filtered.", type, includeInChat: false);
                return true;
            }

            return false;
        }

        private static void ProcessMessage(string playerName, string message, XivChatType type)
        {
            if (!NonEnglishRegex.IsMatch(message))
            {
                // Eng character detected
                if (Service.configuration.TranslateEn)
                {
                    Task.Run(() => Translator.TranslateChat(playerName, message, type));
                    return;
                }
                else if (Service.configuration.TranslateFrDe)
                {
                    Task.Run(() => Translator.TranslateFrDeChat(playerName, message, type));
                    return;
                }
                return;
            }

            // Filter specific Japanese messages
            if (FilterJapaneseGreetings(playerName, message, type))
                return;

            Task.Run(() => Translator.TranslateChat(playerName, message, type));
        }

        private static bool FilterJapaneseGreetings(string playerName, string message, XivChatType type)
        {
            if (JPWelcomeRegex.IsMatch(message))
            {
                LogAndPrint(playerName, message, "Welcome message filtered.", type, "Let's do it!");
                return true;
            }
            if (JPByeRegex.IsMatch(message))
            {
                LogAndPrint(playerName, message, "Bye message filtered.", type, "Good game!");
                return true;
            }
            if (JPDomaRegex.IsMatch(message))
            {
                LogAndPrint(playerName, message, "Doma message filtered.", type, "It's okay!");
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

        public static void LogAndPrint(string playerName, string message, string logMessage, XivChatType type, string? response = null, bool includeInChat = true)
        {
            Service.mainWindow.PrintToOutput($"{playerName}: {message}");
            Service.pluginLog.Debug(logMessage);
            if (includeInChat && Service.configuration.ChatIntegration && response != null)
                Plugin.OutputChatLine(playerName, $"{message} || {response}", type);
        }

        public static string Sanitize(string input)
        {
            if (Service.configuration.SelectedMode == Configuration.Mode.GPTProxy)
            {
                input = input.Replace("\uE040", "{[");
                input = input.Replace("\uE041", "]}");
            }

            input = SpecialCharacterRegex.Replace(input, "");

            return input;
        }

        public void Dispose() => Service.chatGui.ChatMessage -= OnChatMessage;
    }
}

