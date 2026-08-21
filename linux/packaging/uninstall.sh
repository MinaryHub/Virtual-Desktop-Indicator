#!/usr/bin/env bash
# Removes DeskCue (binary, menu entry, autostart entry).
# Leaves the config under ~/.config/DeskCue untouched.
set -euo pipefail

rm -f "$HOME/.local/bin/deskcue"
rm -f "$HOME/.local/share/applications/deskcue.desktop"
rm -f "$HOME/.config/autostart/deskcue.desktop"

# Pre-rename names, in case this machine was never upgraded through install.sh.
rm -f "$HOME/.local/bin/virtual-desktop-indicator"
rm -f "$HOME/.local/share/applications/virtual-desktop-indicator.desktop"
rm -f "$HOME/.config/autostart/virtual-desktop-indicator.desktop"

echo "Removed DeskCue."
echo "Config kept at ~/.config/DeskCue (delete manually if desired)."
