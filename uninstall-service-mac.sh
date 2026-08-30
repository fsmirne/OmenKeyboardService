#!/bin/bash

# HP Omen Keyboard RGB Service Uninstallation Script for macOS
# This script must be run with sudo/root privileges

set -e

SERVICE_LABEL="local.omen-keyboard-rgb"
INSTALL_DIR="/usr/local/bin/omen-keyboard-rgb"
PLIST_FILE="/Library/LaunchDaemons/${SERVICE_LABEL}.plist"

echo "=========================================="
echo "HP Omen Keyboard RGB Service Uninstaller"
echo "=========================================="
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: This script must be run as root (use sudo)"
    exit 1
fi

echo "Step 1: Stopping and unloading service..."
launchctl bootout system "$PLIST_FILE" 2>/dev/null || true

echo "Step 2: Removing launchd job file..."
rm -f "$PLIST_FILE"

echo "Step 3: Removing installation directory..."
rm -rf "$INSTALL_DIR"

echo "Step 4: Removing log file..."
rm -f /var/log/omen-keyboard-rgb.log

echo ""
echo "=========================================="
echo "Uninstallation Complete!"
echo "=========================================="
echo ""
