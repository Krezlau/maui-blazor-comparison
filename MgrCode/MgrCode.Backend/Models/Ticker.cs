using CommunityToolkit.Mvvm.ComponentModel;

namespace MgrCode.Backend.Models;

public partial class Ticker : ObservableObject
{
    public Ticker(string symbol)
    {
        Symbol = symbol;
    }

    public string Symbol { get; }

    public string Topic => $"ticker.{Symbol}";

    [ObservableProperty]
    private decimal _lastPrice;

    [ObservableProperty]
    private decimal _indexPrice;

    [ObservableProperty]
    private decimal _markPrice;

    [ObservableProperty]
    private decimal _prevPrice24h;

    [ObservableProperty]
    private decimal _price24hPcnt;

    [ObservableProperty]
    private decimal _highPrice24h;

    [ObservableProperty]
    private decimal _lowPrice24h;

    [ObservableProperty]
    private decimal _openPrice24h;

    [ObservableProperty]
    private decimal _turnover24h;

    [ObservableProperty]
    private decimal _volume24h;

    [ObservableProperty]
    private decimal _bid1Price;

    [ObservableProperty]
    private decimal _ask1Price;

    [ObservableProperty]
    private PriceDirection _direction;

    [ObservableProperty]
    private long _priceTick;

    public void Apply(Ticker update)
    {
        Direction = update.LastPrice > LastPrice
            ? PriceDirection.Up
            : update.LastPrice < LastPrice
                ? PriceDirection.Down
                : PriceDirection.Flat;

        if (Direction != PriceDirection.Flat)
            PriceTick++;

        LastPrice = update.LastPrice;
        IndexPrice = update.IndexPrice;
        MarkPrice = update.MarkPrice;
        PrevPrice24h = update.PrevPrice24h;
        Price24hPcnt = update.Price24hPcnt;
        HighPrice24h = update.HighPrice24h;
        LowPrice24h = update.LowPrice24h;
        OpenPrice24h = update.OpenPrice24h;
        Turnover24h = update.Turnover24h;
        Volume24h = update.Volume24h;
        Bid1Price = update.Bid1Price;
        Ask1Price = update.Ask1Price;
    }
}
