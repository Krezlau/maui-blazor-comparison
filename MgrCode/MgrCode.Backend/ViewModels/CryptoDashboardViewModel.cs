using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MgrCode.Backend.Models;
using MgrCode.Backend.Services;

namespace MgrCode.Backend.ViewModels;

public partial class CryptoDashboardViewModel : ObservableObject
{
    private readonly IBybitService _service;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly Dictionary<string, Ticker> _bySymbol = new();
    private CancellationTokenSource? _streamCts;

    public event Action? UpdateApplied;

    public ObservableCollection<Ticker> Tickers { get; } = new();

    [ObservableProperty]
    private ObservableCollection<Ticker> _filteredTickers = new();

    public PerformanceViewModel Performance { get; }

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _status = "Not started";

    [ObservableProperty]
    private long _updatesReceived;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _totalTurnoverText = "—";

    [ObservableProperty]
    private string _topGainerText = "—";

    [ObservableProperty]
    private string _topLoserText = "—";

    public CryptoDashboardViewModel(
        IBybitService service,
        IMainThreadDispatcher dispatcher,
        PerformanceViewModel performance)
    {
        _service = service;
        _dispatcher = dispatcher;
        Performance = performance;
    }

    partial void OnSearchTextChanged(string value)
        => RebuildFiltered();

    [RelayCommand]
    private async Task InitializeAsync()
    {
        SetStatus("Fetching tickers…");
        try
        {
            await _service.InitializeAsync();
            var tickers = await _service.GetTickersAsync();
            if (tickers.Count == 0)
            {
                SetStatus("No tickers returned from server.");
                return;
            }

            var snapshot = tickers.ToArray();
            _dispatcher.Dispatch(() =>
            {
                Tickers.Clear();
                _bySymbol.Clear();
                foreach (var ticker in snapshot)
                {
                    _bySymbol[ticker.Symbol] = ticker;
                    Tickers.Add(ticker);
                }

                RebuildFiltered();
            });

            IsInitialized = true;
            SetStatus($"{snapshot.Length} tickers loaded.");
        }
        catch (Exception ex)
        {
            SetStatus($"Initialize failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsRunning)
            return;

        if (!IsInitialized)
            await InitializeCommand.ExecuteAsync(null);

        if (_bySymbol.Count == 0)
        {
            SetStatus("No tickers to stream.");
            return;
        }

        IsRunning = true;
        SetStatus("Streaming…");

        _streamCts = new CancellationTokenSource();
        var symbols = _bySymbol.Keys.ToArray();
        var token = _streamCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var ticker in _service.Subscribe(symbols).WithCancellation(token))
                    Apply(marshalled: true, ticker);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                SetStatus($"Stream error: {ex.Message}");
            }
            finally
            {
                DispatchState(() => IsRunning = false);
            }
        });
    }

    [RelayCommand]
    private void Stop()
    {
        _streamCts?.Cancel();
    }

    private void Apply(bool marshalled, Ticker ticker)
    {
        if (!marshalled)
        {
            Performance.RecordUpdateStart();
            _dispatcher.Dispatch(() => Apply(true, ticker));
            return;
        }

        if (_bySymbol.TryGetValue(ticker.Symbol, out var existing))
        {
            existing.Apply(ticker);
        }
        else
        {
            _bySymbol[ticker.Symbol] = ticker;
            Tickers.Add(ticker);
            if (MatchesFilter(ticker))
                FilteredTickers.Add(ticker);
        }

        UpdatesReceived++;
        RecomputeMarketStrip();
        UpdateApplied?.Invoke();
    }

    private void RebuildFiltered()
    {
        var query = SearchText?.Trim() ?? string.Empty;
        var rebuilt = new ObservableCollection<Ticker>();
        foreach (var ticker in Tickers)
        {
            if (query.Length == 0 || ticker.Symbol.Contains(query, StringComparison.OrdinalIgnoreCase))
                rebuilt.Add(ticker);
        }

        FilteredTickers = rebuilt;
    }

    private bool MatchesFilter(Ticker ticker)
    {
        var query = SearchText?.Trim() ?? string.Empty;
        return query.Length == 0 || ticker.Symbol.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void RecomputeMarketStrip()
    {
        decimal total = 0;
        Ticker? gainer = null;
        Ticker? loser = null;

        foreach (var ticker in Tickers)
        {
            total += ticker.Turnover24h;
            if (gainer is null || ticker.Price24hPcnt > gainer.Price24hPcnt)
                gainer = ticker;
            if (loser is null || ticker.Price24hPcnt < loser.Price24hPcnt)
                loser = ticker;
        }

        TotalTurnoverText = Tickers.Count == 0 ? "—" : Formatting.FormatCompact(total);
        TopGainerText = gainer is null ? "—" : $"{gainer.Symbol} {Formatting.FormatPct(gainer.Price24hPcnt)}";
        TopLoserText = loser is null ? "—" : $"{loser.Symbol} {Formatting.FormatPct(loser.Price24hPcnt)}";
    }

    private void SetStatus(string message)
        => DispatchState(() => Status = message);

    private void DispatchState(Action action)
    {
        if (_dispatcher.IsMainThread)
            action();
        else
            _dispatcher.Dispatch(action);
    }
}