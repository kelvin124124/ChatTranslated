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
        private static readonly Regex AutoTranslateRegex = new Regex(@"^\uE040\u0020.*\u0020\uE041$", RegexOptions.Compiled);
        private static readonly Regex NonEnglishRegex = new Regex(@"[^\u0020-\u007E]+", RegexOptions.Compiled);
        private static readonly Regex SpecialCharacterRegex = new Regex(@"[\uE000-\uF8FF]+", RegexOptions.Compiled);

        public ChatHandler()
        {
            Service.chatGui.ChatMessage += OnChatMessage;
        }

        private void OnChatMessage(XivChatType type, uint _, ref SeString sender, ref SeString message, ref bool _1)
        {
            if (10 <= (uint)type && (uint)type <= 15)
            {
                // return if message is entirely auto-translate
                // return if message does not contain non-English characters
                if (AutoTranslateRegex.IsMatch(message.TextValue)) return;
                if (!NonEnglishRegex.IsMatch(message.TextValue)) return;

                PlayerPayload? playerPayload;
                playerPayload = sender.Payloads.SingleOrDefault(x => x is PlayerPayload) as PlayerPayload;
                string playerName = Sanitize(playerPayload?.PlayerName ?? sender.ToString());

                if (type == XivChatType.TellOutgoing && Service.clientState.LocalPlayer != null)
                {
                    playerName = Sanitize(Service.clientState.LocalPlayer.Name.ToString());
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
