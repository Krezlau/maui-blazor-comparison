using MgrCode.Backend.Models;

namespace MgrCode.Backend.Services;

public abstract class BybitService : IBybitService
{
    private readonly HttpClient _http;
    private readonly string _restTickersUrl;
    private readonly string _webSocketUrl;
    private readonly int _heartbeatMs;
    private readonly int _reconnectDelayMs;

    protected BybitService(HttpClient http, string restTickersUrl, string webSocketUrl, int heartbeatMs, int reconnectDelayMs)
    {
        _http = http;
        _restTickersUrl = restTickersUrl;
        _webSocketUrl = webSocketUrl;
        _heartbeatMs = heartbeatMs;
        _reconnectDelayMs = reconnectDelayMs;
    }

    public virtual Task InitializeAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual async Task<IReadOnlyList<Ticker>> GetTickersAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(_restTickersUrl, HttpCompletionOption.ResponseContentRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var wire = await System.Text.Json.JsonSerializer.DeserializeAsync<WireTickersResponse>(stream, BybitWire.JsonOptions, cancellationToken);

        if (wire is null || wire.RetCode != 0 || wire.Result?.List is null)
            return Array.Empty<Ticker>();

        return wire.Result.List.Select(BybitWire.ToTicker).ToList();
    }

    public virtual IAsyncEnumerable<Ticker> Subscribe(IEnumerable<string> symbols)
        => new TickerStream(
            new Uri(_webSocketUrl),
            symbols,
            _heartbeatMs,
            _reconnectDelayMs);
}
