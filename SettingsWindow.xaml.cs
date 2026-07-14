using System.Windows;
using System.Windows.Controls;
using VirtualDesktopIndicator.Services;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Orientation = System.Windows.Controls.Orientation;

namespace VirtualDesktopIndicator;

public partial class SettingsWindow : Window
{
    private const int MaxDesktops = 9; // number keys 1..9

    private readonly AppConfig _config;
    private readonly Action _onSaved;

    // Per-desktop controls (1-based; index 0 unused).
    private readonly CheckBox[] _win = new CheckBox[MaxDesktops + 1];
    private readonly CheckBox[] _ctrl = new CheckBox[MaxDesktops + 1];
    private readonly CheckBox[] _shift = new CheckBox[MaxDesktops + 1];
    private readonly CheckBox[] _alt = new CheckBox[MaxDesktops + 1];
    private readonly ComboBox[] _key = new ComboBox[MaxDesktops + 1];

    private const string NoKey = "(none)";

    public SettingsWindow(AppConfig config, Action onSaved)
    {
        _config = config;
        _onSaved = onSaved;
        InitializeComponent();

        VersionText.Text = $"Version {AppVersion.Display}";
        AutoStartCheck.IsChecked = StartupManager.IsEnabled();

        MultiMonitorCheck.IsChecked = _config.ShowOnAllMonitors;
        OpacitySlider.Value = Math.Round(Math.Clamp(_config.Opacity, 0.05, 1.0) * 100);

        BuildRows();
        SeedFromConfig();
    }

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValueText != null)
            OpacityValueText.Text = $"{(int)e.NewValue}%";
    }

    /// <summary>All key tokens offered in the combobox, in a sensible order.</summary>
    private static IEnumerable<string> KeyChoices()
    {
        yield return NoKey;
        for (int i = 1; i <= 9; i++) yield return i.ToString();
        yield return "0";
        for (char c = 'A'; c <= 'Z'; c++) yield return c.ToString();
        for (int i = 1; i <= 24; i++) yield return "F" + i;
        for (int i = 0; i <= 9; i++) yield return "Num" + i;
    }

    private void BuildRows()
    {
        for (int d = 1; d <= MaxDesktops; d++)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });      // label
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // modifiers
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // spacer
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // key combo
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // clear

            var label = new TextBlock
            {
                Text = $"Desktop {d}",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13,
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var mods = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _win[d] = MakeModifierCheck("Win");
            _ctrl[d] = MakeModifierCheck("Ctrl");
            _shift[d] = MakeModifierCheck("Shift");
            _alt[d] = MakeModifierCheck("Alt");
            mods.Children.Add(_win[d]);
            mods.Children.Add(_ctrl[d]);
            mods.Children.Add(_shift[d]);
            mods.Children.Add(_alt[d]);
            Grid.SetColumn(mods, 1);
            grid.Children.Add(mods);

            var key = new ComboBox
            {
                Width = 74,
                Height = 28,
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0),
            };
            foreach (var choice in KeyChoices()) key.Items.Add(choice);
            key.SelectedItem = NoKey;
            _key[d] = key;
            Grid.SetColumn(key, 3);
            grid.Children.Add(key);

            var clear = new Button
            {
                Content = "Clear",
                Width = 62,
                Height = 28,
                Tag = d,
                FontSize = 12,
            };
            clear.Click += OnClearClick;
            Grid.SetColumn(clear, 4);
            grid.Children.Add(clear);

            RowsPanel.Children.Add(grid);
        }
    }

    private static CheckBox MakeModifierCheck(string text) => new()
    {
        Content = text,
        FontSize = 12,
        Margin = new Thickness(0, 0, 8, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Populate the controls from existing config hotkey strings.</summary>
    private void SeedFromConfig()
    {
        foreach (var b in _config.Hotkeys)
        {
            if (b.Desktop < 1 || b.Desktop > MaxDesktops) continue;
            if (!TryParseCombo(b.Hotkey, out bool win, out bool ctrl, out bool shift, out bool alt, out string? key))
                continue;

            int d = b.Desktop;
            _win[d].IsChecked = win;
            _ctrl[d].IsChecked = ctrl;
            _shift[d].IsChecked = shift;
            _alt[d].IsChecked = alt;
            _key[d].SelectedItem = key != null && _key[d].Items.Contains(key) ? key : NoKey;
        }
    }

    /// <summary>Parses "Ctrl+Alt+1" into modifier flags + key token.</summary>
    private static bool TryParseCombo(string? text, out bool win, out bool ctrl, out bool shift, out bool alt, out string? key)
    {
        win = ctrl = shift = alt = false;
        key = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": ctrl = true; break;
                case "alt": alt = true; break;
                case "shift": shift = true; break;
                case "win": case "windows": case "meta": win = true; break;
                default: key = NormalizeKeyToken(raw); break; // last non-modifier wins
            }
        }
        return key != null;
    }

    /// <summary>Maps a parsed token to the exact combobox item spelling (e.g. "a" → "A", "f5" → "F5").</summary>
    private static string? NormalizeKeyToken(string token)
    {
        token = token.Trim();
        if (token.Length == 1 && char.IsLetter(token[0])) return char.ToUpperInvariant(token[0]).ToString();
        if (token.Length == 1 && char.IsDigit(token[0])) return token;
        if (token.StartsWith("num", StringComparison.OrdinalIgnoreCase) && token.Length == 4 && char.IsDigit(token[3]))
            return "Num" + token[3];
        if ((token[0] is 'F' or 'f') && int.TryParse(token.AsSpan(1), out int fn) && fn is >= 1 and <= 24)
            return "F" + fn;
        return null;
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        int d = (int)((Button)sender).Tag!;
        _win[d].IsChecked = _ctrl[d].IsChecked = _shift[d].IsChecked = _alt[d].IsChecked = false;
        _key[d].SelectedItem = NoKey;
        HideStatus();
    }

    /// <summary>Builds a combo string like "Ctrl+Alt+1" from the row's controls, or null if the row is empty.</summary>
    private string? BuildCombo(int d, out bool invalid)
    {
        invalid = false;
        string keyToken = _key[d].SelectedItem as string ?? NoKey;
        bool hasKey = keyToken != NoKey;

        var parts = new List<string>();
        if (_ctrl[d].IsChecked == true) parts.Add("Ctrl");
        if (_alt[d].IsChecked == true) parts.Add("Alt");
        if (_shift[d].IsChecked == true) parts.Add("Shift");
        if (_win[d].IsChecked == true) parts.Add("Win");
        bool hasMod = parts.Count > 0;

        if (!hasKey && !hasMod) return null; // empty row → no binding

        if (!hasKey || !hasMod)
        {
            invalid = true;
            return null;
        }

        parts.Add(keyToken);
        return string.Join("+", parts);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var combos = new string?[MaxDesktops + 1];
        for (int d = 1; d <= MaxDesktops; d++)
        {
            combos[d] = BuildCombo(d, out bool invalid);
            if (invalid)
            {
                ShowStatus($"Desktop {d}: choose at least one modifier (Ctrl·Alt·Shift·Win) together with a key.");
                return;
            }
        }

        // Warn on duplicate combos (only the first would register successfully).
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int d = 1; d <= MaxDesktops; d++)
        {
            var c = combos[d];
            if (!string.IsNullOrEmpty(c) && !seen.Add(c))
            {
                ShowStatus($"Hotkey '{c}' is duplicated. Please assign a different combination.");
                return;
            }
        }

        _config.Hotkeys = new List<HotkeyBinding>();
        for (int d = 1; d <= MaxDesktops; d++)
            if (!string.IsNullOrEmpty(combos[d]))
                _config.Hotkeys.Add(new HotkeyBinding { Hotkey = combos[d]!, Desktop = d });

        _config.ShowOnAllMonitors = MultiMonitorCheck.IsChecked == true;
        _config.Opacity = Math.Clamp(OpacitySlider.Value / 100.0, 0.05, 1.0);

        StartupManager.SetEnabled(AutoStartCheck.IsChecked == true);

        _config.Save();
        _onSaved();
        Close();
    }

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        var prevContent = UpdateButton.Content;
        UpdateButton.Content = "Checking…";
        try
        {
            var result = await UpdateService.CheckAsync();
            await UpdateFlow.HandleAsync(result, this, silentIfNoUpdate: false);
        }
        finally
        {
            UpdateButton.Content = prevContent;
            UpdateButton.IsEnabled = true;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void ShowStatus(string msg)
    {
        StatusText.Text = msg;
        StatusText.Visibility = Visibility.Visible;
    }

    private void HideStatus()
    {
        StatusText.Visibility = Visibility.Collapsed;
    }
}
