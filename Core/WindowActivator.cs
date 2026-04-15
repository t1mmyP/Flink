namespace Flink.Core;

/// <summary>
/// Brings a window to the foreground, including non-minimized background windows.
///
/// Windows restricts SetForegroundWindow to the current foreground process.
/// The workaround: attach our thread's input queue to the foreground thread,
/// which grants us permission to move focus, then detach again.
/// </summary>
internal static class WindowActivator
{
    public static void Activate(WindowInfo window)
    {
        IntPtr hwnd = window.Handle;

        // Restore if minimized
        if (NativeMethods.IsIconic(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);

        IntPtr foreground = NativeMethods.GetForegroundWindow();

        // If it's already the foreground window, nothing to do
        if (foreground == hwnd)
            return;

        uint fgThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
        uint myThread = NativeMethods.GetCurrentThreadId();

        bool attached = false;
        if (fgThread != myThread)
        {
            // Attach our input queue to the foreground thread — this is what
            // allows SetForegroundWindow to succeed from a background process
            attached = NativeMethods.AttachThreadInput(myThread, fgThread, true);
        }

        try
        {
            NativeMethods.BringWindowToTop(hwnd);
            NativeMethods.SetForegroundWindow(hwnd);
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOW);
        }
        finally
        {
            if (attached)
                NativeMethods.AttachThreadInput(myThread, fgThread, false);
        }
    }
}
