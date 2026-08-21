using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace DeskCue.Services;

/// <summary>
/// Registers global hotkeys via RegisterHotKey and raises <see cref="DesktopRequested"/>
/// with the target 1-based desktop index when one fires.
/// </summary>
public sealed class HotKeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    [Flags]
    private enum Mod : uint
    {
        None = 0,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008,
        NoRepeat = 0x4000,
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Dictionary<int, int> _idToDesktop = new(); // hotkey id → desktop index
    private int _nextId = 1;

    /// <summary>Fires with the target desktop index (1-based).</summary>
    public event Action<int>? DesktopRequested;

    /// <summary>Reports hotkeys that failed to register (e.g. already taken by another app).</summary>
    public List<string> FailedRegistrations { get; } = new();

    public HotKeyManager(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd)
            ?? throw new InvalidOperationException("Window handle has no HwndSource.");
        _source.AddHook(WndProc);
    }

    public void Register(IEnumerable<HotkeyBinding> bindings)
    {
        Clear();
        foreach (var b in bindings)
        {
            if (!TryParse(b.Hotkey, out var mods, out var vk))
            {
                FailedRegistrations.Add($"{b.Hotkey} (invalid format)");
                continue;
            }

            int id = _nextId++;
            if (RegisterHotKey(_hwnd, id, (uint)(mods | Mod.NoRepeat), vk))
            {
                _idToDesktop[id] = b.Desktop;
                Log.Write($"RegisterHotKey OK  id={id} '{b.Hotkey}' mods={mods} vk=0x{vk:X2} -> desktop {b.Desktop}");
            }
            else
            {
                int err = Marshal.GetLastWin32Error();
                FailedRegistrations.Add($"{b.Hotkey} → desktop {b.Desktop} (registration failed)");
                Log.Write($"RegisterHotKey FAIL id={id} '{b.Hotkey}' mods={mods} vk=0x{vk:X2} err={err}");
            }
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            if (_idToDesktop.TryGetValue((int)wParam, out int desktop))
            {
                Log.Write($"WM_HOTKEY id={(int)wParam} -> requesting desktop {desktop}");
                DesktopRequested?.Invoke(desktop);
                handled = true;
            }
            else
            {
                Log.Write($"WM_HOTKEY id={(int)wParam} (unmapped)");
            }
        }
        return IntPtr.Zero;
    }

    private void Clear()
    {
        foreach (var id in _idToDesktop.Keys)
            UnregisterHotKey(_hwnd, id);
        _idToDesktop.Clear();
        FailedRegistrations.Clear();
    }

    /// <summary>Parses strings like "Ctrl+Alt+1", "Win+Shift+F5".</summary>
    private static bool TryParse(string text, out Mod mods, out uint vk)
    {
        mods = Mod.None;
        vk = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        string? keyToken = null;
        foreach (var p in parts)
        {
            switch (p.ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= Mod.Control; break;
                case "alt": mods |= Mod.Alt; break;
                case "shift": mods |= Mod.Shift; break;
                case "win": case "windows": case "meta": mods |= Mod.Win; break;
                default: keyToken = p; break; // last non-modifier wins
            }
        }

        if (keyToken == null) return false;
        return TryParseKey(keyToken, out vk);
    }

    private static bool TryParseKey(string token, out uint vk)
    {
        vk = 0;
        token = token.Trim();

        // Numpad digits: "Num0".."Num9"  (VK_NUMPAD0 = 0x60)
        if (token.Length == 4 && token.StartsWith("Num", StringComparison.OrdinalIgnoreCase)
            && char.IsDigit(token[3]))
        {
            vk = (uint)(0x60 + (token[3] - '0'));
            return true;
        }
        // Digits 0-9 (top row)
        if (token.Length == 1 && char.IsDigit(token[0]))
        {
            vk = (uint)(0x30 + (token[0] - '0'));
            return true;
        }
        // Letters A-Z
        if (token.Length == 1 && char.IsLetter(token[0]))
        {
            vk = (uint)char.ToUpperInvariant(token[0]);
            return true;
        }
        // Function keys F1-F24
        if ((token[0] == 'F' || token[0] == 'f') && int.TryParse(token.AsSpan(1), out int fn)
            && fn is >= 1 and <= 24)
        {
            vk = (uint)(0x70 + (fn - 1)); // VK_F1 = 0x70
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        Clear();
        _source.RemoveHook(WndProc);
    }
}
