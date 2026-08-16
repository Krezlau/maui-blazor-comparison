using System.Globalization;
using System.Text.Json;
using MgrCode.Backend.Models;

namespace MgrCode.Backend.Services;

internal sealed class WireTicker
{
    public string Symbol { get; set; } = string.Empty;
    public string LastPrice { get; set; } = string.Empty;
    public string IndexPrice { get; set; } = string.Empty;
    public string MarkPrice { get; set; } = string.Empty;
    public string PrevPrice24h { get; set; } = string.Empty;
    public string Price24hPcnt { get; set; } = string.Empty;
    public string HighPrice24h { get; set; } = string.Empty;
    public string LowPrice24h { get; set; } = string.Empty;
    public string OpenPrice24h { get; set; } = string.Empty;
    public string Turnover24h { get; set; } = string.Empty;
    public string Volume24h { get; set; } = string.Empty;
    public string Bid1Price { get; set; } = string.Empty;
    public string Bid1Size { get; set; } = string.Empty;
    public string Ask1Price { get; set; } = string.Empty;
    public string Ask1Size { get; set; } = string.Empty;
}

internal sealed class WireTickersResult
{
    public string Category { get; set; } = string.Empty;
    public List<WireTicker> List { get; set; } = new();
}

internal sealed class WireTickersResponse
{
    public int RetCode { get; set; }
    public string? RetMsg { get; set; }
    public WireTickersResult? Result { get; set; }
}

internal static class BybitWire
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;

    public static Ticker ToTicker(WireTicker wire)
    {
        var lastPrice = ParseDecimal(wire.LastPrice);
        return new Ticker(wire.Symbol)
        {
            LastPrice = lastPrice,
            IndexPrice = ParseDecimal(wire.IndexPrice),
            MarkPrice = ParseDecimal(wire.MarkPrice),
            PrevPrice24h = ParseDecimal(wire.PrevPrice24h),
            Price24hPcnt = ParseDecimal(wire.Price24hPcnt),
            HighPrice24h = ParseDecimal(wire.HighPrice24h),
            LowPrice24h = ParseDecimal(wire.LowPrice24h),
            OpenPrice24h = ParseDecimal(wire.OpenPrice24h),
            Turnover24h = ParseDecimal(wire.Turnover24h),
            Volume24h = ParseDecimal(wire.Volume24h),
            Bid1Price = ParseDecimal(wire.Bid1Price),
            Ask1Price = ParseDecimal(wire.Ask1Price)
        };
    }
}
