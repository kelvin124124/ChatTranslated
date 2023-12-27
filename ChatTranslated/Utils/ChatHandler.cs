using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;
using System;

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
                
            }
        }
    }
}
