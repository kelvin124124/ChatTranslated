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
            if (sender.TextValue.Contains("[CT]"))
                return;

            if (Service.configuration.ChatTypes.Contains(type))
            {
                var playerPayload = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
                string playerName = Sanitize(playerPayload?.PlayerName ?? sender.ToString());

                // fix outgoing tell messages
                if (type == XivChatType.TellOutgoing && Service.clientState?.LocalPlayer != null)
                {
                    playerName = Sanitize(Service.clientState.LocalPlayer.Name.ToString());
                }

                // return if message is entirely auto-translate
                // return if message is from self
                if (AutoTranslateRegex.IsMatch(message.TextValue)
                    || playerName == Sanitize(Service.clientState?.LocalPlayer?.Name.ToString() ?? ""))
                {
                    Service.mainWindow.PrintToOutput($"{playerName}: {message}");
                    Service.pluginLog.Debug("Message filtered by standard rules.");
                    return;
                };

                // Filter macros
                var now = DateTime.Now;
                if (lastMessageTime.TryGetValue(playerName, out var lastMsgTime))
                {
                    var interval = (now - lastMsgTime).TotalMilliseconds;
                    lastMessageTime[playerName] = now;
                    if (interval < 600)
                    {
                        Service.mainWindow.PrintToOutput($"{playerName}: {message}");
                        Service.pluginLog.Debug($"Macro filtered. {interval}ms");
                        return;
                    }
                }
                else
                {
                    lastMessageTime[playerName] = now;
                }

                string _message = Sanitize(message.TextValue);

                bool isEnglishChar = !NonEnglishRegex.IsMatch(message.TextValue);
                // Possible FrDe message
                if (Service.configuration.TranslateFrDe && isEnglishChar)
                {
                    // Translate French and German, reutrn if message is in English
                    Task.Run(() => Translator.TranslateFrDe(playerName, _message, type));
                }
                else
                {
                    if (isEnglishChar) return;

                    // likely Japanese
                    // JP players like to use these, so filter them
                    if (JPWelcomeRegex.IsMatch(message.TextValue))
                    {
                        Service.pluginLog.Debug($"Welcome message filtered.");
                        Service.mainWindow.PrintToOutput($"{playerName}: Let's do it!");
                        if (Service.configuration.ChatIntegration)
                            Plugin.OutputChatLine(playerName, $"{message} || Let's do it!", type);
                        return;
                    }
                    if (JPByeRegex.IsMatch(message.TextValue))
                    {
                        Service.pluginLog.Debug($"Bye message filtered.");
                        Service.mainWindow.PrintToOutput($"{playerName}: Good game!");
                        if (Service.configuration.ChatIntegration)
                            Plugin.OutputChatLine(playerName, $"{message} || Good game!", type);
                        return;
                    }
                    if (JPDomaRegex.IsMatch(message.TextValue))
                    {
                        Service.pluginLog.Debug($"Doma message filtered.");
                        Service.mainWindow.PrintToOutput($"{playerName}: It's okay!");
                        if (Service.configuration.ChatIntegration)
                            Plugin.OutputChatLine(playerName, $"{message} || It's okay!", type);
                        return;
                    }

                    // Normal translate operation
                    Task.Run(() => Translator.Translate(playerName, _message, type));
                }
            }
        }

        private static string Sanitize(string input)
        {
            return SpecialCharacterRegex.Replace(input, "");
        }

        public void Dispose()
        {
            Service.chatGui.ChatMessage -= OnChatMessage;
        }
    }
}
