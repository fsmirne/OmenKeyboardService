#!/bin/bash

# HP Omen Keyboard RGB Service Uninstallation Script for Ubuntu/Linux
# This script must be run with sudo/root privileges

set -e

SERVICE_NAME="omen-keyboard-rgb"
INSTALL_DIR="/usr/local/bin/omen-keyboard-rgb"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
UDEV_RULES="/etc/udev/rules.d/99-omen-keyboard.rules"

echo "=========================================="
echo "HP Omen Keyboard RGB Service Uninstaller"
echo "=========================================="
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: This script must be run as root (use sudo)"
    exit 1
fi

echo "Step 1: Stopping service..."
systemctl stop $SERVICE_NAME || true

echo "Step 2: Disabling service..."
systemctl disable $SERVICE_NAME || true

echo "Step 3: Removing systemd service file..."
rm -f "$SERVICE_FILE"

echo "Step 4: Reloading systemd daemon..."
systemctl daemon-reload

echo "Step 5: Removing installation directory..."
rm -rf "$INSTALL_DIR"

echo "Step 6: Removing udev rules..."
rm -f "$UDEV_RULES"

echo "Step 7: Reloading udev rules..."
udevadm control --reload-rules
udevadm trigger

echo ""
echo "=========================================="
echo "Uninstallation Complete!"
echo "=========================================="
echo ""
