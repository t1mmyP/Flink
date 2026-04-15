using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Flink.Core;

/// <summary>
/// Extracts and caches app icons.
///
/// Priority chain per window:
///   1. WM_GETICON (big) — window's own icon, most accurate
///   2. GetClassLongPtr(GCL_HICON) — class icon fallback
///   3. SHGetFileInfo on exe path — shell icon, covers UWP/Store apps
///   4. Icon.ExtractAssociatedIcon — last resort
///
/// Icons are cached by executable path so each app only pays the extraction
/// cost once per Flink session.
/// </summary>
internal static class IconCache
{
    // null in the dict means "tried and failed" — don't retry
    private static readonly ConcurrentDictionary<string, BitmapSource?> _cache = new();

    public static BitmapSource? Get(WindowInfo info)
    {
        // Try HWND-based methods first (no caching needed — fast SendMessage calls)
        var fromHwnd = GetFromHwnd(info.Handle);
        if (fromHwnd != null) return fromHwnd;

        // Exe-path-based methods — cache these
        string key = info.ExecutablePath.ToLowerInvariant();
        if (string.IsNullOrEmpty(key)) return null;

        return _cache.GetOrAdd(key, path => GetFromExe(path, info.Handle));
    }

    public static void Clear() => _cache.Clear();

    // ── HWND methods ─────────────────────────────────────────────────────────

    private static BitmapSource? GetFromHwnd(IntPtr hwnd)
    {
        // 1. WM_GETICON big
        IntPtr hIcon = NativeMethods.SendMessage(hwnd, NativeMethods.WM_GETICON, NativeMethods.ICON_BIG, 0);

        // 2. WM_GETICON small
        if (hIcon == IntPtr.Zero)
            hIcon = NativeMethods.SendMessage(hwnd, NativeMethods.WM_GETICON, NativeMethods.ICON_SMALL, 0);

        // 3. Class icon
        if (hIcon == IntPtr.Zero)
            hIcon = NativeMethods.GetClassLongPtr(hwnd, NativeMethods.GCL_HICON);

        if (hIcon == IntPtr.Zero)
            hIcon = NativeMethods.GetClassLongPtr(hwnd, NativeMethods.GCL_HICONSM);

        return hIcon != IntPtr.Zero ? HIconToBitmapSource(hIcon) : null;
    }

    // ── Exe-path methods ─────────────────────────────────────────────────────

    private static BitmapSource? GetFromExe(string exePath, IntPtr hwnd)
    {
        // 4. SHGetFileInfo — works for UWP, Store apps, and everything the shell knows
        var bmp = GetViaSHGetFileInfo(exePath);
        if (bmp != null) return bmp;

        // 5. ExtractAssociatedIcon — classic Win32 fallback
        return GetViaExtractAssociatedIcon(exePath);
    }

    private static BitmapSource? GetViaSHGetFileInfo(string exePath)
    {
        try
        {
            var shfi = new NativeMethods.SHFILEINFO();
            IntPtr result = NativeMethods.SHGetFileInfo(
                exePath, 0, ref shfi,
                (uint)System.Runtime.InteropServices.Marshal.SizeOf(shfi),
                NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);

            if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                return null;

            var bmp = HIconToBitmapSource(shfi.hIcon);
            NativeMethods.DestroyIcon(shfi.hIcon);
            return bmp;
        }
        catch { return null; }
    }

    private static BitmapSource? GetViaExtractAssociatedIcon(string exePath)
    {
        try
        {
            var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            if (icon == null) return null;
            var bmp = HIconToBitmapSource(icon.Handle);
            icon.Dispose();
            return bmp;
        }
        catch { return null; }
    }

    private static BitmapSource? HIconToBitmapSource(IntPtr hIcon)
    {
        try
        {
            var bmp = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
