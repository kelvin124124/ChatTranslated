using ChatTranslated.Translate;
using ChatTranslated.Utils;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChatTranslated.Chat;

internal static class PhraseFilter
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

    // Normalizes a message before lookup.
    public static string Normalize(string text)
    {
        text = text.Normalize(NormalizationForm.FormKC);        // ｄ２ → d2
        text = text.ToLowerInvariant();
        text = Regex.Replace(text, @"^[\s!！?？.。～~ーｰ\-_,、…]+|[\s!！?？.。～~ーｰ\-_,、…]+$", "").Trim();
        text = Regex.Replace(text, @"(.)\1{2,}", "$1$1");       // ohhh → ohh
        text = Regex.Replace(text, @"(ha){3,}", "haha");
        text = Regex.Replace(text, @"(lo){2,}l", "lol");
        text = Regex.Replace(text, @"\bxd+\b", "xd");

        var noSpace = text.Replace(" ", "");
        if (noSpace.Length <= 8 && text.Contains(' ')) text = noSpace; // "gg ty" → "ggty"

        text = text switch
        {
            "おつかれさまでした" => "お疲れさまでした",
            "おつかれさま" => "お疲れさま",
            "すいません" => "すみません",
            _ => text
        };

        return text;
    }

    // Returns true if the phrase was matched and should be routed from the normal pipeline.
    public static bool TryFilter(Message message)
    {
        var key = Normalize(message.CleanedContent);
        if (!Filter.TryGetValue(key, out var entry))
            return false; // unknown phrase
                
        // if no lingual meaning
        if (entry.Translations == null) return true;

        // feed channel boost: phrase filter has high-confidence language identity
        if (LanguageDetector.NameToIsoCode.TryGetValue(entry.language, out var iso))
            LanguageDetector.UpdateChannelCache(message.Type, iso);

        // known language
        if (Service.configuration.KnownLanguages.Contains(entry.language)) 
        {
            Service.mainWindow.PrintToOutput($"{message.Sender}: {message.OriginalText}");
            return true;
        }

        // unknown language
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
