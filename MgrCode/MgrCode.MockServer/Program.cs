using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MgrCode.MockServer;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<MockServerOptions>(builder.Configuration.GetSection(MockServerOptions.SectionName));
builder.Logging.AddConsole();

var app = builder.Build();

var options = app.Configuration.GetSection(MockServerOptions.SectionName).Get<MockServerOptions>()
    ?? new MockServerOptions();

var generator = new MockDataGenerator(options.TickerCount);
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    DefaultIgnoreCondition = JsonIgnoreCondition.Never
};

app.UseWebSockets();

using var updateCts = new CancellationTokenSource();
_ = Task.Run(() => UpdateLoopAsync(generator, options, updateCts.Token));

app.MapGet("/v5/market/tickers", (string? category) =>
{
    var tickers = generator.GetSnapshot();
    return Results.Json(new TickersResponse(
        RetCode: 0,
        RetMsg: "OK",
        Result: new TickersResult(category ?? "spot", tickers.ToList()),
        Time: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), jsonOptions);
});

app.Map("/v5/public/spot", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var connId = Guid.NewGuid().ToString("N")[..16];
    var subscriptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var buffer = new byte[16 * 1024];
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);

    var sender = Task.Run(async () =>
    {
        var debug = Environment.GetEnvironmentVariable("MGR_DEBUG_SERVER") == "1";
        long sent = 0;
        try
        {
            while (!cts.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                if (options.LatencyMs > 0)
                    await Task.Delay(options.LatencyMs, cts.Token);

                string[] subscribed;
                lock (subscriptions)
                    subscribed = subscriptions.ToArray();

                if (subscribed.Length > 0)
                {
                    if (debug)
                        Console.WriteLine($"[server] conn={connId} sending batch for {subscribed.Length} topics");
                    await SendTickerUpdatesAsync(socket, generator, subscribed, options, cts.Token);
                    sent += subscribed.Length;
                    if (debug)
                        Console.WriteLine($"[server] conn={connId} sent batch (total {sent})");
                }

                await Task.Delay(options.UpdateIntervalMs, cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex)
        {
            if (debug) Console.WriteLine($"[server] conn={connId} ws exception: {ex.Message}");
        }
        catch (Exception ex)
        {
            if (debug) Console.WriteLine($"[server] conn={connId} exception: {ex}");
        }
    });

    try
    {
        while (socket.State == WebSocketState.Open && !cts.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            var message = new MemoryStream();
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
                message.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            var payload = Encoding.UTF8.GetString(message.ToArray());
            await HandleClientMessageAsync(socket, connId, payload, subscriptions, jsonOptions, cts.Token);
        }
    }
    catch (OperationCanceledException) { }
    catch (WebSocketException) { }
    finally
    {
        cts.Cancel();
        try { await sender; } catch { }
        if (socket.State != WebSocketState.Closed)
        {
            try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); }
            catch { }
        }
    }
});

app.Logger.LogInformation(
    "Bybit mock server ready: {Tickers} tickers, update every {Interval}ms, burst={Burst}, REST http://localhost:5010/v5/market/tickers, WS ws://localhost:5010/v5/public/spot",
    options.TickerCount, options.UpdateIntervalMs, options.BurstMode);

app.Run();

static async Task UpdateLoopAsync(MockDataGenerator generator, MockServerOptions options, CancellationToken token)
{
    try
    {
        while (!token.IsCancellationRequested)
        {
            generator.Tick();
            await Task.Delay(options.UpdateIntervalMs, token);
        }
    }
    catch (OperationCanceledException) { }
}

static async Task SendTickerUpdatesAsync(
    WebSocket socket,
    MockDataGenerator generator,
    string[] subscriptions,
    MockServerOptions options,
    CancellationToken token)
{
    if (!options.BurstMode)
    {
        foreach (var topic in subscriptions)
        {
            var symbol = TopicToSymbol(topic);
            var ticker = generator.GetTicker(symbol);
            if (ticker is null) continue;

            var update = new WsTickerUpdate(
                Topic: topic,
                Type: "snapshot",
                Ts: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Data: ticker);
            await SendJsonAsync(socket, update, token);
        }
        return;
    }

    var pool = subscriptions
        .Select(TopicToSymbol)
        .Where(s => generator.GetTicker(s) is not null)
        .ToArray();

    if (pool.Length == 0) return;

    var burst = Math.Max(options.BurstSize, 1);
    for (var i = 0; i < burst; i++)
    {
        var symbol = pool[i % pool.Length];
        var ticker = generator.GetTicker(symbol);
        var update = new WsTickerUpdate(
            Topic: $"ticker.{symbol}",
            Type: "snapshot",
            Ts: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data: ticker!);
        await SendJsonAsync(socket, update, token);
    }
}

static async Task HandleClientMessageAsync(
    WebSocket socket,
    string connId,
    string payload,
    HashSet<string> subscriptions,
    JsonSerializerOptions jsonOptions,
    CancellationToken token)
{
    using var doc = JsonDocument.Parse(payload);
    var root = doc.RootElement;

    if (!root.TryGetProperty("op", out var opElement))
        return;

    var op = opElement.GetString();
    if (op is null) return;

    switch (op)
    {
        case "ping":
            await SendJsonAsync(socket, new WsPong("pong", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), token);
            return;

        case "subscribe":
        case "unsubscribe":
            var args = root.TryGetProperty("args", out var argsElement)
                ? argsElement.EnumerateArray().Select(x => x.GetString()).Where(x => x is not null).Cast<string>().ToArray()
                : Array.Empty<string>();

            lock (subscriptions)
            {
                foreach (var arg in args)
                {
                    if (op == "subscribe") subscriptions.Add(arg);
                    else subscriptions.Remove(arg);
                }
            }

            var response = new WsSubscribeResponse(
                Success: true,
                RetMsg: op,
                ConnId: connId,
                ReqId: null,
                Op: op,
                Args: args);
            await SendJsonAsync(socket, response, token);
            return;
    }
}

static async Task SendJsonAsync(WebSocket socket, object value, CancellationToken token)
{
    var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
}

static string TopicToSymbol(string topic)
    => topic.StartsWith("ticker.", StringComparison.OrdinalIgnoreCase)
        ? topic[7..]
        : topic;
