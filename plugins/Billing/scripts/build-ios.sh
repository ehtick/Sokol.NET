#!/bin/bash
# Build sokol_billing.framework for iOS — standalone Billing plugin library.
# Output: libs/ios/<target>/{debug,release}/sokol_billing.framework
# Run from any directory; the script resolves the repo root automatically.
#
# Usage: ./plugins/Billing/scripts/build-ios.sh [device|simulator-arm64|simulator-x64|all]
# Default: all
#
# StoreKit 2 is a Swift-only API, so the framework compiles a Swift shim with
# @_cdecl entry points plus the shared C event queue. Deployment target 15.0
# (the StoreKit 2 floor). No link against the main sokol framework is required.

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
PLUGIN_NATIVE="$REPO_ROOT/plugins/Billing/native"

BUILD_TARGET="${1:-all}"

build_for_target() {
    local TARGET=$1
    local ARCH=$2
    local SDK=$3
    local OUTPUT_DIR=$4

    echo "========================================="
    echo "sokol_billing — iOS $TARGET ($ARCH)"
    echo "========================================="

    local BUILD_DIR="$REPO_ROOT/build-sokol-billing-ios-$TARGET"
    rm -rf "$BUILD_DIR"

    cmake -S "$PLUGIN_NATIVE" -B "$BUILD_DIR" -G Xcode \
        -DCMAKE_SYSTEM_NAME=iOS \
        -DCMAKE_OSX_DEPLOYMENT_TARGET=15.0 \
        -DCMAKE_OSX_ARCHITECTURES="$ARCH" \
        -DCMAKE_OSX_SYSROOT="$SDK"

    echo "Building Release..."
    cmake --build "$BUILD_DIR" --config Release

    echo "Building Debug..."
    cmake --build "$BUILD_DIR" --config Debug

    rm -rf "$BUILD_DIR"

    echo "Done — $TARGET"
    echo "  Release: plugins/Billing/libs/ios/$OUTPUT_DIR/release/sokol_billing.framework"
    echo "  Debug:   plugins/Billing/libs/ios/$OUTPUT_DIR/debug/sokol_billing.framework"
}

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
        build_for_target "device"          "arm64"  "iphoneos"        "arm64"
        build_for_target "simulator-arm64" "arm64"  "iphonesimulator" "simulator-arm64"
        build_for_target "simulator-x64"   "x86_64" "iphonesimulator" "simulator-x64"
        echo "========================================="
        echo "sokol_billing — iOS all targets done!"
        echo "  Device:              plugins/Billing/libs/ios/arm64/{debug,release}/sokol_billing.framework"
        echo "  Simulator (arm64):   plugins/Billing/libs/ios/simulator-arm64/{debug,release}/sokol_billing.framework"
        echo "  Simulator (x86_64):  plugins/Billing/libs/ios/simulator-x64/{debug,release}/sokol_billing.framework"
        echo "========================================="
        ;;
    *)
        echo "Error: unknown target '$BUILD_TARGET'"
        echo "Usage: $0 [device|simulator-arm64|simulator-x64|all]"
        exit 1
        ;;
esac

echo ""
echo "Next: embed sokol_billing.framework in the app bundle alongside sokol.framework."
