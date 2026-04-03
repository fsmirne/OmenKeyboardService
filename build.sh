#!/bin/bash

# Build script for HP Omen Keyboard RGB Service (Linux)

set -e

echo "======================================"
echo "Building HP Omen Keyboard RGB Service"
echo "======================================"
echo ""

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK not found. Please install .NET 10 SDK first."
    echo "Visit: https://docs.microsoft.com/en-us/dotnet/core/install/linux"
    exit 1
fi

echo "Building project for Linux x64..."
dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true

PUBLISH_DIR="bin/Release/net10.0/linux-x64/publish"

if [ -d "$PUBLISH_DIR" ]; then
    echo ""
    echo "======================================"
    echo "Build Complete!"
    echo "======================================"
    echo ""
    echo "Output directory: $PUBLISH_DIR"
    echo ""
    echo "Next steps:"
    echo "  1. cd $PUBLISH_DIR"
    echo "  2. chmod +x install-service.sh"
    echo "  3. sudo ./install-service.sh"
    echo ""
else
    echo "ERROR: Build failed. Output directory not found."
    exit 1
fi
