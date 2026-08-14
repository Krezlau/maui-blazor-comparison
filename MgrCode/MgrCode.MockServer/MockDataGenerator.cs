using System.Globalization;

namespace MgrCode.MockServer;

public sealed class MockDataGenerator
{
    private static readonly string[] KnownSymbols =
    {
        "BTCUSDT", "ETHUSDT", "BNBUSDT", "XRPUSDT", "SOLUSDT", "ADAUSDT", "DOGEUSDT", "TRXUSDT",
        "AVAXUSDT", "LINKUSDT", "POLUSDT", "DOTUSDT", "LTCUSDT", "BCHUSDT", "UNIUSDT", "ATOMUSDT",
        "XLMUSDT", "ETCUSDT", "FILUSDT", "NEARUSDT", "APTUSDT", "ARBUSDT", "OPUSDT", "SUIUSDT",
        "SEIUSDT", "TIAUSDT", "PEPEUSDT", "WIFUSDT", "BONKUSDT", "SHIBUSDT", "FLOKIUSDT", "TONUSDT",
        "NOTUSDT", "OMUSDT", "AAVEUSDT", "MKRUSDT", "LDOUSDT", "CRVUSDT", "RUNEUSDT", "ALGOUSDT",
        "HBARUSDT", "VETUSDT", "ICPUSDT", "RNDRUSDT", "IMXUSDT", "INJUSDT", "ENSUSDT", "GRTUSDT",
        "SANDUSDT", "MANAUSDT", "GALAUSDT", "AXSUSDT", "SNXUSDT", "COMPUSDT", "YFIUSDT", "FTMUSDT",
        "MATICUSDT", "MINAUSDT", "ASTRUSDT", "AIGPTUSDT", "AIUSDT", "DOGSUSDT", "HOOKUSDT",
        "PORTALUSDT", "LISTAUSDT", "BBUSDT", "OMNIUSDT", "TAIKOUSDT", "ZETAUSDT", "ETHFIUSDT",
        "ENAUSDT", "REZUSDT", "PIXELUSDT", "SAFEUSDT", "WUSDT", "MOCAUSDT", "MANTAUSDT",
        "PHAUSDT", "PENDLEUSDT", "BIGTIMEUSDT", "CYBERUSDT", "HIFIUSDT", "SFPUSDT", "MVBUSDT",
        "MAGICUSDT", "AGIXUSDT", "FETUSDT", "OCEANUSDT", "JASMYUSDT", "BLURUSDT", "SUPRAUSDT",
        "MORPHOUSDT", "EIGENUSDT", "STRKUSDT", "ZKUSDT", "AEVOUSDT", "WALUSDT", "KASUSDT",
        "DOGSUSDT", "MYRIAUSDT", "BLASTUSDT", "LQTYUSDT"
    };

