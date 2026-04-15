using System.Windows.Media.Imaging;

namespace Flink.Core;

/// <summary>
/// Represents a single open window.
/// </summary>
public sealed class WindowInfo
{
    public IntPtr Handle { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public string ExecutablePath { get; init; } = string.Empty;
    public uint ProcessId { get; init; }

    /// <summary>Assigned key binding, e.g. "t" or "tq"</summary>
    public string Binding { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable app name from config, or auto-capitalized process name.
    /// E.g. "Zen Browser" instead of "zen".
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Window title with the app name suffix stripped.
    /// E.g. "GitHub - Zen Browser" → "GitHub"
    /// Falls back to the raw title if no suffix is found.
    /// </summary>
    public string CleanTitle { get; set; } = string.Empty;

    /// <summary>App icon, loaded lazily</summary>
    public BitmapSource? Icon { get; set; }
}
