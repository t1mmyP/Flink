using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Flink.Core;

/// <summary>
/// Low-level keyboard hook that intercepts Alt+Tab system-wide.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private IntPtr _hookHandle = IntPtr.Zero;
    private readonly NativeMethods.HookProc _hookProc;

    public event Action? AltTabPressed;
    public event Action<char>? KeyPressed;
    public event Action? EscapePressed;

    private bool _overlayVisible = false;

    public KeyboardHook()
    {
        // Keep a reference to prevent GC collection
        _hookProc = HookCallback;
    }

    public void Install()
    {
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _hookProc,
            NativeMethods.GetModuleHandle(module.ModuleName),
            0);

        if (_hookHandle == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to install keyboard hook. Error: {Marshal.GetLastWin32Error()}");
    }

    public void SetOverlayVisible(bool visible)
    {
        _overlayVisible = visible;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        var kbd = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
        bool isKeyDown = wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN;
        bool isKeyUp = wParam == NativeMethods.WM_KEYUP || wParam == NativeMethods.WM_SYSKEYUP;

        // Intercept Alt+Tab (suppress and show overlay)
        if (isKeyDown && kbd.vkCode == NativeMethods.VK_TAB)
        {
            bool altDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_ALT) & 0x8000) != 0;
            if (altDown)
            {
                AltTabPressed?.Invoke();
                return (IntPtr)1; // Suppress
            }
        }

        // When overlay is visible, intercept letter keys and Escape
        if (_overlayVisible && isKeyDown)
        {
            if (kbd.vkCode == NativeMethods.VK_ESCAPE)
            {
                EscapePressed?.Invoke();
                return (IntPtr)1;
            }

            // a-z
            if (kbd.vkCode >= 0x41 && kbd.vkCode <= 0x5A)
            {
                char c = (char)('a' + (kbd.vkCode - 0x41));
                KeyPressed?.Invoke(c);
                return (IntPtr)1;
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }
}
