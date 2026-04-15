# Flink

A keyboard-driven Alt+Tab replacement for Windows. Press a key, switch windows — instantly.

![Flink Overlay](docs/preview.png)

---

## What it does

Flink replaces the default Alt+Tab switcher with a minimal overlay that assigns a letter to every open window. Press the letter, the window comes to the front. No mouse, no scrolling through a carousel.

- **Single window per app** → one key (`t` for Terminal)
- **Multiple windows** → two keys, second from the QWERTY row (`tq`, `tw`, `te` ...)
- **Type to filter** → press the first key, the list narrows to matching windows
- **Configurable** → pin any app to any letter, set display names

---

## Install

> Requires Windows 10/11 and [.NET 9 Runtime](https://dotnet.microsoft.com/download/dotnet/9).

1. Download the latest `Flink.exe` from [Releases](../../releases)
2. Run it — Flink appears in the system tray
3. Press **Alt+Tab** to open the switcher

To add Flink to autostart: right-click the tray icon → **Autostart**.

---

## Usage

| Action | Key |
|---|---|
| Open switcher | `Alt+Tab` |
| Switch to window | Type the shown key(s) |
| Dismiss | `Esc` |

### Multi-window apps

When an app has more than one open window, Flink assigns two-letter bindings. The second letter follows the QWERTY row (`q w e r t y u i o p`, then home row, then bottom row) — keys that sit close together and are easy to hit in sequence.

```
t   →  Terminal (single window)

tq  →  Terminal — nvim
tw  →  Terminal — ssh prod
te  →  Terminal — logs
```

---

## Configuration

Config is stored at `~/.flink/flink.json` and is created automatically on first run.

```json
{
  "bindings": {
    "windowsterminal": "t",
    "zen":             "z",
    "code":            "c",
    "chrome":          "b",
    "msedge":          "e",
    "explorer":        "x"
  },
  "names": {
    "windowsterminal": "Terminal",
    "zen":             "Zen Browser",
    "code":            "VS Code",
    "chrome":          "Chrome",
    "msedge":          "Edge",
    "explorer":        "Explorer"
  },
  "followMouse": false,
  "autostart":   false,
  "theme":       "dark"
}
```

### Options

| Key | Type | Description |
|---|---|---|
| `bindings` | object | Map process name → fixed letter. Process names are lowercase, no `.exe`. |
| `names` | object | Map process name → display name shown in the overlay. |
| `followMouse` | bool | `true`: overlay appears on the monitor the mouse is on. `false`: always primary monitor. |
| `autostart` | bool | Launch Flink when Windows starts. Can also be toggled via the tray icon. |
| `theme` | string | `"dark"` (more coming) |

**Finding process names:** Open Task Manager → Details tab. The name without `.exe` is what goes in the config.

---

## Building from source

```
git clone https://github.com/you/flink
cd flink
dotnet build
dotnet run
```

Requires [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9).

### Single-file release build

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output: `bin/Release/net9.0-windows/win-x64/publish/Flink.exe`

---

## How it works

Flink installs a low-level Windows keyboard hook (`WH_KEYBOARD_LL`) to intercept Alt+Tab before it reaches the system. The overlay window is pre-created at startup and only shown/hidden on demand — no creation overhead on keypress. Window focus is transferred via `AttachThreadInput` + `SetForegroundWindow`, which works reliably for both minimized and background windows.

---

## License

MIT
