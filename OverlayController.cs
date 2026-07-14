using System.Windows;
using System.Windows.Threading;
using VirtualDesktopIndicator.Services;
using Forms = System.Windows.Forms;

namespace VirtualDesktopIndicator;

/// <summary>
/// Owns the app's single set of shared machinery — global hotkeys, the poll timer, the
/// virtual-desktop COM wrapper and the all-desktops pin — and drives one <see cref="OverlayWindow"/>
/// per monitor. When <see cref="AppConfig.ShowOnAllMonitors"/> is true an overlay is shown on every
/// display at the same position; otherwise only on the primary monitor. The window set is rebuilt
/// when the monitor option changes or the physical display layout changes.
/// </summary>
public sealed class OverlayController : IDisposable
{
    private AppConfig _config;
    private readonly List<OverlayWindow> _overlays = new();
    private readonly VirtualDesktopManagerCom _com = new();
    private HotKeyManager? _hotkeys;
    private DispatcherTimer? _timer;
    private DesktopInfo? _last;
    private bool _pinned;

    public OverlayController(AppConfig config) => _config = config;

    /// <summary>Hotkeys Windows refused to register (surfaced once by the caller).</summary>
    public IReadOnlyList<string> FailedHotkeys => _hotkeys?.FailedRegistrations ?? [];

    public void Start()
    {
        DesktopSwitcher.SmoothSwitch = _config.SmoothSwitch;

        CreateWindows();
        HookHotkeys();

        // Pin the app to every desktop so the overlays never have to be moved on a switch
        // (moving a window across desktops flickers other apps' taskbar buttons).
        _pinned = VirtualDesktopPinner.Pin();

        _timer = new DispatcherTimer { Interval = PollInterval() };
        _timer.Tick += (_, _) => Update();
        _timer.Start();
        Update();

        // Track monitor plug/unplug and resolution/scale changes.
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public void ApplyConfig(AppConfig config)
    {
        bool monitorModeChanged = config.ShowOnAllMonitors != _config.ShowOnAllMonitors;
        _config = config;

        DesktopSwitcher.SmoothSwitch = _config.SmoothSwitch;
        if (_timer != null) _timer.Interval = PollInterval();
        _hotkeys?.Register(_config.Hotkeys);

        if (monitorModeChanged)
        {
            RebuildWindows();
            return; // RebuildWindows already re-applies config + renders
        }

        foreach (var w in _overlays) w.ApplyConfig(_config);
        _last = null; // force a re-render at the new appearance
        Update();
    }

    private TimeSpan PollInterval() => TimeSpan.FromMilliseconds(Math.Max(100, _config.PollIntervalMs));

    private void CreateWindows()
    {
        var screens = _config.ShowOnAllMonitors
            ? Forms.Screen.AllScreens
            : new[] { Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0] };

        foreach (var screen in screens)
        {
            var w = new OverlayWindow(_config, screen);
            w.Show(); // realizes the HWND so hotkeys can hook it and it can be placed
            _overlays.Add(w);
        }
    }

    /// <summary>(Re)bind global hotkeys onto the first overlay's message loop — one set serves all monitors.</summary>
    private void HookHotkeys()
    {
        _hotkeys?.Dispose();
        _hotkeys = new HotKeyManager(_overlays[0].Handle);
        _hotkeys.DesktopRequested += DesktopSwitcher.SwitchTo;
        _hotkeys.Register(_config.Hotkeys);
    }

    private void RebuildWindows()
    {
        _hotkeys?.Dispose();
        _hotkeys = null;

        foreach (var w in _overlays) w.Close();
        _overlays.Clear();

        CreateWindows();
        HookHotkeys();

        _last = null;
        Update();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // SystemEvents may fire off the UI thread; marshal window work onto the Dispatcher.
        System.Windows.Application.Current?.Dispatcher.Invoke(RebuildWindows);
    }

    private void Update()
    {
        var info = VirtualDesktopRegistry.Read();
        if (info == null) return;

        if (_last == null || info != _last)
        {
            _last = info;
            foreach (var w in _overlays) w.Render(info);
        }

        // Only needed if pinning failed on this build: keep each overlay on the current desktop.
        if (!_pinned && _com.IsAvailable)
        {
            foreach (var w in _overlays)
                if (w.Handle != IntPtr.Zero && !_com.IsWindowOnCurrentDesktop(w.Handle))
                    _com.MoveWindowToDesktop(w.Handle, info.CurrentId);
        }
    }

    public void Dispose()
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _timer?.Stop();
        _hotkeys?.Dispose();
        _com.Dispose();
        foreach (var w in _overlays) w.Close();
        _overlays.Clear();
    }
}
