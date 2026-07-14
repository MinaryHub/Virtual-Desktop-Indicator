using System.Runtime.InteropServices;

namespace VirtualDesktopIndicator.Linux.Services;

/// <summary>
/// Registers global hotkeys on the X11 root window via XGrabKey and raises
/// <see cref="DesktopRequested"/> (from a background thread) when one fires.
///
/// Grabs and the event loop must share one Display connection, so all X calls
/// live on a dedicated thread; the UI thread only queues re-registration requests.
/// </summary>
public sealed class HotKeyManager : IDisposable
{
    // Modifiers we care about when matching (ignore CapsLock/NumLock in comparison).
    private const uint RelevantMods = X11.ControlMask | X11.Mod1Mask | X11.ShiftMask | X11.Mod4Mask;

    // Keep the error handler delegate alive for the process lifetime.
    private static X11.XErrorHandler? _errorHandler;

    private readonly Thread _thread;
    private readonly object _gate = new();
    private volatile bool _running = true;

    private IntPtr _display;
    private IntPtr _root;

    private readonly Dictionary<(int keycode, uint mods), int> _map = new();
    private List<HotkeyBinding>? _pending;
    private volatile bool _hasPending;

    /// <summary>Fires with the target desktop index (1-based), on the hotkey thread.</summary>
    public event Action<int>? DesktopRequested;

    /// <summary>Hotkeys that could not be parsed / mapped to a key.</summary>
    public List<string> FailedRegistrations { get; } = new();

    public HotKeyManager()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "x11-hotkeys" };
        _thread.Start();
    }

    /// <summary>Queues a new set of bindings; applied on the hotkey thread.</summary>
    public void Register(IEnumerable<HotkeyBinding> bindings)
    {
        lock (_gate)
        {
            _pending = bindings.ToList();
            _hasPending = true;
        }
    }

    private void Run()
    {
        X11.XInitThreads();
        _display = X11.XOpenDisplay(null);
        if (_display == IntPtr.Zero)
        {
            Log.Write("X11 hotkeys: XOpenDisplay failed");
            return;
        }

        // Never let an Xlib protocol error (e.g. BadAccess from an already-grabbed
        // key) terminate the process — the default handler calls exit().
        _errorHandler = (IntPtr d, ref XErrorEvent e) =>
        {
            Log.Write($"X11 error: code={e.error_code} request={e.request_code}");
            return 0;
        };
        X11.XSetErrorHandler(_errorHandler);

        _root = X11.XDefaultRootWindow(_display);
        X11.XSelectInput(_display, _root, (IntPtr)X11.KeyPressMask);

        while (_running)
        {
            if (_hasPending) ApplyPending();

            while (_running && X11.XPending(_display) > 0)
            {
                X11.XNextEvent(_display, out XEvent ev);
                if (ev.type == X11.KeyPress) HandleKeyPress(ev.xkey);
            }

            Thread.Sleep(20);
        }

        Clear();
        X11.XCloseDisplay(_display);
    }

    private void ApplyPending()
    {
        List<HotkeyBinding> bindings;
        lock (_gate)
        {
            bindings = _pending ?? new();
            _pending = null;
            _hasPending = false;
        }

        Clear();
        FailedRegistrations.Clear();

        foreach (var b in bindings)
        {
            if (!TryParse(b.Hotkey, out uint mods, out IntPtr keysym))
            {
                FailedRegistrations.Add($"{b.Hotkey} (invalid format)");
                continue;
            }

            int keycode = X11.XKeysymToKeycode(_display, keysym);
            if (keycode == 0)
            {
                FailedRegistrations.Add($"{b.Hotkey} (no key on this layout)");
                continue;
            }

            foreach (uint extra in LockVariants())
                X11.XGrabKey(_display, keycode, mods | extra, _root, false,
                    X11.GrabModeAsync, X11.GrabModeAsync);

            _map[(keycode, mods)] = b.Desktop;
            Log.Write($"XGrabKey '{b.Hotkey}' keycode={keycode} mods=0x{mods:X} -> desktop {b.Desktop}");
        }

        X11.XSync(_display, false);
    }

    private void HandleKeyPress(XKeyEvent key)
    {
        uint mods = key.state & RelevantMods;
        if (_map.TryGetValue(((int)key.keycode, mods), out int desktop))
        {
            Log.Write($"hotkey -> desktop {desktop}");
            DesktopRequested?.Invoke(desktop);
        }
    }

    private void Clear()
    {
        foreach (var ((keycode, mods), _) in _map)
            foreach (uint extra in LockVariants())
                X11.XUngrabKey(_display, keycode, mods | extra, _root);
        _map.Clear();
    }

    // CapsLock (Lock) and NumLock (Mod2) must be grabbed in every combination so
    // the hotkey works regardless of their state.
    private static uint[] LockVariants() =>
        [0, X11.LockMask, X11.Mod2Mask, X11.LockMask | X11.Mod2Mask];

    /// <summary>Parses "Ctrl+Alt+1", "Super+F5" into an X11 modifier mask + keysym.</summary>
    private static bool TryParse(string text, out uint mods, out IntPtr keysym)
    {
        mods = 0;
        keysym = IntPtr.Zero;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string? keyToken = null;
        foreach (var p in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (p.ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= X11.ControlMask; break;
                case "alt": mods |= X11.Mod1Mask; break;
                case "shift": mods |= X11.ShiftMask; break;
                case "win": case "super": case "meta": case "windows": mods |= X11.Mod4Mask; break;
                default: keyToken = p; break;
            }
        }

        return keyToken != null && TryKeysym(keyToken, out keysym);
    }

    private static bool TryKeysym(string token, out IntPtr keysym)
    {
        keysym = IntPtr.Zero;
        token = token.Trim();

        // Numpad digits: Num0..Num9 -> XK_KP_0 (0xFFB0)
        if (token.Length == 4 && token.StartsWith("Num", StringComparison.OrdinalIgnoreCase)
            && char.IsDigit(token[3]))
        {
            keysym = (IntPtr)(0xFFB0 + (token[3] - '0'));
            return true;
        }
        // Digits 0-9 -> ASCII keysym
        if (token.Length == 1 && char.IsDigit(token[0]))
        {
            keysym = (IntPtr)(0x30 + (token[0] - '0'));
            return true;
        }
        // Letters A-Z -> lowercase keysym XK_a (0x61)
        if (token.Length == 1 && char.IsLetter(token[0]))
        {
            keysym = (IntPtr)(0x61 + (char.ToUpperInvariant(token[0]) - 'A'));
            return true;
        }
        // Function keys F1-F24 -> XK_F1 (0xFFBE)
        if ((token[0] is 'F' or 'f') && int.TryParse(token.AsSpan(1), out int fn) && fn is >= 1 and <= 24)
        {
            keysym = (IntPtr)(0xFFBE + (fn - 1));
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        _running = false;
        try { _thread.Join(500); } catch { /* ignore */ }
    }
}
