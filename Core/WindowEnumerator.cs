using System.Diagnostics;

namespace Flink.Core;

/// <summary>
/// Enumerates all visible, taskbar-relevant windows.
/// </summary>
internal static class WindowEnumerator
{
    public static List<WindowInfo> GetOpenWindows()
    {
        var windows = new List<WindowInfo>(32);

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!IsTaskbarWindow(hwnd))
                return true;

            var title = GetWindowTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title))
                return true;

            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            var (procName, exePath) = GetProcessInfo(pid);

            windows.Add(new WindowInfo
            {
                Handle = hwnd,
                Title = title,
                ProcessName = procName,
                ExecutablePath = exePath,
                ProcessId = pid,
            });

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static bool IsTaskbarWindow(IntPtr hwnd)
    {
        if (!NativeMethods.IsWindowVisible(hwnd))
            return false;

        if (NativeMethods.GetParent(hwnd) != IntPtr.Zero)
            return false;

        long exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);

        if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
            return false;

        bool hasAppWindow = (exStyle & NativeMethods.WS_EX_APPWINDOW) != 0;
        IntPtr owner = NativeMethods.GetWindow(hwnd, 4 /* GW_OWNER */);

        if (!hasAppWindow && owner != IntPtr.Zero)
            return false;

        return true;
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        int len = NativeMethods.GetWindowTextLength(hwnd);
        if (len == 0) return string.Empty;

        var sb = new System.Text.StringBuilder(len + 1);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static (string name, string path) GetProcessInfo(uint pid)
    {
        try
        {
            var proc = Process.GetProcessById((int)pid);
            string name = proc.ProcessName.ToLowerInvariant();
            string path = string.Empty;
            try { path = proc.MainModule?.FileName ?? string.Empty; } catch { }
            return (name, path);
        }
        catch
        {
            return ("unknown", string.Empty);
        }
    }
}
