#!/bin/bash

# Sync Logi Options+ runtime DLLs from local installation to lib/
# Run this after a Logi Options+ update to keep dependencies current.

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LIB_DIR="$SCRIPT_DIR/../lib"
MONO_BUNDLE="/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle"

REQUIRED_DLLS=(
    "PluginApi.dll"
    "ExCSS.dll"
    "LogiEventTracing.dll"
    "Newtonsoft.Json.dll"
    "ShimSkiaSharp.dll"
    "SkiaSharp.dll"
    "Svg.Custom.dll"
    "Svg.Model.dll"
    "Svg.Skia.dll"
    "websocket-sharp.dll"
    "YamlDotNet.dll"
)

if [ ! -d "$MONO_BUNDLE" ]; then
    echo "Error: Logi Options+ not found at $MONO_BUNDLE"
    echo "Make sure Logi Options+ is installed."
    exit 1
fi

echo "Syncing DLLs from Logi Options+..."
mkdir -p "$LIB_DIR"

for dll in "${REQUIRED_DLLS[@]}"; do
    if [ -f "$MONO_BUNDLE/$dll" ]; then
        cp "$MONO_BUNDLE/$dll" "$LIB_DIR/$dll"
        echo "  $dll"
    else
        echo "  WARNING: $dll not found in $MONO_BUNDLE"
    fi
done

# Record version info
echo "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "$LIB_DIR/.synced-at"
strings "$MONO_BUNDLE/PluginApi.dll" | grep -E "^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$" | head -1 > "$LIB_DIR/.pluginapi-version"

echo ""
echo "Done. PluginApi version: $(cat "$LIB_DIR/.pluginapi-version")"
echo "Synced at: $(cat "$LIB_DIR/.synced-at")"
echo ""
echo "Next: git add lib/ && git commit -m 'chore: sync Logi Options+ runtime DLLs'"
