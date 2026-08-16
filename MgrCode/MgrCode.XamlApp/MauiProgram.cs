using Microsoft.Extensions.Logging;
using MgrCode.Backend;
using MgrCode.Backend.Services;
using MgrCode.Backend.ViewModels;

namespace MgrCode.XamlApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton(new BackendOptions());
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<IMainThreadDispatcher, MauiMainThreadDispatcher>();
        builder.Services.AddSingleton<IBybitService, MockBybitService>();
        builder.Services.AddSingleton<PerformanceViewModel>();
        builder.Services.AddSingleton<CryptoDashboardViewModel>();
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
