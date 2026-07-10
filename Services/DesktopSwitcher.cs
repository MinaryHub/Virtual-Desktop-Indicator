using System.Runtime.InteropServices;

namespace VirtualDesktopIndicator.Services;

/// <summary>
/// Switches virtual desktops by synthesising the built-in Windows shortcuts
/// (Win+Ctrl+Left / Win+Ctrl+Right) via SendInput.
///
/// This is intentionally used instead of the undocumented IVirtualDesktopManagerInternal
/// SwitchDesktop method — key synthesis is stable across every Windows build and produces
/// exactly the same animation the user gets from the keyboard. Absolute jumps are done by
/// reading the current index from the registry and stepping the required number of times.
/// </summary>
public static class DesktopSwitcher
{
    // Windows animates each switch and drops a Win+Ctrl+Arrow sent mid-animation; the registry
    // also reflects the new desktop with a lag (~1.4s). So we step CLOSED-LOOP: send one step,
    // wait for the registry to confirm the move, then recompute — instead of firing N presses
    // blindly. All switching is serialized through one worker; a newer request supersedes an
    // in-flight one (via the generation counter) so concurrent hotkeys never fight each other.
    private const int ConfirmTimeoutMs = 3000;
    private const int PollMs = 40;

    private static readonly object SwitchGate = new();
    private static int _generation;

    /// <summary>
    /// Jump to an absolute 1-based desktop index (does nothing if already there).
    /// Must be called on the UI (STA) thread — the direct COM path requires it.
    /// </summary>
    public static void SwitchTo(int targetIndex)
    {
        // Preferred: jump straight to the target via the internal COM interface (no stepping
        // through intermediate desktops). Runs synchronously on the STA caller thread; it's instant.
        var ids = VirtualDesktopRegistry.ReadOrderedIds();
        if (ids is { Length: > 0 })
        {
            int t = Math.Clamp(targetIndex, 1, ids.Length);
            if (VirtualDesktopInternal.TrySwitch(ids[t - 1], ids.Length))
            {
                Log.Write($"SwitchTo {t}: direct COM switch OK");
                return;
            }
            Log.Write($"SwitchTo {t}: direct switch unavailable → keystroke stepping");
        }

        // Fallback: synthesize Win+Ctrl+Arrow and step closed-loop, off the UI thread so the
        // confirm-waits don't freeze the overlay.
        int gen = System.Threading.Interlocked.Increment(ref _generation);
        System.Threading.Tasks.Task.Run(() =>
        {
            lock (SwitchGate)
            {
                if (gen != System.Threading.Volatile.Read(ref _generation)) return; // already superseded
                SwitchLoop(targetIndex, gen);
            }
        });
    }

    public static void SwitchRight() => System.Threading.Tasks.Task.Run(() => { lock (SwitchGate) { ClearHeldModifiers(); SendSwitch(Direction.Right); } });
    public static void SwitchLeft() => System.Threading.Tasks.Task.Run(() => { lock (SwitchGate) { ClearHeldModifiers(); SendSwitch(Direction.Left); } });

    private enum Direction { Left, Right }

    private static bool Superseded(int gen) => gen != System.Threading.Volatile.Read(ref _generation);

    private static void SwitchLoop(int targetIndex, int gen)
    {
        var info = VirtualDesktopRegistry.Read();
        if (info == null) { Log.Write($"SwitchTo({targetIndex}): registry read FAILED"); return; }

        int target = Math.Clamp(targetIndex, 1, info.Count);
        Log.Write($"SwitchTo target={target} current={info.Index} count={info.Count} gen={gen}");
        if (info.Index == target) return;

        int maxAttempts = info.Count * 3 + 3; // guard against a stuck loop
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (Superseded(gen)) { Log.Write($"gen={gen} superseded, aborting"); return; }

            info = VirtualDesktopRegistry.Read();
            if (info == null) return;
            if (info.Index == target) { Log.Write($"reached desktop {target}"); return; }

            int before = info.Index;
            ClearHeldModifiers();
            SendSwitch(before < target ? Direction.Right : Direction.Left);
            WaitForIndexChange(before, ConfirmTimeoutMs, gen);
        }
        Log.Write($"SwitchLoop stopped before reaching {target}");
    }

    /// <summary>Block until the current desktop index differs from <paramref name="before"/>, the request is superseded, or timeout.</summary>
    private static void WaitForIndexChange(int before, int timeoutMs, int gen)
    {
        for (int waited = 0; waited < timeoutMs; waited += PollMs)
        {
            System.Threading.Thread.Sleep(PollMs);
            if (Superseded(gen)) return;
            var info = VirtualDesktopRegistry.Read();
            if (info != null && info.Index != before) return;
        }
    }

    /// <summary>
    /// Release every modifier that might still be physically held from the triggering hotkey
    /// (e.g. Ctrl+Alt from "Ctrl+Alt+1"). Otherwise they mix into the synthesized Win+Ctrl+Arrow
    /// and Windows no longer recognizes the "switch desktop" shortcut.
    /// </summary>
    private static void ClearHeldModifiers()
    {
        var seq = new List<INPUT>();
        foreach (ushort vk in new ushort[]
                 { VK_LMENU, VK_RMENU, VK_LSHIFT, VK_RSHIFT, VK_LCONTROL, VK_RCONTROL, VK_LWIN, VK_RWIN })
            seq.Add(Key(vk, up: true));
        var arr = seq.ToArray();
        SendInput((uint)arr.Length, arr, Marshal.SizeOf<INPUT>());
    }

    /// <summary>Send one clean Win+Ctrl+Arrow (a single desktop switch).</summary>
    private static void SendSwitch(Direction dir)
    {
        ushort arrow = dir == Direction.Right ? VK_RIGHT : VK_LEFT;
        var arr = new[]
        {
            Key(VK_LWIN, up: false),
            Key(VK_LCONTROL, up: false),
            Key(arrow, up: false),
            Key(arrow, up: true),
            Key(VK_LCONTROL, up: true),
            Key(VK_LWIN, up: true),
        };
        uint sent = SendInput((uint)arr.Length, arr, Marshal.SizeOf<INPUT>());
        int err = Marshal.GetLastWin32Error();
        Log.Write($"SendSwitch {dir}: sent={sent}/{arr.Length} err={err}");
    }

    // --- Win32 SendInput ----------------------------------------------------
    private const ushort VK_LWIN = 0x5B;
    private const ushort VK_RWIN = 0x5C;
    private const ushort VK_LCONTROL = 0xA2;
    private const ushort VK_RCONTROL = 0xA3;
    private const ushort VK_LSHIFT = 0xA0;
    private const ushort VK_RSHIFT = 0xA1;
    private const ushort VK_LMENU = 0xA4; // left Alt
    private const ushort VK_RMENU = 0xA5; // right Alt
    private const ushort VK_LEFT = 0x25;
    private const ushort VK_RIGHT = 0x27;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private static INPUT Key(ushort vk, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = 0,
                dwFlags = up ? KEYEVENTF_KEYUP : 0,
                time = 0,
                dwExtraInfo = IntPtr.Zero,
            }
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    // The union must be sized by its LARGEST member (MOUSEINPUT), otherwise Marshal.SizeOf<INPUT>
    // is too small, cbSize won't match what SendInput expects (40 bytes on x64), and SendInput
    // rejects every event with ERROR_INVALID_PARAMETER (87) — injecting nothing.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}
