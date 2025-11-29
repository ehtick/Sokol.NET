# Compute Boids Example

A flocking simulation using compute shaders to update particle positions and velocities on the GPU. Based on Craig Reynolds' Boids algorithm with three flocking rules: cohesion, separation, and alignment.


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
- Support Metal on macOS, D3D11 on Windows , OpenGL on Linux
- Compute shader support

### iOS
- Metal API
- iOS 12+ (for compute shaders)

## Known Limitations
### Android - Device Compatibility

**Status**: ✅ **Works on high-end devices** with OpenGL ES 3.1+ support

**Build Configuration**: The project must use `glsl310es` shader target for Android to enable compute shader support. This is already configured in `computeboids.csproj`:
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

**Incompatible Devices** (will show errors):

**Shader Compilation Error**:
```
error GCADCCEF8: SPIRVCross exception: At least ESSL 3.10 required for compute shaders.
**Older/Budget Devices** (typically OpenGL ES 3.0 only):

1. **Compute Shaders**: Require OpenGL ES 3.1+ (not available in ES 3.0)
   - Compute shaders were introduced in OpenGL ES 3.1 (2014)
   - Many budget and older devices still use OpenGL ES 3.0

2. **Storage Buffers in Vertex Shaders**: The display shader also requires:
   - The vertex shader reads from a storage buffer (`vs_ssbo`)
   - OpenGL ES 3.0 allows 0 storage buffers in vertex shaders
   - Storage buffers require OpenGL ES 3.1+

3. **Hardware Limitations**: Budget/older Android devices don't have:
   - Compute shader hardware
   - Storage buffer support (SSBO)
   - OpenGL ES 3.1 driversupport OpenGL ES 3.0
   - OpenGL ES 3.1 support is inconsistent across Android devices

2. **Storage Buffers in Vertex Shaders**: Even the display shader fails because:
   - The vertex shader reads from a storage buffer (`vs_ssbo`)
   - OpenGL ES 3.0 allows 0 storage buffers in vertex shaders
   - Storage buffers in vertex shaders require OpenGL ES 3.1+

3. **Hardware Limitations**: Many Android devices (especially older ones) don't have hardware support for:
   - Compute shaders
   - Storage buffers in graphics stages
   - SSBO (Shader Storage Buffer Objects)

**Why Vulkan Isn't Used**:
- While modern Android devices support Vulkan, the current build configuration uses OpenGL ES
- Switching to Vulkan would require:
  - Device capability detection at runtime
  - Separate build configuration
  - Fallback path for older devices
  - Significant build system changes

**Potential Solutions**:

1. **Add Vulkan Backend** (Complex):
   - Configure build system to use Vulkan on Android
   - Requires Android API 24+ (Android 7.0, 2016)
   - Would work on ~90% of current Android devices
   - Needs device capability checks
**Recommended Approach**: 

For production apps targeting wide Android compatibility:
1. **Detect OpenGL ES version at runtime** (check for ES 3.1+ support)
2. **Provide fallback** for OpenGL ES 3.0 devices:
   - CPU particle updates (slower but compatible)
   - Simpler graphics-only demo
   - Or gracefully disable the feature
3. **Consider Vulkan backend** for best performance on modern devices
4. **Test on multiple device tiers** (flagship, mid-range, budget)

This example demonstrates compute shader capabilities and works great on modern Android devices!
   - Would be significantly slower (10,000 particles might drop to <30 FPS)
   - Requires maintaining two code paths

3. **Fragment Shader Workaround** (Complex):
   - Use render-to-texture for particle updates instead of compute shaders
   - Encode particle data in textures
   - More complex shader code
   - Limited by texture formats and precision

**Recommended Approach**: This example is intended to demonstrate compute shader capabilities. For production use on Android, consider:
- Using Vulkan backend for modern devices
- Providing a simpler graphics-only implementation for older devices
- Or accept that compute shader examples are desktop/iOS only

### WebAssembly - Emscripten Version Limitation

**Current Issue**: .NET SDK bundles older Emscripten versions that lack WebGPU compute shader support:
- .NET 8: Emscripten 3.1.34
- .NET 10: Emscripten 3.1.56
- **Required**: Emscripten 4.0.10+ (for `emdawnwebgpu` port)

**Technical Details**:
- Sokol's WebGPU backend uses native WebGPU implementation
- Native WebGPU in Emscripten requires the `emdawnwebgpu` port (added in Emscripten 4.0.10)
- The `--use-port=emdawnwebgpu` flag is not recognized by Emscripten 3.1.x
- Cannot override .NET SDK's bundled Emscripten version when using `dotnet publish`

**Status**: Blocked until .NET SDK bundles Emscripten 4.0.10 or later (timeline unknown)

**Alternative Approaches** (not currently implemented):
1. Manual Emscripten 4.0.20+ build outside of `dotnet publish` workflow
2. Complete rewrite using browser WebGPU API (WebGPU.NET library)
3. Wait for future .NET SDK update with newer Emscripten

## Building

### Desktop
```bash
dotnet build computeboids.csproj
dotnet run --project computeboids.csproj
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

## Shader Compilation

Shaders are automatically compiled during build. To manually compile:
```bash
dotnet build computeboids.csproj -t:CompileShaders
```

Shader outputs:
- `shaders/compiled/osx/` - Metal shaders (macOS)
- `shaders/compiled/ios/` - Metal shaders (iOS)
- `shaders/compiled/windows/` - HLSL shaders (D3D11/D3D12)
- `shaders/compiled/linux/` - GLSL shaders (OpenGL/Vulkan)
- `shaders/compiled/web/` - WGSL shaders (WebGPU) - *not usable yet*
- `shaders/compiled/android/` - GLSL ES shaders - *compute not supported*

## Implementation Details

### Compute Shader (`cs`)
- Workgroup size: 64x1x1
- Updates particle positions and velocities
- Implements three flocking rules:
  - **Rule 1 (Cohesion)**: Steer towards center of mass of nearby boids
  - **Rule 2 (Separation)**: Avoid getting too close to other boids
  - **Rule 3 (Alignment)**: Steer towards average velocity of nearby boids
- Uses ping-pong storage buffers (input read-only, output write-only)
- Wraps particles at world boundaries (-1 to 1)

### Display Shader (`vs` + `fs`)
- Vertex shader synthesizes triangle vertices (no vertex buffer)
- Reads particle data from storage buffer
- Rotates triangle based on velocity direction
- Per-instance rendering using `gl_InstanceIndex`
- Fragment shader outputs interpolated color based on velocity and rotation

### Storage Buffers
```c
struct particle {
    vec2 pos;  // Position in normalized device coordinates
    vec2 vel;  // Velocity vector
}
```

### UI Controls
- **dt**: Simulation timestep (0.0001 - 0.1)
- **rule1_distance**: Cohesion radius (0.0 - 0.5)
- **rule2_distance**: Separation radius (0.0 - 0.5)
- **rule3_distance**: Alignment radius (0.0 - 0.5)
- **rule1_scale**: Cohesion strength (0.0 - 0.2)
- **rule2_scale**: Separation strength (0.0 - 0.2)
- **rule3_scale**: Alignment strength (0.0 - 0.2)
- **num_particles**: Particle count (256 - 10000)

## References

- Original compute boids example: [sokol-samples](https://github.com/floooh/sokol-samples)
- Vulkan flocking implementation: [Austin Eng's Project6-Vulkan-Flocking](https://github.com/austinEng/Project6-Vulkan-Flocking)
- Craig Reynolds' Boids: [Red3d.com](https://www.red3d.com/cwr/boids/)
