using System.Runtime.InteropServices;

namespace DeskCue.Linux.Services;

/// <summary>
/// Makes an X11 window click-through by setting an empty input shape via the
/// X Shape extension (libXext). Best-effort: if the extension is unavailable the
/// overlay still shows, it just won't pass clicks through.
/// </summary>
public static class ClickThrough
{
    private const int ShapeSet = 0;
    private const int ShapeInput = 2;
    private const int Unsorted = 0;

    [DllImport("libXext.so.6")]
    private static extern void XShapeCombineRectangles(
        IntPtr display, IntPtr window, int destKind, int xOff, int yOff,
        IntPtr rects, int nRects, int op, int ordering);

    public static void Apply(IntPtr x11Window)
    {
        if (x11Window == IntPtr.Zero) return;
        try
        {
            IntPtr display = X11.XOpenDisplay(null);
            if (display == IntPtr.Zero) return;
            try
            {
                // An empty input region (0 rectangles) => the window receives no
                // pointer input, so clicks fall through to whatever is beneath it.
                XShapeCombineRectangles(display, x11Window, ShapeInput, 0, 0,
                    IntPtr.Zero, 0, ShapeSet, Unsorted);
                X11.XFlush(display);
                Log.Write("click-through input shape applied");
            }
            finally { X11.XCloseDisplay(display); }
        }
        catch (Exception ex) { Log.Write($"click-through failed: {ex.Message}"); }
    }
}
