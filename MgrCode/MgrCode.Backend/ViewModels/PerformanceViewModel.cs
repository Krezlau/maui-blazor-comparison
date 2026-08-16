using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using MgrCode.Backend.Models;

namespace MgrCode.Backend.ViewModels;

public partial class PerformanceViewModel : ObservableObject
{
    private long _updateStartTicks;

    [ObservableProperty]
    private double _updateLatencyMs;

    [ObservableProperty]
    private double _fps;

    [ObservableProperty]
    private int _droppedFrames;

    [ObservableProperty]
    private double _uiThreadBusyPct;

    [ObservableProperty]
    private double _memoryMb;

    [ObservableProperty]
    private long _gcGen0;

    [ObservableProperty]
    private long _gcGen1;

    [ObservableProperty]
    private long _gcGen2;

    [ObservableProperty]
    private double _gcPauseMs;

    [ObservableProperty]
    private double _uptimeMs;

    [ObservableProperty]
    private string _runLabel = string.Empty;

    public void RecordUpdateStart()
        => _updateStartTicks = Stopwatch.GetTimestamp();

    public void RecordRenderEnd()
    {
        if (_updateStartTicks == 0)
            return;

        UpdateLatencyMs = Stopwatch.GetElapsedTime(_updateStartTicks).TotalMilliseconds;
        _updateStartTicks = 0;
    }

    public PerformanceSnapshot Capture(string runLabel, DateTimeOffset timestamp)
        => new(
            Timestamp: timestamp,
            TickerCount: 0,
            UpdateLatencyMs: UpdateLatencyMs,
            Fps: Fps,
            DroppedFrames: DroppedFrames,
            UiThreadBusyPct: UiThreadBusyPct,
            MemoryMb: MemoryMb,
            GcGen0: GcGen0,
            GcGen1: GcGen1,
            GcGen2: GcGen2,
            GcPauseMs: GcPauseMs,
            UptimeMs: UptimeMs);
}
