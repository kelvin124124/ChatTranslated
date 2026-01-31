using ChatTranslated.Utils;
using Dalamud.Utility;
using MessagePack;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Translate
{
    internal static class DeepLTranslate
    {
        private const string DefaultContentType = "application/json";

        public static async Task<(string, TranslationMode?)> Translate(string text, string targetLanguage)
        {
            if (TryGetLanguageCode(targetLanguage, out var languageCode))
            {
                var requestBody = new { text = new[] { text }, target_lang = languageCode, context = "FFXIV, MMORPG" };
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api-free.deepl.com/v2/translate")
                {
                    Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, DefaultContentType),
                    Headers = { { HttpRequestHeader.Authorization.ToString(), $"DeepL-Auth-Key {Service.configuration.DeepL_API_Key}" } }
                };

                try
                {
                    var response = await TranslationHandler.HttpClient.SendAsync(requestMessage).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var translated = JObject.Parse(jsonResponse)["translations"]?[0]?["text"]?.ToString().Trim();

                    if (translated.IsNullOrWhitespace())
                    {
                        throw new Exception("Translation not found in the expected JSON structure.");
                    }

                    return (translated, TranslationMode.DeepL);
                }
                catch (Exception ex)
                {
                    Service.pluginLog.Warning($"DeepL Translate failed to translate. Falling back to machine translation.\n{ex.Message}");
                    return await MachineTranslate.Translate(text, targetLanguage);
                }
            }
            return ("Target language not supported by DeepL.", null);
        }

        internal static bool TryGetLanguageCode(string language, out string? languageCode)
        {
            languageCode = language switch
            {
                "English" => "EN",
                "Japanese" => "JA",
                "German" => "DE",
                "French" => "FR",
                "Chinese (Simplified)" => "ZH",
                "Chinese (Traditional)" => "ZH",
                "Korean" => "KO",
                "Spanish" => "ES",
                "Arabic" => "AR",
                "Bulgarian" => "BG",
                "Czech" => "CS",
                "Danish" => "DA",
                "Dutch" => "NL",
                "Estonian" => "ET",
                "Finnish" => "FI",
                "Greek" => "EL",
                "Hungarian" => "HU",
                "Indonesian" => "ID",
                "Italian" => "IT",
                "Latvian" => "LV",
                "Lithuanian" => "LT",
                "Norwegian Bokmal" => "NB",
                "Polish" => "PL",
                "Portuguese" => "PT",
                "Romanian" => "RO",
                "Russian" => "RU",
                "Slovak" => "SK",
                "Slovenian" => "SL",
                "Swedish" => "SV",
                "Turkish" => "TR",
                "Ukrainian" => "UK",
                _ => null
            };
            return !string.IsNullOrEmpty(languageCode);
        }
    }

    // Based on free-deepl-translator: https://github.com/RealDarkCraft/free-deepl-translator
    internal static class DeeplsTranslate
    {
        private static DeepLConnection? _connection;
        private static string _input = "", _output = "";

        public static async Task<(string, TranslationMode?)> Translate(string message, string targetLanguage)
        {
            if (!DeepLTranslate.TryGetLanguageCode(targetLanguage, out string? langCode))
                return ("Target language not supported by DeepL.", null);

            try
            {
                if (_connection is not { Connected: true })
                {
                    if (_connection is not null) await _connection.Close();
                    _connection = new DeepLConnection();
                    await _connection.Connect();
                }

                if (_connection is not { Connected: true })
                    throw new Exception("Failed to establish DeepL session.");

                var result = await GetTranslations(message, langCode!);
                if (result is not null)
                    return (result, TranslationMode.DeepL);

                throw new Exception(_connection.OnErrorLast);
            }
            catch (Exception ex)
            {
                Service.pluginLog.Warning($"DeeplsTranslate failed. Falling back.\n{ex.Message}");
                return Service.configuration.DeepL_API_Key != "YOUR-API-KEY:fx"
                    ? await DeepLTranslate.Translate(message, targetLanguage)
                    : await MachineTranslate.Translate(message, targetLanguage);
            }
        }

        private static async Task<string?> GetTranslations(string text, string targetLang)
        {
            var pid = new ParticipantId { Value = 2 };
            List<FieldEvent> events =
            [
                new() { FieldName = 2, SetPropertyOperation = new() { PropertyName = 5, TranslatorRequestedTargetLanguageValue = new() { TargetLanguage = new() { Code = targetLang } } }, ParticipantId = pid },
                new() { FieldName = 1, SetPropertyOperation = new() { PropertyName = 3 }, ParticipantId = pid },
                new() { FieldName = 2, SetPropertyOperation = new() { PropertyName = 16, TranslatorLanguageModelValue = new() { LanguageModel = new() { Value = "next-gen" } } }, ParticipantId = pid },
                new() { FieldName = 1, TextChangeOperation = new() { Range = new() { End = _input.Length }, Text = text }, ParticipantId = pid }
            ];

            (_input, _output) = (text, "");

            var request = new ParticipantRequest { AppendMessage = new() { Events = events, BaseVersion = new() { Version = new() { Value = _connection!.BVer } } } };
            var buf = new ArrayBufferWriter<byte>();
            var w = new MessagePackWriter(buf);
            w.WriteArrayHeader(4); w.Write(2); w.WriteMapHeader(0); w.Write("1");
            w.WriteExtensionFormat(new ExtensionResult(4, request.ToProtoBytes()));
            w.Flush();

            await _connection.SendFramed(buf.WrittenSpan.ToArray());

            while (true)
            {
                var msg = await _connection.PopMessageAsync();
                if (msg is null || _connection.OnError) return null;
                if (msg is not List<object> { Count: >= 4 } lst) continue;

                var protoBytes = lst[3] switch { byte[] b => b, ExtensionResult e => e.Data.ToArray(), _ => null };
                if (protoBytes is null) continue;

                var resp = protoBytes.FromProtoBytes<ParticipantResponse>();
                if (resp?.MetaInfoMessage?.Idle is not null) return _output;

                if (resp?.PublishedMessage is { } pub)
                {
                    if (pub.CurrentVersion?.Version is not null) _connection.BVer = pub.CurrentVersion.Version.Value;
                    foreach (var evt in pub.Events ?? [])
                        if (evt.TextChangeOperation is { } op)
                            if (evt.FieldName == 2) _output = ApplyTextChange(_output, op);
                            else if (evt.FieldName == 1) _input = ApplyTextChange(_input, op);
                }
            }
        }

        private static string ApplyTextChange(string orig, TextChangeOperation op)
        {
            var (start, end) = (Math.Clamp(op.Range?.Start ?? 0, 0, orig.Length), Math.Clamp(op.Range?.End ?? 0, 0, orig.Length));
            return $"{orig[..start]}{op.Text ?? ""}{orig[end..]}";
        }
    }
}
