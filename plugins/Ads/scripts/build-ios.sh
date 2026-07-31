#!/bin/bash
# Build sokol_ads.framework for iOS — standalone Ads plugin library.
# Output: libs/ios/<target>/{debug,release}/sokol_ads.framework
# Requires the Google SDK fetched first: ./plugins/Ads/scripts/fetch-googlemobileads-ios.sh
# Usage: ./plugins/Ads/scripts/build-ios.sh [device|simulator-arm64|simulator-x64|all]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
PLUGIN_NATIVE="$REPO_ROOT/plugins/Ads/native"

"$SCRIPT_DIR/fetch-googlemobileads-ios.sh"

BUILD_TARGET="${1:-device}"

build_for_target() {
    local TARGET=$1
    local ARCH=$2
    local SDK=$3
    local OUTPUT_DIR=$4

    echo "========================================="
    echo "sokol_ads — iOS $TARGET ($ARCH)"
    echo "========================================="

    local BUILD_DIR="$REPO_ROOT/build-sokol-ads-ios-$TARGET"
    rm -rf "$BUILD_DIR"

    cmake -S "$PLUGIN_NATIVE" -B "$BUILD_DIR" -G Xcode \
        -DCMAKE_SYSTEM_NAME=iOS \
        -DCMAKE_OSX_DEPLOYMENT_TARGET=14.0 \
        -DCMAKE_OSX_ARCHITECTURES="$ARCH" \
        -DCMAKE_OSX_SYSROOT="$SDK"

    echo "Building Release..."
    cmake --build "$BUILD_DIR" --config Release

    echo "Building Debug..."
    cmake --build "$BUILD_DIR" --config Debug

    rm -rf "$BUILD_DIR"

    echo "Done — $TARGET"
    echo "  Release: plugins/Ads/libs/ios/$OUTPUT_DIR/release/sokol_ads.framework"
    echo "  Debug:   plugins/Ads/libs/ios/$OUTPUT_DIR/debug/sokol_ads.framework"
}

case "$BUILD_TARGET" in
    device)          build_for_target "device" "arm64" "iphoneos" "arm64" ;;
    simulator-arm64) build_for_target "simulator-arm64" "arm64" "iphonesimulator" "simulator-arm64" ;;
    simulator-x64)   build_for_target "simulator-x64" "x86_64" "iphonesimulator" "simulator-x64" ;;
    all)
        build_for_target "device"          "arm64"  "iphoneos"        "arm64"
        build_for_target "simulator-arm64" "arm64"  "iphonesimulator" "simulator-arm64"
        build_for_target "simulator-x64"   "x86_64" "iphonesimulator" "simulator-x64"
        ;;
    *) echo "Usage: $0 [device|simulator-arm64|simulator-x64|all]"; exit 1 ;;
esac
