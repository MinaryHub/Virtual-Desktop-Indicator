using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DeskCue.Linux.Services;

/// <summary>
/// Enables/disables "run at login" by writing a freedesktop.org autostart entry
/// to $XDG_CONFIG_HOME/autostart/deskcue.desktop
/// (~/.config/autostart/... by default). Per-user, no root required.
/// </summary>
public static class StartupManager
{
    private const string FileName = "deskcue.desktop";
    // Entry name used before the rename; see MigrateLegacyEntry below.
    private const string LegacyFileName = "virtual-desktop-indicator.desktop";

    private static string AutostartDir
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var baseDir = !string.IsNullOrWhiteSpace(xdg)
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(baseDir, "autostart");
        }
    }

    private static string EntryPath => Path.Combine(AutostartDir, FileName);

    /// <summary>Path to the running executable.</summary>
    public static string ExePath
    {
        get
        {
            var p = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(p)) return p;
            return Process.GetCurrentProcess().MainModule?.FileName ?? "";
        }
    }

    /// <summary>
    /// Replaces the autostart entry written under the pre-rename filename. The old entry points
    /// at the old binary path, which the installer no longer ships, so leaving it behind would
    /// mean a failing autostart plus a toggle that reads "off".
    /// </summary>
    public static void MigrateLegacyEntry()
    {
        try
        {
            var legacy = Path.Combine(AutostartDir, LegacyFileName);
            if (!File.Exists(legacy)) return;

            bool wasEnabled = !File.ReadLines(legacy).Any(l =>
                l.Trim().Replace(" ", "").Equals("Hidden=true", StringComparison.OrdinalIgnoreCase));
            File.Delete(legacy);
            if (wasEnabled && !File.Exists(EntryPath)) SetEnabled(true);
            Log.Write("migrated autostart entry to deskcue.desktop");
        }
        catch (Exception ex) { Log.Write($"autostart migration failed: {ex.Message}"); }
    }

    public static bool IsEnabled()
    {
        try
        {
            if (!File.Exists(EntryPath)) return false;
            // Treat "Hidden=true" as disabled (some tools disable rather than delete).
            foreach (var line in File.ReadLines(EntryPath))
                if (line.Trim().Replace(" ", "").Equals("Hidden=true", StringComparison.OrdinalIgnoreCase))
                    return false;
            return true;
        }
        catch { return false; }
    }

    /// <summary>Returns true on success.</summary>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                Directory.CreateDirectory(AutostartDir);
                var exec = ExePath;
                var content =
                    "[Desktop Entry]\n" +
                    "Type=Application\n" +
                    "Name=DeskCue\n" +
                    $"Exec=\"{exec}\"\n" +
                    "X-GNOME-Autostart-enabled=true\n" +
                    "Terminal=false\n";
                File.WriteAllText(EntryPath, content);
            }
            else if (File.Exists(EntryPath))
            {
                File.Delete(EntryPath);
            }
            return true;
        }
        catch { return false; }
    }
}
