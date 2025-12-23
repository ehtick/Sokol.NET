# SkiaSokolApp - SkiaSharp Sample Gallery

A cross-platform sample gallery demonstrating SkiaSharp rendering capabilities using Sokol for graphics and windowing. This application showcases various SkiaSharp features including shapes, text rendering, image processing, animations, and more.

## Features

- **40+ Interactive Samples**: Demonstrating various SkiaSharp capabilities
- **Cross-Platform**: Runs on Desktop (Windows, macOS, Linux), Mobile (iOS, Android), and Web (WebAssembly)
- **ImGui Interface**: Built-in sample browser with navigation controls
- **Dual Display**: Samples rendered both on a rotating 3D cube and as a 2D preview in the ImGui window
- **Interactive Input**: Mouse and touch support for sample interaction

## Architecture

### Rendering Pipeline

The application uses a dual-stage rendering approach:

1. **SkiaSharp Rendering**: Samples render to an 1024x1024 SkiaSharp bitmap
2. **Dual Display**: The bitmap is applied as a texture in two places:
   - **3D Cube**: Rotating cube in the background using Sokol pipeline
   - **ImGui Preview**:  flat preview in the sample browser UI (Imgui texture)

### Threading Model

The application uses different threading strategies based on the platform:

#### Desktop & Mobile (Multithreaded)
```csharp
// Async drawing on background thread
drawTask = Task.Run(() =>
{
    lock (drawLock)
    {
        state.bitmap.Prepare();
        currentSample?.DrawSample(canvas, width, height);
        state.bitmap.FlushCanvas();
        textureNeedsUpdate = true;
    }
});

// Texture update on main thread
if (textureNeedsUpdate)
{
    lock (drawLock)
    {
        state.bitmap.UpdateTexture();
        textureNeedsUpdate = false;
    }
}
```

**Benefits:**
- Non-blocking UI
- Better performance for complex samples
- Smooth frame rates

#### Web (Single-threaded)
```csharp
// Synchronous drawing (no threading support in WebAssembly)
if(currentSample?.IsDrawOnce == false)
{
    state.bitmap.Prepare();
}
currentSample?.DrawSample(canvas, width, height);
state.bitmap.FlushCanvas();
state.bitmap.UpdateTexture();
```

**Web Constraints:**
- No multithreading support in WebAssembly
- Samples set `IsDrawOnce = true` by default to avoid unnecessary redraws
- Animated samples use `IsDrawOnce = false` for continuous updates
- Some samples may not work due to threading limitations

### Sample Base Classes

#### `SampleBase`
Base class for static samples that render once.

**Key Features:**
- One-time rendering (configurable via `IsDrawOnce`)
- Touch/mouse input support via `OnTapped()`
- Pan and pinch gestures for canvas manipulation

#### `AnimatedSampleBase`
Extends `SampleBase` for animated samples.

**Desktop/Mobile:**
```csharp
// Background loop for updates
Task.Run(async () =>
{
    while (!cts.IsCancellationRequested)
    {
        await OnUpdate(cts.Token);
        Refresh();
    }
});
```

**Web:**
```csharp
// Synchronous update before each draw
public override void DrawSample(SKCanvas canvas, int width, int height)
{
    _ = OnUpdate(CancellationToken.None); // Fire and forget
    base.DrawSample(canvas, width, height);
}
```

## Sample Categories

- **General**: Basic shapes and rendering
- **Text**: Text rendering, shaping (Arabic, complex scripts), and layout
- **Bitmap Decoding**: Image loading (PNG, JPEG, GIF, DNG)
- **Image Filters**: Blur, magnifier, dilate, erode, color filters
- **Shaders**: Gradients, noise, bitmap shaders
- **Animations**: Lottie (Skottie), GIF frames, 3D transforms
- **Paths**: Path effects, measurements, transformations

## Platform-Specific Features

### iOS
- Custom DllImport resolver for framework loading
- Support for libSkiaSharp.framework and libHarfBuzzSharp.framework
- Proper framework path resolution at runtime
- **Native Library Setup Required**: Add the following to `Directory.Build.props`:
  ```xml
  <IOSNativeLibrary_libSkiaSharpPath>libs/skiasharp/iOS/3.119.1/runtimes/ios/native</IOSNativeLibrary_libSkiaSharpPath>
  <IOSNativeLibrary_libHarfBuzzSharpPath>libs/harfbuzzsharp/iOS/8.3.1.2/runtimes/ios/native</IOSNativeLibrary_libHarfBuzzSharpPath>
  ```

