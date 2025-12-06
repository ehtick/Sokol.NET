#!/bin/bash

# Build script for sokol framework on tvOS - builds both Release and Debug configurations
# Supports physical devices (arm64) and simulators (arm64 for Apple Silicon, x86_64 for Intel)
# Usage: ./build-tvos-sokol-library.sh [device|simulator-arm64|simulator-x64|all]
# Default: all

set -e

BUILD_TARGET="${1:-all}"

build_for_target() {
    local TARGET=$1
    local ARCH=$2
    local SDK=$3
    local OUTPUT_DIR=$4
    
    echo "=========================================="
    echo "Building sokol framework for tvOS $TARGET ($ARCH)"
    echo "Building both Release and Debug configurations"
    echo "=========================================="
    
    # Clean previous build
    rm -rf "build-xcode-tvos-$TARGET"
    mkdir -p "build-xcode-tvos-$TARGET"
    cd "build-xcode-tvos-$TARGET"
    
    # Configure CMake
    echo "Configuring CMake for tvOS $TARGET..."
    cmake -G Xcode \
        -DCMAKE_SYSTEM_NAME=tvOS \
        -DCMAKE_OSX_DEPLOYMENT_TARGET=14.0 \
        -DCMAKE_OSX_ARCHITECTURES="$ARCH" \
        -DCMAKE_OSX_SYSROOT="$SDK" \
        -DCMAKE_XCODE_ATTRIBUTE_CODE_SIGN_IDENTITY="" \
        -DCMAKE_XCODE_ATTRIBUTE_CODE_SIGNING_REQUIRED=NO \
        -DCMAKE_XCODE_ATTRIBUTE_CODE_SIGNING_ALLOWED=NO \
        ../ext
    
    # Build Release configuration
    echo "Building Release configuration..."
    cmake --build . --config Release
    
    # Build Debug configuration  
    echo "Building Debug configuration..."
    cmake --build . --config Debug
    
    # Create output directories
    mkdir -p "../libs/tvos/$OUTPUT_DIR/release"
    mkdir -p "../libs/tvos/$OUTPUT_DIR/debug"
    
    # Determine build output directory suffix
    if [ "$SDK" = "appletvsimulator" ]; then
        BUILD_SUFFIX="appletvsimulator"
    else
        BUILD_SUFFIX="appletvos"
    fi
    
    # Copy Release framework
    echo "Copying Release framework..."
    cp -rf "Release-$BUILD_SUFFIX/sokol.framework" "../libs/tvos/$OUTPUT_DIR/release/"
    
    # Copy Debug framework
    echo "Copying Debug framework..."
    cp -rf "Debug-$BUILD_SUFFIX/sokol.framework" "../libs/tvos/$OUTPUT_DIR/debug/"
    
    # Cleanup
    cd ..
    rm -rf "build-xcode-tvos-$TARGET"
    
    echo "=========================================="
    echo "Build complete for $TARGET!"
    echo "Release framework: libs/tvos/$OUTPUT_DIR/release/sokol.framework"
    echo "Debug framework: libs/tvos/$OUTPUT_DIR/debug/sokol.framework"
    echo "=========================================="
    
    # Verify frameworks were created
    if [ -d "libs/tvos/$OUTPUT_DIR/release/sokol.framework" ]; then
        echo "✓ Release framework created successfully"
        ls -lah "libs/tvos/$OUTPUT_DIR/release/sokol.framework"
    else
        echo "✗ Failed to create Release framework"
    fi
    
    if [ -d "libs/tvos/$OUTPUT_DIR/debug/sokol.framework" ]; then
        echo "✓ Debug framework created successfully"  
        ls -lah "libs/tvos/$OUTPUT_DIR/debug/sokol.framework"
    else
        echo "✗ Failed to create Debug framework"
    fi
}

# Build based on target
case "$BUILD_TARGET" in
    device)
        build_for_target "device" "arm64" "appletvos" "arm64"
        ;;
    simulator-arm64)
        build_for_target "simulator-arm64" "arm64" "appletvsimulator" "simulator-arm64"
        ;;
    simulator-x64)
        build_for_target "simulator-x64" "x86_64" "appletvsimulator" "simulator-x64"
        ;;
    all)
        echo "Building for all targets: device, simulator-arm64, simulator-x64"
        echo ""
        build_for_target "device" "arm64" "appletvos" "arm64"
        echo ""
        build_for_target "simulator-arm64" "arm64" "appletvsimulator" "simulator-arm64"
        echo ""
        build_for_target "simulator-x64" "x86_64" "appletvsimulator" "simulator-x64"
        echo ""
        echo "=========================================="
        echo "All builds complete!"
        echo "Device: libs/tvos/arm64/{debug,release}/sokol.framework"
        echo "Simulator (Apple Silicon): libs/tvos/simulator-arm64/{debug,release}/sokol.framework"
        echo "Simulator (Intel): libs/tvos/simulator-x64/{debug,release}/sokol.framework"
        echo "=========================================="
        ;;
    *)
        echo "Error: Unknown target '$BUILD_TARGET'"
        echo "Usage: $0 [device|simulator-arm64|simulator-x64|all]"
        exit 1
        ;;
esac
