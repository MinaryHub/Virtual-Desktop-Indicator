using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using VirtualDesktopIndicator.Services;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace VirtualDesktopIndicator;

public partial class OverlayWindow : Window
{
    private AppConfig _config;
    private readonly VirtualDesktopManagerCom _com = new();
    private HotKeyManager? _hotkeys;
    private DispatcherTimer? _timer;
    private IntPtr _hwnd = IntPtr.Zero;
    private DesktopInfo? _last;
    private bool _pinned;

    public OverlayWindow(AppConfig config)
    {
        _config = config;
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => Reposition();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        MakeClickThroughToolWindow(_hwnd);

        // Pin the app to every desktop so we never have to move the overlay on a switch.
        // Moving a window across desktops forces the shell to re-enumerate the taskbar
        // and flickers other apps' buttons; a pinned app is exempt from that.
        _pinned = VirtualDesktopPinner.Pin();

        _hotkeys = new HotKeyManager(_hwnd);
        _hotkeys.DesktopRequested += DesktopSwitcher.SwitchTo;
        _hotkeys.Register(_config.Hotkeys);

        ApplyConfig();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(100, _config.PollIntervalMs))
        };
        _timer.Tick += (_, _) => Update();
        _timer.Start();
        Update();
    }

    /// <summary>List of hotkeys that could not be registered (shown once at startup).</summary>
    public IReadOnlyList<string> FailedHotkeys => _hotkeys?.FailedRegistrations ?? [];

    // -------------------------------------------------------------------------
    public void ApplyConfig(AppConfig? newConfig = null)
    {
        if (newConfig != null)
        {
            _config = newConfig;
            _hotkeys?.Register(_config.Hotkeys);
            if (_timer != null)
                _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(100, _config.PollIntervalMs));
        }

        Root.Background = MakeBrush(_config.Background, Colors.Black);
        Root.Opacity = Math.Clamp(_config.Opacity, 0.05, 1.0);
        Root.CornerRadius = new CornerRadius(_config.CornerRadius);

        var fg = MakeBrush(_config.Foreground, Colors.White);
        NumberText.Foreground = fg;
        NameText.Foreground = fg;
        NumberText.FontSize = _config.FontSize;
        NameText.FontSize = Math.Max(10, _config.FontSize * 0.58);

        NumberText.Visibility = _config.ShowNumber ? Visibility.Visible : Visibility.Collapsed;

        _last = null;   // force refresh
        Update();
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

    // -------------------------------------------------------------------------
    private void Update()
    {
        var info = VirtualDesktopRegistry.Read();
        if (info == null) return;

        if (_last == null || info != _last)
        {
            _last = info;

            NumberText.Text = _config.ShowCount
                ? $"{info.Index} / {info.Count}"
                : info.Index.ToString();

            bool showName = _config.ShowName && !string.IsNullOrWhiteSpace(info.Name);
            NameText.Text = showName ? info.Name : "";
            NameText.Visibility = showName ? Visibility.Visible : Visibility.Collapsed;

            UpdateLayout();
            Reposition();
        }

        // Keep the overlay on whatever desktop the user is currently viewing. When the window
        // is pinned to all desktops this is unnecessary — and moving it each switch is exactly
        // what makes the taskbar buttons flicker — so only fall back to moving if pinning failed.
        if (!_pinned && _com.IsAvailable && _hwnd != IntPtr.Zero && !_com.IsWindowOnCurrentDesktop(_hwnd))
            _com.MoveWindowToDesktop(_hwnd, info.CurrentId);
    }

    private void Reposition()
    {
        var wa = SystemParameters.WorkArea;
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double mx = _config.MarginX, my = _config.MarginY;
        double left, top;

        switch (_config.Position?.Trim().ToLowerInvariant())
        {
            case "topleft":      left = wa.Left + mx;                 top = wa.Top + my; break;
            case "topright":     left = wa.Right - w - mx;            top = wa.Top + my; break;
            case "bottomleft":   left = wa.Left + mx;                 top = wa.Bottom - h - my; break;
            case "bottomcenter": left = wa.Left + (wa.Width - w) / 2 + mx; top = wa.Bottom - h - my; break;
            case "bottomright":  left = wa.Right - w - mx;            top = wa.Bottom - h - my; break;
            case "center":       left = wa.Left + (wa.Width - w) / 2 + mx; top = wa.Top + (wa.Height - h) / 2 + my; break;
            default: /* topcenter */
                                 left = wa.Left + (wa.Width - w) / 2 + mx; top = wa.Top + my; break;
        }

        Left = left;
        Top = top;
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

    protected override void OnClosed(EventArgs e)
    {
        _timer?.Stop();
        _hotkeys?.Dispose();
        _com.Dispose();
        base.OnClosed(e);
    }
}
