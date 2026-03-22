#!/bin/bash

# HaTTaPtic Build Script
# Builds the plugin in Docker and creates a .lplug4 package

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

CONFIG="${1:-Release}"
PLUGIN_NAME="HaTTaPtic"
LOGI_PLUGINS="$HOME/Library/Application Support/Logi/LogiPluginService/Plugins"

echo "========================================"
echo "Building $PLUGIN_NAME ($CONFIG)"
echo "========================================"

# Check for Docker
if ! command -v docker &> /dev/null; then
    echo ""
    echo "Error: Docker not found"
    echo "Install Docker Desktop from https://www.docker.com/products/docker-desktop/"
    exit 1
fi

# Check for lib/ DLLs
if [ ! -f "lib/PluginApi.dll" ]; then
    echo ""
    echo "Error: lib/PluginApi.dll not found"
    echo ""
    echo "Run: ./scripts/sync-libs.sh"
    echo "Or:  git pull (DLLs are committed in lib/)"
    exit 1
fi

echo ""
echo "Using PluginApi version: $(cat lib/.pluginapi-version 2>/dev/null || echo 'unknown')"

# Build with Docker
echo ""
echo "Building in Docker..."
docker build --no-cache --target output --output "type=local,dest=./bin/$CONFIG" -f Dockerfile .

echo ""
echo "Build complete!"
echo ""

# Create .lplug4 package (it's just a zip)
echo "Creating plugin package..."
cd "bin/$CONFIG/output"
zip -r "$SCRIPT_DIR/$PLUGIN_NAME.lplug4" . -x "*.pdb"
cd "$SCRIPT_DIR"
echo "  -> $PLUGIN_NAME.lplug4"

# Install: create link file for development
echo ""
mkdir -p "$LOGI_PLUGINS"
echo "$SCRIPT_DIR/bin/$CONFIG/output/" > "$LOGI_PLUGINS/${PLUGIN_NAME}Plugin.link"
echo "Plugin linked for development."

echo ""
echo "========================================"
echo "Installation"
echo "========================================"
echo ""
echo "The plugin has been linked to Logi Plugin Service."
echo ""
echo "Next steps:"
echo "  1. Restart Logi Options+ (or logout/login)"
echo "  2. Test with:"
echo "     curl http://127.0.0.1:18274/health"
echo "     curl http://127.0.0.1:18274/haptic/knock"
echo ""
echo "To distribute:"
echo "  Share $PLUGIN_NAME.lplug4 — double-click to install."
echo ""
