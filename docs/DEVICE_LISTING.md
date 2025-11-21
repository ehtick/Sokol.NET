# Device Listing Guide

This guide explains how to list connected Android and iOS devices using the SokolApplicationBuilder's cross-platform device listing feature.

## Overview

The `listdevices` task provides a unified, cross-platform way to list connected devices. It replaces platform-specific shell scripts with a C# implementation that works on Windows, macOS, and Linux.

## Prerequisites

### Android Devices

- Android SDK installed with `adb` (Android Debug Bridge) in your PATH
- Environment variables set:
  - `ANDROID_SDK_ROOT` or `ANDROID_HOME` (optional, will try common locations)
- USB debugging enabled on your Android device
- Device connected via USB

### iOS Devices (macOS Only)

- Xcode installed with command line tools
- `ios-deploy` installed for physical devices:
  ```bash
  brew install ios-deploy
  ```
- For simulators, only Xcode is required
- Physical devices must be trusted and paired with Xcode

## Using VS Code Tasks

### List Android Devices

1. Open Command Palette (`Cmd+Shift+P` on macOS, `Ctrl+Shift+P` on Windows/Linux)
2. Type "Tasks: Run Task"
3. Select "Android: List Devices"

Or run directly from terminal:
```bash
dotnet run --project tools/SokolApplicationBuilder -- --task listdevices --architecture android
```

**Example Output:**
```
🔍 Checking for connected Android devices...

📱 Connected Android devices:
=========================
Device ID: R5CT30ABCDE (Samsung Galaxy S21)
Device ID: emulator-5554 (Android SDK Emulator)

To use a specific device, copy the Device ID and use it with --device parameter.
```

### List iOS Devices

1. Open Command Palette (`Cmd+Shift+P`)
2. Type "Tasks: Run Task"
3. Select "iOS: List Devices"

Or run directly from terminal (macOS only):
```bash
dotnet run --project tools/SokolApplicationBuilder -- --task listdevices --architecture ios
```

**Example Output:**
```
🔍 Checking for iOS devices and simulators...

📱 Physical iOS Devices:
======================
Device: 00008030-001234567890001E (iPhone 14 Pro)

📱 iOS Simulators:
==================
Simulator ID: 12345678-1234-1234-1234-123456789012 (iPhone 14 Pro)
Simulator ID: 87654321-4321-4321-4321-210987654321 (iPad Pro 12.9-inch)

To use a specific device/simulator, copy the ID and use it with --ios-device parameter.

Note: Physical devices require trust and pairing with Xcode.
      Simulators can be launched directly.
```

## Command Line Usage

### Android
```bash
# From Sokol.NET repository root
dotnet run --project tools/SokolApplicationBuilder -- --task listdevices --architecture android

# From anywhere (after registration)
cd /path/to/your/project
dotnet run --project "$(cat ~/.sokolnet_config/sokolnet_home)/tools/SokolApplicationBuilder" -- --task listdevices --architecture android
```

### iOS
```bash
# From Sokol.NET repository root (macOS only)
dotnet run --project tools/SokolApplicationBuilder -- --task listdevices --architecture ios

# From anywhere (after registration)
cd /path/to/your/project
dotnet run --project "$(cat ~/.sokolnet_config/sokolnet_home)/tools/SokolApplicationBuilder" -- --task listdevices --architecture ios
```

### Windows
```powershell
# Read Sokol.NET home path
$sokolHome = Get-Content $env:USERPROFILE\.sokolnet_config\sokolnet_home

# List Android devices
dotnet run --project "$sokolHome\tools\SokolApplicationBuilder\SokolApplicationBuilder.csproj" -- --task listdevices --architecture android

# List iOS devices (not supported on Windows)
# iOS device listing only works on macOS
```

## Integration in Your Project

The `listdevices` task is automatically available in projects created with the "Create New Project" feature. The VS Code tasks are pre-configured in `.vscode/tasks.json`.

### Custom Integration

Add these tasks to your `.vscode/tasks.json`:

