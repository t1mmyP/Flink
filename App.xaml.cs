using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Flink.Config;
using Flink.Core;
using Flink.UI;

namespace Flink;

public partial class App : System.Windows.Application
{
    private KeyboardHook? _hook;
    private OverlayWindow? _overlay;
    private ConfigManager? _configManager;
    private NotifyIcon? _trayIcon;

    // Single-instance mutex
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance guard
        _mutex = new Mutex(true, "Flink_SingleInstance", out bool isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        _configManager = new ConfigManager();
        _configManager.Load();
        _configManager.ApplyAutostart();

        // Pre-create overlay window (key for instant response)
        _overlay = new OverlayWindow(_configManager.Config);
        _overlay.WindowSelected += OnWindowSelected;

        // Install keyboard hook
        _hook = new KeyboardHook();
        _hook.AltTabPressed += OnAltTabPressed;
        _hook.KeyPressed += OnKeyPressed;
        _hook.EscapePressed += OnEscapePressed;
        _hook.Install();

        // System tray
        SetupTrayIcon();
    }

    private void OnAltTabPressed()
    {
        Dispatcher.Invoke(() =>
        {
            _overlay!.ShowOverlay();
            _hook!.SetOverlayVisible(true);
        });
    }

    private void OnKeyPressed(char c)
    {
        Dispatcher.Invoke(() => _overlay!.HandleKeyPress(c));
    }

    private void OnEscapePressed()
    {
        Dispatcher.Invoke(() =>
        {
            _overlay!.HandleEscape();
            // Only release the hook's overlay-mode when fully closed
            if (!_overlay.IsVisible)
                _hook!.SetOverlayVisible(false);
        });
    }

    private void OnWindowSelected(WindowInfo window)
    {
        _hook!.SetOverlayVisible(false);
        WindowActivator.Activate(window);
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Text = "Flink",
            Visible = true,
        };

        // Try to load icon, fall back to default
        try
        {
            string iconPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Assets", "flink.ico");
            if (System.IO.File.Exists(iconPath))
                _trayIcon.Icon = new Icon(iconPath);
            else
                _trayIcon.Icon = SystemIcons.Application;
        }
        catch
        {
            _trayIcon.Icon = SystemIcons.Application;
        }

        var menu = new ContextMenuStrip();

        var autostartItem = new ToolStripMenuItem("Autostart")
        {
            Checked = _configManager!.Config.Autostart,
            CheckOnClick = true,
        };
        autostartItem.CheckedChanged += (_, _) =>
        {
            _configManager.Config.Autostart = autostartItem.Checked;
            _configManager.Save();
            _configManager.ApplyAutostart();
        };

        var openConfigItem = new ToolStripMenuItem("Open Config");
        openConfigItem.Click += (_, _) =>
        {
            string path = _configManager!.GetConfigPath();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        };

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApp();

        menu.Items.Add(autostartItem);
        menu.Items.Add(openConfigItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) =>
        {
            string path = _configManager!.GetConfigPath();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        };
    }

    private void ExitApp()
    {
        _hook?.Dispose();
        _trayIcon?.Dispose();
        _mutex?.ReleaseMutex();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
