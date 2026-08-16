using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MgrCode.Backend.Models;
using MgrCode.Backend.Services;

namespace MgrCode.Backend.ViewModels;

public partial class CryptoDashboardViewModel : ObservableObject
{
    private readonly IBybitService _service;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly PerformanceViewModel _performance;
    private readonly Dictionary<string, Ticker> _bySymbol = new();
    private CancellationTokenSource? _streamCts;
    private DateTimeOffset _startedAt;

    public ObservableCollection<Ticker> Tickers { get; } = new();

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _status = "Not started";

    [ObservableProperty]
    private long _updatesReceived;

    public CryptoDashboardViewModel(
        IBybitService service,
        IMainThreadDispatcher dispatcher,
        PerformanceViewModel performance)
    {
        _service = service;
        _dispatcher = dispatcher;
        _performance = performance;
    }

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
        _startedAt = DateTimeOffset.UtcNow;
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
            _performance.RecordUpdateStart();
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
        }

        UpdatesReceived++;
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
