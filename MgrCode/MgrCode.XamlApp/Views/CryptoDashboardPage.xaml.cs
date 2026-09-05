using MgrCode.Backend.ViewModels;

namespace MgrCode.XamlApp.Views;

public partial class CryptoDashboardPage : ContentPage
{
    private readonly CryptoDashboardViewModel _viewModel;
    private readonly PerformanceViewModel _performance;
    private CancellationTokenSource? _sampleCts;

    public CryptoDashboardPage(CryptoDashboardViewModel viewModel, PerformanceViewModel performance)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _performance = performance;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_viewModel.IsInitialized)
            await _viewModel.InitializeCommand.ExecuteAsync(null);

        _sampleCts = new CancellationTokenSource();
        _ = SampleLoopAsync(_sampleCts.Token);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _sampleCts?.Cancel();
        _sampleCts?.Dispose();
        _sampleCts = null;
    }

    private async Task SampleLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(500, token);
                _performance.SampleStats();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}