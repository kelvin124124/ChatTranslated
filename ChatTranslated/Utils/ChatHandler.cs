using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChatTranslated.Utils
{
    internal class ChatHandler
    {
        public ChatHandler()
        {
            Service.chatGui.ChatMessage += OnChatMessage;
        }

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (10 <= (uint)type && (uint)type <= 15)
            {
                PlayerPayload? playerPayload;
                playerPayload = sender.Payloads.SingleOrDefault(x => x is PlayerPayload) as PlayerPayload;

                string playerName = playerPayload?.PlayerName ?? sender.ToString();

                Service.translator.Translate(playerName, message.TextValue);
            }
        }

        public void Dispose()
        {
            Service.chatGui.ChatMessage -= OnChatMessage;
        }
    }
}
