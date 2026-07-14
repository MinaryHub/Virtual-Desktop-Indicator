using System.Runtime.InteropServices;

namespace VirtualDesktopIndicator.Linux.Services;

/// <summary>
/// Minimal P/Invoke bindings to libX11 used for EWMH desktop queries/switching
/// and global hotkeys. XIDs (Window, Atom) are unsigned long in C; on the
/// linux-x64 target we build for, that is 64-bit, so we marshal them as IntPtr.
/// </summary>
internal static class X11
{
    private const string Lib = "libX11.so.6";

    // --- event / masks / modes ------------------------------------------------
    public const int KeyPress = 2;
    public const int ClientMessage = 33;

    public const int KeyPressMask = 1 << 0;
    public const long SubstructureNotifyMask = 1L << 19;
    public const long SubstructureRedirectMask = 1L << 20;

    public const int GrabModeAsync = 1;

    // Keyboard modifier masks.
    public const uint ShiftMask = 1 << 0;
    public const uint LockMask = 1 << 1;   // CapsLock
    public const uint ControlMask = 1 << 2;
    public const uint Mod1Mask = 1 << 3;   // Alt
    public const uint Mod2Mask = 1 << 4;   // NumLock
    public const uint Mod4Mask = 1 << 6;   // Super / "Windows" key

    // --- lifecycle ------------------------------------------------------------
    [DllImport(Lib)] public static extern int XInitThreads();
    [DllImport(Lib)] public static extern IntPtr XOpenDisplay(string? display);
    [DllImport(Lib)] public static extern int XCloseDisplay(IntPtr display);
    [DllImport(Lib)] public static extern IntPtr XDefaultRootWindow(IntPtr display);
    [DllImport(Lib)] public static extern int XFlush(IntPtr display);
    [DllImport(Lib)] public static extern int XSync(IntPtr display, bool discard);
    [DllImport(Lib)] public static extern int XFree(IntPtr data);

    // --- atoms / properties ---------------------------------------------------
    [DllImport(Lib, CharSet = CharSet.Ansi)]
    public static extern IntPtr XInternAtom(IntPtr display, string atomName, bool onlyIfExists);

    [DllImport(Lib)]
    public static extern int XGetWindowProperty(
        IntPtr display, IntPtr window, IntPtr property,
        IntPtr longOffset, IntPtr longLength, bool delete, IntPtr reqType,
        out IntPtr actualType, out int actualFormat,
        out IntPtr nItems, out IntPtr bytesAfter, out IntPtr prop);

    // --- events / send --------------------------------------------------------
    [DllImport(Lib)]
    public static extern int XSendEvent(IntPtr display, IntPtr window, bool propagate,
        IntPtr eventMask, ref XEvent eventSend);

    [DllImport(Lib)] public static extern int XNextEvent(IntPtr display, out XEvent ev);
    [DllImport(Lib)] public static extern int XPending(IntPtr display);
    [DllImport(Lib)] public static extern int XSelectInput(IntPtr display, IntPtr window, IntPtr mask);

    // --- hotkeys --------------------------------------------------------------
    [DllImport(Lib)] public static extern byte XKeysymToKeycode(IntPtr display, IntPtr keysym);

    [DllImport(Lib)]
    public static extern int XGrabKey(IntPtr display, int keycode, uint modifiers,
        IntPtr grabWindow, bool ownerEvents, int pointerMode, int keyboardMode);

    [DllImport(Lib)]
    public static extern int XUngrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grabWindow);

    // --- error handling -------------------------------------------------------
    // The default Xlib error handler calls exit() on protocol errors (e.g. a
    // BadAccess when a hotkey is already grabbed by another client), which would
    // kill the app. We install a no-op handler to keep running.
    public delegate int XErrorHandler(IntPtr display, ref XErrorEvent ev);

    [DllImport(Lib)]
    public static extern XErrorHandler XSetErrorHandler(XErrorHandler handler);
}

[StructLayout(LayoutKind.Sequential)]
internal struct XErrorEvent
{
    public int type;
    public IntPtr display;
    public IntPtr resourceid;
    public IntPtr serial;
    public byte error_code;
    public byte request_code;
    public byte minor_code;
}

// Xlib's XEvent is a union; 192 bytes is its size on 64-bit. We overlay only the
// members we read (type, key, client message) at offset 0.
[StructLayout(LayoutKind.Explicit, Size = 192)]
internal struct XEvent
{
    [FieldOffset(0)] public int type;
    [FieldOffset(0)] public XKeyEvent xkey;
    [FieldOffset(0)] public XClientMessageEvent xclient;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XKeyEvent
{
    public int type;
    public IntPtr serial;
    public int send_event;
    public IntPtr display;
    public IntPtr window;
    public IntPtr root;
    public IntPtr subwindow;
    public IntPtr time;
    public int x, y;
    public int x_root, y_root;
    public uint state;
    public uint keycode;
    public int same_screen;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XClientMessageEvent
{
    public int type;
    public IntPtr serial;
    public int send_event;
    public IntPtr display;
    public IntPtr window;
    public IntPtr message_type;
    public int format;
    public IntPtr data0;
    public IntPtr data1;
    public IntPtr data2;
    public IntPtr data3;
    public IntPtr data4;
}
