using System.Globalization;
using System.Threading.Channels;
using MgrCode.Backend.Models;

namespace MgrCode.Backend.Services;

/// <summary>
/// Writes <see cref="PerformanceSnapshot"/> rows to a fixed-schema CSV file.
/// Rows are queued through a Channel and drained by a background writer so
/// no file I/O ever happens on the UI thread. <see cref="Complete"/> flushes
/// the writer and appends a metadata footer.
/// </summary>
public sealed class CsvPerformanceSink : IDisposable
{
    private static readonly string[] Columns =
    {
        "timestamp", "tickers", "latMs", "fps", "droppedFrames",
        "uiBusyPct", "memMb", "gc0", "gc1", "gc2", "gcPauseMs", "uptimeMs"
    };

    private readonly string _path;
    private readonly IReadOnlyDictionary<string, string> _metadata;
    private readonly object _startLock = new();
    private readonly Channel<PerformanceSnapshot> _channel =
        Channel.CreateUnbounded<PerformanceSnapshot>(new UnboundedChannelOptions { SingleReader = true });

    private bool _started;
    private bool _completed;
    private Task? _writerTask;

    public CsvPerformanceSink(string outputDirectory, string runLabel, IReadOnlyDictionary<string, string>? metadata = null)
    {
        _metadata = metadata ?? new Dictionary<string, string>();
        Directory.CreateDirectory(outputDirectory);
        _path = Path.Combine(outputDirectory, SanitizeFileName(runLabel) + ".csv");
    }

    public string FilePath => _path;

    /// <summary>Queues one row for the background writer. Cheap; safe from any thread.</summary>
    public void Append(PerformanceSnapshot snapshot)
    {
        EnsureStarted();
        _channel.Writer.TryWrite(snapshot);
    }

    /// <summary>Stops the writer, flushes remaining rows and writes the metadata footer.</summary>
    public void Complete()
    {
        lock (_startLock)
        {
            if (_completed)
                return;
            _completed = true;
        }

        if (_started)
        {
            _channel.Writer.TryComplete();
            try
            {
                _writerTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
            }
        }

        AppendFooter();
    }

    private void EnsureStarted()
    {
        lock (_startLock)
        {
            if (_started)
                return;
            _started = true;
            _writerTask = Task.Run(WriteLoopAsync);
        }
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            var headerWritten = false;
            await foreach (var snapshot in _channel.Reader.ReadAllAsync())
            {
                if (!headerWritten)
                {
                    await File.WriteAllTextAsync(_path, string.Join(',', Columns) + Environment.NewLine);
                    headerWritten = true;
                }

                await File.AppendAllTextAsync(_path, FormatRow(snapshot) + Environment.NewLine);
            }
        }
        catch (Exception)
        {
        }
    }

    private void AppendFooter()
    {
        if (_metadata.Count == 0)
            return;

        try
        {
            var lines = new List<string> { "# --- metadata ---" };
            lines.AddRange(_metadata.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(
                kv => $"# {SanitizeCell(kv.Key)}={SanitizeCell(kv.Value)}"));
            File.AppendAllLines(_path, lines);
        }
        catch (Exception)
        {
        }
    }

    public static string FormatRow(PerformanceSnapshot s)
    {
        var i = CultureInfo.InvariantCulture;
        return string.Join(',', new[]
        {
            s.Timestamp.ToString("o", i),
            s.TickerCount.ToString(i),
            s.UpdateLatencyMs.ToString("F3", i),
            s.Fps.ToString("F2", i),
            s.DroppedFrames.ToString(i),
            s.UiThreadBusyPct.ToString("F2", i),
            s.MemoryMb.ToString("F2", i),
            s.GcGen0.ToString(i),
            s.GcGen1.ToString(i),
            s.GcGen2.ToString(i),
            s.GcPauseMs.ToString("F2", i),
            s.UptimeMs.ToString("F1", i)
        });
    }

    public void Dispose()
    {
        Complete();
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "run" : safe;
    }

    private static string SanitizeCell(string value)
        => (value ?? string.Empty).Replace(',', '_').Replace('\r', ' ').Replace('\n', ' ').Trim();
}
