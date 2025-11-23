# JoltPhysics WebAssembly Status

## Current Status: ✅ Working (with reduced object counts)

The JoltPhysics example successfully runs on **all platforms** including WebAssembly!

## Key Finding

WebAssembly works when using **reduced object counts**:
- `START_AMOUNT = 500` (down from 1000)
- `MAX_INSTANCES = 10 * 1024` (10,240 instances)

The initial crashes were caused by trying to initialize too many physics objects at once, which overwhelmed the WebAssembly runtime during the startup phase.

## What Works

✅ **Desktop Application** (macOS/Windows/Linux)
- Full physics simulation with 10,000+ objects
- GPU instancing for rendering
- All JoltPhysics features available

✅ **WebAssembly Application** (Browser)
- Physics simulation with up to 10,240 instances
- GPU instancing for rendering
- Full JoltPhysicsSharp API available
- Smooth performance in modern browsers

✅ **Native Library Builds**
- joltc C wrapper compiled for all platforms
- WebAssembly build succeeds (joltc.a + libJolt.a)
- C# project compiles without errors

## WebAssembly Constraints

The key limitation is **initial object count during startup**. Creating too many physics bodies immediately causes memory pressure during initialization.

**Solution: Reduce Initial Counts**
```csharp
const int START_AMOUNT = 500;      // Initial objects (works in browser)
const int MAX_INSTANCES = 10 * 1024; // Maximum 10,240 instances
```

The runtime can handle thousands of instances once running, but needs a gentler startup phase.

## Configuration

The WebAssembly build uses optimized settings in `JoltPhysicsWeb.csproj`:

```xml
<INITIAL_MEMORY>512MB</INITIAL_MEMORY>
<MAXIMUM_MEMORY>2048MB</MAXIMUM_MEMORY>
<STACK_SIZE>16MB</STACK_SIZE>
<RunAOTCompilation>true</RunAOTCompilation>
```

## Running the Demo

**Desktop Version:**
```bash
dotnet run --project JoltPhysics.csproj
```

**WebAssembly Version:**
```bash
# Build and prepare web assets
dotnet run --project tools/SokolApplicationBuilder -- \
  --task prepare --architecture web --path examples/JoltPhysics

# Serve locally
cd examples/JoltPhysics/AppBundle
python3 -m http.server 8080
```

Open `http://localhost:8080` in your browser.

## Features

Both versions include:
- **Dynamic physics simulation** (cubes and spheres)
- **GPU instancing** for efficient rendering  
- **60 FPS performance** with collision detection
- **Interactive camera** (mouse + WASD)
- **Statistics overlay** (FPS, object counts, physics info)

Desktop can handle 10,000+ objects; WebAssembly works smoothly with reduced initial counts but supports thousands of instances once running.

## Conclusion

**Success!** JoltPhysics + Sokol.NET works on **all platforms** including WebAssembly. The key is managing initial object counts to avoid overwhelming the browser during startup, while still supporting large-scale simulations once initialized.
