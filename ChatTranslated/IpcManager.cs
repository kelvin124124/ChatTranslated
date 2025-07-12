using ChatTranslated.Chat;
using ChatTranslated.Translate;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace ChatTranslated
{
    public static class IpcManager
    {
        // in: Original text, context, target language
        // out: Translated text
        private static ICallGateProvider<string, string?, string, string>? CallGateTranslate;

        public static void Register(IDalamudPluginInterface pi)
        {
            Unregister();

            CallGateTranslate = pi.GetIpcProvider<string, string?, string, string>("ChatTranslated.Translate");
            CallGateTranslate.RegisterFunc(IpcTranslateText);
        }

        private static string IpcTranslateText(string originalText, string? context, string toLang)
        {
            Message IpcMessage = new Message("IPC", MessageSource.Ipc, originalText)
            {
                Context = context
            };

            var translatedMessage = TranslationHandler.TranslateMessage(IpcMessage, toLang).Result;
            return translatedMessage.TranslatedContent ?? string.Empty;
        }

        public static void Unregister()
        {
            CallGateTranslate?.UnregisterFunc();
            CallGateTranslate = null;
        }
    }
}
