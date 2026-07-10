using System.Runtime.InteropServices;

namespace VirtualDesktopIndicator.Services;

/// <summary>
/// Thin wrapper over the DOCUMENTED IVirtualDesktopManager COM interface.
/// Unlike the internal switching interface, this one (CLSID aa509086-…, IID a5cd92ff-…)
/// is part of the public Windows SDK and has been stable since Windows 10 1607.
///
/// We use it only to keep our overlay visible on whichever desktop the user is on:
/// when the overlay's desktop no longer matches the current desktop, we move it.
/// </summary>
public sealed class VirtualDesktopManagerCom : IDisposable
{
    [ComImport]
    [Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
    private class CVirtualDesktopManager { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
    private interface IVirtualDesktopManager
    {
        [PreserveSig] int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out int onCurrentDesktop);
        [PreserveSig] int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);
        [PreserveSig] int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }

    private IVirtualDesktopManager? _manager;

    public VirtualDesktopManagerCom()
    {
        try
        {
            _manager = (IVirtualDesktopManager)new CVirtualDesktopManager();
        }
        catch
        {
            _manager = null; // COM unavailable → callers degrade gracefully
        }
    }

    public bool IsAvailable => _manager != null;

    /// <summary>True if the window is currently on the active desktop.</summary>
    public bool IsWindowOnCurrentDesktop(IntPtr hwnd)
    {
        if (_manager == null || hwnd == IntPtr.Zero) return true;
        try
        {
            return _manager.IsWindowOnCurrentVirtualDesktop(hwnd, out int on) == 0 && on != 0;
        }
        catch { return true; }
    }

    /// <summary>Move the window onto the given desktop. Returns true on success.</summary>
    public bool MoveWindowToDesktop(IntPtr hwnd, Guid desktopId)
    {
        if (_manager == null || hwnd == IntPtr.Zero || desktopId == Guid.Empty) return false;
        try
        {
            return _manager.MoveWindowToDesktop(hwnd, ref desktopId) == 0;
        }
        catch { return false; }
    }

    public void Dispose()
    {
        if (_manager != null)
        {
            try { Marshal.FinalReleaseComObject(_manager); } catch { }
            _manager = null;
        }
    }
}
