namespace Flink.Config;

public sealed class AppConfig
{
    /// <summary>
    /// Maps process name (lowercase) to a fixed first letter.
    /// Example: { "windowsterminal": "t", "chrome": "b" }
    /// </summary>
    public Dictionary<string, string> Bindings { get; set; } = new();

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
}
