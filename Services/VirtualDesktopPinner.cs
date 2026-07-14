using System.Runtime.InteropServices;

namespace VirtualDesktopIndicator.Services;

/// <summary>
/// Pins this app to ALL virtual desktops via the undocumented IVirtualDesktopPinnedApps
/// interface, so the overlay no longer has to be moved onto the current desktop after every
/// switch.
///
/// Moving a window across desktops (IVirtualDesktopManager.MoveWindowToDesktop) makes the
/// shell re-enumerate the taskbar and briefly redraw every taskbar button — which reads as a
/// flicker of other apps' buttons on each switch. Pinning once removes the per-switch move
/// entirely, so nothing re-triggers that redraw.
///
/// We pin by AppUserModelID (PinAppID), NOT by view (PinView): the overlay is a WS_EX_TOOLWINDOW
/// and the shell never creates an IApplicationView for tool windows, so the view-based path fails.
/// Pinning the app id makes every window this process owns appear on all desktops. Set the id via
/// <see cref="SetAppId"/> before any window is created.
///
/// IIDs are build-specific (defined here for Windows 11 24H2/25H2, build 26100+), matching
/// the internal switching interface in <see cref="VirtualDesktopInternal"/>. Everything is
/// guarded: on ANY failure <see cref="Pin"/> returns false and the caller keeps the existing
/// move-on-poll behaviour, so an interface change degrades gracefully instead of crashing.
///
/// Must be called on an STA thread (the WPF UI thread).
/// </summary>
public static class VirtualDesktopPinner
{
    /// <summary>Explicit AppUserModelID we assign to the process and then pin.</summary>
    public const string AppId = "VirtualDesktopIndicator.Overlay";

    private static readonly Guid CLSID_ImmersiveShell = new("C2F03A33-21F5-47FA-B4BB-156362A2F239");
    private static readonly Guid CLSID_VirtualDesktopPinnedApps = new("B5A399E7-1C87-46B8-88E9-FC5747B171BD");
    private static readonly Guid IID_IVirtualDesktopPinnedApps = new("4CE81583-1E4C-4632-A621-07A53543148F");

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    private interface IServiceProvider10
    {
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object QueryService(ref Guid service, ref Guid riid);
    }

    // Declared in EXACT vtable order up to PinAppID; UnpinAppID onward is unused but each entry
    // still occupies one slot. Do not reorder.
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("4CE81583-1E4C-4632-A621-07A53543148F")]
    private interface IVirtualDesktopPinnedApps
    {
        [PreserveSig] int IsAppIdPinned([MarshalAs(UnmanagedType.LPWStr)] string appId, out bool pinned);
        [PreserveSig] int PinAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);
        [PreserveSig] int UnpinAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);
    }

    /// <summary>Set once the interface is known not to bind on this machine — avoids retrying.</summary>
    private static bool _knownUnavailable;

    /// <summary>
    /// Assign the process an explicit AppUserModelID. Must run before any window is created so the
    /// overlay inherits it; the same id is what <see cref="Pin"/> pins.
    /// </summary>
    public static void SetAppId()
    {
        try { SetCurrentProcessExplicitAppUserModelID(AppId); }
        catch (Exception ex) { Log.Write($"Pinner: SetAppId failed ({ex.GetType().Name}: {ex.Message})"); }
    }

    /// <summary>
    /// Pin this app to every virtual desktop. Returns true on success; on false the caller
    /// should keep moving the overlay onto the current desktop as before.
    /// </summary>
    public static bool Pin()
    {
        if (_knownUnavailable) return false;

        try
        {
            var shellType = Type.GetTypeFromCLSID(CLSID_ImmersiveShell);
            if (shellType == null) { _knownUnavailable = true; return false; }

            var provider = (IServiceProvider10)Activator.CreateInstance(shellType)!;

            Guid service = CLSID_VirtualDesktopPinnedApps;
            Guid iid = IID_IVirtualDesktopPinnedApps;
            var pinned = (IVirtualDesktopPinnedApps)provider.QueryService(ref service, ref iid);

            if (pinned.IsAppIdPinned(AppId, out bool already) == 0 && already)
            {
                Log.Write("Pinner: app already pinned to all desktops");
                return true;
            }

            if (pinned.PinAppID(AppId) != 0)
            {
                Log.Write("Pinner: PinAppID failed; using fallback");
                return false;
            }

            Log.Write("Pinner: app pinned to all desktops");
            return true;
        }
        catch (Exception ex)
        {
            Log.Write($"Pinner: unavailable ({ex.GetType().Name}: {ex.Message}); using fallback");
            _knownUnavailable = true; // don't keep retrying a broken interface
            return false;
        }
    }

    [DllImport("shell32.dll", PreserveSig = false)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appId);
}