    private static readonly Dictionary<string, decimal> BasePrices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTCUSDT"] = 67000m,
        ["ETHUSDT"] = 3500m,
        ["BNBUSDT"] = 590m,
        ["XRPUSDT"] = 0.52m,
        ["SOLUSDT"] = 145m,
        ["ADAUSDT"] = 0.45m,
        ["DOGEUSDT"] = 0.13m,
        ["TRXUSDT"] = 0.21m,
        ["AVAXUSDT"] = 28m,
        ["LINKUSDT"] = 14m,
        ["POLUSDT"] = 0.42m,
        ["DOTUSDT"] = 6.8m,
        ["LTCUSDT"] = 84m,
        ["BCHUSDT"] = 480m,
        ["UNIUSDT"] = 9.4m,
        ["ATOMUSDT"] = 7.2m,
        ["XLMUSDT"] = 0.11m,
        ["ETCUSDT"] = 22m,
        ["FILUSDT"] = 4.7m,
        ["NEARUSDT"] = 5.2m,
        ["APTUSDT"] = 8.6m,
        ["ARBUSDT"] = 0.75m,
        ["OPUSDT"] = 1.6m,
        ["SUIUSDT"] = 1.05m,
        ["SEIUSDT"] = 0.34m,
        ["TIAUSDT"] = 5.1m,
        ["PEPEUSDT"] = 0.000011m,
        ["WIFUSDT"] = 1.9m,
        ["BONKUSDT"] = 0.000022m,
        ["SHIBUSDT"] = 0.000015m,
        ["FLOKIUSDT"] = 0.00013m,
        ["TONUSDT"] = 5.4m,
        ["NOTUSDT"] = 0.0088m,
        ["OMUSDT"] = 4.1m,
        ["AAVEUSDT"] = 98m,
        ["MKRUSDT"] = 2200m,
        ["LDOUSDT"] = 1.2m,
        ["CRVUSDT"] = 0.28m,
        ["RUNEUSDT"] = 3.2m,
        ["ALGOUSDT"] = 0.16m,
        ["HBARUSDT"] = 0.072m,
        ["VETUSDT"] = 0.024m,
        ["ICPUSDT"] = 7.9m,
        ["RNDRUSDT"] = 6.1m,
        ["IMXUSDT"] = 1.1m,
        ["INJUSDT"] = 18m,
        ["ENSUSDT"] = 25m,
        ["GRTUSDT"] = 0.17m,
        ["SANDUSDT"] = 0.30m,
        ["MANAUSDT"] = 0.38m,
        ["GALAUSDT"] = 0.025m,
        ["AXSUSDT"] = 5.0m,
        ["SNXUSDT"] = 1.9m,
        ["COMPUSDT"] = 44m,
        ["YFIUSDT"] = 5800m,
        ["FTMUSDT"] = 0.62m,
        ["MATICUSDT"] = 0.44m,
        ["MINAUSDT"] = 0.42m,
        ["ASTRUSDT"] = 0.065m
    };

    private readonly object _gate = new();
    private readonly Random _random = new();
    private readonly TickerState[] _states;
    private readonly Dictionary<string, int> _symbolIndex = new(StringComparer.OrdinalIgnoreCase);
    private int _tickCount;

    public MockDataGenerator(int tickerCount)
    {
        var count = Math.Max(1, tickerCount);
        _states = new TickerState[count];
        for (var i = 0; i < count; i++)
        {
            var symbol = i < KnownSymbols.Length ? KnownSymbols[i] : $"MOCK{i}USDT";
            var basePrice = BasePrices.TryGetValue(symbol, out var price) ? price : RandomPrice();
            _states[i] = new TickerState(symbol, basePrice);
            _symbolIndex[symbol] = i;
        }
    }

    public void Tick()
    {
        lock (_gate)
        {
            _tickCount++;
            foreach (var state in _states)
                Advance(state);
        }
    }

    public TickerDto[] GetSnapshot()
    {
        lock (_gate)
        {
            var result = new TickerDto[_states.Length];
            for (var i = 0; i < _states.Length; i++)
                result[i] = ToDto(_states[i]);
            return result;
        }
    }

    public TickerDto? GetTicker(string symbol)
    {
        lock (_gate)
        {
            if (_symbolIndex.TryGetValue(symbol, out var index))
                return ToDto(_states[index]);
            return null;
        }
    }

    private void Advance(TickerState state)
    {
        var volatility = 0.0015m;
        var changePct = (decimal)((_random.NextDouble() * 2 - 1) * (double)volatility);
        var drift = (decimal)((_random.NextDouble() - 0.5) * (double)volatility * 0.1);
        var newPrice = state.LastPrice * (1m + changePct + drift);
        newPrice = Math.Max(newPrice, state.LastPrice * 0.5m);

        if (newPrice > state.HighPrice24h) state.HighPrice24h = newPrice;
        if (newPrice < state.LowPrice24h) state.LowPrice24h = newPrice;

        var volumeIncrement = state.Volume24h * (decimal)(_random.NextDouble() * 0.0002);
        state.Volume24h += volumeIncrement;
        state.LastPrice = newPrice;

        if (_tickCount % 43200 == 0)
        {
            state.OpenPrice24h = newPrice;
            state.PrevPrice24h = newPrice;
            state.HighPrice24h = newPrice;
            state.LowPrice24h = newPrice;
        }
    }

    private decimal RandomPrice()
    {
        var orderOfMagnitude = _random.Next(1, 6);
        return (decimal)(_random.NextDouble() * Math.Pow(10, orderOfMagnitude)) + 0.0001m;
    }

    private static TickerDto ToDto(TickerState state)
    {
        var price24hPcnt = state.OpenPrice24h == 0
            ? 0m
            : (state.LastPrice - state.OpenPrice24h) / state.OpenPrice24h;

        return new TickerDto(
            Symbol: state.Symbol,
            LastPrice: Fmt(state.LastPrice),
            IndexPrice: Fmt(state.LastPrice * 1.0001m),
            MarkPrice: Fmt(state.LastPrice * 1.00005m),
            PrevPrice24h: Fmt(state.PrevPrice24h),
            Price24hPcnt: FmtPct(price24hPcnt),
            HighPrice24h: Fmt(state.HighPrice24h),
            LowPrice24h: Fmt(state.LowPrice24h),
            OpenPrice24h: Fmt(state.OpenPrice24h),
            Turnover24h: Fmt(state.LastPrice * state.Volume24h),
            Volume24h: Fmt(state.Volume24h),
            Bid1Price: Fmt(state.LastPrice * 0.9999m),
            Bid1Size: Fmt(state.LastPrice * 0.001m),
            Ask1Price: Fmt(state.LastPrice * 1.0001m),
            Ask1Size: Fmt(state.LastPrice * 0.001m));
    }

    private static string Fmt(decimal value)
    {
        if (value == 0m) return "0";
        var rounded = Math.Round(value, 8);
        return rounded.ToString("0.##########", CultureInfo.InvariantCulture);
    }

    private static string FmtPct(decimal value)
        => Math.Round(value, 6).ToString("0.######", CultureInfo.InvariantCulture);

    private sealed class TickerState
    {
        public string Symbol { get; }
        public decimal LastPrice { get; set; }
        public decimal PrevPrice24h { get; set; }
        public decimal OpenPrice24h { get; set; }
        public decimal HighPrice24h { get; set; }
        public decimal LowPrice24h { get; set; }
        public decimal Volume24h { get; set; }

        public TickerState(string symbol, decimal basePrice)
        {
            Symbol = symbol;
            LastPrice = basePrice;
            OpenPrice24h = basePrice;
            PrevPrice24h = basePrice;
            HighPrice24h = basePrice * 1.05m;
            LowPrice24h = basePrice * 0.95m;
            Volume24h = basePrice > 1 ? basePrice * 1000m : basePrice * 100_000_000m;
        }
    }
}
