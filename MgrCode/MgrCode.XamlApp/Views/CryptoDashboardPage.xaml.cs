using MgrCode.Backend.Services;
using MgrCode.Backend.ViewModels;

namespace MgrCode.XamlApp.Views;

public partial class CryptoDashboardPage : ContentPage
{
    private readonly CryptoDashboardViewModel _viewModel;
    private readonly PerformanceViewModel _performance;
    private readonly CsvPerformanceSink? _sink;
    private readonly RunConfig _runConfig;
    private CancellationTokenSource? _sampleCts;

    public CryptoDashboardPage(
        CryptoDashboardViewModel viewModel,
        PerformanceViewModel performance,
        CsvPerformanceSink? sink,
        RunConfig runConfig)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _performance = performance;
        _sink = sink;
        _runConfig = runConfig;
        _performance.SetRefreshRate(DeviceDisplay.Current.MainDisplayInfo.RefreshRate);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_viewModel.IsInitialized)
            await _viewModel.InitializeCommand.ExecuteAsync(null);

        _sampleCts = new CancellationTokenSource();
        _ = SampleLoopAsync(_sampleCts.Token);

        if (_runConfig.AutoStart)
        {
            await _viewModel.StartCommand.ExecuteAsync(null);
            if (_runConfig.DurationMs > 0)
                _ = AutoFinishAsync(_sampleCts.Token, _runConfig.DurationMs);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _sampleCts?.Cancel();
        _sampleCts?.Dispose();
        _sampleCts = null;
        _sink?.Complete();
    }

    private async Task SampleLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(500, token);
                _performance.SampleStats();
                _sink?.Append(_performance.Capture(DateTimeOffset.UtcNow, _viewModel.FilteredTickers.Count));
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task AutoFinishAsync(CancellationToken token, int durationMs)
    {
        try
        {
            await Task.Delay(durationMs, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _viewModel.StopCommand.Execute(null);
        _sink?.Complete();
    }
}
