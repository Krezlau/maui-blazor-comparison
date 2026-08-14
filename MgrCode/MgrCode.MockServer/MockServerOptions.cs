namespace MgrCode.MockServer;

public sealed class MockServerOptions
{
    public const string SectionName = "MockServer";

    public int TickerCount { get; set; } = 100;

    public int UpdateIntervalMs { get; set; } = 100;

    public int LatencyMs { get; set; }

    public bool BurstMode { get; set; }

    public int BurstSize { get; set; } = 500;
}
