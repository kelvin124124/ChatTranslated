using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System;
using System.Text;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Chat
{
    public class Message(string sender, MessageSource source, SeString originalContent, XivChatType type)
    {
        public string Sender { get; set; } = sender;
        public MessageSource Source { get; set; } = source;
        public SeString OriginalContent { get; set; } = originalContent;

        private string? cleanedContent;
        public string CleanedContent => cleanedContent ??= RemoveNonTextPayloads(OriginalContent);

        public string? TranslatedContent { get; set; }
        public TranslationMode? translationMode { get; set; }
        public XivChatType Type { get; set; } = type;
        public string? Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        public string? Context { get; set; }

        public Message(string sender, MessageSource source, string input)
            : this(sender, source, new SeString(new TextPayload(input)), XivChatType.Say) { }

        public static string RemoveNonTextPayloads(SeString inputMsg)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < inputMsg.Payloads.Count; i++)
            {
                var payload = inputMsg.Payloads[i];
                switch (payload)
                {
                    case TextPayload textPayload:
                        sb.Append(textPayload.Text);
                        break;
                    case PlayerPayload _:
                        i += 2;
                        break;
                    case ItemPayload _:
                    case QuestPayload _:
                    case MapLinkPayload _:
                        i += 7;
                        break;
                    case StatusPayload _:
                        i += 10;
                        break;
                    case PartyFinderPayload _:
                        i += 6;
                        break;
                }
            }
            return Sanitize(ChatRegex.AutoTranslateRegex().Replace(sb.ToString(), string.Empty));
        }

        public static string Sanitize(string input) => ChatRegex.SpecialCharacterRegex().Replace(input, "*");
    }

    public enum MessageSource
    {
        Chat,
        PartyFinder,
        MainWindow
    }
}