### Android
- Native library bundling for SkiaSharp and HarfBuzzSharp
- Touch input support
- Screen orientation handling
- **Native Library Setup Required**: Add the following to `Directory.Build.props`:
  ```xml
  <AndroidNativeLibrary_SkiaSharpPath>libs/skiasharp/Android/3.119.1</AndroidNativeLibrary_SkiaSharpPath>
  <AndroidNativeLibrary_HarfBuzzSharpPath>libs/harfbuzzsharp/Android/8.3.1.2/runtimes</AndroidNativeLibrary_HarfBuzzSharpPath>
  ```

### Web
- WebAssembly native library linking
- Single-threaded execution model
- Conditional compilation for threading code (`#if WEB`)
- **Native Library Setup Required**: Add the following to `SkiaSokolAppWeb.csproj`:
  ```xml
  <SkiaSharpLibPath>libs/skiasharp/webassembly/3.119.1/buildTransitive/netstandard1.0/libSkiaSharp.a/3.1.56/st/libSkiaSharp.a</SkiaSharpLibPath>
  <HarfBuzzSharpLibPath>libs/harfbuzzsharp/webassembly/8.3.1.2/buildTransitive/netstandard1.0/libHarfBuzzSharp.a/3.1.56/st/libHarfBuzzSharp.a</HarfBuzzSharpLibPath>
  ```

## Web Compatibility

### Working on Web
- Static rendering samples (shapes, text, images)
- Animations with proper Web handling (Skottie, GIF decoding)
- Touch input

### Limited/Not Working on Web
- Samples requiring multithreading (`Task.Run`, `Task.Delay`)
- Blocking operations (`.GetAwaiter().GetResult()`)
- Real-time video decoding

### Web Adaptation Pattern

Samples adapted for Web use conditional compilation:

```csharp
protected override async Task OnUpdate(CancellationToken token)
{
#if !WEB
    await Task.Delay(duration, token);
    // Update logic
#else
    accumulatedTime += sapp_frame_duration() * 1000;
    if (accumulatedTime >= duration)
    {
        // Update logic
    }
    await Task.CompletedTask;
#endif
}
```

## Building and Running

### Desktop
```bash
dotnet build SkiaSokolApp.csproj
dotnet run --project SkiaSokolApp.csproj
```

### Web
```bash
dotnet publish SkiaSokolAppWeb.csproj
# Serve the wwwroot folder with a web server
```

### iOS
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build --type debug --architecture ios \
    --interactive --path examples/SkiaSokolApp
```

### Android
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build --type debug --architecture android \
    --install --interactive --path examples/SkiaSokolApp
```

## Input Controls

### Desktop
- **Left Mouse Button**: Trigger `OnTapped()` in current sample
- **Mouse Drag**: Pan canvas (sample-specific)
- **Mouse Wheel**: Zoom canvas (sample-specific)

### Mobile
- **Touch**: Trigger `OnTapped()` in current sample
- **Touch Drag**: Pan canvas
- **Pinch**: Zoom canvas

### UI Controls
- **Prev/Next Buttons**: Navigate between samples
- **Sample Preview**: Click to view full-size in 3D

## Dependencies

- **SkiaSharp 3.119.1**: 2D graphics library
- **SkiaSharp.Skottie 3.119.1**: Lottie animation support
- **SkiaSharp.HarfBuzz 3.119.1**: Complex text shaping
- **Sokol**: Cross-platform graphics and windowing
- **ImGui**: Immediate mode UI for sample browser

## Technical Notes

### Thread Safety
Desktop and mobile builds use locking around bitmap operations:
```csharp
lock (drawLock)
{
    // Safe to access bitmap from background thread
}
```

### Texture Updates
- **Desktop/Mobile**: Texture updated on main thread when `textureNeedsUpdate` flag is set
- **Web**: Synchronous texture update after each draw

### Memory Management
- Samples implement `IDisposable` pattern
- Resources cleaned up in `OnDestroy()`
- Bitmap disposed on application cleanup

## Known Limitations

1. **Web Threading**: Some samples requiring background threads won't work
2. **Web Performance**: Complex animations may run slower than native
3. **HarfBuzz on Web**: Requires explicit native library linking
4. **Mobile Touch**: Limited gesture support in some samples

## License

See LICENSE file in the repository root.
