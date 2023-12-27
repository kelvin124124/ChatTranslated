using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;
using System;

namespace ChatTranslated.Utils
{
    internal class ChatHandler
    {
        public string handledMsg = "";

        public ChatHandler()
        {
            OutputChatLine("ChatHandler loaded!");
            Service.chatGui.ChatMessage += OnChatMessage;
        }

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (10 <= (uint)type && (uint)type <= 15) 
            {
                handledMsg = HandleMsg(message);
                // debug
                OutputChatLine($"{sender}: {message}");
                OutputChatLine($"{sender}: {handledMsg}");
                // print to main window
                Service.mainWindow.PrintToOutput($"{sender}: {message}");
                Service.mainWindow.PrintToOutput($"{sender}: {handledMsg}");
            }
        }

        private string HandleMsg(SeString message) 
        {
            handledMsg = Service.sanitizer.Sanitize(message.TextValue);

            return Service.translator.Translate(handledMsg);
        }

        public static void OutputChatLine(SeString message)
        {
            var sb = new SeStringBuilder().AddUiForeground("[CT] ", 58).Append(message);
            Service.chatGui.Print(new XivChatEntry { Message = sb.BuiltString });
        }

        public static void dispose()
        {
            
        }
    }
}
