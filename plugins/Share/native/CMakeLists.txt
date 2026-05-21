cmake_minimum_required(VERSION 3.20)
project(sokol_share)

# sokol_share — standalone native share library for Android and iOS.
#
# Desktop (macOS / Windows / Linux): SharePlugin.cs uses Process.Start — no native
# binary is needed. Configure will exit early with a status message.
#
# Android build (output → plugins/Share/libs/android/<abi>/<config>/):
#   cmake -S . -B build-android-<abi> \
#     -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK/build/cmake/android.toolchain.cmake \
#     -DANDROID_ABI=arm64-v8a \
#     -DANDROID_PLATFORM=android-26
#   (libsokol.so path is auto-derived from libs/android/<abi>/release/ in the repo root.
#    Override with -DSOKOL_LIB_DIR=/abs/path if your layout differs.)
#
# iOS build (output → plugins/Share/libs/ios/<target>/<config>/):
#   cmake -S . -B build-ios-device -G Xcode \
#     -DCMAKE_SYSTEM_NAME=iOS \
#     -DCMAKE_OSX_ARCHITECTURES=arm64 \
#     -DCMAKE_OSX_SYSROOT=iphoneos

set(PLUGIN_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/..")   # plugins/Share/
set(LIB_NAME    "sokol_share")

# ── Desktop early-out (Windows / Linux — use Process.Start / MAPI / xdg-email) ──
if(NOT ANDROID AND NOT (CMAKE_SYSTEM_NAME STREQUAL "iOS") AND NOT APPLE)
    message(STATUS
        "sokol_share: desktop (${CMAKE_SYSTEM_NAME}) uses managed fallback — "
        "no native library needed. Nothing to build.")
    return()
endif()

# ── Target definition ─────────────────────────────────────────────────────────
add_library(${LIB_NAME} SHARED)
target_include_directories(${LIB_NAME} PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})

# ── Android ───────────────────────────────────────────────────────────────────
if(ANDROID)
    target_sources(${LIB_NAME} PRIVATE sokol_share_android.c)
    target_compile_options(${LIB_NAME} PRIVATE -x c)
    target_link_libraries(${LIB_NAME} PRIVATE android log)

    # Link against the pre-built libsokol.so (for sapp_android_get_native_activity).
    # Default path is derived from this plugin's location in the repo.
    # Override with -DSOKOL_LIB_DIR=/abs/path/to/libs/android/<abi>/release if needed.
    if(NOT DEFINED SOKOL_LIB_DIR)
        set(SOKOL_LIB_DIR
            "${CMAKE_CURRENT_SOURCE_DIR}/../../../libs/android/${ANDROID_ABI}/release")
    endif()

    set(SOKOL_SO "${SOKOL_LIB_DIR}/libsokol.so")
    if(NOT EXISTS "${SOKOL_SO}")
        message(FATAL_ERROR
            "sokol_share: libsokol.so not found at '${SOKOL_SO}'.\n"
            "Build the main sokol library first:\n"
            "  ./scripts/build-android-sokol-libraries.sh")
    endif()

    add_library(sokol SHARED IMPORTED)
    set_target_properties(sokol PROPERTIES IMPORTED_LOCATION "${SOKOL_SO}")
    target_link_libraries(${LIB_NAME} PRIVATE sokol)

    # Post-build: copy to plugins/Share/libs/android/<abi>/<config>/
    set(ANDROID_OUT "${PLUGIN_ROOT}/libs/android/${ANDROID_ABI}/$<LOWER_CASE:$<CONFIG>>")
    add_custom_command(TARGET ${LIB_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E make_directory "${ANDROID_OUT}"
        COMMAND ${CMAKE_COMMAND} -E copy
            "$<TARGET_FILE:${LIB_NAME}>"
            "${ANDROID_OUT}/$<TARGET_FILE_NAME:${LIB_NAME}>"
        COMMENT "Copying lib${LIB_NAME}.so → ${ANDROID_OUT}"
    )

# ── macOS ─────────────────────────────────────────────────────────────────────
elseif(APPLE AND NOT CMAKE_SYSTEM_NAME STREQUAL "iOS")
    target_sources(${LIB_NAME} PRIVATE sokol_share_macos.m)
    target_compile_options(${LIB_NAME} PRIVATE -x objective-c)
    target_link_libraries(${LIB_NAME} PRIVATE "-framework AppKit")

    # Determine output arch (arm64 or X64)
    if(CMAKE_OSX_ARCHITECTURES MATCHES "x86_64" OR CMAKE_SYSTEM_PROCESSOR MATCHES "x86_64")
        set(MACOS_ARCH_DIR "X64")
    else()
        set(MACOS_ARCH_DIR "arm64")
    endif()

    # Post-build: copy to plugins/Share/libs/macos/<arch>/<config>/
    set(MACOS_OUT "${PLUGIN_ROOT}/libs/macos/${MACOS_ARCH_DIR}/$<LOWER_CASE:$<CONFIG>>")
    add_custom_command(TARGET ${LIB_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E make_directory "${MACOS_OUT}"
        COMMAND ${CMAKE_COMMAND} -E copy
            "$<TARGET_FILE:${LIB_NAME}>"
            "${MACOS_OUT}/$<TARGET_FILE_NAME:${LIB_NAME}>"
        COMMENT "Copying lib${LIB_NAME}.dylib → ${MACOS_OUT}"
    )

# ── iOS ───────────────────────────────────────────────────────────────────────
elseif(CMAKE_SYSTEM_NAME STREQUAL "iOS")
    target_sources(${LIB_NAME} PRIVATE sokol_share_ios.m)
    target_compile_options(${LIB_NAME} PRIVATE -x objective-c)

    # iOS share sheet uses only UIKit — no link against sokol needed.
    target_link_libraries(${LIB_NAME} PRIVATE "-framework UIKit")

    set_target_properties(${LIB_NAME} PROPERTIES
        FRAMEWORK                        TRUE
        MACOSX_FRAMEWORK_IDENTIFIER      com.sokolnet.plugins.share
        FRAMEWORK_VERSION                A
        XCODE_ATTRIBUTE_TARGETED_DEVICE_FAMILY "1,2"
    )

    # Determine output path from sysroot (iphoneos vs iphonesimulator).
    if(CMAKE_OSX_SYSROOT MATCHES "iphonesimulator")
        if(CMAKE_OSX_ARCHITECTURES MATCHES "x86_64")
            set(IOS_ARCH_DIR "simulator-x64")
        else()
            set(IOS_ARCH_DIR "simulator-arm64")
        endif()
    else()
        set(IOS_ARCH_DIR "arm64")
    endif()

    # Post-build: copy to plugins/Share/libs/ios/<target>/<config>/
    set(IOS_OUT "${PLUGIN_ROOT}/libs/ios/${IOS_ARCH_DIR}/$<LOWER_CASE:$<CONFIG>>")
    add_custom_command(TARGET ${LIB_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E make_directory "${IOS_OUT}"
        COMMAND ${CMAKE_COMMAND} -E copy_directory
            "$<TARGET_BUNDLE_DIR:${LIB_NAME}>"
            "${IOS_OUT}/${LIB_NAME}.framework"
        COMMENT "Copying ${LIB_NAME}.framework → ${IOS_OUT}"
    )
endif()
