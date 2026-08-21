using System.Diagnostics;
using Microsoft.Win32;

namespace DeskCue.Services;

public sealed record DesktopInfo(int Index, int Count, string Name, Guid CurrentId);

/// <summary>
/// Reads the current virtual-desktop state directly from the registry.
///
/// This deliberately avoids the undocumented COM interfaces (IVirtualDesktopManagerInternal,
/// whose IIDs change with every Windows build). The registry layout below has been stable
/// across Windows 10/11 builds:
///
///   HKCU\...\Explorer\VirtualDesktops\VirtualDesktopIDs   (REG_BINARY, 16 bytes per desktop, in order)
///   HKCU\...\Explorer\SessionInfo\{sid}\VirtualDesktops\CurrentVirtualDesktop  (REG_BINARY, 16 bytes)
///   HKCU\...\Explorer\VirtualDesktops\Desktops\{GUID}\Name  (REG_SZ, custom name)
/// </summary>
public static class VirtualDesktopRegistry
{
    private const string ExplorerPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer";
    private const string VdPath = ExplorerPath + @"\VirtualDesktops";

    public static DesktopInfo? Read()
    {
        try
        {
            using var vd = Registry.CurrentUser.OpenSubKey(VdPath);
            if (vd?.GetValue("VirtualDesktopIDs") is not byte[] idsBlob || idsBlob.Length < 16)
                return null;

            int count = idsBlob.Length / 16;
            var ids = new Guid[count];
            for (int i = 0; i < count; i++)
            {
                var b = new byte[16];
                Array.Copy(idsBlob, i * 16, b, 0, 16);
                ids[i] = new Guid(b);
            }

            Guid current = ReadCurrent(vd);
            int idx = Array.IndexOf(ids, current);
            if (idx < 0) idx = 0; // current not resolvable → assume first

            string name = ReadName(ids[idx]);
            return new DesktopInfo(idx + 1, count, name, ids[idx]);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The ordered list of desktop GUIDs (index 0 = desktop 1), or null if unavailable.</summary>
    public static Guid[]? ReadOrderedIds()
    {
        try
        {
            using var vd = Registry.CurrentUser.OpenSubKey(VdPath);
            if (vd?.GetValue("VirtualDesktopIDs") is not byte[] blob || blob.Length < 16)
                return null;

            int count = blob.Length / 16;
            var ids = new Guid[count];
            for (int i = 0; i < count; i++)
            {
                var b = new byte[16];
                Array.Copy(blob, i * 16, b, 0, 16);
                ids[i] = new Guid(b);
            }
            return ids;
        }
        catch
        {
            return null;
        }
    }

    private static Guid ReadCurrent(RegistryKey vd)
    {
        // Per-session key is the authoritative source on Windows 11.
        try
        {
            int sid = Process.GetCurrentProcess().SessionId;
            using var sk = Registry.CurrentUser.OpenSubKey(
                $@"{ExplorerPath}\SessionInfo\{sid}\VirtualDesktops");
            if (sk?.GetValue("CurrentVirtualDesktop") is byte[] b && b.Length == 16)
                return new Guid(b);
        }
        catch { /* ignore, try fallback */ }

        if (vd.GetValue("CurrentVirtualDesktop") is byte[] b2 && b2.Length == 16)
            return new Guid(b2);

        return Guid.Empty;
    }

    private static string ReadName(Guid id)
    {
        // The Desktops subkey name has appeared with braces (upper/lower) across builds.
        string[] forms =
        {
            "{" + id.ToString().ToUpperInvariant() + "}",
            "{" + id.ToString() + "}",
            id.ToString(),
        };

        foreach (var form in forms)
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey($@"{VdPath}\Desktops\{form}");
                if (k?.GetValue("Name") is string n && !string.IsNullOrWhiteSpace(n))
                    return n;
            }
            catch { /* try next form */ }
        }
        return "";
    }
}
