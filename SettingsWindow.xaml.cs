using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VirtualDesktopIndicator.Services;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using SystemColors = System.Windows.SystemColors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace VirtualDesktopIndicator;

public partial class SettingsWindow : Window
{
    private const int MaxDesktops = 9; // number keys 1..9

    private readonly AppConfig _config;
    private readonly Action _onSaved;

    private readonly Button[] _buttons = new Button[MaxDesktops + 1]; // 1-based
    private readonly string?[] _combos = new string?[MaxDesktops + 1]; // 1-based
    private int _capturing = 0; // desktop index currently capturing, 0 = none

    public SettingsWindow(AppConfig config, Action onSaved)
    {
        _config = config;
        _onSaved = onSaved;
        InitializeComponent();

        AutoStartCheck.IsChecked = StartupManager.IsEnabled();

        // Seed combos from existing config
        foreach (var b in _config.Hotkeys)
            if (b.Desktop >= 1 && b.Desktop <= MaxDesktops)
                _combos[b.Desktop] = b.Hotkey;

        BuildRows();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void BuildRows()
    {
        for (int d = 1; d <= MaxDesktops; d++)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = $"데스크톱 {d}",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13,
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var capture = new Button
            {
                Height = 30,
                Tag = d,
                Margin = new Thickness(0, 0, 6, 0),
                FontSize = 13,
            };
            capture.Click += OnCaptureClick;
            Grid.SetColumn(capture, 1);
            grid.Children.Add(capture);
            _buttons[d] = capture;

            var clear = new Button
            {
                Content = "지우기",
                Width = 62,
                Height = 30,
                Tag = d,
                FontSize = 12,
            };
            clear.Click += OnClearClick;
            Grid.SetColumn(clear, 2);
            grid.Children.Add(clear);

            RowsPanel.Children.Add(grid);
            RefreshButton(d);
        }
    }

    private void RefreshButton(int d)
    {
        var btn = _buttons[d];
        if (d == _capturing)
        {
            btn.Content = "입력 대기… (조합을 누르세요)";
            btn.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x73, 0xE8));
            btn.FontWeight = FontWeights.SemiBold;
        }
        else
        {
            btn.Content = string.IsNullOrEmpty(_combos[d]) ? "(없음)" : _combos[d];
            btn.Foreground = string.IsNullOrEmpty(_combos[d])
                ? new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99))
                : SystemColors.ControlTextBrush;
            btn.FontWeight = FontWeights.Normal;
        }
    }

    private void OnCaptureClick(object sender, RoutedEventArgs e)
    {
        int prev = _capturing;
        _capturing = (int)((Button)sender).Tag!;
        if (prev != 0 && prev != _capturing) RefreshButton(prev);
        HideStatus();
        RefreshButton(_capturing);
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        int d = (int)((Button)sender).Tag!;
        _combos[d] = null;
        if (_capturing == d) _capturing = 0;
        RefreshButton(d);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_capturing == 0) return;

        e.Handled = true; // don't let keys activate buttons while capturing
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape) { int d = _capturing; _capturing = 0; RefreshButton(d); return; }
        if (key is Key.Delete or Key.Back)
        {
            int d = _capturing; _combos[d] = null; _capturing = 0; RefreshButton(d); return;
        }

        if (IsModifierKey(key)) return; // wait for the real key

        var mods = Keyboard.Modifiers;
        // WPF's Keyboard.Modifiers never reports the Windows key (the OS swallows it),
        // so detect it directly from the physical key state.
        bool win = IsWinDown();

        if (mods == ModifierKeys.None && !win)
        {
            ShowStatus("최소 하나의 수식어(Ctrl · Alt · Shift · Win)를 포함해야 합니다.");
            return;
        }

        string? token = KeyToToken(key);
        if (token == null)
        {
            ShowStatus("지원하지 않는 키입니다. 숫자·문자·F1~F24·넘패드 숫자만 사용할 수 있습니다.");
            return;
        }

        var parts = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (win) parts.Add("Win");
        parts.Add(token);

        int cd = _capturing;
        _combos[cd] = string.Join("+", parts);
        _capturing = 0;
        HideStatus();
        RefreshButton(cd);
    }

    private static bool IsModifierKey(Key k) => k is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or
        Key.System or Key.None;

    private static string? KeyToToken(Key key)
    {
        if (key is >= Key.D0 and <= Key.D9) return ((int)(key - Key.D0)).ToString();
        // Numpad kept distinct ("Num1") so it registers on the numpad key, not the top row.
        if (key is >= Key.NumPad0 and <= Key.NumPad9) return "Num" + (int)(key - Key.NumPad0);
        if (key is >= Key.A and <= Key.Z) return key.ToString();
        if (key is >= Key.F1 and <= Key.F24) return "F" + (int)(key - Key.F1 + 1);
        return null;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private static bool IsWinDown() =>
        (GetAsyncKeyState(0x5B) & 0x8000) != 0 || // VK_LWIN
        (GetAsyncKeyState(0x5C) & 0x8000) != 0;   // VK_RWIN

    private void ShowStatus(string msg)
    {
        StatusText.Text = msg;
        StatusText.Visibility = Visibility.Visible;
    }

    private void HideStatus()
    {
        StatusText.Visibility = Visibility.Collapsed;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        // Warn on duplicate combos (only the first would register successfully).
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int d = 1; d <= MaxDesktops; d++)
        {
            var c = _combos[d];
            if (!string.IsNullOrEmpty(c) && !seen.Add(c))
            {
                ShowStatus($"단축키 '{c}' 가 중복되었습니다. 서로 다른 조합을 지정하세요.");
                return;
            }
        }

        _config.Hotkeys = new List<HotkeyBinding>();
        for (int d = 1; d <= MaxDesktops; d++)
            if (!string.IsNullOrEmpty(_combos[d]))
                _config.Hotkeys.Add(new HotkeyBinding { Hotkey = _combos[d]!, Desktop = d });

        StartupManager.SetEnabled(AutoStartCheck.IsChecked == true);

        _config.Save();
        _onSaved();
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
