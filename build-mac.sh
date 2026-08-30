#!/bin/bash

# Build script for HP Omen Keyboard RGB Service (macOS)

set -e

echo "======================================"
echo "Building HP Omen Keyboard RGB Service"
echo "======================================"
echo ""

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK not found. Please install .NET 10 SDK first."
    echo "Visit: https://docs.microsoft.com/en-us/dotnet/core/install/macos"
    exit 1
fi

if [ "$(uname -m)" = "arm64" ]; then
    RID="osx-arm64"
else
    RID="osx-x64"
fi

echo "Building project for macOS ($RID)..."
dotnet publish -c Release -r "$RID" --self-contained true /p:PublishSingleFile=true

PUBLISH_DIR="bin/Release/net10.0/$RID/publish"

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
    echo "  2. chmod +x install-service-mac.sh"
    echo "  3. sudo ./install-service-mac.sh"
    echo ""
else
    echo "ERROR: Build failed. Output directory not found."
    exit 1
fi
