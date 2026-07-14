using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VirtualDesktopIndicator.Services;

/// <summary>
/// User configuration, persisted as JSON under %APPDATA%\VirtualDesktopIndicator\config.json.
/// The file is created with defaults on first run and can be edited by hand
/// (tray menu → "Open config file", then "Reload config").
/// </summary>
public sealed class AppConfig
{
    // --- Overlay placement ---------------------------------------------------
    /// TopLeft | TopCenter | TopRight | BottomLeft | BottomCenter | BottomRight | Center
    public string Position { get; set; } = "TopCenter";
    public double MarginX { get; set; } = 0;
    public double MarginY { get; set; } = 12;

    /// <summary>
    /// Show the indicator on every monitor at the same <see cref="Position"/> (default), or only
    /// on the primary monitor when false.
    /// </summary>
    public bool ShowOnAllMonitors { get; set; } = true;

    // --- Overlay appearance --------------------------------------------------
    /// <summary>Overlay (desktop-number) opacity, 0.05–1.0. Default 0.5 = 50%.</summary>
    public double Opacity { get; set; } = 0.5;
    public double FontSize { get; set; } = 28;
    public bool ShowNumber { get; set; } = true;
    public bool ShowCount { get; set; } = true;   // "2 / 4" instead of just "2"
    public bool ShowName { get; set; } = true;
    public string Foreground { get; set; } = "#FFFFFF";
    public string Background { get; set; } = "#000000";
    public double CornerRadius { get; set; } = 10;

    // --- Behaviour -----------------------------------------------------------
    public int PollIntervalMs { get; set; } = 300;

    /// <summary>
    /// Hotkey → desktop mappings. Each entry is like { "Hotkey": "Ctrl+Alt+1", "Desktop": 1 }.
    /// Desktop is 1-based. Supported modifiers: Ctrl, Alt, Shift, Win.
    /// Keys: 1-9, 0, A-Z, F1-F12.
    /// </summary>
    public List<HotkeyBinding> Hotkeys { get; set; } = DefaultHotkeys();

    [JsonIgnore] public string SourcePath { get; private set; } = "";

    private static List<HotkeyBinding> DefaultHotkeys()
    {
        // Ctrl+Alt+N is safe: unlike Win+Shift+N (reserved by Windows for "new taskbar
        // app instance"), it registers cleanly and does not collide with the shell.
        var list = new List<HotkeyBinding>();
        for (int i = 1; i <= 9; i++)
            list.Add(new HotkeyBinding { Hotkey = $"Ctrl+Alt+{i}", Desktop = i });
        return list;
    }

    // -------------------------------------------------------------------------
    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "VirtualDesktopIndicator");

    public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keep "Ctrl+Alt+1" readable
    };

    public static AppConfig Load()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (cfg != null)
                {
                    if (cfg.Hotkeys.Count == 0) cfg.Hotkeys = DefaultHotkeys();
                    cfg.SourcePath = ConfigPath;
                    return cfg;
                }
            }
        }
        catch
        {
            // fall through to defaults on any parse/IO error
        }

        var def = new AppConfig { SourcePath = ConfigPath };
        def.Save();
        return def;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch
        {
            // best-effort; ignore write failures
        }
    }
}

public sealed class HotkeyBinding
{
    public string Hotkey { get; set; } = "";
    public int Desktop { get; set; }
}
