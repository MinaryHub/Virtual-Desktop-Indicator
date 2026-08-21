using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeskCue.Linux.Services;

/// <summary>
/// User configuration, persisted as JSON under
/// $XDG_CONFIG_HOME/DeskCue/config.json (~/.config/... by default).
/// </summary>
public sealed class AppConfig
{
    // --- Overlay placement ---------------------------------------------------
    /// TopLeft | TopCenter | TopRight | BottomLeft | BottomCenter | BottomRight | Center
    public string Position { get; set; } = "TopCenter";
    public double MarginX { get; set; } = 0;
    public double MarginY { get; set; } = 12;

    // --- Overlay appearance --------------------------------------------------
    public double Opacity { get; set; } = 0.55;
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
    /// Hotkey → desktop mappings, e.g. { "Hotkey": "Ctrl+Alt+1", "Desktop": 1 }.
    /// Supported modifiers on Linux: Ctrl, Alt, Shift, Super (the "Windows" key).
    /// Keys: 1-9, 0, A-Z, F1-F24, numpad Num0-Num9.
    /// </summary>
    public List<HotkeyBinding> Hotkeys { get; set; } = DefaultHotkeys();

    [JsonIgnore] public string SourcePath { get; private set; } = "";

    private static List<HotkeyBinding> DefaultHotkeys()
    {
        // Ctrl+Alt+N is a low-conflict default across GNOME/KDE/XFCE. (Super+N is
        // often reserved by the desktop environment, much like on Windows.)
        var list = new List<HotkeyBinding>();
        for (int i = 1; i <= 9; i++)
            list.Add(new HotkeyBinding { Hotkey = $"Ctrl+Alt+{i}", Desktop = i });
        return list;
    }

    // -------------------------------------------------------------------------
    public static string ConfigDirectory
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var baseDir = !string.IsNullOrWhiteSpace(xdg)
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(baseDir, "DeskCue");
        }
    }

    public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    /// <summary>Folder used before the rename to DeskCue; migrated once on first run.</summary>
    private static string LegacyConfigDirectory
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var baseDir = !string.IsNullOrWhiteSpace(xdg)
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(baseDir, "VirtualDesktopIndicator");
        }
    }

    /// <summary>
    /// One-time move of the pre-rename settings folder. No-op once the new folder exists, and a
    /// failure only costs the user their old settings rather than crashing the app.
    /// </summary>
    public static void MigrateLegacyFolder()
    {
        try
        {
            if (Directory.Exists(ConfigDirectory) || !Directory.Exists(LegacyConfigDirectory)) return;
            Directory.Move(LegacyConfigDirectory, ConfigDirectory);
            Log.Write($"migrated settings from {LegacyConfigDirectory}");
        }
        catch (Exception ex) { Log.Write($"settings migration failed: {ex.Message}"); }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static AppConfig Load()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            if (File.Exists(ConfigPath))
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), JsonOpts);
                if (cfg != null)
                {
                    if (cfg.Hotkeys.Count == 0) cfg.Hotkeys = DefaultHotkeys();
                    cfg.SourcePath = ConfigPath;
                    return cfg;
                }
            }
        }
        catch { /* fall through to defaults */ }

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
        catch { /* best-effort */ }
    }
}

public sealed class HotkeyBinding
{
    public string Hotkey { get; set; } = "";
    public int Desktop { get; set; }
}
