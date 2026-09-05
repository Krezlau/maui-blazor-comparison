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
    }

    private static Dictionary<string, string> ReadSettings()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in new[] { "runLabel", "autostart", "collect", "durationMs" })
        {
            var value = Environment.GetEnvironmentVariable(key switch
            {
                "runLabel" => "MGR_RUN_LABEL",
                "autostart" => "MGR_AUTOSTART",
                "collect" => "MGR_COLLECT",
                _ => "MGR_DURATION_MS"
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
            foreach (var key in new[] { "runLabel", "autostart", "collect", "durationMs" })
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

    private static bool IsTrue(string? value)
        => value is not null && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));
}
