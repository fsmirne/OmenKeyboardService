#!/bin/bash

# HP Omen Keyboard RGB Service Installation Script for macOS
# This script must be run with sudo/root privileges

set -e

SERVICE_LABEL="local.omen-keyboard-rgb"
INSTALL_DIR="/usr/local/bin/omen-keyboard-rgb"
PLIST_FILE="/Library/LaunchDaemons/${SERVICE_LABEL}.plist"

echo "======================================"
echo "HP Omen Keyboard RGB Service Installer"
echo "======================================"
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: This script must be run as root (use sudo)"
    exit 1
fi

# Check if build output exists
if [ ! -f "./OmenKeyboardService" ]; then
    echo "ERROR: OmenKeyboardService binary not found in current directory"
    echo "Please run 'dotnet publish -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true' first"
    echo "(use -r osx-x64 instead for Intel Macs)"
    exit 1
fi

echo "Step 1: Unloading any existing service..."
launchctl bootout system "$PLIST_FILE" 2>/dev/null || true

echo "Step 2: Creating installation directory..."
mkdir -p "$INSTALL_DIR"

echo "Step 3: Copying service files..."
cp ./OmenKeyboardService "$INSTALL_DIR/"
cp ./config.json "$INSTALL_DIR/"
chmod +x "$INSTALL_DIR/OmenKeyboardService"

echo "Step 4: Installing launchd job..."
cp ./local.omen-keyboard-rgb.plist "$PLIST_FILE"
chown root:wheel "$PLIST_FILE"
chmod 644 "$PLIST_FILE"

echo "Step 5: Loading and starting the service..."
launchctl bootstrap system "$PLIST_FILE"

echo ""
echo "======================================"
echo "Installation Complete!"
echo "======================================"
echo ""
echo "Service Status:"
launchctl print system/$SERVICE_LABEL 2>/dev/null | head -20 || true
echo ""
echo "Useful Commands:"
echo "  Check status:    sudo launchctl print system/$SERVICE_LABEL"
echo "  View logs:       tail -f /var/log/omen-keyboard-rgb.log"
echo "  Restart service: sudo launchctl kickstart -k system/$SERVICE_LABEL"
echo "  Stop service:    sudo launchctl bootout system/$SERVICE_LABEL"
echo ""
echo "Configuration file: $INSTALL_DIR/config.json"
echo "Edit the config file and the service will automatically reload it."
echo ""
echo "IMPORTANT: macOS may require you to grant 'Input Monitoring' permission for the"
echo "keyboard's vendor HID interface. If colors don't apply, go to System Settings ->"
echo "Privacy & Security -> Input Monitoring and allow OmenKeyboardService."
echo ""
