# Instancing Compute Example

A GPU-accelerated particle system using compute shaders to update particle physics and hardware instancing to render up to 512K particles. Demonstrates compute-to-graphics pipeline integration where storage buffers serve dual purpose as both compute shader output and vertex shader input.



## Platform Support

| Platform | Status | Notes |
|----------|--------|-------|
| **Desktop** (macOS/Windows/Linux) | ✅ **Working** |
| **iOS** | ✅ **Working** | Uses Metal compute shaders |
| **Android** | ⚠️ **Partial** | Requires OpenGL ES 3.1+ (high-end devices only) |
| **Web (WASM)** | ❌ **Not Working** | .NET SDK Emscripten version too old |

### Verified Platforms

- ✅ **macOS** (Apple Silicon & Intel) - Tested and working
- ✅ **iOS** (iPhone/iPad) - Tested and working
- ✅ **Android** (High-tier devices with OpenGL ES 3.1+) - Tested and working

## Technical Requirements

### Desktop
- Support Metal on macOS, D3D11 on Windows, OpenGL on Linux
- Compute shader support
- Storage buffer support

### iOS
- Metal API
- iOS 12+ (for compute shaders)

### Android - Device Compatibility

**Status**: ✅ **Works on high-end devices** with OpenGL ES 3.1+ support

**Build Configuration**: The project must use `glsl310es` shader target for Android to enable compute shader support. This is already configured in `instancing_compute.csproj`:
```xml
<ShaderSlang Condition="'$(DefineConstants)' != '' and $(DefineConstants.Contains('__ANDROID__'))">glsl310es</ShaderSlang>
```

**Device Requirements**:
- **OpenGL ES 3.1 or higher**
- **Compute shader support**
- **Storage buffer support in vertex shaders**

**Known Compatible Devices**:
- Modern flagship phones (2019+)
- High-tier Android devices with OpenGL ES 3.1+ drivers

**Why These Requirements?**

1. **Compute Shaders**: Require OpenGL ES 3.1+ (not available in ES 3.0)
   - Compute shaders were introduced in OpenGL ES 3.1 (2014)
   - Many budget and older devices still use OpenGL ES 3.0

2. **Storage Buffers in Vertex Shaders**: The display shader requires:
   - The vertex shader reads particle positions from a storage buffer
   - OpenGL ES 3.0 allows 0 storage buffers in vertex shaders
   - Storage buffers require OpenGL ES 3.1+

3. **Hardware Limitations**: Budget/older Android devices don't have:
   - Compute shader hardware
   - Storage buffer support (SSBO)
   - OpenGL ES 3.1 drivers

**Recommended Approach**: 

For production apps targeting wide Android compatibility:
1. **Detect OpenGL ES version at runtime** (check for ES 3.1+ support)
2. **Provide fallback** for OpenGL ES 3.0 devices:
   - CPU particle updates (slower but compatible)
   - Simpler graphics-only demo
   - Or gracefully disable the feature
3. **Consider Vulkan backend** for best performance on modern devices
4. **Test on multiple device tiers** (flagship, mid-range, budget)

### WebAssembly - Emscripten Version Limitation

**Current Issue**: .NET SDK bundles older Emscripten versions that lack WebGPU compute shader support:
- .NET 8: Emscripten 3.1.34
- .NET 10: Emscripten 3.1.56
- **Required**: Emscripten 4.0.10+ (for `emdawnwebgpu` port)

**Status**: Blocked until .NET SDK bundles Emscripten 4.0.10 or later (timeline unknown)

## Building

### Desktop
```bash
dotnet build instancing_compute.csproj
dotnet run --project instancing_compute.csproj
```

### iOS
```bash
dotnet run --project ../../tools/SokolApplicationBuilder -- \
  --task build \
  --type release \
  --architecture ios \
  --install \
  --interactive \
  --orientation landscape \
  --path .
```

### Android
```bash
dotnet run --project ../../tools/SokolApplicationBuilder -- \
  --task build \
  --type release \
  --architecture android \
  --subtask aab \
  --install \
  --interactive \
  --path .
```

## Shader Compilation

Shaders are automatically compiled during build. To manually compile:
```bash
dotnet build instancing_compute.csproj -t:CompileShaders
```

Shader outputs:
- `shaders/compiled/osx/` - Metal shaders (macOS)
- `shaders/compiled/ios/` - Metal shaders (iOS)
- `shaders/compiled/windows/` - HLSL shaders (D3D11/D3D12)
- `shaders/compiled/linux/` - GLSL shaders (OpenGL/Vulkan)
- `shaders/compiled/android/` - GLSL ES 3.10 shaders (OpenGL ES 3.1+)
- `shaders/compiled/web/` - WGSL shaders (WebGPU) - *not usable yet*

## Implementation Details

### Initialization Compute Shader (`cs_init`)
- One-time execution during initialization
- Workgroup size: 64x1x1
- Generates pseudo-random initial velocities using xorshift32
- Initializes all 512K particles at origin with random velocities
- Random velocity ranges: X [-0.5, 0.5], Y [2.0, 2.5], Z [-0.5, 0.5]

### Update Compute Shader (`cs_update`)
- Runs every frame to update particle physics
- Workgroup size: 64x1x1
- Applies gravity (acceleration -1.0 in Y direction)
- Updates positions based on velocity and delta time
- Collision detection with ground plane (Y = -2.0)
- Bounce physics with energy loss (velocity *= 0.8)
- Only processes active particles (based on `num_particles`)

### Display Shader (`vs` + `fs`)
- Vertex shader reads particle positions from storage buffer
- Uses hardware instancing to render geometry at each particle position
- Instance data contains particle position (vec4)
- Per-vertex colors interpolated across geometry
- Simple pass-through fragment shader

### Storage Buffer
```c
struct particle {
    vec4 pos;  // Position (xyz) + padding
    vec4 vel;  // Velocity (xyz) + padding
}
```

**Dual-Purpose Design**:
1. **Compute Shader**: Writes updated particle state (positions and velocities)
2. **Vertex Shader**: Reads particle positions for instanced rendering
3. **No CPU Roundtrip**: Data stays on GPU for maximum performance

### Particle Geometry
- Octahedron shape (8 triangular faces, 6 vertices)
- Per-vertex colors (RGB rainbow)
- Indexed rendering (24 indices for 8 triangles)
- Rendered once per particle using hardware instancing

### Camera System
- Perspective projection (60° FOV)
- Fixed camera position: (0, 1.5, 8)
- Looking at origin
- Continuous Y-axis rotation (60°/second)

## Performance Characteristics

- **Maximum Particles**: 512,000
- **Emission Rate**: 10 particles per frame
- **Time to Full**: ~52 seconds at 60 FPS
- **Compute Dispatches**: (num_particles + 63) / 64 workgroups
- **Geometry Instances**: num_particles (up to 512K)
- **Vertices per Particle**: 6 (octahedron)
- **Triangles per Particle**: 8
- **Total Draw Calls**: 1 (instanced rendering)

## References

- Original compute instancing example: [sokol-samples](https://github.com/floooh/sokol-samples)
- Hardware instancing: [instancing-sapp](../instancing/)
- Compute boids: [computeboids](../computeboids/)
