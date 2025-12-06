
#!/bin/bash

# Build script for sokol framework on iOS - builds both Release and Debug configurations
# Supports physical devices (arm64) and simulators (arm64 for Apple Silicon, x86_64 for Intel)
# Usage: ./build-ios-sokol-library.sh [device|simulator-arm64|simulator-x64|all]
# Default: all

set -e

BUILD_TARGET="${1:-all}"

build_for_target() {
    local TARGET=$1
    local ARCH=$2
    local SDK=$3
    local OUTPUT_DIR=$4
    
    echo "=========================================="
    echo "Building sokol framework for iOS $TARGET ($ARCH)"
    echo "Building both Release and Debug configurations"
    echo "=========================================="
    
    # Clean previous build
    rm -rf "build-xcode-ios-$TARGET"
    mkdir -p "build-xcode-ios-$TARGET"
    cd "build-xcode-ios-$TARGET"
    
    # Configure CMake
    echo "Configuring CMake for iOS $TARGET..."
    cmake -G Xcode \
        -DCMAKE_SYSTEM_NAME=iOS \
        -DCMAKE_OSX_DEPLOYMENT_TARGET=14.0 \
        -DCMAKE_OSX_ARCHITECTURES="$ARCH" \
        -DCMAKE_OSX_SYSROOT="$SDK" \
        ../ext
    
    # Build Release configuration
    echo "Building Release configuration..."
    cmake --build . --config Release
    
    # Build Debug configuration  
    echo "Building Debug configuration..."
    cmake --build . --config Debug
    
    # Create output directories
    mkdir -p "../libs/ios/$OUTPUT_DIR/release"
    mkdir -p "../libs/ios/$OUTPUT_DIR/debug"
    
    # Determine build output directory suffix
    if [ "$SDK" = "iphonesimulator" ]; then
        BUILD_SUFFIX="iphonesimulator"
    else
        BUILD_SUFFIX="iphoneos"
    fi
    
    # Copy Release framework
    echo "Copying Release framework..."
    cp -rf "Release-$BUILD_SUFFIX/sokol.framework" "../libs/ios/$OUTPUT_DIR/release/"
    
    # Copy Debug framework
    echo "Copying Debug framework..."
    cp -rf "Debug-$BUILD_SUFFIX/sokol.framework" "../libs/ios/$OUTPUT_DIR/debug/"
    
    # Cleanup
    cd ..
    rm -rf "build-xcode-ios-$TARGET"
    
    echo "=========================================="
    echo "Build complete for $TARGET!"
    echo "Release framework: libs/ios/$OUTPUT_DIR/release/sokol.framework"
    echo "Debug framework: libs/ios/$OUTPUT_DIR/debug/sokol.framework"
    echo "=========================================="
    
    # Verify frameworks were created
    if [ -d "libs/ios/$OUTPUT_DIR/release/sokol.framework" ]; then
        echo "✓ Release framework created successfully"
        ls -lah "libs/ios/$OUTPUT_DIR/release/sokol.framework"
    else
        echo "✗ Failed to create Release framework"
    fi
    
    if [ -d "libs/ios/$OUTPUT_DIR/debug/sokol.framework" ]; then
        echo "✓ Debug framework created successfully"  
        ls -lah "libs/ios/$OUTPUT_DIR/debug/sokol.framework"
    else
        echo "✗ Failed to create Debug framework"
    fi
}

# Build based on target
case "$BUILD_TARGET" in
    device)
        build_for_target "device" "arm64" "iphoneos" "arm64"
        ;;
    simulator-arm64)
        build_for_target "simulator-arm64" "arm64" "iphonesimulator" "simulator-arm64"
        ;;
    simulator-x64)
        build_for_target "simulator-x64" "x86_64" "iphonesimulator" "simulator-x64"
        ;;
    all)
        echo "Building for all targets: device, simulator-arm64, simulator-x64"
        echo ""
        build_for_target "device" "arm64" "iphoneos" "arm64"
        echo ""
        build_for_target "simulator-arm64" "arm64" "iphonesimulator" "simulator-arm64"
        echo ""
        build_for_target "simulator-x64" "x86_64" "iphonesimulator" "simulator-x64"
        echo ""
        echo "=========================================="
        echo "All builds complete!"
        echo "Device: libs/ios/arm64/{debug,release}/sokol.framework"
        echo "Simulator (Apple Silicon): libs/ios/simulator-arm64/{debug,release}/sokol.framework"
        echo "Simulator (Intel): libs/ios/simulator-x64/{debug,release}/sokol.framework"
        echo "=========================================="
        ;;
    *)
        echo "Error: Unknown target '$BUILD_TARGET'"
        echo "Usage: $0 [device|simulator-arm64|simulator-x64|all]"
        exit 1
        ;;
esac
