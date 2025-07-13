using ChatTranslated.Chat;
using ChatTranslated.Translate;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using System.Threading.Tasks;

namespace ChatTranslated
{
    public static class IpcManager
    {
        // in: Original text, context, target language
        // out: Task (translated text)
        private static ICallGateProvider<string, string?, string, Task<string>>? CallGateTranslate;

        public static void Register(IDalamudPluginInterface pi)
        {
            Unregister();

            CallGateTranslate = pi.GetIpcProvider<string, string?, string, Task<string>>("ChatTranslated.Translate");
            CallGateTranslate.RegisterFunc(IpcTranslateText);
        }

        private static async Task<string> IpcTranslateText(string originalText, string? context, string toLang)
        {
            Message IpcMessage = new Message("IPC", MessageSource.Ipc, originalText)
            {
                Context = context
            };

            var translatedMessage = await TranslationHandler.TranslateMessage(IpcMessage, toLang);
            return translatedMessage.TranslatedContent ?? null!;
        }

        public static void Unregister()
        {
            CallGateTranslate?.UnregisterFunc();
            CallGateTranslate = null;
        }
    }
}
