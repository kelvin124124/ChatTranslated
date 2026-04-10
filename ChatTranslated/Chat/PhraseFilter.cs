using ChatTranslated.Translate;
using ChatTranslated.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChatTranslated.Chat;

internal static partial class PhraseFilter
{
    private record Entry(string language, Dictionary<string, string>? Translations = null);

    private static readonly Dictionary<string, Entry> Filter = LoadFilter();

    private static Dictionary<string, Entry> LoadFilter()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("ChatTranslated.Chat.phrases.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, Entry>>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    private static readonly HashSet<string> EnTokens = new(
        Filter.Where(kv => kv.Value.language == "English").Select(kv => kv.Key),
        StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"^[\s!！?？.。～~ーｰ\-_,、…\^]+|[\s!！?？.。～~ーｰ\-_,、…\^]+$")]
    private static partial Regex PunctuationTrimRegex();
    [GeneratedRegex(@"(.)\1{2,}")]
    private static partial Regex RepeatedCharRegex();
    [GeneratedRegex(@"(ha){3,}")]
    private static partial Regex RepeatedHaRegex();
    [GeneratedRegex(@"(lo){2,}l")]
    private static partial Regex RepeatedLolRegex();
    [GeneratedRegex(@"\bxd+\b")]
    private static partial Regex XdRegex();
    [GeneratedRegex(@"[\s,!?.;:""'()\[\]{}]+")]
    private static partial Regex TokenSplitRegex();

    // Normalizes a message before lookup.
    public static string Normalize(string text)
    {
        text = text.Normalize(NormalizationForm.FormKC);
        text = text.ToLowerInvariant();
        if (Filter.ContainsKey(text)) return text;
        text = PunctuationTrimRegex().Replace(text, "").Trim();
        text = RepeatedCharRegex().Replace(text, "$1$1");
        text = RepeatedHaRegex().Replace(text, "haha");
        text = RepeatedLolRegex().Replace(text, "lol");
        text = XdRegex().Replace(text, "xd");

        var noSpace = text.Replace(" ", "");
        if (noSpace.Length <= 8 && text.Contains(' ') && !Filter.ContainsKey(text)) text = noSpace;

        return text switch
        {
            "おつかれさまでした" => "お疲れさまでした",
            "おつかれさま" => "お疲れさま",
            "すいません" => "すみません",
            "よろしくおねがいします" => "よろしくお願いします",
            "お疲れ様でした" => "お疲れさまでした",
            _ => text
        };
    }

    private static bool IsCJK(char c) =>
        c >= '\u2E80' && c <= '\u9FFF' || c >= '\uAC00' && c <= '\uD7AF' || c >= '\uFF65' && c <= '\uFF9F';

    // Returns true when the text contains at least one known English token
    internal static bool HasEnToken(string text)
    {
        foreach (var c in text)
            if (IsCJK(c)) return false;

        foreach (var word in TokenSplitRegex().Split(text.ToLowerInvariant()))
        {
            if (word.Length == 0) continue;
            if (EnTokens.Contains(word)) return true;
            var collapsed = RepeatedCharRegex().Replace(word, "$1$1");
            if (collapsed != word && EnTokens.Contains(collapsed)) return true;
        }
        return false;
    }

    public static bool TryFilter(Message message, out string? detectedIso)
    {
        detectedIso = null;
        var key = Normalize(message.CleanedContent);
        if (!Filter.TryGetValue(key, out var entry))
            return false;

        Service.pluginLog.Information($"Filter matched for '{message.OriginalText}' (normalized: '{key}').");

        // resolve language identity
        if (!string.IsNullOrEmpty(entry.language) &&
            LanguageDetector.NameToIsoCode.TryGetValue(entry.language, out var iso))
        {
            LanguageDetector.UpdateChannelCache(message.Type, iso);
            detectedIso = iso;
        }

        // no translations: swallow non-linguistic (emoticons), pass through identified languages
        if (entry.Translations is null)
            return detectedIso is null;

        // known language → show original
        if (Service.configuration.KnownLanguages.Contains(entry.language))
        {
            Service.mainWindow.PrintToOutput($"{message.Sender}: {message.OriginalText}");
            return true;
        }

        // use static translation if available for target language
        if (entry.Translations.TryGetValue(Service.configuration.SelectedTargetLanguage, out var translation))
        {
            message.TranslatedContent = translation;
            Service.pluginLog.Information($"Translated '{message.OriginalText}' to '{translation}' using phrase filter.");
            ChatHandler.OutputMessage(message, message.Type);
            return true;
        }

        return false;
    }
}
