# Box2D Physics Demo

A real-time physics simulation using Box2D 3.2.0 and Sokol.NET, featuring multiple shape types (boxes, circles, triangles) with interactive spawning and collision detection.

## Features

- **Multiple Shape Types**: Boxes, circles, and triangles with full physics simulation
- **Dynamic Spawning**: Auto-spawn 5 objects every 0.3 seconds with random shapes, colors, and sizes
- **Mouse Interaction**: Click anywhere to spawn shapes that cycle between box → circle → triangle
- **High Performance**: Supports up to 300 physics bodies with 30,000 vertices
- **Cross-Platform**: Runs on Desktop (macOS, Windows, Linux) and Web (WebAssembly)
- **Realistic Physics**: Uses Box2D's collision detection, rigid body dynamics, and gravity

## Controls

- **Left Mouse Click**: Spawn a shape at the cursor position (cycles through box/circle/triangle)
- **Auto-Spawn**: Objects automatically spawn at the top every 0.3 seconds

## Building

### Desktop
```bash
dotnet build box2dPhysics.csproj
dotnet run --project box2dPhysics.csproj
```

### Web (WebAssembly)
```bash
dotnet build box2dPhysicsWeb.csproj
# Serve wwwroot folder with a web server
```

## Technical Details

### Physics Configuration
- **World Gravity**: (0, -10) m/s²
- **Time Step**: 1/60 second (60 FPS)
- **Sub-Steps**: 4 per frame for stability
- **Ground Body**: 40m wide × 2m tall static box at y = -10

### Shape Details
- **Boxes**: Square bodies with rotation, rendered as 2 triangles (6 vertices)
- **Circles**: Rendered with 16 segments as triangle fan (48 vertices)
- **Triangles**: Computed using `b2ComputeHull` for proper convex hull, rendered as 3 vertices

### Rendering
- **Graphics API**: Sokol GFX (OpenGL/Metal/D3D11/WebGL2)
- **Projection**: Orthographic 2D (zoom level: 25 units)
- **Shader**: GLSL vertex/fragment shader compiled via sokol-shdc
- **Vertex Format**: Position (vec2) + Color (vec4)

## WebAssembly-Specific Pitfalls & Solutions

### Critical Issue: Struct Memory Corruption

**Problem**: When using a `struct` for the state class in WebAssembly, arrays would report incorrect lengths (72 instead of 100), causing crashes and memory corruption. Desktop builds worked perfectly with the same code.

**Root Cause**: WebAssembly has different memory layout and pointer arithmetic for structs compared to native platforms. Arrays inside structs can have corrupted metadata, leading to:
- `IndexOutOfRangeException` when accessing valid indices
- Memory access violations in Box2D API calls (`b2Body_GetPosition`)
- Arrays reporting wrong `Length` property

**Solution**: Changed `_state` from `struct` to `class`:
```csharp
// ❌ WRONG - Causes memory corruption in WebAssembly
struct _state 
{
    public b2BodyId[] bodies = new b2BodyId[MAX_BODIES];
    // ... other arrays
}

// ✅ CORRECT - Works on all platforms
class _state
{
    public b2BodyId[] bodies = new b2BodyId[MAX_BODIES];
    // ... other arrays
}
```

Using a class ensures heap allocation with proper reference semantics, avoiding struct layout issues in WebAssembly.

### Box2D API: Pass-by-Reference Parameters

**Problem**: Box2D 3.0+ uses `in` parameters for pass-by-reference, not pointers.

**Solution**: Use `in` keyword when passing vertices to `b2ComputeHull`:
```csharp
fixed (b2Vec2* pVerts = verts)
{
    b2Hull hull = b2ComputeHull(in *pVerts, 3);  // Note the 'in' keyword
    // ...
}
```

### Missing sapp_event Fields

**Problem**: Attempting to access `e->timestamp` field resulted in compilation errors.

**Solution**: Use `e->frame_count` instead for cycling through shapes:
```csharp
int shapeType = (int)(e->frame_count % 3);  // Cycles 0, 1, 2
```

### Array Bounds Safety

**Defensive Programming**: Even after fixing the struct issue, added bounds checking for safety:
```csharp
if (i >= state.bodies.Length || i >= state.body_sizes.Length)
{
    break;  // Safety check
}
```

### Error Handling

Wrapped Box2D calls in try-catch for graceful degradation:
```csharp
try
{
    b2Vec2 pos = b2Body_GetPosition(state.bodies[i]);
    // ... render body
}
catch (Exception ex)
{
    Console.WriteLine($"Error rendering body {i}: {ex.Message}");
}
```

## Performance Considerations

- **Vertex Buffer**: Dynamic streaming buffer updated every frame
- **Draw Calls**: Single draw call for all objects (batched rendering)
- **Physics**: Box2D handles collision detection efficiently
- **Memory**: Pre-allocated arrays for 300 bodies to avoid GC pressure

## Architecture

```
box2d-app.cs
├── Init()           - Initialize graphics, physics world, ground body
├── CreateBox()      - Spawn box with b2MakeBox
├── CreateCircle()   - Spawn circle with b2Circle
├── CreateTriangle() - Spawn triangle with b2ComputeHull
├── Frame()          - Physics step + rendering loop
│   ├── b2World_Step()      - Advance physics simulation
│   ├── Auto-spawn logic    - Timer-based spawning
│   ├── AddBoxVertices()    - Generate box geometry
│   ├── AddCircleVertices() - Generate circle segments
│   └── AddTriangleVertices() - Generate triangle geometry
├── Event()          - Handle mouse input
└── Cleanup()        - Destroy physics world, shutdown graphics
```

## Dependencies

- **Sokol.NET**: Graphics, windowing, input
- **Box2D 3.2.0**: Physics engine with C# P/Invoke bindings
- **System.Numerics**: Vector math (Vector2, Vector4, Matrix4x4)

## Known Limitations

- Objects are not removed when reaching maximum count (300 bodies)
- No sleep/wake optimization (all bodies always active)
- Fixed spawn rate (not adjustable at runtime)

## Future Enhancements

- Body removal when falling off-screen
- Adjustable gravity and spawn rate via UI
- Additional shape types (capsules, polygons)
- Joints and constraints (hinges, springs)
- Physics debug visualization (contact points, velocity vectors)