```json
{
  "label": "Android: List Devices",
  "type": "shell",
  "group": "build",
  "windows": {
    "command": "$sokolHome = Get-Content $env:USERPROFILE\\.sokolnet_config\\sokolnet_home; dotnet run --project \"$sokolHome\\tools\\SokolApplicationBuilder\\SokolApplicationBuilder.csproj\" -- --task listdevices --architecture android"
  },
  "linux": {
    "command": "dotnet run --project \"$(cat ~/.sokolnet_config/sokolnet_home)/tools/SokolApplicationBuilder\" -- --task listdevices --architecture android"
  },
  "osx": {
    "command": "dotnet run --project \"$(cat ~/.sokolnet_config/sokolnet_home)/tools/SokolApplicationBuilder\" -- --task listdevices --architecture android"
  },
  "problemMatcher": []
},
{
  "label": "iOS: List Devices",
  "type": "shell",
  "group": "build",
  "windows": {
    "command": "$sokolHome = Get-Content $env:USERPROFILE\\.sokolnet_config\\sokolnet_home; dotnet run --project \"$sokolHome\\tools\\SokolApplicationBuilder\\SokolApplicationBuilder.csproj\" -- --task listdevices --architecture ios"
  },
  "linux": {
    "command": "dotnet run --project \"$(cat ~/.sokolnet_config/sokolnet_home)/tools/SokolApplicationBuilder\" -- --task listdevices --architecture ios"
  },
  "osx": {
    "command": "dotnet run --project \"$(cat ~/.sokolnet_config/sokolnet_home)/tools/SokolApplicationBuilder\" -- --task listdevices --architecture ios"
  },
  "problemMatcher": []
}
```

## Troubleshooting

### Android: "adb not found"

**Problem:** The tool cannot find the `adb` command.

**Solutions:**
1. Install Android SDK Platform Tools
2. Set environment variable:
   ```bash
   # macOS/Linux
   export ANDROID_SDK_ROOT=/path/to/android/sdk
   
   # Windows
   setx ANDROID_SDK_ROOT "C:\path\to\android\sdk"
   ```
3. Add `adb` to your PATH:
   - macOS/Linux: `export PATH=$PATH:$ANDROID_SDK_ROOT/platform-tools`
   - Windows: Add `%ANDROID_SDK_ROOT%\platform-tools` to system PATH

### Android: No devices found

**Problem:** No devices appear even though one is connected.

**Solutions:**
1. Verify USB debugging is enabled on your Android device
2. Check device is connected: `adb devices`
3. Authorize your computer on the device (unlock screen when prompted)
4. Try a different USB cable or port
5. Restart adb server: `adb kill-server && adb start-server`

### iOS: "ios-deploy not found"

**Problem:** Cannot list physical iOS devices.

**Solution:**
```bash
# Install ios-deploy
brew install ios-deploy

# Verify installation
ios-deploy --version
```

### iOS: Device not trusted

**Problem:** Physical device appears but cannot be used.

**Solutions:**
1. Unlock your iOS device
2. Open Xcode
3. Go to Window → Devices and Simulators
4. Click your device and click "Trust"
5. Confirm on your iOS device

### iOS: Only works on macOS

**Problem:** Trying to list iOS devices on Windows or Linux.

**Explanation:** iOS device management requires macOS-specific tools (Xcode, ios-deploy) that are not available on other platforms. Android device listing works on all platforms.

## Using Device IDs

Once you have a device ID from the list, you can use it when building/installing:

### Android
```bash
# Install to specific device
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture android \
  --type release \
  --install \
  --device "R5CT30ABCDE" \
  --path ./examples/cube
```

### iOS
```bash
# Install to specific device/simulator
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture ios \
  --type release \
  --install \
  --ios-device "12345678-1234-1234-1234-123456789012" \
  --path ./examples/cube
```

## Implementation Details

The `listdevices` task is implemented in `ListDevicesTask.cs` and uses:

- **Android:** `adb devices` to list devices, `adb shell getprop` to get device properties
- **iOS:** `ios-deploy --detect` for physical devices, `xcrun simctl list` for simulators
- **CliWrap:** For cross-platform process execution
- **Regex:** For parsing device information

The task automatically:
- Searches for `adb` in common SDK locations
- Checks if commands exist before executing
- Formats output with emojis and clear section headers
- Provides helpful error messages and troubleshooting hints

## Related Documentation

- [Android Device Selection Guide](ANDROID_DEVICE_SELECTION.md) - Detailed device selection information
- [iOS Device Selection Guide](ios-device-selection.md) - iOS-specific device management
- [VS Code Run Guide](VSCODE_RUN_GUIDE.md) - Using VS Code tasks for building and running
- [Build System Overview](BUILD_SYSTEM.md) - Understanding the SokolApplicationBuilder

## Advantages Over Shell Scripts

The new `listdevices` task replaces the previous shell scripts with these benefits:

1. **Cross-Platform:** Works on Windows, macOS, and Linux
2. **Windows-Friendly:** No bash required, uses native .NET commands
3. **Better Error Handling:** Clear error messages and troubleshooting hints
4. **Consistent Output:** Uniform formatting across platforms
5. **Maintainable:** Single C# implementation instead of platform-specific scripts
6. **Integrated:** Part of SokolApplicationBuilder task system
7. **Flexible:** Can be extended to support more device types and properties
