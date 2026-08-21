using System.Diagnostics;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace DeskCue.Services;

/// <summary>
/// Enables/disables "run at Windows startup".
///
/// Plain-exe build: writes the current executable path to HKCU\...\CurrentVersion\Run
/// (per-user, no admin rights). MSIX/Store build: an HKCU\Run entry written from inside a
/// package is virtualized and ignored by the shell, so we drive the declared MSIX
/// <c>windows.startupTask</c> extension via the <see cref="StartupTask"/> API instead. The
/// <see cref="TaskId"/> must match the TaskId in Package.appxmanifest.
/// </summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    // Pre-rename spelling on purpose (product name is DeskCue): changing it would leave a
    // stale Run entry behind on machines that already have autostart enabled.
    private const string ValueName = "DeskCue";
    // Value name used before the rename; see MigrateLegacyRunValue below.
    private const string LegacyValueName = "VirtualDesktopIndicator";
    private const string TaskId = "DeskCueStartup";

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

    /// <summary>
    /// Drops the Run entry written under the pre-rename value name, re-creating it under the
    /// current one when it was enabled. Without this an upgraded install would autostart from
    /// a stale entry pointing at the old .exe path (and the tray checkbox would read false).
    /// No-op in the packaged build, which uses the StartupTask API instead of the registry.
    /// </summary>
    public static void MigrateLegacyRunValue()
    {
        if (PackageContext.IsPackaged) return;

        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (k?.GetValue(LegacyValueName) is not string legacy || string.IsNullOrWhiteSpace(legacy)) return;

            k.DeleteValue(LegacyValueName, throwOnMissingValue: false);
            if (k.GetValue(ValueName) == null)
                k.SetValue(ValueName, $"\"{ExePath}\"");
            Log.Write("migrated autostart entry to the DeskCue value name");
        }
        catch (Exception ex) { Log.Write($"autostart migration failed: {ex.Message}"); }
    }

    public static bool IsEnabled()
    {
        if (PackageContext.IsPackaged) return PackagedIsEnabled();

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
        if (PackageContext.IsPackaged) return PackagedSetEnabled(enabled);

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

    // --- MSIX StartupTask path ----------------------------------------------
    // The WinRT calls are async; callers here are synchronous UI-thread handlers, so we
    // pump them on the thread pool and block briefly. Running on the pool (not the STA UI
    // thread) keeps the IAsyncOperation continuation from deadlocking on the message loop.

    private static bool PackagedIsEnabled()
    {
        try
        {
            var task = Task.Run(async () => await StartupTask.GetAsync(TaskId)).GetAwaiter().GetResult();
            return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }
        catch (Exception ex) { Log.Write($"StartupTask query failed: {ex.Message}"); return false; }
    }

    private static bool PackagedSetEnabled(bool enabled)
    {
        try
        {
            return Task.Run(async () =>
            {
                var task = await StartupTask.GetAsync(TaskId);
                if (enabled)
                {
                    // The user's Task Manager choice wins: if they disabled it there, the OS
                    // returns DisabledByUser and RequestEnableAsync cannot re-enable it.
                    var state = await task.RequestEnableAsync();
                    if (state is not (StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy))
                        Log.Write($"StartupTask enable request ended in state {state}");
                    return state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
                }

                task.Disable();
                return true;
            }).GetAwaiter().GetResult();
        }
        catch (Exception ex) { Log.Write($"StartupTask set failed: {ex.Message}"); return false; }
    }
}
