# BulletPhysics

A rigid-body physics demo integrating [Bullet Physics](https://github.com/bulletphysics/bullet3) into [Sokol.NET](../../README.md). Spawns 5 000 cubes and spheres that fall onto a ground plane and interact with each other, rendered with instanced draw calls and Phong lighting via a custom GLSL shader.

> **Note:** The [JoltPhysics](../JoltPhysics) example performs significantly better on all platforms. Prefer it for production use.

## Screenshot

![Bullet Physics Demo](screenshots/Screenshot%202026-04-25%20at%2020.23.54.png)

## Features

- **5 000 rigid bodies** at startup — mix of cubes and spheres with randomised positions and colors.
- **Instanced rendering** — all cubes drawn in one call, all spheres in one call, using per-instance model matrix + color vertex buffers.
- **Phong lighting** — directional light with per-fragment normals in the vertex shader.
- **Free-look camera** — WASD movement, left-click drag to look, scroll to zoom.
- **ImGui statistics overlay** — FPS and frame time smoothed over 500 ms, live body counts.
- **Multi-threaded solver** on desktop (Windows/macOS/Linux); single-threaded on WebAssembly.
- **MSAA 4×** anti-aliasing.

## Controls

| Input | Action |
|-------|--------|
| `W` `A` `S` `D` | Move camera |
| Left-click + drag | Look around |
| Scroll wheel | Zoom in / out |

## Running

```bash
# Run on desktop (JIT)
dotnet run --project BulletPhysics.csproj

# Build for WebAssembly
dotnet run --project ../../tools/SokolApplicationBuilder -- --task build --architecture web --path examples/BulletPhysics

# Serve WASM locally
dotnet serve --directory bin/Release/net10.0/browser-wasm/AppBundle
```

## Project Structure

```
BulletPhysics/
├── Source/
│   └── BulletPhysics-app.cs   # Main application logic
├── shaders/                   # GLSL shader sources and compiled output
├── screenshots/
├── BulletPhysics.csproj
└── BulletPhysicsWeb.csproj
```

## See Also

- [JoltPhysics](../JoltPhysics) — higher-performance physics demo using Jolt Physics
- [Sokol.NET](../../README.md) — root readme with platform support and full example list
