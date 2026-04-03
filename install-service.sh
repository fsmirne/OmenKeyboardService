#!/bin/bash

# HP Omen Keyboard RGB Service Installation Script for Ubuntu/Linux
# This script must be run with sudo/root privileges

set -e

SERVICE_NAME="omen-keyboard-rgb"
INSTALL_DIR="/usr/local/bin/omen-keyboard-rgb"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"

echo "======================================"
echo "HP Omen Keyboard RGB Service Installer"
echo "======================================"
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: This script must be run as root (use sudo)"
    exit 1
fi

# Check if build directory exists
if [ ! -f "./OmenKeyboardService" ]; then
    echo "ERROR: OmenKeyboardService binary not found in current directory"
    echo "Please run 'dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true' first"
    exit 1
fi

echo "Step 1: Creating installation directory..."
mkdir -p "$INSTALL_DIR"

echo "Step 2: Copying service files..."
cp ./OmenKeyboardService "$INSTALL_DIR/"
cp ./config.json "$INSTALL_DIR/"
chmod +x "$INSTALL_DIR/OmenKeyboardService"

echo "Step 3: Setting up udev rules for keyboard access..."
cat > /etc/udev/rules.d/99-omen-keyboard.rules << 'EOF'
# HP Omen Sequencer Keyboard - Allow access to HID device
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="03f0", ATTRS{idProduct}=="1f41", MODE="0666"
SUBSYSTEM=="usb", ATTRS{idVendor}=="03f0", ATTRS{idProduct}=="1f41", MODE="0666"
EOF

echo "Step 4: Reloading udev rules..."
udevadm control --reload-rules
udevadm trigger

echo "Step 5: Installing systemd service..."
cp ./omen-keyboard-rgb.service "$SERVICE_FILE"

echo "Step 6: Reloading systemd daemon..."
systemctl daemon-reload

echo "Step 7: Enabling service to start at boot..."
systemctl enable $SERVICE_NAME

echo "Step 8: Starting service..."
systemctl start $SERVICE_NAME

echo ""
echo "======================================"
echo "Installation Complete!"
echo "======================================"
echo ""
echo "Service Status:"
systemctl status $SERVICE_NAME --no-pager || true
echo ""
echo "Useful Commands:"
echo "  Check status:    sudo systemctl status $SERVICE_NAME"
echo "  View logs:       sudo journalctl -u $SERVICE_NAME -f"
echo "  Restart service: sudo systemctl restart $SERVICE_NAME"
echo "  Stop service:    sudo systemctl stop $SERVICE_NAME"
echo "  Disable service: sudo systemctl disable $SERVICE_NAME"
echo ""
echo "Configuration file: $INSTALL_DIR/config.json"
echo "Edit the config file and the service will automatically reload it."
echo ""
