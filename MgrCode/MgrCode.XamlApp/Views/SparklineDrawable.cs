using Microsoft.Maui.Graphics;
using MgrCode.Backend.Models;
using MgrCode.Backend.ViewModels;

namespace MgrCode.XamlApp.Views;

/// <summary>
/// Normalized polyline from a Ticker's sparkline ring buffer.
/// The Draw call is the XAML t1 hook (native paint); per-second count = XAML FPS.
/// </summary>
public sealed class SparklineDrawable : IDrawable
{
    private static readonly Color Gainer = Color.FromArgb("#16A34A");
    private static readonly Color Loser = Color.FromArgb("#DC2626");

    private static PerformanceViewModel? _performance;
    private static bool _resolved;

    public Ticker? Ticker { get; set; }

    public static PerformanceViewModel? ResolvePerformance()
    {
        if (_resolved)
            return _performance;
        _resolved = true;
        _performance = AppServices.Provider?.GetService<PerformanceViewModel>();
        return _performance;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        ResolvePerformance()?.RecordRenderEnd();

        var ticker = Ticker;
        if (ticker is null)
            return;

        var samples = ticker.SparklineSamples;
        var count = samples.Count;
        if (count < 2)
            return;

        var width = dirtyRect.Width;
        var height = dirtyRect.Height;
        if (width <= 0 || height <= 0)
            return;

        var min = samples[0];
        var max = samples[0];
        for (var i = 1; i < count; i++)
        {
            if (samples[i] < min) min = samples[i];
            if (samples[i] > max) max = samples[i];
        }

        var range = max - min;
        if (range <= 0)
            range = 1m;

        var points = new PointF[count];
        var step = width / (count - 1);
        for (var i = 0; i < count; i++)
        {
            var x = i * step;
            var normalized = (samples[i] - min) / range;
            var y = height - (float)normalized * height;
            points[i] = new PointF(x, y);
        }

        var path = new PathF(points[0]);
        for (var i = 1; i < count; i++)
            path.LineTo(points[i]);

        canvas.StrokeSize = 1.5f;
        canvas.StrokeColor = samples[count - 1] >= samples[0] ? Gainer : Loser;
        canvas.DrawPath(path);
    }
}