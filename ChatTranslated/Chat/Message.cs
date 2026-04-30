using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Text;

namespace ChatTranslated.Chat;

public class Message(string sender, MessageSource source, SeString originalContent, XivChatType type)
{
    public string Sender { get; } = sender;
    public MessageSource Source { get; } = source;
    public SeString OriginalContent { get; } = originalContent;
    public XivChatType Type { get; } = type;

    // text accessors
    private string? originalText;
    public string OriginalText => originalText ??= OriginalContent.TextValue;

    private string? cleanedContent;
    public string CleanedContent => cleanedContent ??= Sanitize(ExtractText(OriginalContent));

    // translation state
    public string? TranslatedContent { get; set; }
    public Configuration.TranslationMode? TranslationMode { get; set; }
    public string? Context { get; set; }

    public Message(string sender, MessageSource source, string input)
        : this(sender, source, new SeString(new TextPayload(input)), XivChatType.Say) { }

    private static string ExtractText(SeString seString)
    {
        var sb = new StringBuilder();
        bool inLink = false;
        for (int i = 0; i < seString.Payloads.Count; i++)
        {
            switch (seString.Payloads[i])
            {
                case TextPayload textPayload when !inLink:
                    sb.Append(textPayload.Text);
                    break;
                case PlayerPayload:
                case ItemPayload:
                case QuestPayload:
                case MapLinkPayload:
                case StatusPayload:
                case PartyFinderPayload:
                    inLink = true;
                    break;
                case RawPayload when inLink: // link terminator (0x27 0x03)
                    inLink = false;
                    break;
                case AutoTranslatePayload:
                    i += 2; // self-contained, no link terminator
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
