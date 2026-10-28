using ChatTranslated.Localization;

namespace ChatTranslated.Utils;

internal static class Loc
{
    // a resource string by name, or the name itself when it has no entry
    public static string Get(string key) => Resources.ResourceManager.GetString(key, Resources.Culture) ?? key;
}
