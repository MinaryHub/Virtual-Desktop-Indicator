using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using VirtualDesktopIndicator.Services;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using ColorConverter = System.Windows.Media.ColorConverter;
using Forms = System.Windows.Forms;

namespace VirtualDesktopIndicator;

/// <summary>
/// A single indicator window. One instance is created per target monitor by
/// <see cref="OverlayController"/>; the window is a passive view — it owns no hotkeys,
/// timer or COM. The controller drives it via <see cref="ApplyConfig"/> and <see cref="Render"/>.
/// </summary>
public partial class OverlayWindow : Window
{
    private AppConfig _config;
    private readonly Forms.Screen _screen;
    private IntPtr _hwnd = IntPtr.Zero;

    public OverlayWindow(AppConfig config, Forms.Screen screen)
    {
        _config = config;
        _screen = screen;
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => { ApplyAppearance(); Reposition(); };
    }

    /// <summary>The window handle (valid once the window is shown).</summary>
    public IntPtr Handle => _hwnd;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        MakeClickThroughToolWindow(_hwnd);
        ApplyAppearance();
    }

    /// <summary>Swap in a new config and re-apply appearance + placement.</summary>
    public void ApplyConfig(AppConfig config)
    {
        _config = config;
        ApplyAppearance();
        Reposition();
    }

    /// <summary>Update the displayed desktop number/name, then re-place the window.</summary>
    public void Render(DesktopInfo info)
    {
        NumberText.Text = _config.ShowCount
            ? $"{info.Index} / {info.Count}"
            : info.Index.ToString();

        bool showName = _config.ShowName && !string.IsNullOrWhiteSpace(info.Name);
        NameText.Text = showName ? info.Name : "";
        NameText.Visibility = showName ? Visibility.Visible : Visibility.Collapsed;

        UpdateLayout();
        Reposition();
    }

    // -------------------------------------------------------------------------
    private void ApplyAppearance()
    {
        Root.Background = MakeBrush(_config.Background, Colors.Black);
        Root.Opacity = Math.Clamp(_config.Opacity, 0.05, 1.0);
        Root.CornerRadius = new CornerRadius(_config.CornerRadius);

        var fg = MakeBrush(_config.Foreground, Colors.White);
        NumberText.Foreground = fg;
        NameText.Foreground = fg;
        NumberText.FontSize = _config.FontSize;
        NameText.FontSize = Math.Max(10, _config.FontSize * 0.58);

        NumberText.Visibility = _config.ShowNumber ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Brush MakeBrush(string value, Color fallback)
    {
        try
        {
            if (ColorConverter.ConvertFromString(value) is Color c)
                return new SolidColorBrush(c);
        }
        catch { }
        return new SolidColorBrush(fallback);
    }

    /// <summary>
    /// Place the window within this instance's monitor. Positioning is done in physical pixels via
    /// SetWindowPos (the app is PerMonitorV2), scaling the WPF DIP size and the configured margins by
    /// the target monitor's DPI so the same <see cref="AppConfig.Position"/> lands identically on every
    /// display regardless of its scale factor.
    /// </summary>
    private void Reposition()
    {
        if (_hwnd == IntPtr.Zero) return;
        double dw = ActualWidth, dh = ActualHeight; // DIPs (DPI-independent)
        if (dw <= 0 || dh <= 0) return;

        double scale = GetScaleForScreen(_screen);
        var wa = _screen.WorkingArea; // physical pixels

        double w = dw * scale, h = dh * scale;
        double mx = _config.MarginX * scale, my = _config.MarginY * scale;
        double left, top;

        switch (_config.Position?.Trim().ToLowerInvariant())
        {
            case "topleft":      left = wa.Left + mx;                        top = wa.Top + my; break;
            case "topright":     left = wa.Right - w - mx;                   top = wa.Top + my; break;
            case "bottomleft":   left = wa.Left + mx;                        top = wa.Bottom - h - my; break;
            case "bottomcenter": left = wa.Left + (wa.Width - w) / 2 + mx;   top = wa.Bottom - h - my; break;
            case "bottomright":  left = wa.Right - w - mx;                   top = wa.Bottom - h - my; break;
            case "center":       left = wa.Left + (wa.Width - w) / 2 + mx;   top = wa.Top + (wa.Height - h) / 2 + my; break;
            default: /* topcenter */
                                 left = wa.Left + (wa.Width - w) / 2 + mx;   top = wa.Top + my; break;
        }

        SetWindowPos(_hwnd, IntPtr.Zero, (int)Math.Round(left), (int)Math.Round(top), 0, 0,
                     SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <summary>Effective scale factor (DPI/96) of the given monitor.</summary>
    private static double GetScaleForScreen(Forms.Screen screen)
    {
        try
        {
            var b = screen.Bounds;
            var center = new POINT { X = b.Left + b.Width / 2, Y = b.Top + b.Height / 2 };
            IntPtr mon = MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST);
            if (GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0)
                return dpiX / 96.0;
        }
        catch { /* fall back to no scaling */ }
        return 1.0;
    }

    // --- Click-through / no-activate / tool window ---------------------------
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW  = 0x00000080;
    private const int WS_EX_NOACTIVATE  = 0x08000000;

    private static void MakeClickThroughToolWindow(IntPtr hwnd)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        ex |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLong(hwnd, GWL_EXSTYLE, ex);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    // --- Placement / DPI -----------------------------------------------------
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
}
