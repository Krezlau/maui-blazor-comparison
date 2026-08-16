namespace MgrCode.Backend.Services;

public sealed class RealBybitService : BybitService
{
    private const string RestTickersUrl = "https://api.bybit.com/v5/market/tickers?category=spot";
    private const string WebSocketUrl = "wss://stream.bybit.com/v5/public/spot";

    public RealBybitService(HttpClient http)
        : base(http, RestTickersUrl, WebSocketUrl, 10_000, 2_000)
    {
    }
}
