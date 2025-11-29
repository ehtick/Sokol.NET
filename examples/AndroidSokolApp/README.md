# AndroidSokolApp

A native .NET for Android application demonstrating integration of **Sokol GFX** (graphics rendering library) within an Android GLSurfaceView context. This example showcases how to use Sokol's powerful graphics API in a .NET Android application without using Sokol App.

## Overview

This project demonstrates a rotating 3D cube rendered using:
- **.NET 10.0 for Android** (native Android, not MAUI)
- **Sokol GFX** for cross-platform graphics rendering (OpenGL ES 3.1)
- **Android GLSurfaceView** for OpenGL context management
- **Custom OpenGL integration** without Sokol App

### What This Example Uses

✅ **Sokol GFX (SG)** - The graphics rendering library
- `sg_setup()`, `sg_make_buffer()`, `sg_make_shader()`, `sg_make_pipeline()`
- `sg_begin_pass()`, `sg_apply_pipeline()`, `sg_draw()`, `sg_commit()`
- All graphics rendering operations

✅ **Native Android Framework**
- Standard `Activity` for app lifecycle
- `GLSurfaceView` for OpenGL surface management
- Android's native OpenGL context (no MAUI overhead)

✅ **Manual OpenGL Context Management**
- Direct `GLES31` API calls for viewport queries
- Manual framebuffer management (framebuffer = 0 for default)
- Custom delta time calculation using `JavaSystem.CurrentTimeMillis()`

### What This Example Does NOT Use

❌ **Sokol App (SApp)** - The windowing/input framework
- No `sapp_*` functions can be called
- `sapp_gl_get_framebuffer()` ❌ - Causes crashes
- `sapp_width()` / `sapp_height()` ❌ - Not available
- `sapp_frame_duration()` ❌ - Not available
- `sapp_color_format()` / `sapp_depth_format()` ❌ - Not available

**Why?** Sokol App provides its own window/context management, but in native Android, the context is managed by GLSurfaceView. Using `sapp_*` functions will crash the app because Sokol App is not initialized.

## Key Implementation Details

### OpenGL Context Management

Since we're not using Sokol App, we handle OpenGL context manually:

```csharp
// Get framebuffer (default = 0 in GLSurfaceView)
private static sg_swapchain AndroidSwapchain()
{
    // GL_VIEWPORT = 0x0BA2
    int[] viewport = new int[4];
    GLES31.GlGetIntegerv(0x0BA2, viewport, 0);
    
    return new sg_swapchain
    {
        width = viewport[2],
        height = viewport[3],
        gl = new sg_gl_swapchain
        {
            framebuffer = 0  // Default framebuffer in GLSurfaceView
        }
    };
}
```

### Architecture Support

This project supports three Android ABIs:
- **armeabi-v7a** (32-bit ARM)
- **arm64-v8a** (64-bit ARM)
- **x86_64** (Intel 64-bit for emulators)

## Prerequisites

- .NET SDK (with .NET 10.0 support)
- Android workload: `sudo dotnet workload install android`
- Android SDK (API level 24+)
- Android device or emulator

## Building the Application

### Initial Setup

If you encounter an error about missing Android SDK platform API level 36, run:

```bash
dotnet build -t:InstallAndroidDependencies -f net10.0-android \
  "-p:AndroidSdkDirectory=/Users/elialoni/Library/Developer/Xamarin/android-sdk-macosx" \
  -p:AcceptAndroidSDKLicenses=true
```

### Debug Build

```bash
dotnet build -f net10.0-android
```

### Release Build

```bash
dotnet build -c Release -f net10.0-android
```

## Deployment

### Option 1: Deploy and Run (Recommended for Development)

This method handles Fast Deployment automatically and deploys assemblies correctly:

```bash
dotnet build -t:Run -f net10.0-android
```

### Option 2: Manual Installation with ADB

**Important:** Only Release builds can be manually installed with `adb install`.

Debug builds use Fast Deployment and don't include assemblies in the APK, which will cause the app to crash with:
```
No assemblies found in '/data/user/0/com.companyname.MyAndroidApp/files/.__override__/arm64-v8a'
```

To manually install:

1. Build a Release APK:
   ```bash
   dotnet build -c Release -f net10.0-android
   ```

2. Install with adb:
   ```bash
   adb install bin/Release/net10.0-android/com.companyname.AndroidSokolApp-Signed.apk
   ```

### Option 3: Deploy and Run Release Build

For testing Release builds directly on device:

```bash
dotnet build -c Release -t:Run -f net10.0-android
```

## Project Structure

- `MainActivity.cs` - Native Android activity entry point
- `OpenGLView.cs` - Custom GLSurfaceView with Sokol GFX integration
  - `AndroidEnvironment()` - Sokol environment setup without sapp_* calls
  - `AndroidSwapchain()` - Swapchain configuration for GLSurfaceView
  - `OnSurfaceCreated()` - Sokol GFX initialization
  - `OnDrawFrame()` - Rendering loop with manual timing
- `Resources/` - Android resources (layouts, images, strings)
- `AndroidSokolApp.csproj` - Project configuration with multi-ABI support
- `libs/` - Native libraries (libsokol.so, libclear.so) for all ABIs
- `shaders/` - GLSL shader source files

## Learning Points

### ✅ Correct Approach
```csharp
// Query viewport from OpenGL directly
int[] viewport = new int[4];
GLES31.GlGetIntegerv(0x0BA2, viewport, 0);
int width = viewport[2];
int height = viewport[3];

// Use default framebuffer
gl = new sg_gl_swapchain { framebuffer = 0 };

// Manual delta time
long currentTime = JavaSystem.CurrentTimeMillis();
float deltaSeconds = (currentTime - _lastFrameTime) / 1000.0f;
```

### ❌ Will Crash
```csharp
// DO NOT use sapp_* functions in MAUI Android!
int width = sapp_width();  // ❌ Crashes
uint framebuffer = sapp_gl_get_framebuffer();  // ❌ Crashes
float dt = sapp_frame_duration();  // ❌ Crashes
```

## Use Cases

This example is ideal for:
- Integrating Sokol GFX into native .NET Android apps
- Custom OpenGL rendering in Android applications
- High-performance graphics without MAUI overhead
- Learning how to use Sokol GFX without Sokol App

## References

- [Sokol Headers](https://github.com/floooh/sokol)
- [.NET for Android Documentation](https://learn.microsoft.com/dotnet/android/)
- [Android GLSurfaceView](https://developer.android.com/reference/android/opengl/GLSurfaceView)

### Debug Build
- Uses **Fast Deployment** for faster iteration
- Assemblies are deployed separately during `dotnet build -t:Run`
- APK cannot be installed manually with `adb install`
- Suitable for development and debugging

### Release Build
- Includes all assemblies in the APK
- Larger APK size but self-contained
- Can be installed manually with `adb install`
- Suitable for distribution and testing

## Troubleshooting

### App crashes immediately after installation

**Cause:** You installed a Debug build APK with `adb install`

**Solution:** Either:
- Use `dotnet build -t:Run` for Debug builds
- Build and install a Release APK instead

### Missing Android SDK error

**Cause:** Android SDK platform for API level 36 is not installed

**Solution:** Run the InstallAndroidDependencies command shown in Initial Setup

## Project Structure

- `MainActivity.cs` - Main activity entry point
- `Resources/` - Android resources (layouts, images, strings)
- `MyAndroidApp.csproj` - Project configuration
