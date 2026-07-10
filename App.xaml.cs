using System.Diagnostics;
using System.Drawing;
using System.Windows;
using VirtualDesktopIndicator.Services;
using Forms = System.Windows.Forms;

namespace VirtualDesktopIndicator;

public partial class App : System.Windows.Application
{
    private OverlayWindow? _overlay;
    private Forms.NotifyIcon? _tray;
    private Forms.ToolStripMenuItem? _autoStartItem;
    private SettingsWindow? _settings;
    private AppConfig _config = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Write("=== app startup ===");
        _config = AppConfig.Load();

        _overlay = new OverlayWindow(_config);
        _overlay.Show();

        SetupTray();

        // Surface any hotkeys that Windows refused to register (e.g. taken by another app).
        var failed = _overlay.FailedHotkeys;
        if (failed.Count > 0)
        {
            _tray!.BalloonTipTitle = "일부 단축키를 등록하지 못했습니다";
            _tray.BalloonTipText = string.Join("\n", failed);
            _tray.ShowBalloonTip(5000);
        }
    }

    private void SetupTray()
    {
        var menu = new Forms.ContextMenuStrip();

        menu.Items.Add(new Forms.ToolStripMenuItem("가상 데스크톱 인디케이터") { Enabled = false });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("설정...", null, (_, _) => OpenSettings());

        _autoStartItem = new Forms.ToolStripMenuItem("Windows 시작 시 자동 실행")
        {
            CheckOnClick = true,
        };
        _autoStartItem.Click += (_, _) => ToggleAutoStart();
        menu.Items.Add(_autoStartItem);

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("설정 파일 열기", null, (_, _) => OpenConfig());
        menu.Items.Add("설정 다시 읽기", null, (_, _) => ReloadConfig());

        var posMenu = new Forms.ToolStripMenuItem("위치");
        foreach (var pos in new[] { "TopLeft", "TopCenter", "TopRight",
                                    "BottomLeft", "BottomCenter", "BottomRight", "Center" })
        {
            var item = pos;
            posMenu.DropDownItems.Add(item, null, (_, _) => SetPosition(item));
        }
        menu.Items.Add(posMenu);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => ExitApp());

        // Keep the auto-start checkmark in sync with the registry each time the menu opens.
        menu.Opening += (_, _) => _autoStartItem.Checked = StartupManager.IsEnabled();

        _tray = new Forms.NotifyIcon
        {
            Icon = BuildIcon(),
            Visible = true,
            Text = "가상 데스크톱 인디케이터",
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => OpenSettings();
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
        // Config object was mutated & saved by the settings window; re-apply it live.
        _overlay?.ApplyConfig(_config);

        var failed = _overlay?.FailedHotkeys ?? [];
        if (failed.Count > 0 && _tray != null)
        {
            _tray.BalloonTipTitle = "일부 단축키를 등록하지 못했습니다";
            _tray.BalloonTipText = string.Join("\n", failed);
            _tray.ShowBalloonTip(5000);
        }
    }

    private void ToggleAutoStart()
    {
        bool desired = _autoStartItem?.Checked ?? false;
        if (!StartupManager.SetEnabled(desired) && _autoStartItem != null)
        {
            // Revert the checkmark if the registry write failed.
            _autoStartItem.Checked = StartupManager.IsEnabled();
        }
    }

    private void OpenConfig()
    {
        _config.Save(); // ensure the file exists before opening
        try
        {
            Process.Start(new ProcessStartInfo(AppConfig.ConfigPath) { UseShellExecute = true });
        }
        catch
        {
            Process.Start(new ProcessStartInfo(AppConfig.ConfigDirectory) { UseShellExecute = true });
        }
    }

    private void ReloadConfig()
    {
        _config = AppConfig.Load();
        _overlay?.ApplyConfig(_config);
    }

    private void SetPosition(string position)
    {
        _config.Position = position;
        _config.Save();
        _overlay?.ApplyConfig(_config);
    }

    private void ExitApp()
    {
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }
        _overlay?.Close();
        Shutdown();
    }

    /// <summary>Draws a tiny "VD" tile icon at runtime so we don't need an .ico asset.</summary>
    private static Icon BuildIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var path = RoundedRect(new Rectangle(1, 1, 29, 29), 6);
            using var bg = new SolidBrush(Color.FromArgb(230, 30, 30, 30));
            g.FillPath(bg, path);
            using var pen = new Pen(Color.FromArgb(220, 90, 160, 250), 2);
            g.DrawPath(pen, path);

            using var font = new Font("Segoe UI", 12, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
            using var fg = new SolidBrush(Color.White);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("VD", font, fg, new RectangleF(0, 0, 32, 32), sf);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
