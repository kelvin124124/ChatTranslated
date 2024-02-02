using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatTranslated.Utils
{
    internal class ChatHandler
    {
        private static readonly Regex AutoTranslateRegex = new Regex(@"^\uE040\u0020?.*\u0020?\uE041$", RegexOptions.Compiled);
        private static readonly Regex NonEnglishRegex = new Regex(@"[^\u0020-\u007E\uFF01-\uFF5E]+", RegexOptions.Compiled);
        private static readonly Regex SpecialCharacterRegex = new Regex(@"[\uE000-\uF8FF]+", RegexOptions.Compiled);

        private static readonly Regex JPWelcomeRegex = new Regex(@"^よろしくお(願|ねが)いします[\u3002\uFF01!]*", RegexOptions.Compiled);
        private static readonly Regex JPByeRegex = new Regex(@"^お疲れ様でした[\u3002\uFF01!]*", RegexOptions.Compiled);
        private static readonly Regex JPDomaRegex = new Regex(@"\b(どま|ドマ|どんまい)(です)?[\u3002\uFF01!]*\b", RegexOptions.Compiled); 

        // (uint)type, UIcolor
        private static readonly Dictionary<uint, ushort> ColorDictionary = new()
        {
            { 10, 1 }, // say
            { 11, 577 }, // shout
            { 12, 508 }, // incoming tell
            { 13, 508 }, // outgoing tell
            { 14, 37 }, // party
            { 15, 577 }, // alliance
            { 30, 535 }, // yell
        };


        public ChatHandler()
        {
            Service.chatGui.ChatMessage += OnChatMessage;
        }

        private void OnChatMessage(XivChatType type, uint _, ref SeString sender, ref SeString message, ref bool _1)
        {
            uint chatType = (uint)type;

            if ((10 <= chatType && chatType <= 15) || (chatType == 30))
            {
                var playerPayload = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
                string playerName = Sanitize(playerPayload?.PlayerName ?? sender.ToString());

                // fix outgoing tell messages
                if (chatType == 13 && Service.clientState?.LocalPlayer != null)
                {
                    playerName = Sanitize(Service.clientState.LocalPlayer.Name.ToString());
                }

                ushort color = ColorDictionary.TryGetValue(chatType, out var key) ? key : (ushort)1;

                // return if message is entirely auto-translate
                // return if message is in English (does not contain non-English characters)
                // return if message is from self
                if (AutoTranslateRegex.IsMatch(message.TextValue)
                    || playerName == Sanitize(Service.clientState?.LocalPlayer?.Name.ToString() ?? ""))
                {
                    Service.mainWindow.PrintToOutput($"{playerName}: {message}");
                    Service.pluginLog.Debug("Message filtered by standard rules.");
                    return;
                };

                string _message = Sanitize(message.TextValue);

                // Message contains only English characters
                if (!NonEnglishRegex.IsMatch(message.TextValue)) 
                {
                    // Translate French and German, reutrn if message English
                    Task.Run(() => Translator.TranslateFrDe(playerName, _message, color));
                }
                else 
                {
                    // likely Japanese
                    // JP players like to use these, so filter them
                    if (JPWelcomeRegex.IsMatch(message.TextValue))
                    {
                        Service.pluginLog.Debug($"Welcome message filtered.");
                        Service.mainWindow.PrintToOutput($"{playerName}: Let's do it!");
                        if (Service.configuration.ChatIntergration)
                            Plugin.OutputChatLine($"{playerName}: {message} || Let's do it!", color);
                        return;
                    }
                    if (JPByeRegex.IsMatch(message.TextValue))
                    {
                        Service.pluginLog.Debug($"Bye message filtered.");
                        Service.mainWindow.PrintToOutput($"{playerName}: Good game!");
                        if (Service.configuration.ChatIntergration)
                            Plugin.OutputChatLine($"{playerName}: {message} || Good game!", color);
                        return;
                    }
                    if (JPDomaRegex.IsMatch(message.TextValue))
                    {
                        Service.pluginLog.Debug($"Doma message filtered.");
                        Service.mainWindow.PrintToOutput($"{playerName}: It's okay!");
                        if (Service.configuration.ChatIntergration)
                            Plugin.OutputChatLine($"{playerName}: {message} || It's okay!", color);
                        return;
                    }

                    // Normal translate operation
                    Task.Run(() => Translator.Translate(playerName, _message, color)); 
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
