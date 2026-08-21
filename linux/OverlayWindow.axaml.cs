using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using DeskCue.Linux.Services;

namespace DeskCue.Linux;

public partial class OverlayWindow : Window
{
    private AppConfig _config;
    private readonly DesktopService _desktops;
    private DispatcherTimer? _timer;
    private DesktopInfo? _last;
    private bool _clickThroughApplied;

    // Parameterless ctor for the XAML designer / loader.
    public OverlayWindow() : this(new AppConfig(), new DesktopService()) { }

    public OverlayWindow(AppConfig config, DesktopService desktops)
    {
        _config = config;
        _desktops = desktops;
        InitializeComponent();

        Opened += OnOpened;
        LayoutUpdated += (_, _) => Reposition();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        ApplyClickThrough();
        ApplyConfig();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(100, _config.PollIntervalMs)),
        };
        _timer.Tick += (_, _) => Update();
        _timer.Start();
        Update();
    }

    private void ApplyClickThrough()
    {
        if (_clickThroughApplied) return;
        var handle = TryGetPlatformHandle()?.Handle;
        if (handle is { } h && h != IntPtr.Zero)
        {
            ClickThrough.Apply(h);
            _clickThroughApplied = true;
        }
    }

    public void ApplyConfig(AppConfig? newConfig = null)
    {
        if (newConfig != null)
        {
            _config = newConfig;
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
        NumberText.IsVisible = _config.ShowNumber;

        _last = null; // force refresh
        Update();
    }

    private static IBrush MakeBrush(string value, Color fallback)
    {
        try { return new SolidColorBrush(Color.Parse(value)); }
        catch { return new SolidColorBrush(fallback); }
    }

    private void Update()
    {
        var info = _desktops.Read();
        if (info == null) return;

        if (_last == null || info != _last)
        {
            _last = info;

            NumberText.Text = _config.ShowCount ? $"{info.Index} / {info.Count}" : info.Index.ToString();

            bool showName = _config.ShowName && !string.IsNullOrWhiteSpace(info.Name);
            NameText.Text = showName ? info.Name : "";
            NameText.IsVisible = showName;

            Reposition();
        }
    }

    private void Reposition()
    {
        var screen = Screens.Primary ?? (Screens.All.Count > 0 ? Screens.All[0] : null);
        if (screen == null) return;

        double scaling = RenderScaling <= 0 ? 1.0 : RenderScaling;
        var wa = screen.WorkingArea; // physical pixels

        double w = Bounds.Width * scaling;
        double h = Bounds.Height * scaling;
        if (w <= 0 || h <= 0) return;

        double mx = _config.MarginX * scaling, my = _config.MarginY * scaling;
        double left, top;

        switch (_config.Position?.Trim().ToLowerInvariant())
        {
            case "topleft":      left = wa.X + mx;                       top = wa.Y + my; break;
            case "topright":     left = wa.Right - w - mx;               top = wa.Y + my; break;
            case "bottomleft":   left = wa.X + mx;                       top = wa.Bottom - h - my; break;
            case "bottomcenter": left = wa.X + (wa.Width - w) / 2 + mx;  top = wa.Bottom - h - my; break;
            case "bottomright":  left = wa.Right - w - mx;               top = wa.Bottom - h - my; break;
            case "center":       left = wa.X + (wa.Width - w) / 2 + mx;  top = wa.Y + (wa.Height - h) / 2 + my; break;
            default: /* topcenter */
                                 left = wa.X + (wa.Width - w) / 2 + mx;  top = wa.Y + my; break;
        }

        Position = new PixelPoint((int)left, (int)top);
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer?.Stop();
        base.OnClosed(e);
    }
}
