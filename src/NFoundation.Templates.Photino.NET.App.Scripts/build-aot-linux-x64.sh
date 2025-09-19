#!/bin/bash

# Get script directory and navigate to project
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/../NFoundation.Templates.Photino.NET.App"

# Detect the current platform
OS=$(uname -s)
ARCH=$(uname -m)

echo "Building AOT-optimized Photino.NET application for Linux (x64)..."

cd "$PROJECT_DIR"

echo ""
echo "Regular development build:"
dotnet build
if [ $? -ne 0 ]; then
    echo ""
    echo "========================================"
    echo "BUILD FAILED!"
    echo "========================================"
    exit 1
fi

echo ""
echo "AOT publish for Linux (x64):"
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishAot=true
if [ $? -ne 0 ]; then
    echo ""
    echo "========================================"
    echo "BUILD FAILED!"
    echo "========================================"
    exit 1
fi

OUTPUT_PATH="$PROJECT_DIR/bin/Release/net8.0/linux-x64/publish/"

echo ""
echo "========================================"
echo "BUILD SUCCESSFUL!"
echo "========================================"
echo "Output location: $OUTPUT_PATH"
echo "Executable: ${OUTPUT_PATH}NFoundation.Templates.Photino.NET.App"
echo ""
echo "Files created:"
ls -la "$OUTPUT_PATH"
echo ""
echo "To run the application:"
echo "cd \"$OUTPUT_PATH\""
echo "./NFoundation.Templates.Photino.NET.App"
echo ""
echo "Note: This binary is optimized for x64 processors"
echo "========================================"