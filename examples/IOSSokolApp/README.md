# IOSSokolApp

A native .NET for iOS application demonstrating integration of **Sokol GFX** (graphics rendering library) with **Metal** rendering. This example showcases how to use Sokol's powerful graphics API in a .NET iOS application using MTKView without Sokol App.

## Overview

This project demonstrates a rotating 3D cube rendered using:
- **.NET 10.0 for iOS** (native iOS, not MAUI)
- **Sokol GFX** for cross-platform graphics rendering (Metal)
- **MTKView** for Metal rendering context
- **Custom Metal integration** without Sokol App

### What This Example Uses

✅ **Sokol GFX (SG)** - The graphics rendering library
- `sg_setup()`, `sg_make_buffer()`, `sg_make_shader()`, `sg_make_pipeline()`
- `sg_begin_pass()`, `sg_apply_pipeline()`, `sg_draw()`, `sg_commit()`
- All graphics rendering operations

✅ **Native iOS Framework**
- Standard `UIViewController` for app lifecycle
- `MTKView` for Metal rendering surface
- iOS's native Metal API (no MAUI overhead)

✅ **Manual Metal Context Management**
- Direct `MTLDevice` (Metal GPU device)
- Metal swapchain management via MTKView
- Custom delta time calculation using `DateTimeOffset`

### What This Example Does NOT Use

❌ **Sokol App (SApp)** - The windowing/input framework
- No `sapp_*` functions can be called
- `sapp_gl_get_framebuffer()` ❌ - Causes crashes
- `sapp_width()` / `sapp_height()` ❌ - Not available
- `sapp_frame_duration()` ❌ - Not available
- `sapp_color_format()` / `sapp_depth_format()` ❌ - Not available

**Why?** Sokol App provides its own window/context management, but in native iOS, the context is managed by MTKView. Using `sapp_*` functions will crash the app because Sokol App is not initialized.

## Key Implementation Details

### Metal Context Management

Since we're not using Sokol App, we handle Metal context manually by extracting drawables from MTKView each frame:

```csharp
// iOS-specific swapchain for MTKView
// CRITICAL: Must extract actual Metal drawables from view each frame
private unsafe sg_swapchain IOSSwapchain(MTKView view)
{
    var currentDrawable = view.CurrentDrawable;
    var depthTexture = view.DepthStencilTexture;
    
    return new sg_swapchain
    {
        width = (int)view.DrawableSize.Width,
        height = (int)view.DrawableSize.Height,
        sample_count = (int)view.SampleCount,
        color_format = ConvertPixelFormat(view.ColorPixelFormat),
        depth_format = ConvertPixelFormat(view.DepthStencilPixelFormat),
        metal = new sg_metal_swapchain
        {
            current_drawable = (void*)(currentDrawable?.Handle ?? IntPtr.Zero),
            depth_stencil_texture = (void*)(depthTexture?.Handle ?? IntPtr.Zero),
            msaa_color_texture = (void*)IntPtr.Zero
        }
    };
}

// Initialize with Metal device
public unsafe void Initialize(IMTLDevice device, MTLPixelFormat colorFormat, 
                             MTLPixelFormat depthFormat, MTKView view)
{
    _device = device;
    _view = view;
    
    var env = IOSEnvironment(colorFormat, depthFormat);
    env.metal.device = (void*)device.Handle;
    
    sg_setup(new sg_desc()
    {
        environment = env,
        logger = { func = &slog_func }
    });
}

// Render loop - pass view to extract drawables
public void Render(MTKView view)
{
    pass.swapchain = IOSSwapchain(view);
    sg_begin_pass(pass);
    // ... rendering commands ...
    sg_commit();
}
```

### Architecture Support

This project supports both physical devices and simulators:
- **ios-arm64** (Physical iPhone/iPad devices)
- **iossimulator-arm64** (iOS Simulator on Apple Silicon Macs)
- **iossimulator-x64** (iOS Simulator on Intel Macs)

**Note:** Simulator support requires the appropriate sokol.framework for each target:
- Device: `libs/ios/arm64/{debug,release}/sokol.framework`
- Simulator (Apple Silicon): `libs/ios/simulator-arm64/{debug,release}/sokol.framework`
- Simulator (Intel): `libs/ios/simulator-x64/{debug,release}/sokol.framework`

## Prerequisites

