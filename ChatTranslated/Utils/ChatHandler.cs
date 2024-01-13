using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
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

        private static readonly Regex JPWelcomeRegex = new Regex(@"^よろしくお願いします[\u3002\uFF01!]*", RegexOptions.Compiled);
        private static readonly Regex JPByeRegex = new Regex(@"^お疲れ様でした[\u3002\uFF01!]*", RegexOptions.Compiled);
        private static readonly Regex JPDomaRegex = new Regex(@"\b(どま|ドマ|どんまい)\b", RegexOptions.Compiled);

        public ChatHandler()
        {
            Service.chatGui.ChatMessage += OnChatMessage;
        }

        private void OnChatMessage(XivChatType type, uint _, ref SeString sender, ref SeString message, ref bool _1)
        {
            if ((10 <= (uint)type && (uint)type <= 15) || ((uint)type == 30))
            {
                var playerPayload = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
                string playerName = Sanitize(playerPayload?.PlayerName ?? sender.ToString());

                // fix outgoing tell messages
                if (type == XivChatType.TellOutgoing && Service.clientState?.LocalPlayer != null)
                {
                    playerName = Sanitize(Service.clientState.LocalPlayer.Name.ToString());
                }

                // return if message is entirely auto-translate
                // return if message is in English (does not contain non-English characters)
                // return if message is from self
                if (AutoTranslateRegex.IsMatch(message.TextValue)
                    || !NonEnglishRegex.IsMatch(message.TextValue)
                    || playerName == Sanitize(Service.clientState?.LocalPlayer?.Name.ToString() ?? ""))
                {
                    Service.mainWindow.PrintToOutput($"{playerName}: {message}");
                    Service.pluginLog.Debug("Message filtered by standard rules.");
                    return;
                };

                // JP players like to use these, so filter them
                if (JPWelcomeRegex.IsMatch(message.TextValue))
                {
                    Service.pluginLog.Debug($"Welcome message filtered.");
                    Service.mainWindow.PrintToOutput($"{playerName}: Let's do it!");
                    if (Service.configuration.ChatIntergration)
                        Plugin.OutputChatLine($"{playerName}: {message} || Let's do it!");
                    return;
                }
                if (JPByeRegex.IsMatch(message.TextValue))
                {
                    Service.pluginLog.Debug($"Bye message filtered.");
                    Service.mainWindow.PrintToOutput($"{playerName}: Good game!");
                    if (Service.configuration.ChatIntergration)
                        Plugin.OutputChatLine($"{playerName}: {message} || Good game!");
                    return;
                }
                if (JPDomaRegex.IsMatch(message.TextValue))
                {
                    Service.pluginLog.Debug($"Doma message filtered.");
                    Service.mainWindow.PrintToOutput($"{playerName}: It's okay!");
                    if (Service.configuration.ChatIntergration)
                        Plugin.OutputChatLine($"{playerName}: {message} || It's okay!");
                    return;
                }

                string _message = Sanitize(message.TextValue);
                Task.Run(() => Translator.Translate(playerName, _message));
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
