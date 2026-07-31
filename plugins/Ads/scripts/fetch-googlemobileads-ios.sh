#!/bin/bash
# Fetch the Google Mobile Ads iOS SDK (GoogleMobileAds + UserMessagingPlatform
# xcframeworks) into plugins/Ads/vendor/ios/ — NOT vendored in the repo (Google's
# SDK, ~13 MB zip). Run once before building the iOS plugin or an iOS app that
# uses it. The app builder embeds the DEVICE slices via IOSNativeLibrary_* paths
# pointing at vendor/ios/device/ (created here).

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLUGIN_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
VENDOR="$PLUGIN_ROOT/vendor/ios"
URL="https://dl.google.com/googleadmobadssdk/googlemobileadssdkios.zip"

if [ -d "$VENDOR/GoogleMobileAds.xcframework" ] && [ -d "$VENDOR/UserMessagingPlatform.xcframework" ]; then
    echo "sokol_ads: SDK already present at $VENDOR — nothing to do."
    exit 0
fi

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

echo "Downloading Google Mobile Ads iOS SDK…"
curl -L -o "$TMP/gma.zip" "$URL"
unzip -q "$TMP/gma.zip" -d "$TMP/gma"

SDK_DIR="$(find "$TMP/gma" -maxdepth 1 -type d -name 'GoogleMobileAdsSdkiOS-*' | head -1)"
[ -n "$SDK_DIR" ] || { echo "Error: SDK layout not recognized."; exit 1; }

mkdir -p "$VENDOR"
cp -R "$SDK_DIR/GoogleMobileAds.xcframework"       "$VENDOR/"
cp -R "$SDK_DIR/UserMessagingPlatform.xcframework" "$VENDOR/"

# Device slices exposed as plain .frameworks for the app builder's
# IOSNativeLibrary_* embedding (it globs *.framework, not *.xcframework).
mkdir -p "$VENDOR/device"
rm -rf "$VENDOR/device/GoogleMobileAds.framework" "$VENDOR/device/UserMessagingPlatform.framework"
cp -R "$VENDOR/GoogleMobileAds.xcframework/ios-arm64/GoogleMobileAds.framework"             "$VENDOR/device/"
cp -R "$VENDOR/UserMessagingPlatform.xcframework/ios-arm64/UserMessagingPlatform.framework" "$VENDOR/device/"

echo "sokol_ads: SDK ready:"
echo "  $VENDOR/GoogleMobileAds.xcframework"
echo "  $VENDOR/UserMessagingPlatform.xcframework"
echo "  $VENDOR/device/{GoogleMobileAds,UserMessagingPlatform}.framework  (app embedding)"