- .NET SDK (with .NET 10.0 support)
- iOS workload: `sudo dotnet workload install ios`
- Xcode 14+ with Command Line Tools
- iOS 13.0+ (physical device or simulator)
- **Apple Developer Account** (required for deploying to physical devices)

## Building the Application

### Debug Build

**For Physical Device:**
```bash
dotnet build -f net10.0-ios -r ios-arm64
```

**For Simulator (Apple Silicon Mac):**
```bash
dotnet build -f net10.0-ios -r iossimulator-arm64
```

**For Simulator (Intel Mac):**
```bash
dotnet build -f net10.0-ios -r iossimulator-x64
```

### Release Build

**For Physical Device:**
```bash
dotnet build -c Release -f net10.0-ios -r ios-arm64
```

**For Simulator (Apple Silicon Mac):**
```bash
dotnet build -c Release -f net10.0-ios -r iossimulator-arm64
```

**For Simulator (Intel Mac):**
```bash
dotnet build -c Release -f net10.0-ios -r iossimulator-x64
```

## Deployment

### Option 1: Using Visual Studio or Rider (Recommended)

1. Open `IOSSokolApp.csproj` in Visual Studio for Mac or JetBrains Rider
2. Connect your physical iOS device OR select an iOS Simulator
3. Select your target from the device/simulator dropdown
4. Click Run/Debug button to build and deploy

Both IDEs provide full iOS development support including device selection, simulator support, debugging, and automatic deployment.

### Option 2: Using Xcode

1. Build the project:
```bash
dotnet build -c Release -f net10.0-ios -r ios-arm64
```

2. Open the generated `.app` bundle from `bin/Release/net10.0-ios/ios-arm64/IOSSokolApp.app` in Xcode

3. Connect your iOS device and deploy through Xcode

### Option 3: Using Command Line Tools

**For Physical Devices:**

You can use `ios-deploy` to install the app directly from the command line:

```bash
# Install ios-deploy (if not already installed)
brew install ios-deploy

# Build and install to connected device
dotnet build -c Release -f net10.0-ios -r ios-arm64
ios-deploy --bundle bin/Release/net10.0-ios/ios-arm64/IOSSokolApp.app
```

**For iOS Simulator:**

Use `xcrun simctl` to install and launch on simulator:

```bash
# Build for simulator (Apple Silicon Mac)
dotnet build -f net10.0-ios -r iossimulator-arm64

# List available simulators and find the device UDID
xcrun simctl list devices

# Boot the simulator (if not already running)
xcrun simctl boot "iPhone 17"

# Install the app
xcrun simctl install booted bin/Debug/net10.0-ios/iossimulator-arm64/IOSSokolApp.app

# Launch the app
xcrun simctl launch booted com.companyname.IOSSokolApp
```

**Viewing Simulator Logs:**

To monitor simulator logs and debug issues:

```bash
# Follow logs in real-time (recommended)
xcrun simctl spawn booted log stream --predicate 'processImagePath contains "IOSSokolApp"' --level debug

# Or use Console.app
# 1. Open Console.app
# 2. Select your simulator device from the left sidebar
# 3. Filter by "IOSSokolApp" in the search box

# View crash logs
xcrun simctl spawn booted log show --predicate 'eventMessage contains "IOSSokolApp"' --last 10m

# Alternative: Get system log path and tail it
xcrun simctl get_app_container booted com.companyname.IOSSokolApp
tail -f ~/Library/Logs/CoreSimulator/<SIMULATOR_UDID>/system.log
```

**Troubleshooting Launch Issues:**

```bash
# 1. Verify app is installed
xcrun simctl listapps booted | grep -i iossokolapp

# 2. Uninstall and reinstall if needed
xcrun simctl uninstall booted com.companyname.IOSSokolApp
xcrun simctl install booted bin/Debug/net10.0-ios/iossimulator-arm64/IOSSokolApp.app

# 3. Launch with verbose output
xcrun simctl launch --console booted com.companyname.IOSSokolApp

# 4. Check if app crashed immediately
xcrun simctl diagnose --all --output ~/Desktop/simulator-diagnostics
```

**Tip:** You can also use the device UDID instead of "booted":
```bash
# Find your simulator's UDID from the list
xcrun simctl list devices | grep "iPhone 17"

# Use the UDID directly
xcrun simctl install <UDID> bin/Debug/net10.0-ios/iossimulator-arm64/IOSSokolApp.app
xcrun simctl launch <UDID> com.companyname.IOSSokolApp
```

