#!/bin/bash
# Build script for ozzutil library on Linux
# Usage: ./build-ozzutil-linux.sh [architecture] [build_type]
# Example: ./build-ozzutil-linux.sh X64 Release
# Example: ./build-ozzutil-linux.sh arm64 Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOKOL_CHARP_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
OZZUTIL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR="$OZZUTIL_DIR/build-linux"

# Parse arguments
ARCH="${1:-X64}"
BUILD_TYPE="${2:-Release}"

# Normalize architecture for directory naming (x86_64 -> X64)
ARCH_DIR="$ARCH"
if [ "$ARCH" = "x86_64" ]; then
    ARCH_DIR="X64"
fi

echo "=========================================="
echo "Building ozzutil for Linux"
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
if [ ! -d "$OZZ_ANIMATION_DIR/bin/linux/$ARCH/$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')" ]; then
    echo "ozz-animation libraries not found. Building them now..."
    if [ -f "$OZZ_ANIMATION_DIR/scripts/build-ozz-animation-linux.sh" ]; then
        "$OZZ_ANIMATION_DIR/scripts/build-ozz-animation-linux.sh" "$BUILD_TYPE"
    else
        echo "Error: ozz-animation build script not found at $OZZ_ANIMATION_DIR/scripts/build-ozz-animation-linux.sh"
        exit 1
    fi
fi

# Create build directory
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Set architecture-specific flags
CMAKE_FLAGS=""
if [ "$ARCH" = "arm64" ] || [ "$ARCH" = "aarch64" ]; then
    CMAKE_FLAGS="-DCMAKE_SYSTEM_PROCESSOR=aarch64"
fi

# Configure with CMake
cmake .. \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -DCMAKE_POSITION_INDEPENDENT_CODE=ON \
    $CMAKE_FLAGS

# Build
make -j$(nproc)

# Create destination directory using normalized architecture
DEST_DIR="$OZZUTIL_DIR/libs/linux/$ARCH_DIR/$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')"
mkdir -p "$DEST_DIR"

# Copy library to destination
echo "Copying library to $DEST_DIR..."
cp "$BUILD_DIR/libozzutil.so" "$DEST_DIR/libozzutil.so" 2>/dev/null || \
find "$BUILD_DIR" -name "libozzutil.so" -exec cp {} "$DEST_DIR/libozzutil.so" \; 2>/dev/null || true

echo "=========================================="
echo "Build complete!"
echo "Output: $DEST_DIR"
echo "=========================================="

# Verify the library was created
if [ -f "$DEST_DIR/libozzutil.so" ]; then
    echo "✓ Successfully built ozzutil library"
    ls -lh "$DEST_DIR"/*.so
    
    # Check library dependencies
    echo ""
    echo "Library dependencies:"
    ldd "$DEST_DIR/libozzutil.so" 2>/dev/null || echo "Could not check dependencies"
else
    echo "✗ Failed to build ozzutil library"
    echo "Available files in build directory:"
    find "$BUILD_DIR" -name "*.so" -type f 2>/dev/null || echo "No .so files found"
    exit 1
fi