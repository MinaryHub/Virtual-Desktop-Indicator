using System.Diagnostics;
using Microsoft.Win32;

namespace VirtualDesktopIndicator.Services;

/// <summary>
/// Enables/disables "run at Windows startup" by writing the current executable path
/// to HKCU\...\CurrentVersion\Run. This is per-user and needs no admin rights.
/// </summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VirtualDesktopIndicator";

    /// <summary>Path to the running .exe (works for the published single-file app).</summary>
    public static string ExePath
    {
        get
        {
            var p = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(p)) return p;
            return Process.GetCurrentProcess().MainModule?.FileName ?? "";
        }
    }

    public static bool IsEnabled()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(ValueName) is string v && !string.IsNullOrWhiteSpace(v);
        }
        catch { return false; }
    }

    /// <summary>Returns true on success.</summary>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (k == null) return false;

            if (enabled)
                k.SetValue(ValueName, $"\"{ExePath}\"");
            else
                k.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }
        catch { return false; }
    }
}
