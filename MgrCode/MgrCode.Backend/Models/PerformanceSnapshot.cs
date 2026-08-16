namespace MgrCode.Backend.Models;

public sealed record PerformanceSnapshot(
    DateTimeOffset Timestamp,
    int TickerCount,
    double UpdateLatencyMs,
    double Fps,
    int DroppedFrames,
    double UiThreadBusyPct,
    double MemoryMb,
    long GcGen0,
    long GcGen1,
    long GcGen2,
    double GcPauseMs,
    double UptimeMs);
