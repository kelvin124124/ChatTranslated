using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

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
                Service.translator.Translate(sender.TextValue, message.TextValue);
            }
        }

        public void Dispose()
        {
            Service.chatGui.ChatMessage -= OnChatMessage;
        }
    }
}
