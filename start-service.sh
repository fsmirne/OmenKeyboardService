#!/bin/bash

# Start HP Omen Keyboard RGB Service
# This script must be run with sudo/root privileges

SERVICE_NAME="omen-keyboard-rgb"

echo "Starting HP Omen Keyboard RGB Service..."
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: This script must be run as root (use sudo)"
    exit 1
fi

# Start the service
systemctl start $SERVICE_NAME

if [ $? -ne 0 ]; then
    echo "ERROR: Failed to start service!"
    echo "Run 'sudo systemctl status $SERVICE_NAME' for details"
    exit 1
fi

echo ""
echo "Service started successfully!"
echo ""
echo "Check status: sudo systemctl status $SERVICE_NAME"
echo "View logs:    sudo journalctl -u $SERVICE_NAME -f"
echo ""