## Project Structure

- `Main.cs` - Application entry point
- `AppDelegate.cs` - iOS application delegate
- `ViewController.cs` - Main view controller with UI button
- `MetalView.cs` - Custom MTKView with Sokol GFX integration (Metal backend)
  - `MetalView` - MTKView subclass for Metal rendering surface
  - `MetalViewDelegate` - Rendering delegate for draw calls (IMTKViewDelegate)
  - `SokolRenderer` - Core rendering logic with Sokol GFX
  - `IOSEnvironment()` - Sokol environment setup for Metal backend
  - `IOSSwapchain()` - Extracts actual Metal drawables from MTKView per frame
  - `Initialize()` - Sokol GFX initialization with Metal device and view
  - `Render()` - Rendering loop with MTKView drawable management
- `Info.plist` - iOS app configuration
- `IOSSokolApp.csproj` - Project configuration with multi-architecture support
- `shaders/` - Shader source files (GLSL → Metal via sokol-shdc)

## Important Implementation Notes

### ✅ Correct Approach
```csharp
// Initialize with Metal device and environment
var env = IOSEnvironment(colorFormat, depthFormat);
env.metal.device = (void*)device.Handle;

sg_setup(new sg_desc()
{
    environment = env,
    logger = { func = &slog_func }
});

// Extract actual Metal drawables from MTKView each frame
public void Render(MTKView view)
{
    pass.swapchain = IOSSwapchain(view);  // Gets CurrentDrawable and DepthStencilTexture
    sg_begin_pass(pass);
    
    // Manual delta time
    long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    float deltaSeconds = (currentTime - _lastFrameTime) / 1000.0f;
}
```

