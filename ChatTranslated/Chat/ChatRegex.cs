using System.Text.RegularExpressions;

namespace ChatTranslated.Chat;

internal static partial class ChatRegex
{
    [GeneratedRegex(@"\uE040\u0020(.*?)\u0020\uE041")]
    public static partial Regex AutoTranslateRegex();

    [GeneratedRegex(@"[\uE000-\uF8FF]+")]
    public static partial Regex SpecialCharacterRegex();

    [GeneratedRegex(@"^よろしくお(願|ねが)いします[\u3002\uFF01!]*")]
    public static partial Regex JPWelcomeRegex();

    [GeneratedRegex(@"^お疲れ様でした[\u3002\uFF01!]*")]
    public static partial Regex JPByeRegex();

    [GeneratedRegex(@"\b(どまい?|ドマ|どんまい)(です)?[\u3002\uFF01!]*\b")]
    public static partial Regex JPDomaRegex();
}
