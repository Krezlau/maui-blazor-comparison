using System.Globalization;

namespace MgrCode.Backend.Services;

/// <summary>
/// Deterministic formatting shared by both UI apps so text/colors are byte-identical.
/// </summary>
public static class Formatting
{
    /// <summary>
    /// Formats a price with magnitude-based precision: ≥1000 → 2 decimals, ≥1 → 4 decimals, else 6.
    /// </summary>
    public static string FormatPrice(decimal price, string symbol)
    {
        _ = symbol; // reserved for per-symbol tick-size overrides
        var decimals = price >= 1000m ? 2 : price >= 1m ? 4 : 6;
        return price.ToString("N" + decimals, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Compacts a large number using B/M/K suffixes (used for volume).
    /// </summary>
    public static string FormatCompact(decimal value)
    {
        if (value >= 1_000_000_000m)
            return (value / 1_000_000_000m).ToString("0.00", CultureInfo.InvariantCulture) + "B";
        if (value >= 1_000_000m)
            return (value / 1_000_000m).ToString("0.00", CultureInfo.InvariantCulture) + "M";
        if (value >= 1_000m)
            return (value / 1_000m).ToString("0.00", CultureInfo.InvariantCulture) + "K";
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a 24h change ratio as a signed percent string (+1.23% / -0.45%).
    /// </summary>
    public static string FormatPct(decimal ratio)
    {
        var pct = ratio * 100m;
        var sign = pct >= 0m ? "+" : "";
        return sign + pct.ToString("0.00", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>
    /// Turns a symbol into a deterministic hex color (FNV-1a → HSL → RGB → hex).
    /// </summary>
    public static string ColorFromSymbol(string symbol)
    {
        var hash = 2166136261u;
        foreach (var c in symbol)
            hash = (hash ^ c) * 16777619u;

        var hue = hash % 360;
        var (r, g, b) = HslToRgb(hue / 360.0, 0.65, 0.5);
        return $"#{ToHex(r)}{ToHex(g)}{ToHex(b)}";
    }

    private static (byte r, byte g, byte b) HslToRgb(double h, double s, double l)
    {
        if (s == 0)
        {
            var v = (byte)(l * 255);
            return (v, v, v);
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        return ((byte)(HueToRgb(p, q, h + 1d / 3d) * 255),
                (byte)(HueToRgb(p, q, h) * 255),
                (byte)(HueToRgb(p, q, h - 1d / 3d) * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1d / 6d) return p + (q - p) * 6 * t;
        if (t < 1d / 2d) return q;
        if (t < 2d / 3d) return p + (q - p) * (2d / 3d - t) * 6;
        return p;
    }

    private static string ToHex(byte v)
        => v.ToString("X2", CultureInfo.InvariantCulture);
}