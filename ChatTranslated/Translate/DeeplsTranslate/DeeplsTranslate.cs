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
            if (!TryGetLanguageCode(targetLanguage, out var targetLang))
            {
                throw new Exception($"Target language '{targetLanguage}' not supported by DeepL.");
            }

            const int maxRetries = 1;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                await EnsureConnectedAsync().ConfigureAwait(false);

                if (connection?.Connected != true)
                {
                    if (attempt < maxRetries)
                    {
                        await ResetConnectionAsync().ConfigureAwait(false);
                        continue;
                    }
                    return await TryDeepLApi(text, targetLang);
                }

                string? result = await GetTranslations(text, targetLang).ConfigureAwait(false);

                if (!result.IsNullOrWhitespace())
                {
                    return (result!, TranslationMode.DeepL);
                }

                if (attempt < maxRetries)
                {
                    await ResetConnectionAsync().ConfigureAwait(false);
                    continue;
                }
            }

            return await TryDeepLApi(text, targetLang);
        }

        private static async Task<(string, TranslationMode?)> TryDeepLApi(string text, string targetLang)
        {
            if (!Service.configuration.DeepL_API_Key.IsNullOrWhitespace() &&
                Service.configuration.DeepL_API_Key != "YOUR-API-KEY:fx")
            {
                return await DeepLTranslate.Translate(text, targetLang);
            }

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
                return;
            }

            await sessionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (connection?.Connected == true)
                {
                    return;
                }

                if (connection != null)
                {
                    await connection.Close().ConfigureAwait(false);
                }

                connection = new Connection();
                await connection.Connect().ConfigureAwait(false);
            }
            finally
            {
                sessionLock.Release();
            }
        }

        private async Task ResetConnectionAsync()
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

        private async Task<string?> GetTranslations(string text, string targetLang)
        {
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

            await connection.SendFramed(buf.WrittenSpan.ToArray());

            while (true)
            {
                var msg = await connection.PopMessageAsync();
                if (msg is null || connection.OnError)
                {
                    return null;
                }

                if (msg is not List<object> { Count: >= 4 } msgList)
                {
                    continue;
                }

                var protoBytes = msgList[3] switch
                {
                    byte[] b => b,
                    ExtensionResult e => e.Data.ToArray(),
                    _ => null
                };

                if (protoBytes is null)
                {
                    continue;
                }

                var response = protoBytes.FromProtoBytes<ParticipantResponse>();
                if (response?.MetaInfoMessage?.Idle is not null)
                {
                    return outputText;
                }

                if (response?.PublishedMessage is { } published)
                {
                    if (published.CurrentVersion?.Version is not null)
                    {
                        connection.BVer = published.CurrentVersion.Version.Value;
                    }

                    foreach (var evt in published.Events ?? [])
                    {
                        if (evt.TextChangeOperation is { } op)
                        {
                            if (evt.FieldName == 2)
                            {
                                outputText = ApplyTextChange(outputText, op);
                            }
                            else if (evt.FieldName == 1)
                            {
                                inputText = ApplyTextChange(inputText, op);
                            }
                        }
                    }
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
                    return true;
                }

                (Connected, OnError) = (false, false);

                try
                {
                    var response = await client.PostAsync(
                        "https://ita-free.www.deepl.com/v1/sessions/negotiate?negotiateVersion=1", null);

                    if (!response.IsSuccessStatusCode)
                    {
                        return false;
                    }

                    var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
                    if (!json.TryGetProperty("connectionToken", out var tokenElement))
                    {
                        return false;
                    }

                    token = tokenElement.GetString();
                    cts = new CancellationTokenSource();
                    recvTask = ReceiveLoop(cts.Token);

                    await Send("{\"protocol\":\"messagepack\",\"version\":1}\x1e"u8.ToArray());
                    await PopMessageAsync();

                    // totally not sus
                    await Send(Convert.FromBase64String(
                        "TpUBgKEwrFN0YXJ0U2Vzc2lvbpHHOAEIARIwCgsIASIHCA5yAwjcCwohCAIiDQgFKgkKBwoFZW4tVVMiDggSkgEJCgcKBWVuLVVTGgIQAQ=="));

                    if (await PopMessageAsync() is not List<object> msg || OnError)
                    {
                        return false;
                    }

                    if (ExtractProto(msg, 4) is not { } data)
                    {
                        return false;
                    }

                    var sessionToken = data.FromProtoBytes<StartSessionResponse>()?.SessionToken;

                    await SendFramed(MessagePackSerializer.Serialize<object?[]>(
                        [1, new object(), null, "AppendMessages", new[] { sessionToken }, new[] { "1" }],
                        MsgPackOptions));

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

                    if (await PopMessageAsync() is not List<object> msg2)
                    {
                        return false;
                    }

                    if (ExtractProto(msg2, 3) is { } data2 &&
                        data2.FromProtoBytes<ParticipantResponse>()?.PublishedMessage is { } published)
                    {
                        BVer = published.CurrentVersion?.Version?.Value ?? 0;
                    }

                    return Connected = true;
                }
                catch
                {
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
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var response = await client.GetAsync(Url, ct);
                        if (!response.IsSuccessStatusCode)
                        {
                            await Task.Delay(1000, ct);
                            continue;
                        }

                        var data = await response.Content.ReadAsByteArrayAsync(ct);

                        // Skip empty JSON responses
                        if (data is [123, .., 125, 0x1e])
                        {
                            continue;
                        }

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
                                (OnError, Connected) = (true, false);
                                await Close();
                            }
                            else if (list is [6, ..])
                            {
                                await SendFramed(MessagePackSerializer.Serialize<object[]>([6], MsgPackOptions));
                            }
                            else
                            {
                                messages.Writer.TryWrite(list);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        await Task.Delay(1000, ct);
                    }
                }
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
