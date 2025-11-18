#!/bin/bash
# Build script for ozzutil library on macOS
# Usage: ./build-ozzutil-macos.sh [architecture] [build_type]
# Example: ./build-ozzutil-macos.sh arm64 Release
# Example: ./build-ozzutil-macos.sh x86_64 Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOKOL_CHARP_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
OZZUTIL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR="$OZZUTIL_DIR/build-xcode-macos"

# Parse arguments
ARCH="${1:-arm64}"
BUILD_TYPE="${2:-Release}"

# Normalize architecture for directory naming (x86_64 -> X64)
ARCH_DIR="$ARCH"
if [ "$ARCH" = "x86_64" ]; then
    ARCH_DIR="X64"
fi

echo "=========================================="
echo "Building ozzutil for macOS"
echo "Architecture: $ARCH"
echo "Build Type: $BUILD_TYPE"
echo "=========================================="

# Check if ozzutil directory exists
if [ ! -d "$OZZUTIL_DIR" ]; then
    echo "Error: ozzutil directory not found at $OZZUTIL_DIR"
    exit 1
fi

# Check if ozz-animation libraries exist
OZZ_ANIMATION_DIR="$SOKOL_CHARP_ROOT/ext/ozz-animation"
if [ ! -d "$OZZ_ANIMATION_DIR/bin/macos/$ARCH_DIR/$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')" ]; then
    echo "ozz-animation libraries not found. Building them now..."
    if [ -f "$OZZ_ANIMATION_DIR/scripts/build-ozz-animation-macos.sh" ]; then
        "$OZZ_ANIMATION_DIR/scripts/build-ozz-animation-macos.sh" "$ARCH" "$BUILD_TYPE"
    else
        echo "Error: ozz-animation build script not found at $OZZ_ANIMATION_DIR/scripts/build-ozz-animation-macos.sh"
        exit 1
    fi
fi

# Create build directory
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with CMake
cmake .. \
    -G Xcode \
    -DCMAKE_OSX_ARCHITECTURES="$ARCH" \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -DCMAKE_OSX_DEPLOYMENT_TARGET="11.0"

# Build
cmake --build . --config "$BUILD_TYPE"

# Create destination directory using normalized architecture
DEST_DIR="$OZZUTIL_DIR/libs/macos/$ARCH_DIR/$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')"
mkdir -p "$DEST_DIR"

# Copy library to destination
echo "Copying library to $DEST_DIR..."
cp "$BUILD_DIR/$BUILD_TYPE/libozzutil.dylib" "$DEST_DIR/libozzutil.dylib" 2>/dev/null || \
cp "$BUILD_DIR/libozzutil.dylib" "$DEST_DIR/libozzutil.dylib" 2>/dev/null || \
cp "$BUILD_DIR/Debug/libozzutil.dylib" "$DEST_DIR/libozzutil.dylib" 2>/dev/null || true

echo "=========================================="
echo "Build complete!"
echo "Output: $DEST_DIR"
echo "=========================================="

# Verify the library was created
if [ -f "$DEST_DIR/libozzutil.dylib" ]; then
    echo "✓ Successfully built ozzutil library"
    ls -lh "$DEST_DIR"/*.dylib
    
    # Sign the library for macOS
    codesign --force --sign - "$DEST_DIR/libozzutil.dylib"
    echo "✓ Library signed successfully"
else
    echo "✗ Failed to build ozzutil library"
    echo "Available files in build directory:"
    find "$BUILD_DIR" -name "*.dylib" -type f 2>/dev/null || echo "No .dylib files found"
    exit 1
fi