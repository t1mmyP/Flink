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

    /// <summary>App icon, loaded lazily</summary>
    public BitmapSource? Icon { get; set; }
}
