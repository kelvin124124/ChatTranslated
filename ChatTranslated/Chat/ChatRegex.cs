using System.Text.RegularExpressions;

namespace ChatTranslated.Chat
{
    internal static partial class ChatRegex
    {
        [GeneratedRegex(@"\uE040\u0020(.*?)\u0020\uE041")]
        public static partial Regex AutoTranslateRegex();

        [GeneratedRegex(@"[\uE000-\uF8FF]+", RegexOptions.Compiled)]
        public static partial Regex SpecialCharacterRegex();

        [GeneratedRegex(@"(?<![\u0020-\u007E\u2000-\u21FF\u3000-\u303F\uFF10-\uFF5A])[^(\u0020-\u007E\u2000-\u21FF\u2501\u3000-\u303F\uFF10-\uFF5A)]{2,}(?![\u0020-\u007E\u2000-\u21FF\u3000-\u303F\uFF10-\uFF5A])", RegexOptions.Compiled)]
        public static partial Regex NonEnglishRegex();

        [GeneratedRegex(@"^よろしくお(願|ねが)いします[\u3002\uFF01!]*", RegexOptions.Compiled)]
        public static partial Regex JPWelcomeRegex();

        [GeneratedRegex(@"^お疲れ様でした[\u3002\uFF01!]*", RegexOptions.Compiled)]
        public static partial Regex JPByeRegex();

        [GeneratedRegex(@"\b(どまい?|ドマ|どんまい)(です)?[\u3002\uFF01!]*\b", RegexOptions.Compiled)]
        public static partial Regex JPDomaRegex();
    }
}
