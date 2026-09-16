#!/bin/bash
# Build libsokol_speech.dylib for macOS — standalone Speech plugin library.
# Output: plugins/Speech/libs/macos/{arm64,X64}/release/libsokol_speech.dylib
# Run from any directory; the script resolves the repo root automatically.
#
# Usage: ./plugins/Speech/scripts/build-macos.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
PLUGIN_NATIVE="$REPO_ROOT/plugins/Speech/native"

echo "========================================="
echo "sokol_speech — macOS build"
echo "========================================="

for ARCH in arm64 x86_64; do
    ARCH_DIR="arm64"
    [ "$ARCH" = "x86_64" ] && ARCH_DIR="X64"

    echo ""
    echo "----- arch: $ARCH ($ARCH_DIR) -----"

    BUILD_DIR="$REPO_ROOT/build-sokol-speech-macos-$ARCH"
    rm -rf "$BUILD_DIR"

    cmake -S "$PLUGIN_NATIVE" -B "$BUILD_DIR" \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_OSX_ARCHITECTURES="$ARCH"

    cmake --build "$BUILD_DIR" --config Release

    rm -rf "$BUILD_DIR"

    echo "  -> plugins/Speech/libs/macos/$ARCH_DIR/release/libsokol_speech.dylib"
done

echo ""
echo "========================================="
echo "sokol_speech — macOS done!"
echo "========================================="
