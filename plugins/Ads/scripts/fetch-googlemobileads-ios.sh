#!/bin/bash
# Fetch the Google Mobile Ads iOS SDK (GoogleMobileAds + UserMessagingPlatform
# xcframeworks) into plugins/Ads/vendor/ios/ — NOT vendored in the repo (Google's
# SDK, ~13 MB zip). Run once before building the iOS plugin or an iOS app that
# uses it. The app builder embeds the DEVICE slices via IOSNativeLibrary_* paths
# pointing at vendor/ios/device/ (created here).
#
#   ./fetch-googlemobileads-ios.sh            fetch if absent; repair device/ if stale
#   ./fetch-googlemobileads-ios.sh --force    re-fetch, i.e. UPDATE to Google's current SDK
#
# Google publishes this zip at an UNVERSIONED url, always serving the latest, so a
# plain re-run cannot tell "up to date" from "years old" — hence --force to update.

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLUGIN_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
VENDOR="$PLUGIN_ROOT/vendor/ios"
URL="https://dl.google.com/googleadmobadssdk/googlemobileadssdkios.zip"

installed_version() {
    local plist="$VENDOR/$1.xcframework/ios-arm64/$1.framework/Info.plist"
    [ -f "$plist" ] && /usr/libexec/PlistBuddy -c "Print :CFBundleShortVersionString" "$plist" 2>/dev/null
}

# The device slices are DERIVED from the xcframeworks — plain .frameworks, because the
# app builder's IOSNativeLibrary_* embedding globs *.framework, not *.xcframework.
# Always rebuild them from whatever is vendored: they are cheap to recreate, and an
# interrupted or pre-device/ fetch otherwise leaves an iOS ads build with no SDK to
# embed and nothing to say why.
make_device_slices() {
    mkdir -p "$VENDOR/device"
    for f in GoogleMobileAds UserMessagingPlatform; do
        rm -rf "$VENDOR/device/$f.framework"
        cp -R "$VENDOR/$f.xcframework/ios-arm64/$f.framework" "$VENDOR/device/"
    done
}

if [ "${1:-}" != "--force" ] \
   && [ -d "$VENDOR/GoogleMobileAds.xcframework" ] \
   && [ -d "$VENDOR/UserMessagingPlatform.xcframework" ]; then
    make_device_slices
    echo "sokol_ads: already present at $VENDOR"
    echo "  GoogleMobileAds        $(installed_version GoogleMobileAds)"
    echo "  UserMessagingPlatform  $(installed_version UserMessagingPlatform)"
    echo "  device/ slices refreshed. Re-run with --force to UPDATE to Google's current SDK."
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
# Replace outright: cp -R onto an existing bundle MERGES, which would leave stale
# slices from the previous SDK behind and silently produce a mixed framework.
for f in GoogleMobileAds UserMessagingPlatform; do
    rm -rf "$VENDOR/$f.xcframework"
    cp -R "$SDK_DIR/$f.xcframework" "$VENDOR/"
done
make_device_slices

echo "sokol_ads: SDK ready:"
echo "  $VENDOR/GoogleMobileAds.xcframework        $(installed_version GoogleMobileAds)"
echo "  $VENDOR/UserMessagingPlatform.xcframework  $(installed_version UserMessagingPlatform)"
echo "  $VENDOR/device/{GoogleMobileAds,UserMessagingPlatform}.framework  (app embedding)"
