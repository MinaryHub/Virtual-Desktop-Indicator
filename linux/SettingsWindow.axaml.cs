using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using VirtualDesktopIndicator.Linux.Services;

namespace VirtualDesktopIndicator.Linux;

public partial class SettingsWindow : Window
{
    private const int MaxDesktops = 9;
    private const string NoKey = "(none)";

    private readonly AppConfig _config;
    private readonly Action _onSaved;

    private readonly CheckBox[] _super = new CheckBox[MaxDesktops + 1];
    private readonly CheckBox[] _ctrl = new CheckBox[MaxDesktops + 1];
    private readonly CheckBox[] _shift = new CheckBox[MaxDesktops + 1];
    private readonly CheckBox[] _alt = new CheckBox[MaxDesktops + 1];
    private readonly ComboBox[] _key = new ComboBox[MaxDesktops + 1];

    // Parameterless ctor for the XAML loader.
    public SettingsWindow() : this(new AppConfig(), () => { }) { }

    public SettingsWindow(AppConfig config, Action onSaved)
    {
        _config = config;
        _onSaved = onSaved;
        InitializeComponent();

        VersionText.Text = $"Version {AppVersion.Display}";
        AutoStartCheck.IsChecked = StartupManager.IsEnabled();

        BuildRows();
        SeedFromConfig();
    }

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
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("78,Auto,*,Auto,Auto"),
                Margin = new Avalonia.Thickness(0, 0, 0, 6),
            };

            var label = new TextBlock
            {
                Text = $"Desktop {d}",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13,
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var mods = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _super[d] = MakeModifierCheck("Super");
            _ctrl[d] = MakeModifierCheck("Ctrl");
            _shift[d] = MakeModifierCheck("Shift");
            _alt[d] = MakeModifierCheck("Alt");
            mods.Children.Add(_super[d]);
            mods.Children.Add(_ctrl[d]);
            mods.Children.Add(_shift[d]);
            mods.Children.Add(_alt[d]);
            Grid.SetColumn(mods, 1);
            grid.Children.Add(mods);

            var key = new ComboBox { Width = 82, FontSize = 12, Margin = new Avalonia.Thickness(6, 0, 6, 0) };
            foreach (var choice in KeyChoices()) key.Items.Add(choice);
            key.SelectedItem = NoKey;
            _key[d] = key;
            Grid.SetColumn(key, 3);
            grid.Children.Add(key);

            var clear = new Button { Content = "Clear", Width = 66, FontSize = 12, Tag = d };
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
        Margin = new Avalonia.Thickness(0, 0, 8, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private void SeedFromConfig()
    {
        foreach (var b in _config.Hotkeys)
        {
            if (b.Desktop < 1 || b.Desktop > MaxDesktops) continue;
            if (!TryParseCombo(b.Hotkey, out bool su, out bool ct, out bool sh, out bool al, out string? key))
                continue;

            int d = b.Desktop;
            _super[d].IsChecked = su;
            _ctrl[d].IsChecked = ct;
            _shift[d].IsChecked = sh;
            _alt[d].IsChecked = al;
            _key[d].SelectedItem = key != null && _key[d].Items.Contains(key) ? key : NoKey;
        }
    }

    private static bool TryParseCombo(string? text, out bool su, out bool ct, out bool sh, out bool al, out string? key)
    {
        su = ct = sh = al = false;
        key = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": ct = true; break;
                case "alt": al = true; break;
                case "shift": sh = true; break;
                case "win": case "super": case "meta": case "windows": su = true; break;
                default: key = NormalizeKeyToken(raw); break;
            }
        }
        return key != null;
    }

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

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        int d = (int)((Button)sender!).Tag!;
        _super[d].IsChecked = _ctrl[d].IsChecked = _shift[d].IsChecked = _alt[d].IsChecked = false;
        _key[d].SelectedItem = NoKey;
        HideStatus();
    }

    private string? BuildCombo(int d, out bool invalid)
    {
        invalid = false;
        string keyToken = _key[d].SelectedItem as string ?? NoKey;
        bool hasKey = keyToken != NoKey;

        var parts = new List<string>();
        if (_ctrl[d].IsChecked == true) parts.Add("Ctrl");
        if (_alt[d].IsChecked == true) parts.Add("Alt");
        if (_shift[d].IsChecked == true) parts.Add("Shift");
        if (_super[d].IsChecked == true) parts.Add("Super");
        bool hasMod = parts.Count > 0;

        if (!hasKey && !hasMod) return null;
        if (!hasKey || !hasMod) { invalid = true; return null; }

        parts.Add(keyToken);
        return string.Join("+", parts);
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        var combos = new string?[MaxDesktops + 1];
        for (int d = 1; d <= MaxDesktops; d++)
        {
            combos[d] = BuildCombo(d, out bool invalid);
            if (invalid)
            {
                ShowStatus($"Desktop {d}: choose at least one modifier (Super·Ctrl·Shift·Alt) together with a key.");
                return;
            }
        }

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

        StartupManager.SetEnabled(AutoStartCheck.IsChecked == true);

        _config.Save();
        _onSaved();
        Close();
    }

    private async void OnCheckUpdate(object? sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        var prev = UpdateButton.Content;
        UpdateButton.Content = "Checking…";
        try
        {
            var result = await UpdateService.CheckAsync();
            await UpdateFlow.HandleAsync(result, silentIfNoUpdate: false);
        }
        finally
        {
            UpdateButton.Content = prev;
            UpdateButton.IsEnabled = true;
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void ShowStatus(string msg)
    {
        StatusText.Text = msg;
        StatusText.IsVisible = true;
    }

    private void HideStatus() => StatusText.IsVisible = false;
}
