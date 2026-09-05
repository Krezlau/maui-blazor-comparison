namespace MgrCode.XamlApp;

/// <summary>
/// Minimal static service locator so non-DI-created Controls
/// (e.g. DataTemplate row ContentViews) can reach registered singletons.
/// </summary>
public static class AppServices
{
    public static IServiceProvider? Provider { get; set; }
}