using ChatTranslated.Utils;
using Dalamud.Utility;
using MessagePack;
using ProtoBuf;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static ChatTranslated.Configuration;

namespace ChatTranslated.Translate
{
    public class DeeplsTranslate : IDisposable
    {
        private readonly SemaphoreSlim sessionLock = new(1, 1);
        private Connection? connection;
        private string inputText = "";
        private string outputText = "";

        public static string Name => "Deepl";

        public async Task<(string, TranslationMode?)> TranslateAsync(string text, string targetLanguage)
        {
            Service.pluginLog.Warning($"[DeeplsTranslate] TranslateAsync called: text='{text}', targetLanguage='{targetLanguage}'"); // debug
            if (!TryGetLanguageCode(targetLanguage, out var targetLang))
            {
                Service.pluginLog.Warning($"[DeeplsTranslate] Language '{targetLanguage}' not supported"); // debug
                throw new Exception($"Target language '{targetLanguage}' not supported by DeepL.");
            }

            const int maxRetries = 1;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                Service.pluginLog.Warning($"[DeeplsTranslate] Attempt {attempt}/{maxRetries}, ensuring connection..."); // debug
                await EnsureConnectedAsync().ConfigureAwait(false);

                if (connection?.Connected != true)
                {
                    Service.pluginLog.Warning($"[DeeplsTranslate] Connection not established on attempt {attempt}"); // debug
                    if (attempt < maxRetries)
                    {
                        Service.pluginLog.Warning("[DeeplsTranslate] Resetting connection for retry..."); // debug
                        await ResetConnectionAsync().ConfigureAwait(false);
                        continue;
                    }
                    Service.pluginLog.Warning("[DeeplsTranslate] All retries exhausted, falling back to DeepL API"); // debug
                    return await TryDeepLApi(text, targetLang);
                }

                Service.pluginLog.Warning("[DeeplsTranslate] Connection active, calling GetTranslations..."); // debug
                string? result = await GetTranslations(text, targetLang).ConfigureAwait(false);

                if (!result.IsNullOrWhitespace())
                {
                    Service.pluginLog.Warning($"[DeeplsTranslate] Translation successful: '{result}'"); // debug
                    return (result!, TranslationMode.DeepL);
                }

                Service.pluginLog.Warning($"[DeeplsTranslate] GetTranslations returned empty on attempt {attempt}"); // debug
                if (attempt < maxRetries)
                {
                    Service.pluginLog.Warning("[DeeplsTranslate] Resetting connection for retry..."); // debug
                    await ResetConnectionAsync().ConfigureAwait(false);
                    continue;
                }
            }

            Service.pluginLog.Warning("[DeeplsTranslate] All attempts failed, falling back to DeepL API"); // debug
            return await TryDeepLApi(text, targetLang);
        }

        private static async Task<(string, TranslationMode?)> TryDeepLApi(string text, string targetLang)
        {
            Service.pluginLog.Warning($"[DeeplsTranslate] TryDeepLApi called, targetLang='{targetLang}'"); // debug
            if (!Service.configuration.DeepL_API_Key.IsNullOrWhitespace() &&
                Service.configuration.DeepL_API_Key != "YOUR-API-KEY:fx")
            {
                Service.pluginLog.Warning("[DeeplsTranslate] API key present, calling DeepL API..."); // debug
                return await DeepLTranslate.Translate(text, targetLang);
            }

            Service.pluginLog.Warning("[DeeplsTranslate] No valid API key, throwing exception"); // debug
            throw new Exception("Translation failed after retries.");
        }

