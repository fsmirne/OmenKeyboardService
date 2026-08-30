#!/bin/bash

# Start HP Omen Keyboard RGB Service (macOS)
# This script must be run with sudo/root privileges

SERVICE_LABEL="local.omen-keyboard-rgb"
PLIST_FILE="/Library/LaunchDaemons/${SERVICE_LABEL}.plist"

echo "Starting HP Omen Keyboard RGB Service..."
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: This script must be run as root (use sudo)"
    exit 1
fi

launchctl bootstrap system "$PLIST_FILE"

if [ $? -ne 0 ]; then
    echo "ERROR: Failed to start service!"
    echo "Run 'sudo launchctl print system/$SERVICE_LABEL' for details"
    exit 1
fi

echo ""
echo "Service started successfully!"
echo ""
echo "Check status: sudo launchctl print system/$SERVICE_LABEL"
echo "View logs:    tail -f /var/log/omen-keyboard-rgb.log"
echo ""
