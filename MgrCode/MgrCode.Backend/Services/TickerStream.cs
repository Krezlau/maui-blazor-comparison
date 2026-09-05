using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MgrCode.Backend.Models;

namespace MgrCode.Backend.Services;

public sealed class TickerStream : IAsyncEnumerable<Ticker>
{
    private const int BufferSize = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Uri _uri;
    private readonly string[] _symbols;
    private readonly TimeSpan _heartbeat;
    private readonly TimeSpan _staleTimeout;
    private readonly TimeSpan _reconnectDelay;

    public TickerStream(Uri uri, IEnumerable<string> symbols, int heartbeatMs = 10_000, int reconnectDelayMs = 2_000)
    {
        _uri = uri;
        _symbols = symbols.ToArray();
        _heartbeat = TimeSpan.FromMilliseconds(heartbeatMs);
        _staleTimeout = TimeSpan.FromMilliseconds(Math.Max(heartbeatMs * 3, 1_500));
        _reconnectDelay = TimeSpan.FromMilliseconds(reconnectDelayMs);
    }

    public async IAsyncEnumerator<Ticker> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[BufferSize];
        var debug = Environment.GetEnvironmentVariable("MGR_DEBUG_STREAM") == "1";
        long frames = 0, parsed = 0, subscribeBytes = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            using var socket = new ClientWebSocket();

            try
            {
                await socket.ConnectAsync(_uri, cancellationToken);
                if (debug) Console.Error.WriteLine($"[stream] connected {_uri}");
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (Exception)
            {
                await DelayReconnectAsync(cancellationToken);
                continue;
            }

            if (socket.State != WebSocketState.Open)
            {
                await DelayReconnectAsync(cancellationToken);
                continue;
            }

            try
            {
                var payload = BuildSubscribePayload();
                await SendAsync(socket, payload, cancellationToken);
                subscribeBytes = payload.Length;
                if (debug) Console.Error.WriteLine($"[stream] subscribed {_symbols.Length} symbols ({subscribeBytes}b)");
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (Exception)
            {
                await DelayReconnectAsync(cancellationToken);
                continue;
            }

            if (socket.State != WebSocketState.Open)
            {
                await DelayReconnectAsync(cancellationToken);
                continue;
            }

            var lastReceived = Stopwatch.GetTimestamp();
            var lastSent = lastReceived;

            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                if (Stopwatch.GetElapsedTime(lastSent) >= _heartbeat)
                {
                    try
                    {
                        await SendAsync(socket, BuildPingPayload(), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        yield break;
                    }
                    catch (Exception)
                    {
                        break;
                    }

                    lastSent = Stopwatch.GetTimestamp();
                }

                if (Stopwatch.GetElapsedTime(lastReceived) >= _staleTimeout)
                    break;

                var frame = await ReceiveFrameAsync(socket, buffer, cancellationToken);
                if (frame is null)
                    break;

                lastReceived = Stopwatch.GetTimestamp();
                frames++;
                if (TryParseTicker(frame, out var ticker) && ticker is not null)
                {
                    parsed++;
                    yield return ticker;
                }
                else if (debug)
                {
                    Console.Error.WriteLine($"[stream] unparsed frame #{frames}: {Truncate(frame, 120)}");
                }
            }

            if (debug)
                Console.Error.WriteLine($"[stream] loop exit: frames={frames} parsed={parsed} subscribe={subscribeBytes}b state={socket.State}");
        }
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";

    private async Task DelayReconnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_reconnectDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private byte[] BuildSubscribePayload()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("op", "subscribe");
            writer.WritePropertyName("args");
            writer.WriteStartArray();
            foreach (var symbol in _symbols)
                writer.WriteStringValue($"ticker.{symbol}");
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static byte[] BuildPingPayload()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("op", "ping");
            writer.WriteNumber("ts", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static Task SendAsync(ClientWebSocket socket, byte[] payload, CancellationToken cancellationToken)
        => socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellationToken);

    private static async Task<string?> ReceiveFrameAsync(ClientWebSocket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        try
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                    return null;
                message.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(message.ToArray());
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (WebSocketException)
        {
            return null;
        }
    }

    private static bool TryParseTicker(string frame, out Ticker? ticker)
    {
        ticker = null;
        if (string.IsNullOrEmpty(frame))
            return false;

        using var document = JsonDocument.Parse(frame);
        var root = document.RootElement;

        if (!root.TryGetProperty("topic", out var topic))
            return false;

        var topicName = topic.GetString();
        if (topicName is null || !topicName.StartsWith("ticker.", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!root.TryGetProperty("data", out var data))
            return false;

        var wire = data.Deserialize<WireTicker>(JsonOptions);
        if (wire is null || string.IsNullOrEmpty(wire.Symbol))
            return false;

        ticker = BybitWire.ToTicker(wire);
        return true;
    }
}