        public async Task CloseAsync()
        {
            await sessionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (connection != null)
                {
                    await connection.Close().ConfigureAwait(false);
                    connection = null;
                }
            }
            finally
            {
                sessionLock.Release();
            }
        }

        private async Task EnsureConnectedAsync()
        {
            if (connection?.Connected == true)
            {
                Service.pluginLog.Warning("[DeeplsTranslate] EnsureConnectedAsync: already connected"); // debug
                return;
            }

            Service.pluginLog.Warning("[DeeplsTranslate] EnsureConnectedAsync: acquiring lock to establish connection..."); // debug
            await sessionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (connection?.Connected == true)
                {
                    Service.pluginLog.Warning("[DeeplsTranslate] EnsureConnectedAsync: connected after acquiring lock"); // debug
                    return;
                }

                if (connection != null)
                {
                    Service.pluginLog.Warning("[DeeplsTranslate] EnsureConnectedAsync: closing stale connection"); // debug
                    await connection.Close().ConfigureAwait(false);
                }

                Service.pluginLog.Warning("[DeeplsTranslate] EnsureConnectedAsync: creating new Connection and connecting..."); // debug
                connection = new Connection();
                var connected = await connection.Connect().ConfigureAwait(false);
                Service.pluginLog.Warning($"[DeeplsTranslate] EnsureConnectedAsync: Connect() returned {connected}"); // debug
            }
            finally
            {
                sessionLock.Release();
            }
        }

        private async Task ResetConnectionAsync()
        {
            Service.pluginLog.Warning("[DeeplsTranslate] ResetConnectionAsync: resetting connection..."); // debug
            await sessionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (connection != null)
                {
                    Service.pluginLog.Warning("[DeeplsTranslate] ResetConnectionAsync: closing existing connection"); // debug
                    await connection.Close().ConfigureAwait(false);
                    connection = null;
                }
                else
                {
                    Service.pluginLog.Warning("[DeeplsTranslate] ResetConnectionAsync: no connection to close"); // debug
                }
            }
            finally
            {
                sessionLock.Release();
            }
        }

        private async Task<string?> GetTranslations(string text, string targetLang)
        {
            Service.pluginLog.Warning($"[DeeplsTranslate] GetTranslations: text='{text}', targetLang='{targetLang}', BVer={connection!.BVer}, inputText.Length={inputText.Length}"); // debug
            var participantId = new ParticipantId { Value = 2 };
            var events = new List<FieldEvent>
            {
                new()
                {
                    FieldName = 2,
                    SetPropertyOperation = new SetPropertyOperation
                    {
                        PropertyName = 5,
                        TranslatorRequestedTargetLanguageValue = new TranslatorRequestedTargetLanguageValue
                        {
                            TargetLanguage = new Language { Code = targetLang }
                        }
                    },
                    ParticipantId = participantId
                },
                new()
                {
                    FieldName = 1,
                    SetPropertyOperation = new SetPropertyOperation { PropertyName = 3 },
                    ParticipantId = participantId
                },
                new()
                {
                    FieldName = 2,
                    SetPropertyOperation = new SetPropertyOperation
                    {
                        PropertyName = 16,
                        TranslatorLanguageModelValue = new TranslatorLanguageModelValue
                        {
                            LanguageModel = new LanguageModel { Value = "next-gen" }
                        }
                    },
                    ParticipantId = participantId
                },
                new()
                {
                    FieldName = 1,
                    TextChangeOperation = new TextChangeOperation
                    {
                        Range = new TextRange { End = inputText.Length },
                        Text = text
                    },
                    ParticipantId = participantId
                }
            };

            (inputText, outputText) = (text, "");

            var request = new ParticipantRequest
            {
                AppendMessage = new AppendMessage
                {
                    Events = events,
                    BaseVersion = new EventVersion
                    {
                        Version = new EventVersionValue { Value = connection!.BVer }
                    }
                }
            };

            var proto = request.ToProtoBytes();
            var buf = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buf);
            writer.WriteArrayHeader(4);
            writer.Write(2);
            writer.WriteMapHeader(0);
            writer.Write("1");
            writer.WriteExtensionFormat(new ExtensionResult(4, proto));
            writer.Flush();

            Service.pluginLog.Warning($"[DeeplsTranslate] GetTranslations: sending request ({buf.WrittenCount} bytes)"); // debug
            await connection.SendFramed(buf.WrittenSpan.ToArray());

            while (true)
            {
                var msg = await connection.PopMessageAsync();
                if (msg is null || connection.OnError)
                {
                    Service.pluginLog.Warning($"[DeeplsTranslate] GetTranslations: msg is null={msg is null}, OnError={connection.OnError}"); // debug
                    return null;
                }

                if (msg is not List<object> { Count: >= 4 } msgList)
                {
                    Service.pluginLog.Warning($"[DeeplsTranslate] GetTranslations: skipping non-list or short message (type={msg.GetType().Name})"); // debug
                    continue;
                }

                Service.pluginLog.Warning($"[DeeplsTranslate] GetTranslations: msgList[0]={msgList[0]}, [1]={msgList[1]?.GetType().Name}, [2]={msgList[2]}, [3]={msgList[3]?.GetType().Name}"); // debug
                var protoBytes = msgList[3] switch
                {
                    byte[] b => b,
                    ExtensionResult e => e.Data.ToArray(),
                    _ => null
                };

                if (protoBytes is null)
                {
                    Service.pluginLog.Warning($"[DeeplsTranslate] GetTranslations: protoBytes is null (element type={msgList[3]?.GetType().Name}), skipping"); // debug
                    continue;
                }

                var response = protoBytes.FromProtoBytes<ParticipantResponse>();
                Service.pluginLog.Warning($"[DeeplsTranslate] GetTranslations: deserialized response - hasIdle={response?.MetaInfoMessage?.Idle is not null}, hasPublished={response?.PublishedMessage is not null}, protoBytes.Length={protoBytes.Length}"); // debug
                if (response?.MetaInfoMessage?.Idle is not null)
                {
                    Service.pluginLog.Warning($"[DeeplsTranslate] GetTranslations: received Idle, returning outputText='{outputText}'"); // debug
                    return outputText;
                }

                if (response?.PublishedMessage is { } published)
                {
                    if (published.CurrentVersion?.Version is not null)
                    {
                        Service.pluginLog.Warning($"[DeeplsTranslate] GetTranslations: updating BVer from {connection.BVer} to {published.CurrentVersion.Version.Value}"); // debug
                        connection.BVer = published.CurrentVersion.Version.Value;
                    }

                    Service.pluginLog.Warning($"[DeeplsTranslate] GetTranslations: published has {published.Events?.Count ?? 0} events"); // debug
                    foreach (var evt in published.Events ?? [])
                    {
                        if (evt.TextChangeOperation is { } op)
                        {
                            if (evt.FieldName == 2)
                            {
                                outputText = ApplyTextChange(outputText, op);
                                Service.pluginLog.Warning($"[DeeplsTranslate] GetTranslations: output text change -> '{outputText}'"); // debug
                            }
                            else if (evt.FieldName == 1)
                            {
                                inputText = ApplyTextChange(inputText, op);
                                Service.pluginLog.Warning($"[DeeplsTranslate] GetTranslations: input text change -> '{inputText}'"); // debug
                            }
                        }
                        else if (evt.SetPropertyOperation is { } setProp)
                        {
                            Service.pluginLog.Warning($"[DeeplsTranslate] GetTranslations: event fieldName={evt.FieldName}, setProp.PropertyName={setProp.PropertyName}"); // debug
                        }
                    }
                }
                else
                {
                    Service.pluginLog.Warning($"[DeeplsTranslate] GetTranslations: response has neither Idle nor PublishedMessage, raw hex={Convert.ToHexString(protoBytes)}"); // debug
                }
            }
        }

        private static string ApplyTextChange(string original, TextChangeOperation op)
        {
            var start = Math.Clamp(op.Range?.Start ?? 0, 0, original.Length);
            var end = Math.Clamp(op.Range?.End ?? 0, 0, original.Length);
            return $"{original[..start]}{op.Text ?? ""}{original[end..]}";
        }

        private static bool TryGetLanguageCode(string language, out string languageCode)
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
                _ => ""
            };
            return !string.IsNullOrEmpty(languageCode);
        }

        private class Connection
        {
            private static readonly MessagePackSerializerOptions MsgPackOptions =
                MessagePackSerializerOptions.Standard.WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);

            private static readonly int[] VarintShifts = [0, 7, 14, 21, 28];

            private readonly HttpClient client = new()
            {
                DefaultRequestHeaders =
                {
                    { "User-Agent", "Mozilla/5.0" },
                    { "Accept-Language", "en-US" }
                }
            };

            private readonly Channel<object> messages = Channel.CreateUnbounded<object>();
            private string? token;
            private CancellationTokenSource? cts;
            private Task? recvTask;

            public bool Connected { get; private set; }
            public bool OnError { get; private set; }
            public int BVer { get; internal set; }

            private string Url => $"https://ita-free.www.deepl.com/v1/sessions?id={token}&_={DateTime.UtcNow.Ticks / 10000}";

            public async Task<bool> Connect()
            {
                if (Connected)
                {
                    Service.pluginLog.Warning("[DeeplsTranslate] Connection.Connect: already connected"); // debug
                    return true;
                }

                (Connected, OnError) = (false, false);
                Service.pluginLog.Warning("[DeeplsTranslate] Connection.Connect: starting negotiation..."); // debug

                try
                {
                    var response = await client.PostAsync(
                        "https://ita-free.www.deepl.com/v1/sessions/negotiate?negotiateVersion=1", null);

                    if (!response.IsSuccessStatusCode)
                    {
                        Service.pluginLog.Warning($"[DeeplsTranslate] Connection.Connect: negotiate failed with status {response.StatusCode}"); // debug
                        return false;
                    }

                    var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
                    if (!json.TryGetProperty("connectionToken", out var tokenElement))
                    {
                        Service.pluginLog.Warning("[DeeplsTranslate] Connection.Connect: no connectionToken in negotiate response"); // debug
                        return false;
                    }

                    token = tokenElement.GetString();
                    Service.pluginLog.Warning($"[DeeplsTranslate] Connection.Connect: got token, starting receive loop"); // debug
                    cts = new CancellationTokenSource();
                    recvTask = ReceiveLoop(cts.Token);

                    await Send("{\"protocol\":\"messagepack\",\"version\":1}\x1e"u8.ToArray());
                    Service.pluginLog.Warning("[DeeplsTranslate] Connection.Connect: sent protocol handshake"); // debug
                    await PopMessageAsync();

                    // totally not sus
                    await Send(Convert.FromBase64String(
                        "TpUBgKEwrFN0YXJ0U2Vzc2lvbpHHOAEIARIwCgsIASIHCA5yAwjcCwohCAIiDQgFKgkKBwoFZW4tVVMiDggSkgEJCgcKBWVuLVVTGgIQAQ=="));
                    Service.pluginLog.Warning("[DeeplsTranslate] Connection.Connect: sent StartSession"); // debug

                    if (await PopMessageAsync() is not List<object> msg || OnError)
                    {
                        Service.pluginLog.Warning($"[DeeplsTranslate] Connection.Connect: StartSession response invalid or OnError={OnError}"); // debug
                        return false;
                    }

                    if (ExtractProto(msg, 4) is not { } data)
                    {
                        Service.pluginLog.Warning("[DeeplsTranslate] Connection.Connect: failed to extract proto from StartSession response"); // debug
                        return false;
                    }

                    var sessionToken = data.FromProtoBytes<StartSessionResponse>()?.SessionToken;
                    Service.pluginLog.Warning($"[DeeplsTranslate] Connection.Connect: got sessionToken={sessionToken != null}"); // debug

                    await SendFramed(MessagePackSerializer.Serialize<object?[]>(
                        [1, new object(), null, "AppendMessages", new[] { sessionToken }, new[] { "1" }],
                        MsgPackOptions));
                    Service.pluginLog.Warning("[DeeplsTranslate] Connection.Connect: sent AppendMessages"); // debug

                    var buf = new ArrayBufferWriter<byte>();
                    var writer = new MessagePackWriter(buf);
                    writer.WriteArrayHeader(5);
                    writer.Write(4);
                    writer.WriteMapHeader(0);
                    writer.Write("2");
                    writer.Write("GetMessages");
                    writer.WriteArrayHeader(2);
                    writer.Write(sessionToken);
                    writer.WriteExtensionFormat(new ExtensionResult(3, Array.Empty<byte>()));
                    writer.Flush();
                    await SendFramed(buf.WrittenSpan.ToArray());
                    Service.pluginLog.Warning("[DeeplsTranslate] Connection.Connect: sent GetMessages"); // debug

                    if (await PopMessageAsync() is not List<object> msg2)
                    {
                        Service.pluginLog.Warning("[DeeplsTranslate] Connection.Connect: GetMessages response invalid"); // debug
                        return false;
                    }

                    if (ExtractProto(msg2, 3) is { } data2 &&
                        data2.FromProtoBytes<ParticipantResponse>()?.PublishedMessage is { } published)
                    {
                        BVer = published.CurrentVersion?.Version?.Value ?? 0;
                        Service.pluginLog.Warning($"[DeeplsTranslate] Connection.Connect: initial BVer={BVer}, events={published.Events?.Count ?? 0}"); // debug
                        foreach (var evt in published.Events ?? []) // debug
                        { // debug
                            if (evt.TextChangeOperation is { } op) // debug
                                Service.pluginLog.Warning($"[DeeplsTranslate] Connection.Connect: initial event fieldName={evt.FieldName}, textOp range=[{op.Range?.Start ?? 0},{op.Range?.End ?? 0}], text='{op.Text}'"); // debug
                            else if (evt.SetPropertyOperation is { } setProp) // debug
                                Service.pluginLog.Warning($"[DeeplsTranslate] Connection.Connect: initial event fieldName={evt.FieldName}, setProp={setProp.PropertyName}"); // debug
                        } // debug
                    }

                    Service.pluginLog.Warning("[DeeplsTranslate] Connection.Connect: connected successfully"); // debug
                    return Connected = true;
                }
                catch (Exception ex) // debug
                {
                    Service.pluginLog.Warning($"[DeeplsTranslate] Connection.Connect: exception: {ex.Message}"); // debug
                    return false;
                }
            }

            public async Task Send(byte[] data)
            {
                (await client.PostAsync(Url, new ByteArrayContent(data))).EnsureSuccessStatusCode();
            }

            public async Task SendFramed(byte[] message)
            {
                var output = new List<byte>();
                var length = message.Length;

                do
                {
                    output.Add((byte)((length & 0x7F) | (length > 127 ? 0x80 : 0)));
                    length >>= 7;
                }
                while (length > 0);

                output.AddRange(message);
                await Send([.. output]);
            }

            public async Task<object?> PopMessageAsync(int timeoutMs = 5000)
            {
                using var timeoutCts = new CancellationTokenSource(timeoutMs);
                try
                {
                    return await messages.Reader.ReadAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }

            public async Task Close()
            {
                cts?.Cancel();
                if (recvTask is not null)
                {
                    await Task.WhenAny(recvTask, Task.Delay(1000));
                }
                Connected = false;
            }

            private static byte[]? ExtractProto(List<object> msg, int index)
            {
                if (index >= msg.Count)
                {
                    return null;
                }

                var bytes = ToBytes(msg[index]);
                if (bytes is not null)
                {
                    return bytes;
                }

                if (msg[index] is List<object> { Count: > 0 } nested)
                {
                    return ToBytes(nested[0]);
                }

                return null;
            }

            private static byte[]? ToBytes(object obj)
            {
                return obj switch
                {
                    byte[] b => b,
                    ExtensionResult e => e.Data.ToArray(),
                    _ => null
                };
            }

            private async Task ReceiveLoop(CancellationToken ct)
            {
                Service.pluginLog.Warning("[DeeplsTranslate] ReceiveLoop: started"); // debug
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var response = await client.GetAsync(Url, ct);
                        if (!response.IsSuccessStatusCode)
                        {
                            Service.pluginLog.Warning($"[DeeplsTranslate] ReceiveLoop: poll returned status {response.StatusCode}"); // debug
                            await Task.Delay(1000, ct);
                            continue;
                        }

                        var data = await response.Content.ReadAsByteArrayAsync(ct);

                        // Skip empty JSON responses
                        if (data is [123, .., 125, 0x1e])
                        {
                            continue;
                        }

                        Service.pluginLog.Warning($"[DeeplsTranslate] ReceiveLoop: received {data.Length} bytes"); // debug
                        foreach (var msgBytes in UnpackMessages(data))
                        {
                            var reader = new MessagePackReader(msgBytes);
                            var list = new List<object>();
                            var count = reader.ReadArrayHeader();

                            for (int i = 0; i < count; i++)
                            {
                                list.Add(ReadValue(ref reader)!);
                            }

                            if (list is [_, _, _, "OnError", ..])
                            {
                                var errorDetails = list.Count > 4 ? string.Join(", ", list.Skip(4).Select(x => x?.ToString() ?? "null")) : "no details"; // debug
                                Service.pluginLog.Warning($"[DeeplsTranslate] ReceiveLoop: received OnError [{string.Join(", ", list.Select(x => x?.ToString() ?? "null"))}], details: {errorDetails}"); // debug
                                (OnError, Connected) = (true, false);
                                await Close();
                            }
                            else if (list is [6, ..])
                            {
                                Service.pluginLog.Warning("[DeeplsTranslate] ReceiveLoop: received ping, sending pong"); // debug
                                await SendFramed(MessagePackSerializer.Serialize<object[]>([6], MsgPackOptions));
                            }
                            else
                            {
                                Service.pluginLog.Warning($"[DeeplsTranslate] ReceiveLoop: queuing message (count={list.Count})"); // debug
                                messages.Writer.TryWrite(list);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Service.pluginLog.Warning("[DeeplsTranslate] ReceiveLoop: cancelled"); // debug
                        break;
                    }
                    catch (Exception ex) // debug
                    {
                        Service.pluginLog.Warning($"[DeeplsTranslate] ReceiveLoop: exception: {ex.Message}"); // debug
                        await Task.Delay(1000, ct);
                    }
                }
                Service.pluginLog.Warning("[DeeplsTranslate] ReceiveLoop: exiting"); // debug
            }

            private static List<byte[]> UnpackMessages(byte[] data)
            {
                var result = new List<byte[]>();
                int position = 0;

                while (position < data.Length)
                {
                    int size = 0;
                    int byteIndex = 0;

                    while (true)
                    {
                        if (position + byteIndex >= data.Length)
                        {
                            throw new ArgumentException("Varint incomplete");
                        }

                        var currentByte = data[position + byteIndex];
                        size |= (currentByte & 0x7F) << VarintShifts[byteIndex++];

                        if ((currentByte & 0x80) == 0 || byteIndex >= 5)
                        {
                            break;
                        }
                    }

                    if (position + byteIndex + size > data.Length)
                    {
                        throw new ArgumentException("Incomplete message");
                    }

                    result.Add(data[(position + byteIndex)..(position + byteIndex + size)]);
                    position += byteIndex + size;
                }

                return result;
            }

            private static object? ReadValue(ref MessagePackReader reader, bool nested = false)
            {
                switch (reader.NextMessagePackType)
                {
                    case MessagePackType.Extension:
                        var ext = reader.ReadExtensionFormat();
                        return ext.Header.TypeCode == 4 ? ext.Data.ToArray() : ext;

                    case MessagePackType.String:
                        return reader.ReadString();

                    case MessagePackType.Integer:
                        return reader.ReadInt32();

                    case MessagePackType.Array when !nested:
                        var list = new List<object?>();
                        var count = reader.ReadArrayHeader();
                        for (int i = 0; i < count; i++)
                        {
                            list.Add(ReadValue(ref reader, true));
                        }
                        return list;

                    default:
                        reader.Skip();
                        return null;
                }
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    internal static class ProtoExtensions
    {
        public static T? FromProtoBytes<T>(this byte[] data)
        {
            if (data?.Length > 0)
            {
                return Serializer.Deserialize<T>(new MemoryStream(data));
            }
            return default;
        }

        public static byte[] ToProtoBytes<T>(this T data)
        {
            var stream = new MemoryStream();
            Serializer.Serialize(stream, data);
            return stream.ToArray();
        }
    }
}
