namespace MgrCode.Backend.Services;

public sealed class MockBybitService : BybitService
{
    public MockBybitService(HttpClient http, BackendOptions options)
        : base(
            http,
            $"{options.BaseUrl.TrimEnd('/')}/v5/market/tickers?category=spot",
            options.WebSocketUrl,
            options.HeartbeatMs,
            options.ReconnectDelayMs)
    {
    }
}
