using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Text;

namespace ChatTranslated.Chat;

public class Message(string sender, MessageSource source, SeString originalContent, XivChatType type)
{
    // Immutable identity
    public string Sender { get; } = sender;
    public MessageSource Source { get; } = source;
    public SeString OriginalContent { get; } = originalContent;
    public XivChatType Type { get; } = type;

    // Derived text accessors
    public string OriginalText => OriginalContent.TextValue;

    private string? cleanedContent;
    public string CleanedContent => cleanedContent ??= Sanitize(ExtractText(OriginalContent));

    // Mutable translation state
    public string? TranslatedContent { get; set; }
    public Configuration.TranslationMode? TranslationMode { get; set; }
    public string? Context { get; set; }

    public Message(string sender, MessageSource source, string input)
        : this(sender, source, new SeString(new TextPayload(input)), XivChatType.Say) { }

    private static string ExtractText(SeString seString)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < seString.Payloads.Count; i++)
        {
            switch (seString.Payloads[i])
            {
                case TextPayload textPayload:
                    sb.Append(textPayload.Text);
                    break;
                case PlayerPayload:
                    i += 2;
                    break;
                case ItemPayload:
                case QuestPayload:
                case MapLinkPayload:
                    i += 7;
                    break;
                case StatusPayload:
                    i += 10;
                    break;
                case PartyFinderPayload:
                    i += 6;
                    break;
                case AutoTranslatePayload:
                    i += 2;
                    break;
            }
        }
        return sb.ToString();
    }

    private static string Sanitize(string input) => ChatRegex.SpecialCharacterRegex().Replace(input, "\uFFFD");
}

public enum MessageSource
{
    Chat,
    PartyFinder,
    MainWindow,
    Ipc
}