### ❌ Will Crash
```csharp
// DO NOT pass null/IntPtr.Zero for Metal drawables!
metal = new sg_metal_swapchain
{
    current_drawable = (void*)IntPtr.Zero,  // ❌ Crashes sg_begin_pass
    depth_stencil_texture = (void*)IntPtr.Zero  // ❌ Fails validation
};

// DO NOT use sapp_* functions in native iOS!
| Feature | AndroidSokolApp | IOSSokolApp |
|---------|-----------------|-------------|
| **Platform** | Android (API 24+) | iOS 13.0+ |
| **Rendering View** | `GLSurfaceView` | `MTKView` |
| **Context** | Auto-managed by GLSurfaceView | `MTLDevice` (Metal) |
| **Rendering API** | OpenGL ES 3.1 | Metal |
| **Backend** | Sokol GFX (GLES3 backend) | Sokol GFX (Metal backend) |
| **Time Source** | `Java.Lang.JavaSystem.CurrentTimeMillis()` | `DateTimeOffset.UtcNow` |
| **Viewport Query** | `GLES31.GlGetIntegerv()` | `MTKView.DrawableSize` |
| **UI Framework** | Android Activity + XML Layout | UIViewController + UIButton |


## Comparison with AndroidSokolApp

Both projects demonstrate native Sokol GFX integration without Sokol App:

| Feature | AndroidSokolApp | IOSSokolApp |
|---------|-----------------|-------------|
| **Platform** | Android (API 24+) | iOS 13.0+ |
| **Rendering View** | `GLSurfaceView` | `MTKView` |
| **Context** | Auto-managed by GLSurfaceView | `MTLDevice` (Metal) |
| **Rendering API** | OpenGL ES 3.1 | Metal |
| **Backend** | Sokol GFX (GLES3 backend) | Sokol GFX (Metal backend) |
| **Time Source** | `Java.Lang.JavaSystem.CurrentTimeMillis()` | `DateTimeOffset.UtcNow` |
| **Viewport Query** | `GLES31.GlGetIntegerv()` | `MTKView.DrawableSize` |
| **UI Framework** | Android Activity + XML Layout | UIViewController + UIButton |

## Use Cases

This example is ideal for:
- Integrating Sokol GFX into native .NET iOS apps
- Custom Metal rendering in iOS applications
- High-performance graphics without MAUI overhead
- Learning Metal backend integration with Sokol GFX

## Related Projects

- [AndroidSokolApp](../AndroidSokolApp) - Android counterpart using OpenGL ES
- [Sokol Headers](https://github.com/floooh/sokol) - Original Sokol library
- [.NET for iOS Documentation](https://learn.microsoft.com/dotnet/ios/)
- [MTKView Documentation](https://developer.apple.com/documentation/metalkit/mtkview)
- [Metal Documentation](https://developer.apple.com/metal/)

## Troubleshooting

### App fails to launch on physical device

**Cause:** Device not included in provisioning profile

**Symptoms:**
- App works on iPad but fails on iPhone (or vice versa)
- Error: "Failed to launch the app with bundle identifier 'com.sokol.IOSSokolApp' on the device for unknown reasons"
- Rider/Visual Studio shows "The app failed to launch for unknown reasons"

**Solution:** Add your device to the provisioning profile:

1. **Get your device's UDID:**
   - Connect your device via USB
   - Open Finder (macOS Catalina+) or iTunes (older macOS)
   - Click on your device and find the UDID (Serial Number field - click to reveal UDID)
   - Or run: `xcrun devicectl device info details --device <your-device-id> | grep identifier`

2. **Register device in Apple Developer portal:**
   - Visit https://developer.apple.com/account/resources/devices/list
   - Sign in with your Apple ID
   - Click the **"+"** button to register a new device
   - Enter device name (e.g., "My iPhone") and UDID
   - Click **Continue** and **Register**

3. **Update the Provisioning Profile:**
   - Go to https://developer.apple.com/account/resources/profiles/list
   - Find your provisioning profile (typically "iOS Team Provisioning Profile: *")
   - Click **Edit**
   - Check the box next to your newly added device
   - Click **Generate** or **Save**
   - **Download** the updated provisioning profile

4. **Install the provisioning profile:**
   - Double-click the downloaded `.mobileprovision` file to install it
   - Or manually copy to: `~/Library/MobileDevice/Provisioning Profiles/`

5. **Clean and rebuild:**
   ```bash
   cd examples/IOSSokolApp
   rm -rf bin obj
   dotnet build -c Release -r ios-arm64
   ```

6. **Deploy again** from Rider/Visual Studio or command line

### App crashes on launch or at sg_begin_pass

**Cause 1:** Metal device creation failed or Sokol GFX initialization error
**Solution:** Check Console output for error messages. Ensure device supports Metal (iOS 13.0+ required).

**Cause 2:** Null Metal drawables passed to swapchain
**Solution:** Verify `IOSSwapchain()` extracts actual drawables from MTKView:
```csharp
var currentDrawable = view.CurrentDrawable;
var depthTexture = view.DepthStencilTexture;
metal.current_drawable = (void*)(currentDrawable?.Handle ?? IntPtr.Zero);
```
Check for Sokol validation errors:
- `VALIDATE_BEGINPASS_SWAPCHAIN_METAL_EXPECT_CURRENTDRAWABLE`
- `VALIDATE_BEGINPASS_SWAPCHAIN_METAL_EXPECT_DEPTHSTENCILTEXTURE`

### Black screen

**Cause:** Shaders not compiled or loaded correctly

**Solution:** Verify shader files are present in `shaders/compiled/ios/` directory

### Build fails with "Could not find iOS SDK"

**Cause:** Xcode or Command Line Tools not properly installed

**Solution:** Install Xcode 14+ and run `xcode-select --install`

### Build fails with "linking in dylib built for 'iOS'" on simulator

**Cause:** Missing simulator framework or using wrong RuntimeIdentifier

**Solution:** 
1. Ensure simulator frameworks are built: Run `./scripts/build-ios-sokol-library.sh all` from repository root
2. Use correct RuntimeIdentifier:
   - Apple Silicon simulator: `-r iossimulator-arm64`
   - Intel simulator: `-r iossimulator-x64`
3. Verify frameworks exist:
   - `libs/ios/simulator-arm64/{debug,release}/sokol.framework`
   - `libs/ios/simulator-x64/{debug,release}/sokol.framework`

## Notes

- This is a **native .NET iOS application**, not MAUI
- Uses standard iOS APIs (`UIKit`, `Metal`, `MetalKit`) directly
- Uses **Metal** as the rendering backend (Apple's recommended graphics API)
- MTKView manages Metal drawables automatically - must extract them each frame
- Metal device (`IMTLDevice`) is passed to Sokol via `sg_environment.metal.device`
- Designed for landscape orientation by default
- Demonstrates proper resource cleanup in `Cleanup()` method
- Status bar is hidden for fullscreen rendering experience
- Metal is significantly faster and more efficient than deprecated OpenGL ES on iOS
