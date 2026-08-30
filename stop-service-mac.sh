#!/bin/bash

# Stop HP Omen Keyboard RGB Service (macOS)
# This script must be run with sudo/root privileges

SERVICE_LABEL="local.omen-keyboard-rgb"
PLIST_FILE="/Library/LaunchDaemons/${SERVICE_LABEL}.plist"

echo "Stopping HP Omen Keyboard RGB Service..."
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: This script must be run as root (use sudo)"
    exit 1
fi

launchctl bootout system "$PLIST_FILE"

if [ $? -ne 0 ]; then
    echo "ERROR: Failed to stop service!"
    echo "Run 'sudo launchctl print system/$SERVICE_LABEL' for details"
    exit 1
fi

echo ""
echo "Service stopped successfully!"
echo ""
