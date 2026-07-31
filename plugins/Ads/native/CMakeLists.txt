cmake_minimum_required(VERSION 3.20)
project(sokol_ads C)

# sokol_ads — standalone native AdMob interstitial library.
# Android (Google Mobile Ads via Java helper) and iOS (GoogleMobileAds.xcframework)
# only; desktop/web have no ad SDK and the managed layer (AdsPlugin.cs) stubs out.
#
# Android build (output → plugins/Ads/libs/android/<abi>/<config>/):
#   cmake -S . -B build-android-<abi> \
#     -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK/build/cmake/android.toolchain.cmake \
#     -DANDROID_ABI=arm64-v8a \
#     -DANDROID_PLATFORM=android-26
#
# iOS build (output → plugins/Ads/libs/ios/<target>/<config>/):
#   First fetch the SDK (not vendored): ./plugins/Ads/scripts/fetch-googlemobileads-ios.sh
#   cmake -S . -B build-ios-device -G Xcode \
#     -DCMAKE_SYSTEM_NAME=iOS \
#     -DCMAKE_OSX_ARCHITECTURES=arm64 \
#     -DCMAKE_OSX_SYSROOT=iphoneos

set(PLUGIN_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/..")   # plugins/Ads/
set(LIB_NAME    "sokol_ads")

# ── Desktop/web early-out ─────────────────────────────────────────────────────
if(NOT ANDROID AND NOT (CMAKE_SYSTEM_NAME STREQUAL "iOS"))
    message(STATUS
        "sokol_ads: no ad SDK on ${CMAKE_SYSTEM_NAME} — managed layer stubs "
        "ads out. Nothing to build.")
    return()
endif()

# ── Android ───────────────────────────────────────────────────────────────────
if(ANDROID)
    add_library(${LIB_NAME} SHARED
        sokol_ads_android.c
        sokol_ads_queue.c)
    target_include_directories(${LIB_NAME} PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
    target_compile_options(${LIB_NAME} PRIVATE -x c)
    target_link_libraries(${LIB_NAME} PRIVATE android log)

    # Link against the pre-built libsokol.so (for sapp_android_get_native_activity).
    if(NOT DEFINED SOKOL_LIB_DIR)
        set(SOKOL_LIB_DIR
            "${CMAKE_CURRENT_SOURCE_DIR}/../../../libs/android/${ANDROID_ABI}/release")
    endif()

    set(SOKOL_SO "${SOKOL_LIB_DIR}/libsokol.so")
    if(NOT EXISTS "${SOKOL_SO}")
        message(FATAL_ERROR
            "sokol_ads: libsokol.so not found at '${SOKOL_SO}'.\n"
            "Build the main sokol library first:\n"
            "  ./scripts/build-android-sokol-libraries.sh")
    endif()

    add_library(sokol SHARED IMPORTED)
    set_target_properties(sokol PROPERTIES IMPORTED_LOCATION "${SOKOL_SO}")
    target_link_libraries(${LIB_NAME} PRIVATE sokol)

    set(ANDROID_OUT "${PLUGIN_ROOT}/libs/android/${ANDROID_ABI}/$<LOWER_CASE:$<CONFIG>>")
    add_custom_command(TARGET ${LIB_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E make_directory "${ANDROID_OUT}"
        COMMAND ${CMAKE_COMMAND} -E copy
            "$<TARGET_FILE:${LIB_NAME}>"
            "${ANDROID_OUT}/$<TARGET_FILE_NAME:${LIB_NAME}>"
        COMMENT "Copying lib${LIB_NAME}.so → ${ANDROID_OUT}"
    )

# ── iOS ───────────────────────────────────────────────────────────────────────
elseif(CMAKE_SYSTEM_NAME STREQUAL "iOS")
    # The Google SDKs are fetched, not vendored (scripts/fetch-googlemobileads-ios.sh).
    set(GMA_DIR "${PLUGIN_ROOT}/vendor/ios")
    set(GMA_XC  "${GMA_DIR}/GoogleMobileAds.xcframework")
    set(UMP_XC  "${GMA_DIR}/UserMessagingPlatform.xcframework")
    if(NOT EXISTS "${GMA_XC}" OR NOT EXISTS "${UMP_XC}")
        message(FATAL_ERROR
            "sokol_ads: GoogleMobileAds SDK not found under '${GMA_DIR}'.\n"
            "Fetch it first:  ./plugins/Ads/scripts/fetch-googlemobileads-ios.sh")
    endif()

    if(CMAKE_OSX_SYSROOT MATCHES "iphonesimulator")
        set(GMA_SLICE "ios-arm64_x86_64-simulator")
        if(CMAKE_OSX_ARCHITECTURES MATCHES "x86_64")
            set(IOS_ARCH_DIR "simulator-x64")
        else()
            set(IOS_ARCH_DIR "simulator-arm64")
        endif()
    else()
        set(GMA_SLICE "ios-arm64")
        set(IOS_ARCH_DIR "arm64")
    endif()

    add_library(${LIB_NAME} SHARED
        sokol_ads_ios.m
        sokol_ads_queue.c)
    target_include_directories(${LIB_NAME} PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
    set_source_files_properties(sokol_ads_ios.m PROPERTIES
        COMPILE_OPTIONS "-x;objective-c;-fobjc-arc")
    # Framework search paths are needed at COMPILE time too (#import <GoogleMobileAds/…>).
    target_compile_options(${LIB_NAME} PRIVATE
        "-F${GMA_XC}/${GMA_SLICE}"
        "-F${UMP_XC}/${GMA_SLICE}")
    # GoogleMobileAds ships as a STATIC framework: it is absorbed into this dylib, so its
    # transitive dependencies must be linked here — JavaScriptCore for the OMID JS bridge,
    # and the Swift runtime search paths (the SDK contains Swift; clang drives this link,
    # so auto-linked swift libs need explicit -L via Xcode's own variables).
    target_link_libraries(${LIB_NAME} PRIVATE
        "-F${GMA_XC}/${GMA_SLICE}"
        "-F${UMP_XC}/${GMA_SLICE}"
        "-framework GoogleMobileAds"
        "-framework UserMessagingPlatform"
        "-framework UIKit"
        "-framework JavaScriptCore"
        "-lc++")

    # The SDK's Swift objects force-load the compatibility shims; clang drives this link,
    # so resolve them explicitly from the active toolchain.
    execute_process(COMMAND xcode-select -p OUTPUT_VARIABLE XCODE_DEV OUTPUT_STRIP_TRAILING_WHITESPACE)
    set(SWIFT_COMPAT_DIR "${XCODE_DEV}/Toolchains/XcodeDefault.xctoolchain/usr/lib/swift/iphoneos")
    target_link_libraries(${LIB_NAME} PRIVATE
        "-L${SWIFT_COMPAT_DIR}"
        "-lswiftCompatibility51"
        "-lswiftCompatibility56"
        "-lswiftCompatibilityConcurrency"
        "-lswiftCompatibilityPacks")

    set_target_properties(${LIB_NAME} PROPERTIES
        FRAMEWORK                        TRUE
        MACOSX_FRAMEWORK_IDENTIFIER      com.sokolnet.plugins.ads
        FRAMEWORK_VERSION                A
        XCODE_ATTRIBUTE_TARGETED_DEVICE_FAMILY "1,2"
        XCODE_ATTRIBUTE_LIBRARY_SEARCH_PATHS "$(TOOLCHAIN_DIR)/usr/lib/swift/$(PLATFORM_NAME) $(SDKROOT)/usr/lib/swift"
    )

    set(IOS_OUT "${PLUGIN_ROOT}/libs/ios/${IOS_ARCH_DIR}/$<LOWER_CASE:$<CONFIG>>")
    add_custom_command(TARGET ${LIB_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E make_directory "${IOS_OUT}"
        COMMAND ${CMAKE_COMMAND} -E copy_directory
            "$<TARGET_BUNDLE_DIR:${LIB_NAME}>"
            "${IOS_OUT}/${LIB_NAME}.framework"
        COMMENT "Copying ${LIB_NAME}.framework → ${IOS_OUT}"
    )
endif()
