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
        public ChatHandler()
        {
            Service.chatGui.ChatMessage += OnChatMessage;
        }

        private void OnChatMessage(XivChatType type, uint _, ref SeString sender, ref SeString message, ref bool _1)
        {
            if (10 <= (uint)type && (uint)type <= 15)
            {
                PlayerPayload? playerPayload;
                playerPayload = sender.Payloads.SingleOrDefault(x => x is PlayerPayload) as PlayerPayload;
                string playerName = Sanitize(playerPayload?.PlayerName ?? sender.ToString());

                string _message = Sanitize(message.TextValue);

                Task.Run(() => Translator.Translate(playerName, _message));
            }
        }

        private static string Sanitize(string input)
        {
            var regex = new Regex(@"[\uE000-\uF8FF]+", RegexOptions.Compiled);
            return regex.Replace(input, "");
        }

        public void Dispose()
        {
            Service.chatGui.ChatMessage -= OnChatMessage;
        }
    }
}
