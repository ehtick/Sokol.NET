#!/bin/bash
# Build camerac for iOS. Produces camerac.framework for device or simulator,
# or an XCFramework that bundles both (pass "xcframework").
#
# Usage: ./build-camerac-ios.sh [sdk] [build_type]
#   sdk = iphoneos | iphonesimulator | xcframework
# Examples:
#   ./build-camerac-ios.sh iphoneos Release
#   ./build-camerac-ios.sh xcframework Release

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CAMERAC_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

SDK="${1:-iphoneos}"
BUILD_TYPE="${2:-Release}"
BUILD_TYPE_LOWER=$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')

# Build for a single SDK, return path of produced .framework
build_sdk() {
    local THIS_SDK="$1"
    local BUILD_DIR="$CAMERAC_DIR/build-xcode-ios-$THIS_SDK"

    # iphonesimulator gets fat slice (arm64 + x86_64) for Apple Silicon + Intel
    local THIS_ARCH="arm64"
    if [ "$THIS_SDK" = "iphonesimulator" ]; then
        THIS_ARCH="arm64 x86_64"
    fi

    # All progress output goes to stderr so callers can capture the framework path via stdout
    echo "" >&2
    echo "--- Building camerac.framework for $THIS_SDK ($THIS_ARCH) ---" >&2
    rm -rf "$BUILD_DIR"
    mkdir -p "$BUILD_DIR"
    cd "$BUILD_DIR"

    cmake "$CAMERAC_DIR" \
        -G Xcode \
        -DCMAKE_SYSTEM_NAME=iOS \
        -DCMAKE_OSX_ARCHITECTURES="$THIS_ARCH" \
        -DCMAKE_OSX_DEPLOYMENT_TARGET=15.0 \
        -DCMAKE_XCODE_ATTRIBUTE_ONLY_ACTIVE_ARCH=NO \
        -DCMAKE_XCODE_ATTRIBUTE_SUPPORTED_PLATFORMS="$THIS_SDK" \
        -DCMAKE_XCODE_ATTRIBUTE_CODE_SIGNING_ALLOWED=NO \
        -DCMAKE_XCODE_ATTRIBUTE_CODE_SIGN_IDENTITY="" \
        -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
        -DBUILD_SHARED_LIBS=ON \
        -DCAMERAC_BUILD_SAMPLE=OFF >&2

    cmake --build . --config "$BUILD_TYPE" --target camerac >&2

    # Only the framework path is printed to stdout (what the caller captures)
    find "$BUILD_DIR" -name "camerac.framework" -type d | head -n 1
}

echo "=========================================="
echo "Building camerac iOS framework"
echo "SDK: $SDK  |  Build: $BUILD_TYPE"
echo "=========================================="

OUTPUT_DIR="$CAMERAC_DIR/libs/ios/$BUILD_TYPE_LOWER"
mkdir -p "$OUTPUT_DIR"

if [ "$SDK" = "xcframework" ]; then
    DEVICE_FW=$(build_sdk iphoneos)
    SIM_FW=$(build_sdk iphonesimulator)

    if [ -z "$DEVICE_FW" ] || [ -z "$SIM_FW" ]; then
        echo "Could not find framework(s) after build"
        exit 1
    fi

    XCFW="$OUTPUT_DIR/camerac.xcframework"
    rm -rf "$XCFW"
    xcodebuild -create-xcframework \
        -framework "$DEVICE_FW" \
        -framework "$SIM_FW" \
        -output "$XCFW"

    echo ""
    echo "=========================================="
    echo "Build complete!"
    echo "=========================================="
    echo "camerac.xcframework -> $XCFW"
else
    FW=$(build_sdk "$SDK")

    if [ -z "$FW" ]; then
        echo "camerac.framework not found after build"
        exit 1
    fi

    DEST="$OUTPUT_DIR/camerac.framework"
    rm -rf "$DEST"
    cp -R "$FW" "$DEST"

    echo ""
    echo "=========================================="
    echo "Build complete!"
    echo "=========================================="
    echo "camerac.framework -> $DEST"
    if [ -f "$DEST/camerac" ]; then
        file "$DEST/camerac"
    fi
fi
