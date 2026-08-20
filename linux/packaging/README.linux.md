# DeskCue (Linux / X11)

A tray app that shows your current virtual desktop as a translucent overlay and
lets you jump to a desktop with global hotkeys. This is the **Linux/X11** build.

## Requirements

- An **X11** session (Xorg). Wayland is not supported by this build — the desktop
  index/switch relies on the EWMH X11 protocol and hotkeys use X11 key grabs.
  (On GNOME/KDE, log in on an "Xorg"/"X11" session.)
- A **system tray / AppIndicator**. Most desktops (KDE, XFCE, Cinnamon, MATE) have
  one built in. On GNOME you may need the "AppIndicator and KStatusNotifierItem"
  extension for the tray menu to appear.
- No .NET install required — this is a self-contained build.

## Install (per-user, no root)

```bash
./install.sh
```

This copies the binary to `~/.local/bin` and adds a menu entry. Launch it from
your application menu, or run `virtual-desktop-indicator`.

## Uninstall

```bash
./uninstall.sh
```

## Usage

- The overlay shows `current / total  name` and follows you across desktops.
- Default hotkeys: `Ctrl+Alt+1` … `Ctrl+Alt+9` jump to that desktop.
- Right-click the tray icon (or open **Settings**) to change hotkeys, toggle
  "Run at login", change position, or check for updates.
- Modifiers: `Super` (the Windows/⌘-style key), `Ctrl`, `Shift`, `Alt`.

Config lives at `~/.config/VirtualDesktopIndicator/config.json`.
