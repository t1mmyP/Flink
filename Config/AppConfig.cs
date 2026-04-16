namespace Flink.Config;

public sealed class AppConfig
{
    /// <summary>
    /// Maps process name (lowercase) to a fixed first letter.
    /// Example: { "windowsterminal": "t", "zen": "z" }
    /// </summary>
    public Dictionary<string, string> Bindings { get; set; } = new();

    /// <summary>
    /// Maps process name (lowercase) to a human-readable display name.
    /// Example: { "windowsterminal": "Terminal", "zen": "Zen Browser" }
    /// If not set, the process name is auto-capitalized.
    /// </summary>
    public Dictionary<string, string> Names { get; set; } = new();

    /// <summary>
    /// When true, the overlay appears centered on the monitor where the mouse cursor is.
    /// When false, always uses the primary monitor.
    /// </summary>
    public bool FollowMouse { get; set; } = false;

    /// <summary>
    /// Add Flink to Windows autostart.
    /// </summary>
    public bool Autostart { get; set; } = false;

    /// <summary>
    /// UI theme: "dark" or "light"
    /// </summary>
    public string Theme { get; set; } = "dark";

    /// <summary>
    /// Maximum number of apps per column before splitting into two columns.
    /// When the app count exceeds this value, items are distributed evenly across two columns.
    /// </summary>
    public int MaxAppsPerColumn { get; set; } = 10;
}
