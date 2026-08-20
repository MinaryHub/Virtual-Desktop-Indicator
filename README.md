# DeskCue

A tray app that **always shows your current Windows virtual desktop as a
translucent on-screen overlay** and lets you **jump straight to a specific
desktop with custom hotkeys**.

- Shows `current / total  desktop-name` as a translucent, click-through overlay at the top of the screen (default)
- The overlay follows you and stays visible across all virtual desktops
- Jump to a desktop instantly with `Ctrl+Alt+1` ~ `Ctrl+Alt+9` (defaults)
- Freely change position, opacity, font size, colors, and hotkeys via the config file
- **Update check via GitHub Releases with one-click auto-update**
- Shows the current **version** in the tray menu and settings window

## Download

Get the latest build from the **[Releases page](https://github.com/knoxxr/Virtual-Desktop-Indicator/releases/latest)** (no .NET install required):

- **Windows**: `DeskCue-Setup-<version>.exe` — download and run it.
- **Linux (X11)**: `DeskCue-linux-x64-<version>.tar.gz` — extract and run `./install.sh` (see [Linux (X11)](#linux-x11) below).
- **Windows (MSIX)**: `DeskCue-<version>.msix` — the same build as the Microsoft Store package,
  attached for sideloading. It is **unsigned**, so installing it takes an elevated PowerShell and,
  because the package carries executable content, Windows installs it **for all users**:

  ```powershell
  Add-AppxPackage -Path .\DeskCue-<version>.msix -AllowUnsigned
  ```

  Prefer the `Setup.exe` above unless you specifically want the packaged build — that one is
  per-user and needs no admin rights. The Store version, once published, is signed by Microsoft
  and installs normally.

On Windows, if it is already installed the app checks for a newer version on startup (see "Updates" below).

## Install (recommended)

Installer: **`installer/DeskCue-Setup-<version>.exe`**

Double-click it and follow the wizard.

- **No .NET install required** — it is a self-contained build with the runtime bundled, so nothing needs to be pre-installed on the target PC.
- **No admin rights required** — installs for the current user only (`%LocalAppData%\Programs\DeskCue`).
- During setup you can choose a **desktop shortcut** (optional) and **Run at Windows startup** (checked by default).
- Uninstall from **Settings → Apps → Installed apps** or the Start-menu entry *"Uninstall DeskCue"*; the autostart entry is cleaned up too.

## Requirements / running from source

- Windows 10/11 (64-bit)
- To build/run from source, [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0):

```powershell
dotnet run -c Release              # run
dotnet publish -c Release          # framework-dependent exe (.NET runtime required)
```

## Linux (X11)

A separate build under [linux/](linux/) targets **Linux/X11** using Avalonia UI.
It provides the same overlay, global hotkeys, tray menu, settings window, and
update check, adapted to Linux:

- **Desktop detection/switching** uses the EWMH X11 protocol
  (`_NET_CURRENT_DESKTOP` / `_NET_NUMBER_OF_DESKTOPS` / `_NET_DESKTOP_NAMES`) — the
  documented interface supported by GNOME/Xorg, KDE/X11, XFCE, and most WMs.
- **Global hotkeys** use X11 key grabs (`XGrabKey`). The "Windows" modifier is the
  **Super** key on Linux; defaults remain `Ctrl+Alt+1`…`Ctrl+Alt+9`.
- **Autostart** writes a freedesktop entry to `~/.config/autostart`.
- **Config** lives at `~/.config/VirtualDesktopIndicator/config.json`.
- On Linux the app does **not** self-install updates; it notifies you and opens
  the release page.

**Requirements**: an **X11** (Xorg) session — Wayland is not supported by this
build — and a system tray/AppIndicator (on GNOME, the "AppIndicator" extension).

**Install** (from the released tarball): extract and run `./install.sh`
(per-user, no root). Build from source:

```bash
dotnet publish linux/VirtualDesktopIndicator.Linux.csproj \
  -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish-linux
./publish-linux/virtual-desktop-indicator
```

> ⚠️ The Linux build has been verified to compile and publish, but has **not yet
> been runtime-tested on a live X11 desktop**. Please report issues from real
> GNOME/KDE/XFCE sessions. macOS is not supported: it exposes no public API for
> the current Space, so the core feature cannot be implemented reliably.

## Rebuilding the installer

Requires [Inno Setup 6](https://jrsoftware.org/isdl.php) (`winget install JRSoftware.InnoSetup`).

```powershell
# 1) Publish a self-contained build (runtime bundled)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-sc
Remove-Item publish-sc\*.pdb -EA SilentlyContinue
# 2) Compile the installer (produces Setup.exe under installer\)
& "$env:LocalAppData\Programs\Inno Setup 6\ISCC.exe" installer.iss
```

The wizard configuration lives in [installer.iss](installer.iss) (version, shortcuts, autostart, etc.).

> **Releases are automated.** Pushing a tag like `v1.1.0` triggers the
> [release workflow](.github/workflows/release.yml), which publishes the
> self-contained build, compiles the Inno Setup installer, and creates a
> GitHub Release with the installer attached — no local Inno Setup needed.
> The same tag also runs the [MSIX workflow](.github/workflows/msix.yml), which builds the
> Microsoft Store package as a workflow artifact — see
> [packaging/msix](packaging/msix/README.md) for submitting it.

## Tray menu

Right-click the **VD** icon in the notification area (or double-click → settings window):

- The current **version** (`v1.1.0.x`) is shown at the top of the menu
- **Settings...** — opens the window to change hotkeys and autostart
- **Check for updates...** — queries GitHub Releases for a newer version (see "Updates")
- **Run at Windows startup** — when checked, launches at logon (per-user, no admin rights)
- **Open config file** — opens `config.json` in the default editor
- **Reload config** — apply changes made by editing the file directly
- **Position** — quickly change the overlay position (TopCenter, TopRight, etc.)
- **Support development ♥** — opens the [GitHub Sponsors page](https://github.com/sponsors/MinaryHub)
  in your browser (see "Support" below)
- **Exit**

## Settings window (changing hotkeys)

Open it by double-clicking the tray icon or via the **Settings...** menu.

- **Run at Windows startup** checkbox
- **Hotkeys** — for each desktop 1–9, check the modifier checkboxes
  (`Win` `Ctrl` `Shift` `Alt`) and pick a key from the **key combobox** on the right.
  Example: check `Ctrl` `Alt` + select key `3` → assigns `Ctrl+Alt+3`.
  - Select `(none)` in the key combobox or press the **Clear** button to empty a hotkey
  - At least one modifier (`Ctrl`/`Alt`/`Shift`/`Win`) plus a key is required; duplicate combinations are warned about on save
  - Press **Save** to apply immediately (no restart needed)
- The current **version** and a **Check for updates** button are at the bottom of the window

## Updates (versioning · auto-update)

- **Automatic versioning** — every build increments `build.counter` by one, so the
  app version is auto-stamped as `1.1.0.<build>` (the `StampBuildVersion` target in
  [csproj](VirtualDesktopIndicator.csproj)). To bump to a meaningful release version,
  just edit `<VersionPrefix>` in the csproj. The installer version is read from the
  published exe automatically.
- **Update check** — on startup the app queries the latest GitHub release in the
  background (it stays quiet when there is no new version or on a network error).
  You can also check manually via **Check for updates...** in the tray menu or the
  **Check for updates** button in the settings window.
- **Auto-install** — when a newer version exists, the app shows a prompt and, on
  your consent, downloads the latest installer (`DeskCue-Setup-*.exe`),
  runs it, and exits. The wizard replaces the existing files (if a release has no
  installer attached, the release page is opened instead).
- Version comparison uses `Major.Minor.Patch` only; the local build number (4th part) is ignored.

> Autostart writes the executable path to the registry at `HKCU\...\CurrentVersion\Run`.
> If you moved the executable, toggle autostart off and on to refresh the path.

## Config file

`%APPDATA%\VirtualDesktopIndicator\config.json` (created with defaults on first run)

| Key | Description | Default |
|-----|-------------|---------|
| `Position` | Overlay position: `TopLeft` `TopCenter` `TopRight` `BottomLeft` `BottomCenter` `BottomRight` `Center` | `TopCenter` |
| `ShowOnAllMonitors` | Show the indicator on every monitor (same position on each); when `false`, primary monitor only | `true` |
| `MarginX` / `MarginY` | Margin from the edge (px) | `0` / `12` |
| `Opacity` | Overlay (desktop-number) opacity (0.05 ~ 1.0) | `0.5` |
| `FontSize` | Number font size | `28` |
| `ShowNumber` | Show the number | `true` |
| `ShowCount` | Use the `2 / 5` format (include total count) | `true` |
| `ShowName` | Show the desktop name | `true` |
| `Foreground` / `Background` | Text / background color (`#RRGGBB`) | white / black |
| `CornerRadius` | Corner rounding | `10` |
| `PollIntervalMs` | State refresh interval (ms) | `300` |
| `SmoothSwitch` | Switch with the native animation (no taskbar flicker); `false` = instant COM jump | `true` |
| `Hotkeys` | List of `{ "Hotkey": "Ctrl+Alt+1", "Desktop": 1 }` | desktops 1–9 |

**Hotkey format**: `modifier+modifier+key`. Modifiers are `Ctrl` `Alt` `Shift` `Win`;
keys are `1`~`0`, `A`~`Z`, `F1`~`F24`, and numpad digits `Num0`~`Num9`.
e.g. `Ctrl+Alt+3`, `Ctrl+Alt+F5`, `Ctrl+Win+Num1`. `Desktop` is the 1-based desktop number.
Numpad digits (`Num*`) are only recognized when **NumLock is on**.
Hotkeys already used by another program fail to register; you are notified with a balloon tip on startup.

> ⚠️ Some combinations such as `Win+Shift+<digit>` and `Win+<digit>` are **reserved by
> Windows** (e.g. launching the Nth taskbar app) and fail to register. That is why the
> defaults use the conflict-free `Ctrl+Alt+<digit>`. Failed hotkeys are reported with a
> balloon tip on startup; just pick a different combination in the settings window.

### Desktop switching behavior

By default the app switches **straight to the target desktop** via the Windows internal
API (`IVirtualDesktopManagerInternal.SwitchDesktop`). Even if the target is several slots
away, it jumps directly without stepping through the desktops in between.

This internal API is undocumented and can change between Windows builds. So the app
verifies the interface connects correctly before use and, on failure, **automatically
falls back to key-input stepping (`Win+Ctrl+←/→`)** (which steps one desktop at a time,
so far-away desktops are a bit slower). Either way the result is correct.

> 💡 You can name a desktop by double-clicking its name in Task View (`Win+Tab`).

## How it works (stability by design)

Many virtual-desktop APIs are undocumented and their interfaces change between Windows
builds, so they break easily. This app uses **only approaches that survive build updates**.

- **Detecting the current position**: reads the registry directly
  (`...\Explorer\VirtualDesktops\VirtualDesktopIDs` order list + `CurrentVirtualDesktop`).
  It does not depend on private COM, so it works regardless of version.
- **Switching desktops**: switches directly to the target via internal COM (`SwitchDesktop`),
  and automatically falls back to the `Win+Ctrl+←/→` key-input method if that interface is
  missing or the build differs (see "Desktop switching behavior"). The fallback path uses
  Windows' built-in shortcuts, so it always works.
- **Showing on every desktop**: moves the overlay to the current desktop via the documented
  public COM interface `IVirtualDesktopManager.MoveWindowToDesktop` (stable since Windows 10 1607).

## Support

The app is free and has no ads, no telemetry, and no paid tier. If it saves you time, you can
chip in on **[GitHub Sponsors](https://github.com/sponsors/MinaryHub)** — reachable from the tray
menu (*Support development ♥*) and from the settings window (*♥ Support*).

Both entries are plain links that open your browser; the app itself never handles payments,
asks for an account, or shows a payment screen, in either the GitHub build or the Store build.

## Structure

```
App.xaml(.cs)                  Entry point, tray icon/menu, startup update check
OverlayWindow.xaml(.cs)        Translucent click-through overlay, polling/repositioning/desktop tracking
SettingsWindow.xaml(.cs)       Hotkey UI (modifier checkboxes + key combobox) + autostart + version/update
UpdateFlow.cs                  Shared UI flow: update prompt → download/install/exit on consent
Services/
  AppConfig.cs                 Loads/saves config.json
  AppVersion.cs                Exposes the app version stamped at build time
  UpdateService.cs             Queries the latest GitHub release + downloads/runs the installer
  VirtualDesktopRegistry.cs    Reads current number/count/name from the registry
  DesktopSwitcher.cs           Moves to the target desktop (internal COM first, key-input fallback)
  VirtualDesktopInternal.cs    Direct switch via internal COM (SwitchDesktop) + vtable verification
  VirtualDesktopManagerCom.cs  Public COM (MoveWindowToDesktop) wrapper
  HotKeyManager.cs             Registers/handles global hotkeys
  StartupManager.cs            Toggles run-at-startup (HKCU\Run)
  Donate.cs                    Opens the GitHub Sponsors page in the browser

linux/                         Linux/X11 build (Avalonia UI) — overlay, tray,
                               settings, EWMH desktop detection/switching,
                               X11 global hotkeys, .desktop autostart, packaging
```
