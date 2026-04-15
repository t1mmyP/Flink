using System.Windows;
using Flink.Config;
using Flink.Core;

namespace Flink.UI;

public sealed partial class OverlayWindow : Window
{
    private readonly AppConfig _config;

    // Current typed sequence (for two-letter bindings)
    private string _typedSequence = string.Empty;
    private List<WindowInfo> _currentWindows = [];

    public event Action<WindowInfo>? WindowSelected;

    public OverlayWindow(AppConfig config)
    {
        _config = config;
        InitializeComponent();
    }

    /// <summary>
    /// Called when Alt+Tab is pressed. Enumerates windows, assigns bindings, shows overlay.
    /// </summary>
    public void ShowOverlay()
    {
        _typedSequence = string.Empty;

        // Enumerate and bind on the UI thread
        var windows = WindowEnumerator.GetOpenWindows();
        KeyBinder.AssignBindings(windows, _config);

        // Load icons asynchronously to not block showing
        _currentWindows = windows;
        WindowList.ItemsSource = windows;
        _typedSequence = string.Empty;

        // Load icons in background
        Task.Run(() => LoadIconsAsync(windows));

        // Position and show
        PositionWindow();
        Show();
        // Do NOT call Activate() — we want the previous window to stay focused
        // The overlay is input-transparent for mouse, keyboard handled via hook
    }

    public void HideOverlay()
    {
        _typedSequence = string.Empty;
        Hide();
    }

    /// <summary>
    /// Escape: if in a sub-selection, go back to full list.
    /// If already on full list, close the overlay.
    /// </summary>
    public void HandleEscape()
    {
        if (!string.IsNullOrEmpty(_typedSequence))
        {
            _typedSequence = string.Empty;
            UpdateFilteredList(string.Empty);
        }
        else
        {
            HideOverlay();
        }
    }

    /// <summary>
    /// Called by KeyboardHook when a letter key is pressed while overlay is visible.
    /// </summary>
    public void HandleKeyPress(char c)
    {
        _typedSequence += c;

        // Try exact match
        var match = _currentWindows.FirstOrDefault(
            w => w.Binding.Equals(_typedSequence, StringComparison.OrdinalIgnoreCase));

        if (match != null)
        {
            WindowSelected?.Invoke(match);
            HideOverlay();
            return;
        }

        // Check if any binding starts with the typed sequence
        bool anyPrefix = _currentWindows.Any(
            w => w.Binding.StartsWith(_typedSequence, StringComparison.OrdinalIgnoreCase));

        if (!anyPrefix)
        {
            // Invalid key — reset to full list
            _typedSequence = string.Empty;
        }

        UpdateFilteredList(_typedSequence);
    }

    private void UpdateFilteredList(string prefix)
    {
        // Capture current top-left so the window doesn't jump when it resizes
        double currentLeft = Left;
        double currentTop = Top;

        if (string.IsNullOrEmpty(prefix))
        {
            WindowList.ItemsSource = _currentWindows;
        }
        else
        {
            var filtered = _currentWindows
                .Where(w => w.Binding.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
            WindowList.ItemsSource = filtered;
        }

        // Re-anchor: keep the window centered on the same spot
        UpdateLayout();
        Left = currentLeft;
        Top = currentTop;
    }

    private void PositionWindow()
    {
        // Force layout so ActualWidth/Height are calculated
        Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(DesiredSize));
        UpdateLayout();

        var screen = GetTargetScreenBounds();

        Left = screen.Left + (screen.Width - ActualWidth) / 2;
        Top = screen.Top + (screen.Height - ActualHeight) / 2;

        // Clamp to screen
        if (Left < screen.Left) Left = screen.Left + 16;
        if (Top < screen.Top) Top = screen.Top + 16;
    }

    private (double Left, double Top, double Width, double Height) GetTargetScreenBounds()
    {
        if (_config.FollowMouse)
        {
            NativeMethods.GetCursorPos(out var pt);
            IntPtr monitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var mi = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };

            if (NativeMethods.GetMonitorInfo(monitor, ref mi))
            {
                return (
                    mi.rcWork.Left,
                    mi.rcWork.Top,
                    mi.rcWork.Right - mi.rcWork.Left,
                    mi.rcWork.Bottom - mi.rcWork.Top);
            }
        }

        // Primary screen
        return (
            SystemParameters.WorkArea.Left,
            SystemParameters.WorkArea.Top,
            SystemParameters.WorkArea.Width,
            SystemParameters.WorkArea.Height);
    }

    private async Task LoadIconsAsync(List<WindowInfo> windows)
    {
        await Task.Run(() =>
        {
            foreach (var w in windows)
            {
                // IconCache.Get already freezes the BitmapSource
                w.Icon = IconCache.Get(w);
            }
        });

        // Refresh list on UI thread — respect current filter
        await Dispatcher.InvokeAsync(() => UpdateFilteredList(_typedSequence));
    }
}
