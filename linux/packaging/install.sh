#!/usr/bin/env bash
# Per-user installer for DeskCue (Linux/X11).
# Installs the binary to ~/.local/bin and a menu entry to ~/.local/share/applications.
set -euo pipefail

cd "$(dirname "$0")"

BIN_DIR="$HOME/.local/bin"
APP_DIR="$HOME/.local/share/applications"
AUTOSTART_DIR="$HOME/.config/autostart"
BIN="$BIN_DIR/deskcue"

mkdir -p "$BIN_DIR" "$APP_DIR"
install -m 755 deskcue "$BIN"

cat > "$APP_DIR/deskcue.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=DeskCue
Comment=Show and switch the current virtual desktop
Exec=$BIN
Terminal=false
Categories=Utility;
EOF

# The binary and the .desktop files were named virtual-desktop-indicator before the rename.
# Leaving them behind would keep a stale menu entry, and a stale autostart entry pointing at a
# binary this install no longer ships. Settings are migrated by the app itself, not here.
LEGACY_BIN="$BIN_DIR/virtual-desktop-indicator"
LEGACY_DESKTOP="$APP_DIR/virtual-desktop-indicator.desktop"
LEGACY_AUTOSTART="$AUTOSTART_DIR/virtual-desktop-indicator.desktop"
if [ -e "$LEGACY_BIN" ] || [ -e "$LEGACY_DESKTOP" ] || [ -e "$LEGACY_AUTOSTART" ]; then
  # An enabled autostart entry is re-created under the new name so "run at login" survives.
  if [ -e "$LEGACY_AUTOSTART" ] && ! grep -qi '^Hidden[[:space:]]*=[[:space:]]*true' "$LEGACY_AUTOSTART"; then
    mkdir -p "$AUTOSTART_DIR"
    sed "s|^Exec=.*|Exec=$BIN|" "$LEGACY_AUTOSTART" > "$AUTOSTART_DIR/deskcue.desktop"
    echo "Carried the autostart entry over to deskcue.desktop"
  fi
  rm -f "$LEGACY_BIN" "$LEGACY_DESKTOP" "$LEGACY_AUTOSTART"
  echo "Removed the pre-rename install (virtual-desktop-indicator)"
fi

echo "Installed:"
echo "  $BIN"
echo "  $APP_DIR/deskcue.desktop"
echo
if ! echo "$PATH" | tr ':' '\n' | grep -qx "$BIN_DIR"; then
  echo "Note: $BIN_DIR is not on your PATH. Launch it from your app menu,"
  echo "      or run it directly: $BIN"
else
  echo "Launch it with: deskcue"
fi
echo "Enable 'Run at login' from the tray menu or the Settings window."
