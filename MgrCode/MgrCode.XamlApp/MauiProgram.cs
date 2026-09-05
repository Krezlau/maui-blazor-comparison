using System.Runtime.InteropServices;
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

        builder.Services.AddSingleton(sp =>
        {
            var rc = sp.GetRequiredService<RunConfig>();
            return new BackendOptions { BaseUrl = rc.BaseUrl, WebSocketUrl = rc.BaseWsUrl };
        });
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<IMainThreadDispatcher, MauiMainThreadDispatcher>();
        builder.Services.AddSingleton<IBybitService, MockBybitService>();
        builder.Services.AddSingleton<PerformanceViewModel>();
        builder.Services.AddSingleton<CryptoDashboardViewModel>();
        builder.Services.AddSingleton(sp => new RunConfig("xaml"));
        builder.Services.AddSingleton(CreateSink);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        AppServices.Provider = app.Services;
        return app;
    }

    private static CsvPerformanceSink CreateSink(IServiceProvider services)
    {
        var runConfig = services.GetRequiredService<RunConfig>();
        return new CsvPerformanceSink(
            ResultsDirectory(),
            runConfig.RunLabel,
            BuildMetadata("xaml"));
    }

    private static string ResultsDirectory()
    {
#if ANDROID
        var dir = Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath;
        if (!string.IsNullOrEmpty(dir))
            return Path.Combine(dir, "results");
#endif
        return Path.Combine(FileSystem.AppDataDirectory, "results");
    }

    private static Dictionary<string, string> BuildMetadata(string appName)
        => new()
        {
            ["app"] = appName,
            ["platform"] = DeviceInfo.Platform.ToString(),
            ["device"] = DeviceInfo.Model,
            ["manufacturer"] = DeviceInfo.Manufacturer,
            ["os"] = DeviceInfo.VersionString,
            ["arch"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["config"] = BuildConfiguration(),
            ["refreshHz"] = DeviceDisplay.Current.MainDisplayInfo.RefreshRate.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)
        };

    private static string BuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }
}
