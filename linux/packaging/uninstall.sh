#!/usr/bin/env bash
# Removes Virtual Desktop Indicator (binary, menu entry, autostart entry).
# Leaves the config under ~/.config/VirtualDesktopIndicator untouched.
set -euo pipefail

rm -f "$HOME/.local/bin/virtual-desktop-indicator"
rm -f "$HOME/.local/share/applications/virtual-desktop-indicator.desktop"
rm -f "$HOME/.config/autostart/virtual-desktop-indicator.desktop"

echo "Removed Virtual Desktop Indicator."
echo "Config kept at ~/.config/VirtualDesktopIndicator (delete manually if desired)."
