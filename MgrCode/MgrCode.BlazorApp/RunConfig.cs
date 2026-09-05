namespace MgrCode.BlazorApp;

/// <summary>
/// Resolves harness-driven run settings (run label, auto-start, collection, duration)
/// from environment variables, command-line arguments and (on Android) Intent extras.
/// Lets the device harness drive each app unattended and name the output CSV.
/// </summary>
public sealed class RunConfig
{
    public string RunLabel { get; }
    public bool AutoStart { get; }
    public bool Collect { get; }
    public int DurationMs { get; }
    public string BaseUrl { get; }
    public string BaseWsUrl { get; }

    public RunConfig(string defaultLabelPrefix)
    {
        var settings = ReadSettings();

        AutoStart = IsTrue(Get(settings, "autostart"));
        Collect = AutoStart || IsTrue(Get(settings, "collect"));

        DurationMs = int.TryParse(Get(settings, "durationMs"), out var ms) && ms > 0
            ? ms
            : AutoStart ? 65_000 : 0;

        var label = Get(settings, "runLabel");
        RunLabel = string.IsNullOrWhiteSpace(label)
            ? $"{defaultLabelPrefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss}"
            : label.Trim();

        BaseUrl = NonEmpty(Get(settings, "baseUrl"), "http://localhost:5010");
        BaseWsUrl = NonEmpty(Get(settings, "baseWsUrl"), "ws://localhost:5010/v5/public/spot");
    }

    private static Dictionary<string, string> ReadSettings()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in new[] { "runLabel", "autostart", "collect", "durationMs", "baseUrl", "baseWsUrl" })
        {
            var value = Environment.GetEnvironmentVariable(key switch
            {
                "runLabel" => "MGR_RUN_LABEL",
                "autostart" => "MGR_AUTOSTART",
                "collect" => "MGR_COLLECT",
                "durationMs" => "MGR_DURATION_MS",
                "baseUrl" => "MGR_BASE_URL",
                _ => "MGR_WS_URL"
            });
            if (!string.IsNullOrWhiteSpace(value))
                dict[key] = value;
        }

        foreach (var arg in Environment.GetCommandLineArgs())
        {
            var idx = arg.IndexOf('=');
            if (idx <= 0)
                continue;
            var key = arg[..idx].TrimStart('-');
            dict[key] = arg[(idx + 1)..];
        }

#if ANDROID
        var intent = Platform.CurrentActivity?.Intent;
        if (intent is not null)
        {
            foreach (var key in new[] { "runLabel", "autostart", "collect", "durationMs", "baseUrl", "baseWsUrl" })
            {
                var value = intent.GetStringExtra(key);
                if (!string.IsNullOrWhiteSpace(value))
                    dict[key] = value;
            }
        }
#endif

        return dict;
    }

    private static string? Get(Dictionary<string, string> settings, string key)
        => settings.TryGetValue(key, out var value) ? value : null;

    private static string NonEmpty(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static bool IsTrue(string? value)
        => value is not null && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));
}
