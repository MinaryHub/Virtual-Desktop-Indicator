using System.Runtime.InteropServices;
using System.Text;

namespace DeskCue.Linux.Services;

/// <summary>Snapshot of the current EWMH desktop state.</summary>
public sealed record DesktopInfo(int Index, int Count, string Name)
{
    // 1-based index for display, matching the Windows app.
}

/// <summary>
/// Reads and switches the current virtual desktop via the EWMH standard
/// (_NET_CURRENT_DESKTOP / _NET_NUMBER_OF_DESKTOPS / _NET_DESKTOP_NAMES) on X11.
/// This is the documented interface implemented by GNOME/Xorg, KDE/X11, XFCE,
/// and most window managers, so it does not depend on any private API.
///
/// Owns its own X11 Display connection and is intended to be used from a single
/// thread (the UI thread).
/// </summary>
public sealed class DesktopService : IDisposable
{
    private const int Success = 0;

    private readonly IntPtr _display;
    private readonly IntPtr _root;
    private readonly IntPtr _aCurrent;
    private readonly IntPtr _aCount;
    private readonly IntPtr _aNames;
    private readonly IntPtr _aUtf8;

    public bool IsAvailable => _display != IntPtr.Zero;

    public DesktopService()
    {
        _display = X11.XOpenDisplay(null);
        if (_display == IntPtr.Zero)
        {
            Log.Write("X11: XOpenDisplay failed (no DISPLAY?)");
            return;
        }
        _root = X11.XDefaultRootWindow(_display);
        _aCurrent = X11.XInternAtom(_display, "_NET_CURRENT_DESKTOP", false);
        _aCount = X11.XInternAtom(_display, "_NET_NUMBER_OF_DESKTOPS", false);
        _aNames = X11.XInternAtom(_display, "_NET_DESKTOP_NAMES", false);
        _aUtf8 = X11.XInternAtom(_display, "UTF8_STRING", false);
    }

    /// <summary>Reads the current desktop index/count/name, or null if unavailable.</summary>
    public DesktopInfo? Read()
    {
        if (!IsAvailable) return null;

        long? current = ReadCardinal(_aCurrent);
        long? count = ReadCardinal(_aCount);
        if (current == null || count == null) return null;

        int idx0 = (int)current.Value;
        var names = ReadNames();
        string name = idx0 >= 0 && idx0 < names.Length ? names[idx0] : "";

        return new DesktopInfo(idx0 + 1, (int)count.Value, name);
    }

    /// <summary>Switches to the given 1-based desktop index.</summary>
    public void SwitchTo(int index1Based)
    {
        if (!IsAvailable || index1Based < 1) return;

        var cme = new XClientMessageEvent
        {
            type = X11.ClientMessage,
            send_event = 1,
            display = _display,
            window = _root,
            message_type = _aCurrent,
            format = 32,
            data0 = (IntPtr)(index1Based - 1), // EWMH desktops are 0-based
            data1 = IntPtr.Zero,               // CurrentTime
        };
        var ev = new XEvent { xclient = cme };

        long mask = X11.SubstructureNotifyMask | X11.SubstructureRedirectMask;
        X11.XSendEvent(_display, _root, false, (IntPtr)mask, ref ev);
        X11.XFlush(_display);
        Log.Write($"EWMH switch -> desktop {index1Based}");
    }

    // --- helpers -------------------------------------------------------------

    private long? ReadCardinal(IntPtr atom)
    {
        if (atom == IntPtr.Zero) return null;

        int status = X11.XGetWindowProperty(
            _display, _root, atom, IntPtr.Zero, (IntPtr)1, false, IntPtr.Zero,
            out _, out int format, out IntPtr nItems, out _, out IntPtr prop);

        if (status != Success || prop == IntPtr.Zero) return null;
        try
        {
            if ((long)nItems < 1 || format != 32) return null;
            // format 32 properties are returned as C long (8 bytes on 64-bit).
            return Marshal.ReadInt64(prop);
        }
        finally { X11.XFree(prop); }
    }

    private string[] ReadNames()
    {
        if (_aNames == IntPtr.Zero) return [];

        int status = X11.XGetWindowProperty(
            _display, _root, _aNames, IntPtr.Zero, (IntPtr)1024, false, _aUtf8,
            out _, out _, out IntPtr nItems, out _, out IntPtr prop);

        if (status != Success || prop == IntPtr.Zero) return [];
        try
        {
            int len = (int)(long)nItems;
            if (len <= 0) return [];
            var bytes = new byte[len];
            Marshal.Copy(prop, bytes, 0, len);
            // NUL-separated UTF-8 strings; a trailing NUL yields an empty tail we drop.
            var s = Encoding.UTF8.GetString(bytes);
            var parts = s.Split('\0');
            if (parts.Length > 0 && parts[^1].Length == 0)
                parts = parts[..^1];
            return parts;
        }
        finally { X11.XFree(prop); }
    }

    public void Dispose()
    {
        if (_display != IntPtr.Zero) X11.XCloseDisplay(_display);
    }
}
