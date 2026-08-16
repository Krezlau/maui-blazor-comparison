using MgrCode.Backend.ViewModels;

namespace MgrCode.XamlApp;

public partial class MainPage : ContentPage
{
    private readonly CryptoDashboardViewModel _viewModel;

    public MainPage(CryptoDashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_viewModel.IsInitialized)
            await _viewModel.InitializeCommand.ExecuteAsync(null);
    }
}
