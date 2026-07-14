#!/usr/bin/env bash
# Per-user installer for Virtual Desktop Indicator (Linux/X11).
# Installs the binary to ~/.local/bin and a menu entry to ~/.local/share/applications.
set -euo pipefail

cd "$(dirname "$0")"

BIN_DIR="$HOME/.local/bin"
APP_DIR="$HOME/.local/share/applications"
BIN="$BIN_DIR/virtual-desktop-indicator"

mkdir -p "$BIN_DIR" "$APP_DIR"
install -m 755 virtual-desktop-indicator "$BIN"

cat > "$APP_DIR/virtual-desktop-indicator.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Virtual Desktop Indicator
Comment=Show and switch the current virtual desktop
Exec=$BIN
Terminal=false
Categories=Utility;
EOF

echo "Installed:"
echo "  $BIN"
echo "  $APP_DIR/virtual-desktop-indicator.desktop"
echo
if ! echo "$PATH" | tr ':' '\n' | grep -qx "$BIN_DIR"; then
  echo "Note: $BIN_DIR is not on your PATH. Launch it from your app menu,"
  echo "      or run it directly: $BIN"
else
  echo "Launch it with: virtual-desktop-indicator"
fi
echo "Enable 'Run at login' from the tray menu or the Settings window."
