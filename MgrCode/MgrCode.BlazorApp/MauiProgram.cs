using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MgrCode.Backend;
using MgrCode.Backend.Services;
using MgrCode.Backend.ViewModels;

namespace MgrCode.BlazorApp;

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
            });

        builder.Services.AddMauiBlazorWebView();

        builder.Services.AddSingleton(sp =>
        {
            var rc = sp.GetRequiredService<RunConfig>();
            return new BackendOptions { BaseUrl = rc.BaseUrl, WebSocketUrl = rc.BaseWsUrl };
        });
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<IMainThreadDispatcher, BlazorMainThreadDispatcher>();
        builder.Services.AddSingleton<IBybitService, MockBybitService>();
        builder.Services.AddSingleton<PerformanceViewModel>();
        builder.Services.AddSingleton<CryptoDashboardViewModel>();
        builder.Services.AddSingleton(sp => new RunConfig("blazor"));
        builder.Services.AddSingleton(CreateSink);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static CsvPerformanceSink CreateSink(IServiceProvider services)
    {
        var runConfig = services.GetRequiredService<RunConfig>();
        return new CsvPerformanceSink(
            ResultsDirectory(),
            runConfig.RunLabel,
            BuildMetadata("blazor"));
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
