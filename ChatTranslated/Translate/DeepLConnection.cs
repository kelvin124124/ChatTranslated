using ChatTranslated.Utils;
using MessagePack;
using ProtoBuf;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ChatTranslated.Translate
{
    internal class DeepLConnection
    {
        private static readonly MessagePackSerializerOptions MsgOpts = MessagePackSerializerOptions.Standard.WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);
        private static readonly int[] VarintShifts = [0, 7, 14, 21, 28];
        private readonly HttpClient _client = new() { DefaultRequestHeaders = { { "User-Agent", "Mozilla/5.0" }, { "Accept-Language", "en-US" } } };
        private readonly Channel<object> _messages = Channel.CreateUnbounded<object>();
        private string? _token;
        private CancellationTokenSource? _cts;
        private Task? _recvTask;

        public bool Connected { get; private set; }
        public bool OnError { get; private set; }
        public string OnErrorLast { get; private set; } = "";
        public int BVer { get; internal set; }

        private string Url => $"https://ita-free.www.deepl.com/v1/sessions?id={_token}&_={DateTime.UtcNow.Ticks / 10000}";

        public async Task<bool> Connect()
        {
            if (Connected) return true;
            (Connected, OnError) = (false, false);

            try
            {
                var resp = await _client.PostAsync("https://ita-free.www.deepl.com/v1/sessions/negotiate?negotiateVersion=1", null);
                if (!resp.IsSuccessStatusCode) return false;

                var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(await resp.Content.ReadAsStringAsync());
                if (!json.TryGetProperty("connectionToken", out var t)) return false;
                _token = t.GetString();

                _cts = new();
                _recvTask = RecvLoop(_cts.Token);

                await Send("{\"protocol\":\"messagepack\",\"version\":1}\x1e"u8.ToArray());
                await PopMessageAsync();

                await Send(Convert.FromBase64String("TpUBgKEwrFN0YXJ0U2Vzc2lvbpHHOAEIARIwCgsIASIHCA5yAwjcCwohCAIiDQgFKgkKBwoFZW4tVVMiDggSkgEJCgcKBWVuLVVTGgIQAQ=="));
                if (await PopMessageAsync() is not List<object> msg || OnError) return false;

                if (ExtractProto(msg, 4) is not { } data) return false;
                var sessionToken = data.FromProtoBytes<StartSessionResponse>()?.SessionToken;

                await SendFramed(MessagePackSerializer.Serialize<object?[]>([1, new object(), null, "AppendMessages", new[] { sessionToken }, new[] { "1" }], MsgOpts));

                var buf = new ArrayBufferWriter<byte>();
                var w = new MessagePackWriter(buf);
                w.WriteArrayHeader(5); w.Write(4); w.WriteMapHeader(0); w.Write("2"); w.Write("GetMessages");
                w.WriteArrayHeader(2); w.Write(sessionToken); w.WriteExtensionFormat(new ExtensionResult(3, Array.Empty<byte>()));
                w.Flush();
                await SendFramed(buf.WrittenSpan.ToArray());

                if (await PopMessageAsync() is not List<object> msg2) return false;
                if (ExtractProto(msg2, 3) is { } data2 && data2.FromProtoBytes<ParticipantResponse>()?.PublishedMessage is { } pub)
                    BVer = pub.CurrentVersion?.Version?.Value ?? 0;

                return Connected = true;
            }
            catch (Exception ex)
            {
                Service.pluginLog.Debug($"DeepLConnection.Connect failed: {ex.Message}");
                return false;
            }
        }

        public async Task Send(byte[] data) => (await _client.PostAsync(Url, new ByteArrayContent(data))).EnsureSuccessStatusCode();

        public async Task SendFramed(byte[] message)
        {
            var output = new List<byte>();
            var v = message.Length;
            do { output.Add((byte)((v & 0x7F) | (v > 127 ? 0x80 : 0))); v >>= 7; } while (v > 0);
            output.AddRange(message);
            await Send([.. output]);
        }

        public async Task<object?> PopMessageAsync(int ms = 5000)
        {
            using var cts = new CancellationTokenSource(ms);
            try { return await _messages.Reader.ReadAsync(cts.Token); }
            catch (OperationCanceledException) { return null; }
        }

        public async Task Close()
        {
            _cts?.Cancel();
            if (_recvTask is not null) await Task.WhenAny(_recvTask, Task.Delay(1000));
            Connected = false;
        }

        private static byte[]? ExtractProto(List<object> msg, int i) =>
            i < msg.Count ? ToBytes(msg[i]) ?? (msg[i] is List<object> { Count: > 0 } a ? ToBytes(a[0]) : null) : null;

        private static byte[]? ToBytes(object o) => o switch { byte[] b => b, ExtensionResult e => e.Data.ToArray(), _ => null };

        private async Task RecvLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
                try
                {
                    var res = await _client.GetAsync(Url, ct);
                    if (!res.IsSuccessStatusCode) { await Task.Delay(1000, ct); continue; }
                    var data = await res.Content.ReadAsByteArrayAsync(ct);
                    if (data is [123, .., 125, 0x1e]) continue;

                    foreach (var msgBytes in UnpackMessages(data))
                    {
                        var reader = new MessagePackReader(msgBytes);
                        var list = new List<object>();
                        for (int i = 0, c = reader.ReadArrayHeader(); i < c; i++) list.Add(ReadValue(ref reader)!);

                        if (list is [_, _, _, "OnError", ..]) { (OnError, Connected) = (true, false); await Close(); }
                        else if (list is [6, ..]) await SendFramed(MessagePackSerializer.Serialize<object[]>([6], MsgOpts));
                        else _messages.Writer.TryWrite(list);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(1000, ct); }
        }

        private static List<byte[]> UnpackMessages(byte[] data)
        {
            List<byte[]> messages = [];
            for (int r = 0; r < data.Length;)
            {
                int size = 0, i = 0, s;
                while (true)
                {
                    if (r + i >= data.Length) throw new ArgumentException("Varint incomplete");
                    s = data[r + i];
                    size |= (s & 0x7F) << VarintShifts[i++];
                    if ((s & 0x80) == 0 || i >= 5) break;
                }
                if (r + i + size > data.Length) throw new ArgumentException("Incomplete message");
                messages.Add(data[(r + i)..(r + i + size)]);
                r += i + size;
            }
            return messages;
        }

        private static object? ReadValue(ref MessagePackReader r, bool nested = false)
        {
            switch (r.NextMessagePackType)
            {
                case MessagePackType.Extension:
                    var e = r.ReadExtensionFormat();
                    return e.Header.TypeCode == 4 ? e.Data.ToArray() : e;
                case MessagePackType.String: return r.ReadString();
                case MessagePackType.Integer: return r.ReadInt32();
                case MessagePackType.Array when !nested:
                    var list = new List<object?>();
                    for (int j = 0, c = r.ReadArrayHeader(); j < c; j++) list.Add(ReadValue(ref r, true));
                    return list;
                default: r.Skip(); return null;
            }
        }
    }

    internal static class DeepLProtoExtensions
    {
        public static T? FromProtoBytes<T>(this byte[] data) => data?.Length > 0 ? Serializer.Deserialize<T>(new MemoryStream(data)) : default;
        public static byte[] ToProtoBytes<T>(this T data) { var s = new MemoryStream(); Serializer.Serialize(s, data); return s.ToArray(); }
    }
}
