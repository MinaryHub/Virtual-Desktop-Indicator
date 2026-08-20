# Microsoft Store (MSIX) packaging

This folder packages DeskCue as an **MSIX** for the Microsoft Store. The app
is packaged as a **full-trust Win32 desktop app** (`EntryPoint=Windows.FullTrustApplication`),
so `SendInput` key synthesis, global hotkeys, and the virtual-desktop registry reads all keep
working exactly as in the plain-exe build.

## What differs in the packaged build

Behaviour is chosen at runtime via `Services/PackageContext.cs` (`IsPackaged`) — the **same
binaries** ship through both the Store and the Inno Setup installer:

| Concern | Plain-exe build | MSIX / Store build |
|---|---|---|
| Updates | GitHub Releases self-update (`UpdateService`) | **Disabled** — the Store updates the app |
| Run at startup | `HKCU\…\Run` registry value | **`windows.startupTask`** extension via the `StartupTask` API |

## Files

- `Package.appxmanifest` — the package manifest (identity, capabilities, tiles, startup task).
- `Assets/` — logo PNGs (committed; regenerate with `generate-assets.ps1`).
- `generate-assets.ps1` — renders the "VD" tile at every required size.
- `build-msix.ps1` — builds the `.msix` locally for testing.
- `../../.github/workflows/msix.yml` — CI: builds the `.msix`, uploads it as a workflow artifact
  and (on tag builds) attaches it to the GitHub Release. Reusable: called by `release.yml` on
  every `v*` tag, and runnable on its own via manual dispatch.

## ⚠️ Before your first submission — fill in three identity values

`Package.appxmanifest` ships with `PLACEHOLDER` identity so it builds locally. Replace these with
the values from **Partner Center ▸ your app ▸ Product management ▸ Product identity**:

| Manifest location | Partner Center field |
|---|---|
| `Identity/@Name` | `Package/Identity/Name` |
| `Identity/@Publisher` (`CN=…`) | `Package/Identity/Publisher` |
| `Properties/PublisherDisplayName` | `Package/Properties/PublisherDisplayName` |

You do **not** need a code-signing certificate: the Store re-signs the package on ingestion.
(A cert is only needed to *sideload* the `.msix` for local testing — see `build-msix.ps1`.)

## Build the package

**In CI (recommended):** every `v*` release tag builds the package automatically — the release
workflow calls this one after creating the GitHub Release, so the `.msix` is both a workflow
artifact and a **release asset** next to the installer. For a Store-only rebuild, use the
Actions tab ▸ *Build MSIX (Store)* ▸ *Run workflow* ▸ enter the version (e.g. `1.3.6`).

**Locally:**
```powershell
pwsh packaging/msix/build-msix.ps1 -Version 1.3.6
```

The MSIX payload is published **self-contained but not single-file** (`-p:PublishSingleFile=false`,
unlike the Inno Setup build): the package is already the container, so an unbundled layout starts
faster and gives Store certification real files to inspect instead of one opaque bundle.

## Submit to the Store

1. Reserve the app name in Partner Center (once), fill in the three identity values above.
2. Build the `.msix` (CI or local).
3. Partner Center ▸ new submission ▸ **Packages** ▸ upload the `.msix`.
4. Complete Store listing (description, screenshots, a 300×300+ Store logo, privacy policy,
   age rating) and submit for certification.

## Certification notes / risks

- **Undocumented COM** (`IVirtualDesktopManagerInternal`, `Services/VirtualDesktopInternal.cs`)
  is a runtime call that automated certification is unlikely to flag, but it is a Store-policy
  grey area and breaks across Windows builds. The keystroke-stepping fallback keeps the app
  working if it ever fails.
- **`runFullTrust`** is a restricted capability. It's allowed for Desktop-Bridge apps but Store
  review may ask you to justify it — the justification is "full-trust Win32 app synthesizing the
  native desktop-switch shortcut and reading virtual-desktop state."
- Verify after packaging: overlay pinning across desktops (`VirtualDesktopPinner.SetAppId`),
  config read/write under `%APPDATA%`, and the startup-task toggle in Task Manager ▸ Startup.
