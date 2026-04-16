# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build                # Build
dotnet run                  # Run in debug mode
```

### Release / Installer

```bash
dotnet publish Flink.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
powershell -ExecutionPolicy Bypass -File build-installer.ps1   # builds release + Inno Setup installer
```

There are no tests in this project.

## Architecture

Flink is a keyboard-driven Alt+Tab replacement for Windows, built with .NET 9 / WPF. It installs a low-level keyboard hook to intercept Alt+Tab, shows a pre-created overlay window, and lets the user type letter keys to switch windows instantly.

### Event flow

```
Alt+Tab pressed
  → KeyboardHook (WH_KEYBOARD_LL) intercepts, fires AltTabPressed
  → App.OnAltTabPressed() dispatches to UI thread
  → OverlayWindow.ShowOverlay()
      1. WindowEnumerator.GetOpenWindows() — enumerates taskbar-visible windows via EnumWindows
      2. KeyBinder.AssignBindings() — assigns stable letter keys to windows
      3. Renders list, starts background icon loading via IconCache
      4. Positions overlay (centered on primary monitor or mouse monitor)

User types letter(s)
  → KeyboardHook fires KeyPressed(char)
  → OverlayWindow.HandleKeyPress() matches typed sequence to a binding
  → WindowActivator.Activate() — uses AttachThreadInput + SetForegroundWindow

Esc → clears filter or hides overlay
```

### Key binding algorithm (KeyBinder.cs)

Bindings are **session-stable**: once a process gets a letter or a window handle gets a second-letter index, they keep it for the entire Flink session.

1. **First letter:** from config `bindings` map, or auto-assigned from process name initial, or next free letter
2. **Single vs multi-window:** if a process ever has >1 window, it permanently enters multi-window mode (two-letter bindings)
3. **Second letter:** follows QWERTY row order (`q w e r t y u i o p a s d ...`), stable per HWND

### Key components

- **App.xaml.cs** — Entry point. Single-instance mutex, config load, overlay + hook init, tray icon, event wiring
- **Core/KeyboardHook.cs** — Low-level hook via `SetWindowsHookEx(WH_KEYBOARD_LL)`. Suppresses Alt+Tab; when overlay is visible, also intercepts a-z and Esc
- **Core/WindowEnumerator.cs** — `EnumWindows` with taskbar-visibility filter (visible, top-level, not tool window)
- **Core/WindowActivator.cs** — `AttachThreadInput` trick to bypass `SetForegroundWindow` restriction
- **Core/KeyBinder.cs** — Four-pass binding algorithm with session-stable caches
- **Core/IconCache.cs** — Icon extraction (WM_GETICON → class icon → SHGetFileInfo → ExtractAssociatedIcon), cached by exe path
- **Core/NativeMethods.cs** — All P/Invoke declarations (hooks, window management, icons, monitors)
- **Config/AppConfig.cs** — Data model: `Bindings`, `Names`, `FollowMouse`, `Autostart`, `Theme`
- **Config/ConfigManager.cs** — Reads/writes `~/.flink/flink.json`, manages autostart registry key
- **UI/OverlayWindow.xaml** — Transparent, topmost, hit-test-invisible WPF window with dark theme
- **UI/OverlayWindow.xaml.cs** — Show/hide logic, key sequence matching, icon async loading, multi-monitor positioning

### Design decisions

- The overlay window is **pre-created at startup** and only shown/hidden — no creation overhead on Alt+Tab
- The overlay is **not activated** (`Show()` without `Activate()`) and is **hit-test-invisible** — all input comes through the keyboard hook, not WPF focus
- Icons load **asynchronously** so the overlay appears instantly
- Uses both WPF (overlay UI) and WinForms (`NotifyIcon` for system tray) — hence both `UseWPF` and `UseWindowsForms` in csproj
- `AllowUnsafeBlocks=true` is needed for P/Invoke marshaling

## Configuration

User config lives at `~/.flink/flink.json`. Process names are lowercase without `.exe` (match Task Manager → Details tab).

## CI/CD

GitHub Actions release workflow (`.github/workflows/release.yml`) triggers on `v*` tags: publishes self-contained exe + builds Inno Setup installer + creates GitHub Release.
