# JoltPhysics Demo

A high-performance physics demonstration using JoltPhysics engine with Sokol.NET graphics. This demo showcases **10,000 dynamic physics bodies** (5,000 cubes + 5,000 spheres) with real-time simulation and rendering.

## Features

### Performance Optimizations

#### GPU Instancing
- **Single draw call per object type**: All cubes rendered in one draw call, all spheres in another
- **Massive performance gain**: Rendering 10,000+ objects with just 2 draw calls instead of 10,000
- **Efficient data upload**: Instance data (transforms, colors) uploaded once per frame
- **Scales to millions**: Can handle extreme object counts limited only by memory

#### Multithreading (Desktop/Mobile)
- **Parallel physics simulation**: JoltPhysics automatically distributes work across CPU cores
- **Configurable worker threads**: Uses `ProcessorCount - 1` threads by default
- **Concurrent collision detection**: Broad phase, narrow phase, and constraint solving run in parallel
- **4096 max concurrent jobs**: Handles complex simulation with thousands of active bodies
- **Platform support**:
  - ✅ Desktop (macOS, Windows, Linux): Full multithreading
  - ✅ Android: Full multithreading
  - ✅ iOS: Full multithreading
  - ℹ️ WebAssembly: Single-threaded (browser compatibility)

### Technical Specifications

- **Physics Engine**: JoltPhysics v5.4.0
- **Rendering**: Sokol Graphics with GPU instancing
- **Objects**: 10,000 dynamic rigid bodies (5,000 cubes + 5,000 spheres) + 1 static ground plane
- **Shaders**: GLSL with per-pixel lighting (Phong shading)
- **Camera**: Free-look camera with mouse/touch controls
- **UI**: ImGui statistics overlay

## Building & Running

### Desktop (Recommended)

**Always use Release mode** for optimal performance:

```bash
# Compile shaders
dotnet build JoltPhysics.csproj -t:CompileShaders

# Run in Release mode
dotnet run --project JoltPhysics.csproj -c Release
```

### WebAssembly

**Important**: WebAssembly must be built with NativeAOT compilation for optimal performance and stability.

```bash
# Compile shaders for Web
dotnet build JoltPhysicsWeb.csproj -t:CompileShaders

# Compile to NativeAOT (required for 10,000 objects)
dotnet publish JoltPhysicsWeb.csproj
```

The output will be in `bin/Release/net8.0/browser-wasm/AppBundle/` - serve this directory with a web server.

#### Why NativeAOT for Web?

- **Debug build** (interpreter mode): Limited to ~5,000 objects due to .NET runtime call stack limitations
- **Release build** (NativeAOT): Handles full 10,000 objects without issues
- **Performance**: NativeAOT is significantly faster than interpreter mode
- **Production-ready**: This is the recommended deployment mode for web

### Android

Use VS Code Command Palette (Cmd/Ctrl+Shift+P):

1. Open Command Palette
2. Select task: `Android: Build APK` or `Android: Install APK`
3. Choose **Release** configuration
4. Select device from list

### iOS

Use VS Code Command Palette (Cmd/Ctrl+Shift+P):

1. Open Command Palette
2. Select task: `iOS: Build` or `iOS: Install`
3. Choose **Release** configuration
4. Select device from list

## Configuration

### Object Count

The demo spawns different amounts based on build configuration:

```csharp
// Desktop/Mobile: 5,000 of each type = 10,000 total
const int START_AMOUNT = 5000;

// Web Debug (interpreter): 2,500 of each type = 5,000 total
// Web Release (NativeAOT): 5,000 of each type = 10,000 total
```

### Physics Settings

```csharp
// Multithreading configuration
var jobSystemConfig = new JobSystemThreadPoolConfig
{
    maxJobs = 4096,           // Maximum concurrent jobs
    maxBarriers = 16,         // Synchronization barriers
    numThreads = ProcessorCount - 1  // Worker threads
};

// Physics system limits
MaxBodies = 65536,            // Maximum bodies in simulation
MaxBodyPairs = 65536,         // Maximum collision pairs
MaxContactConstraints = 10240  // Maximum contact points
```

## Performance

### Expected Performance

- **Desktop (Release)**:
  - 60-120 FPS with 10,000 bodies (Apple Silicon Mac)
  - 2 draw calls per frame
  - Full CPU multithreading utilized

- **WebAssembly (NativeAOT)**:
  - 30-60 FPS with 10,000 bodies (modern browsers)
  - Single-threaded physics (browser limitation)
  - Efficient WASM execution

- **Android (Release)**:
  - 60-120 FPS with 10,000 bodies (medium and flagship devices)
  - Full CPU multithreading utilized

- **iOS (Release)**:
  - 60 FPS with 10,000 bodies (modern devices)
  - Full CPU multithreading utilized

### Performance Tips

1. **Always use Release mode**: Debug builds are significantly slower
2. **Web requires NativeAOT**: Use `dotnet publish` instead of `dotnet build`
3. **Disable VSync** for performance testing
4. **Monitor statistics overlay**: Shows FPS, draw calls, and body counts

## Controls

- **Mouse**: Look around (hold left button and drag)
- **WASD**: Camera movement
- **Mouse Wheel**: Zoom in/out
- **ESC**: Exit application

## Architecture

### GPU Instancing Pipeline

1. **Per-Frame Update**:
   - Physics simulation updates all body positions/rotations
   - Instance data gathered into arrays (cubes/spheres separate)
   - Instance buffers uploaded to GPU once

2. **Rendering**:
   - Bind instance buffer + geometry buffer
   - Single draw call with instance count
   - Vertex shader reads per-instance transform/color
   - Fragment shader applies per-pixel lighting

3. **Benefits**:
   - Minimal CPU overhead (2 draw calls vs 10,000)
   - Efficient GPU utilization
   - Scales linearly with object count

### Multithreading Architecture

1. **Job System Initialization**:
   - Creates worker threads (CPU cores - 1)
   - Sets up job queue and barriers

2. **Physics Update**:
   - Main thread calls `PhysicsSystem.Update()`
   - Jolt distributes work across threads:
     - Broad phase collision detection
     - Narrow phase collision detection
     - Contact point generation
     - Constraint solving
     - Integration

3. **Synchronization**:
   - Barriers ensure work completes before proceeding
   - Main thread retrieves results for rendering

## Troubleshooting

### Web Build Crashes

- **Problem**: Crash at ~10,000 objects in Debug mode
- **Solution**: Use `dotnet publish` (NativeAOT) instead of `dotnet build`

### Low Frame Rate

- **Problem**: Running in Debug mode
- **Solution**: Always use Release configuration (`-c Release`)

### Missing Shaders

- **Problem**: Black screen or rendering errors
- **Solution**: Run shader compilation target first:
  ```bash
  dotnet build JoltPhysics.csproj -t:CompileShaders
  ```

### Android Build Fails

- **Problem**: NDK not found
- **Solution**: Set `ANDROID_NDK` or `ANDROID_NDK_HOME` environment variable

## License

This demo is part of Sokol.NET and follows its licensing terms.

## Credits

- **JoltPhysics**: High-performance physics engine by Jorrit Rouwe
- **Sokol**: Minimal cross-platform graphics library by Andre Weissflog
- **Sokol.NET**: C# bindings and framework
