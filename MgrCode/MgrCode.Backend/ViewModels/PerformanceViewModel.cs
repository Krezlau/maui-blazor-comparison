using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using MgrCode.Backend.Models;

namespace MgrCode.Backend.ViewModels;

public partial class PerformanceViewModel : ObservableObject
{
    private long _updateStartTicks;
    private long _renderCount;
    private long _appliedCount;
    private double _refreshRateHz = 60d;
    private readonly long _startedAt = Stopwatch.GetTimestamp();
    private long _lastSample = Stopwatch.GetTimestamp();

    private const double ExpectedSampleMs = 500d;

    /// <summary>
    /// Called once per app at startup with the real display refresh rate
    /// (<see cref="DeviceDisplay.MainDisplayInfo.RefreshRate"/>). Drives the
    /// dropped-frame estimate in <see cref="SampleStats"/>.
    /// </summary>
    public void SetRefreshRate(double refreshRateHz)
    {
        if (refreshRateHz > 0)
            _refreshRateHz = refreshRateHz;
    }

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

    [ObservableProperty]
    private bool _showHud = true;

    [ObservableProperty]
    private double _updatesPerSecond;

    /// <summary>
    /// t0 hook — stamped before a ticker update is dispatched to the UI thread.
    /// </summary>
    public void RecordUpdateStart()
    {
        _updateStartTicks = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref _appliedCount);
    }

    /// <summary>
    /// t1 hook — called from each app's "render committed" hook (XAML sparkline Draw /
    /// Blazor OnAfterRenderAsync). Computes update→render latency; the second hook for the
    /// same update is a no-op because the pending timestamp was consumed.
    /// </summary>
    public void RecordRenderEnd()
    {
        Interlocked.Increment(ref _renderCount);
        if (_updateStartTicks == 0)
            return;
        UpdateLatencyMs = Stopwatch.GetElapsedTime(_updateStartTicks).TotalMilliseconds;
        _updateStartTicks = 0;
    }

    /// <summary>
    /// Called by each app's ~500ms timer: derives FPS / updates-per-sec from the hooks,
    /// estimates dropped frames and UI-thread pressure from sample drift, and samples
    /// memory + GC counters. Full GC-pause instrumentation stays a stub (see plan).
    /// </summary>
    public void SampleStats()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsedMs = Stopwatch.GetElapsedTime(_lastSample).TotalMilliseconds;
        var seconds = elapsedMs / 1000d;
        if (seconds <= 0)
        {
            _lastSample = now;
            return;
        }

        var renders = Interlocked.Exchange(ref _renderCount, 0);
        var applied = Interlocked.Exchange(ref _appliedCount, 0);

        Fps = renders / seconds;
        UpdatesPerSecond = applied / seconds;

        // UI-thread pressure: a busy UI thread delays the 500ms sample timer continuation,
        // so the measured interval exceeds the requested one.
        UiThreadBusyPct = Math.Clamp((elapsedMs - ExpectedSampleMs) / ExpectedSampleMs, 0d, 1d) * 100d;

        // Dropped-frame estimate: frames that the refresh rate expected but that never rendered.
        var expectedFrames = Math.Ceiling(_refreshRateHz * seconds);
        DroppedFrames = (int)Math.Max(0, expectedFrames - renders);

        _lastSample = now;

        MemoryMb = GC.GetTotalMemory(false) / (1024d * 1024d);
        GcGen0 = GC.CollectionCount(0);
        GcGen1 = GC.CollectionCount(1);
        GcGen2 = GC.CollectionCount(2);
        UptimeMs = Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds;
    }

    public PerformanceSnapshot Capture(DateTimeOffset timestamp, int tickerCount)
        => new(
            Timestamp: timestamp,
            TickerCount: tickerCount,
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