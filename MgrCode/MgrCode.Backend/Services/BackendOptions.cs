namespace MgrCode.Backend.Services;

public sealed class BackendOptions
{
    public const string SectionName = "Bybit";

    public string BaseUrl { get; set; } = "http://localhost:5010";

    public string WebSocketUrl { get; set; } = "ws://localhost:5010/v5/public/spot";

    public int HeartbeatMs { get; set; } = 10_000;

    public int ReconnectDelayMs { get; set; } = 2_000;
}
