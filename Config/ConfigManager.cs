using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace Flink.Config;

internal sealed class ConfigManager
{
    // ~/.flink/flink.json
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".flink", "flink.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const string AutostartKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AutostartName = "Flink";

    public AppConfig Config { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                Config = CreateDefault();
                Save();
                return;
            }

            string json = File.ReadAllText(ConfigPath);
            Config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? CreateDefault();
        }
        catch
        {
            Config = CreateDefault();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            string json = JsonSerializer.Serialize(Config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch { /* non-critical */ }
    }

    public void ApplyAutostart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AutostartKey, writable: true);
            if (key == null) return;

            string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;

            if (Config.Autostart)
                key.SetValue(AutostartName, $"\"{exePath}\"");
            else
                key.DeleteValue(AutostartName, throwOnMissingValue: false);
        }
        catch { /* non-critical */ }
    }

    private static AppConfig CreateDefault() => new()
    {
        Bindings = new Dictionary<string, string>
        {
            { "windowsterminal", "t" },
            { "zen", "z" },
            { "chrome", "b" },
            { "firefox", "f" },
            { "msedge", "e" },
            { "code", "c" },
            { "explorer", "x" },
        },
        Names = new Dictionary<string, string>
        {
            { "windowsterminal", "Terminal" },
            { "zen", "Zen Browser" },
            { "chrome", "Chrome" },
            { "firefox", "Firefox" },
            { "msedge", "Edge" },
            { "code", "VS Code" },
            { "explorer", "Explorer" },
            { "slack", "Slack" },
            { "discord", "Discord" },
            { "teams", "Teams" },
            { "outlook", "Outlook" },
            { "notepad", "Notepad" },
            { "notepad++", "Notepad++" },
        },
        FollowMouse = false,
        Autostart = false,
        Theme = "dark",
    };

    public string GetConfigPath() => ConfigPath;
}
