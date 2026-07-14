using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using VirtualDesktopIndicator.Linux.Services;
using HotKeyManager = VirtualDesktopIndicator.Linux.Services.HotKeyManager;

namespace VirtualDesktopIndicator.Linux;

public partial class App : Application
{
    private AppConfig _config = new();
    private DesktopService? _desktops;
    private OverlayWindow? _overlay;
    private HotKeyManager? _hotkeys;
    private TrayIcon? _tray;
    private NativeMenuItem? _autoStartItem;
    private SettingsWindow? _settings;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Log.Write("=== app startup ===");
        _config = AppConfig.Load();

        _desktops = new DesktopService();
        _overlay = new OverlayWindow(_config, _desktops);
        _overlay.Show();

        _hotkeys = new HotKeyManager();
        _hotkeys.DesktopRequested += OnDesktopRequested;
        _hotkeys.Register(_config.Hotkeys);

        SetupTray();

        // Background update check; only speak up if a newer version exists.
        _ = CheckForUpdatesAsync(silentIfNoUpdate: true);

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopRequested(int desktop)
        => Dispatcher.UIThread.Post(() => _desktops?.SwitchTo(desktop));

    private async Task CheckForUpdatesAsync(bool silentIfNoUpdate)
    {
        var result = await UpdateService.CheckAsync();
        await UpdateFlow.HandleAsync(result, silentIfNoUpdate);
    }

    private void SetupTray()
    {
        var menu = new NativeMenu();

        menu.Add(new NativeMenuItem($"Virtual Desktop Indicator {AppVersion.Display}") { IsEnabled = false });
        menu.Add(new NativeMenuItemSeparator());

        var settingsItem = new NativeMenuItem("Settings...");
        settingsItem.Click += (_, _) => OpenSettings();
        menu.Add(settingsItem);

        var updateItem = new NativeMenuItem("Check for updates...");
        updateItem.Click += (_, _) => _ = CheckForUpdatesAsync(silentIfNoUpdate: false);
        menu.Add(updateItem);

        _autoStartItem = new NativeMenuItem("Run at login")
        {
            ToggleType = NativeMenuItemToggleType.CheckBox,
            IsChecked = StartupManager.IsEnabled(),
        };
        _autoStartItem.Click += (_, _) => ToggleAutoStart();
        menu.Add(_autoStartItem);

        menu.Add(new NativeMenuItemSeparator());

        var openCfg = new NativeMenuItem("Open config file");
        openCfg.Click += (_, _) => OpenConfig();
        menu.Add(openCfg);

        var reload = new NativeMenuItem("Reload config");
        reload.Click += (_, _) => ReloadConfig();
        menu.Add(reload);

        var posMenu = new NativeMenuItem("Position") { Menu = new NativeMenu() };
        foreach (var pos in new[] { "TopLeft", "TopCenter", "TopRight",
                                    "BottomLeft", "BottomCenter", "BottomRight", "Center" })
        {
            var item = pos;
            var mi = new NativeMenuItem(item);
            mi.Click += (_, _) => SetPosition(item);
            posMenu.Menu!.Add(mi);
        }
        menu.Add(posMenu);

        menu.Add(new NativeMenuItemSeparator());

        var exit = new NativeMenuItem("Exit");
        exit.Click += (_, _) => ExitApp();
        menu.Add(exit);

        _tray = new TrayIcon
        {
            Icon = IconFactory.BuildTrayIcon(),
            ToolTipText = $"Virtual Desktop Indicator {AppVersion.Display}",
            IsVisible = true,
            Menu = menu,
        };
        _tray.Clicked += (_, _) => OpenSettings();

        TrayIcon.SetIcons(this, new TrayIcons { _tray });
    }

    private void OpenSettings()
    {
        if (_settings != null)
        {
            _settings.Activate();
            return;
        }
        _settings = new SettingsWindow(_config, OnSettingsSaved);
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
        _settings.Activate();
    }

    private void OnSettingsSaved()
    {
        _overlay?.ApplyConfig(_config);
        _hotkeys?.Register(_config.Hotkeys);
    }

    private void ToggleAutoStart()
    {
        bool desired = _autoStartItem?.IsChecked ?? false;
        if (!StartupManager.SetEnabled(desired) && _autoStartItem != null)
            _autoStartItem.IsChecked = StartupManager.IsEnabled(); // revert on failure
    }

    private void OpenConfig()
    {
        _config.Save();
        try { Process.Start(new ProcessStartInfo("xdg-open", AppConfig.ConfigPath) { UseShellExecute = false }); }
        catch (Exception ex) { Log.Write($"open config failed: {ex.Message}"); }
    }

    private void ReloadConfig()
    {
        _config = AppConfig.Load();
        _overlay?.ApplyConfig(_config);
        _hotkeys?.Register(_config.Hotkeys);
    }

    private void SetPosition(string position)
    {
        _config.Position = position;
        _config.Save();
        _overlay?.ApplyConfig(_config);
    }

    private void ExitApp()
    {
        _tray?.Dispose();
        _hotkeys?.Dispose();
        _overlay?.Close();
        _desktops?.Dispose();
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }
}
