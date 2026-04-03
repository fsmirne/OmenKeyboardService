#!/bin/bash

# Stop HP Omen Keyboard RGB Service
# This script must be run with sudo/root privileges

SERVICE_NAME="omen-keyboard-rgb"

echo "Stopping HP Omen Keyboard RGB Service..."
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: This script must be run as root (use sudo)"
    exit 1
fi

# Stop the service
systemctl stop $SERVICE_NAME

if [ $? -ne 0 ]; then
    echo "ERROR: Failed to stop service!"
    echo "Run 'sudo systemctl status $SERVICE_NAME' for details"
    exit 1
fi

echo ""
echo "Service stopped successfully!"
echo ""
