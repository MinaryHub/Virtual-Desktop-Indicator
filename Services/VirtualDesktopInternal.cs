using System.Runtime.InteropServices;

namespace VirtualDesktopIndicator.Services;

/// <summary>
/// Direct desktop switching via the UNDOCUMENTED IVirtualDesktopManagerInternal COM interface.
/// This jumps straight to the target desktop (no stepping through intermediates), but the IID
/// and method layout are build-specific (defined here for Windows 11 24H2/25H2, build 26100+).
///
/// Everything is guarded: before calling SwitchDesktop we sanity-check the vtable binding
/// (GetCount must match the registry, FindDesktop must return a real object). On ANY failure
/// <see cref="TrySwitch"/> returns false and the caller falls back to keystroke stepping —
/// so a future Windows build that changes the interface degrades gracefully instead of crashing.
///
/// Must be called on an STA thread (the WPF UI thread).
/// </summary>
public static class VirtualDesktopInternal
{
    private static readonly Guid CLSID_ImmersiveShell = new("C2F03A33-21F5-47FA-B4BB-156362A2F239");
    private static readonly Guid CLSID_VirtualDesktopManagerInternal = new("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
    private static readonly Guid IID_IVirtualDesktopManagerInternal = new("53F5CA0B-158F-4124-900C-057158060B27");

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    private interface IServiceProvider10
    {
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object QueryService(ref Guid service, ref Guid riid);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("3F07F4BE-B107-441A-AF0F-39D82529072C")]
    private interface IVirtualDesktop
    {
        bool IsViewVisible(IntPtr view);
        Guid GetId();
    }

    // Methods are declared in EXACT vtable order up to FindDesktop; the ones we never call use
    // placeholder parameter types (each still occupies exactly one slot). Do not reorder.
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("53F5CA0B-158F-4124-900C-057158060B27")]
    private interface IVirtualDesktopManagerInternal
    {
        int GetCount();
        void MoveViewToDesktop(IntPtr view, IVirtualDesktop desktop);
        bool CanViewMoveDesktops(IntPtr view);
        IVirtualDesktop GetCurrentDesktop();
        void GetDesktops(out IntPtr desktops);
        [PreserveSig] int GetAdjacentDesktop(IVirtualDesktop from, int direction, out IVirtualDesktop desktop);
        void SwitchDesktop(IVirtualDesktop desktop);
        void SwitchDesktopAndMoveForegroundView(IVirtualDesktop desktop);
        IVirtualDesktop CreateDesktop();
        void MoveDesktop(IVirtualDesktop desktop, int nIndex);
        void RemoveDesktop(IVirtualDesktop desktop, IVirtualDesktop fallback);
        IVirtualDesktop FindDesktop(ref Guid desktopid);
    }

    /// <summary>
    /// Set only when the interface can NEVER work on this machine (the COM class itself doesn't
    /// exist). A transient runtime failure (RPC disconnect during an explorer restart, a momentary
    /// shell hiccup) must NOT latch this — otherwise one blip permanently degrades every future jump
    /// to keystroke stepping, which looks like "direct jumps worked at first, then started stepping
    /// through 2,3,4 forever." Those get one clean retry on the next call instead.
    /// </summary>
    private static bool _knownUnavailable;

    /// <summary>
    /// Switch straight to the desktop with the given id. Returns false if the internal interface
    /// is unavailable or looks mismatched (caller should fall back to stepping).
    /// </summary>
    public static bool TrySwitch(Guid targetDesktopId, int expectedCount)
    {
        if (_knownUnavailable) return false;

        try
        {
            var shellType = Type.GetTypeFromCLSID(CLSID_ImmersiveShell);
            if (shellType == null) { _knownUnavailable = true; return false; }

            var shell = Activator.CreateInstance(shellType);
            var provider = (IServiceProvider10)shell!;

            Guid clsid = CLSID_VirtualDesktopManagerInternal;
            Guid iid = IID_IVirtualDesktopManagerInternal;
            var vdm = (IVirtualDesktopManagerInternal)provider.QueryService(ref clsid, ref iid);

            // Sanity-check the vtable binding before we trust SwitchDesktop.
            int count = vdm.GetCount();
            if (count <= 0 || (expectedCount > 0 && count != expectedCount))
            {
                Log.Write($"VDInternal: count mismatch (got {count}, expected {expectedCount}); using fallback");
                return false;
            }

            var desktop = vdm.FindDesktop(ref targetDesktopId);
            if (desktop == null)
            {
                Log.Write("VDInternal: FindDesktop returned null; using fallback");
                return false;
            }

            vdm.SwitchDesktop(desktop);
            return true;
        }
        catch (Exception ex)
        {
            // Transient: log and fall back for THIS call only, but leave _knownUnavailable clear so
            // the next hotkey retries the direct jump. A one-off RPC/shell hiccup must not condemn
            // the whole session to stepping through intermediate desktops.
            Log.Write($"VDInternal: transient failure ({ex.GetType().Name}: {ex.Message}); using fallback this time");
            return false;
        }
    }
}
