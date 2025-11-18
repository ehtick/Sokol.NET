#!/bin/bash
# Build script for spine-c library on Linux
# Usage: ./build-spine-c-linux.sh [build_type]
# Example: ./build-spine-c-linux.sh Release
# Example: ./build-spine-c-linux.sh Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SPINE_C_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR="$SPINE_C_DIR/build-linux"

# Parse arguments - default to Release
BUILD_TYPE="${1:-Release}"

echo "=========================================="
echo "Building spine-c for Linux"
echo "Build Type: $BUILD_TYPE"
echo "=========================================="

# Create build directory
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with CMake
cmake .. \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE"

# Build
cmake --build . --config "$BUILD_TYPE" -- -j$(nproc)

echo "=========================================="
echo "Build complete!"
echo "Output: $BUILD_DIR/libspine-c.so"
echo "=========================================="

# Verify the library was created
if [ -f "$BUILD_DIR/libspine-c.so" ]; then
    echo "✓ Successfully built libspine-c.so"
    file "$BUILD_DIR/libspine-c.so"
    
    # Copy to the expected location
    ARCH="X64"  # Use X64 instead of x86_64 to match .NET expectations
    if [ "$BUILD_TYPE" = "Debug" ]; then
        OUTPUT_DIR="$SPINE_C_DIR/libs/linux/$ARCH/debug"
    else
        OUTPUT_DIR="$SPINE_C_DIR/libs/linux/$ARCH/release"
    fi
    
    mkdir -p "$OUTPUT_DIR"
    cp "$BUILD_DIR/libspine-c.so" "$OUTPUT_DIR/"
    
    echo "=========================================="
    echo "✓ Copied to: $OUTPUT_DIR/libspine-c.so"
    echo "=========================================="
else
    echo "✗ Failed to build libspine-c.so"
    exit 1
fi
