# ShaderToy Gallery

A multi-shader demonstration application built with [Sokol.NET](https://github.com/elix22/Sokol.NET), showcasing beautiful ShaderToy examples with an interactive GUI for shader selection.

![ShaderToy Gallery](screenshots/gallery.png)

## Features

- **Multiple Shader Examples** - Browse and switch between different shader effects in real-time
- **Interactive GUI** - ImGui-based interface for shader selection and controls
- **Cross-Platform** - Runs on Desktop (Windows, macOS, Linux), Web (WebAssembly), iOS, and Android
- **Mouse & Touch Support** - Full interaction support for both desktop and mobile devices
- **Real-Time Rendering** - Smooth 60+ FPS shader rendering with Sokol graphics backend

## Included Shaders

### 1. Raymarching Primitives
Beautiful signed distance field (SDF) raymarching demonstration featuring 20+ geometric primitives with lighting, soft shadows, and ambient occlusion.

- **Author:** Inigo Quilez
- **License:** MIT
- **Source:** https://www.shadertoy.com/view/Xds3zN

### 2. Procedural Ocean
Stunning animated ocean with realistic waves, reflections, atmospheric scattering, and dynamic sun position.

- **Author:** afl_ext
- **License:** MIT
- **Source:** https://www.shadertoy.com/view/MdXyzX

### 3. Stormy Torus
Mesmerizing procedural torus with storm-like effects and beautiful color patterns.

- **Author:** Jaenam
- **License:** CC BY-NC-SA 4.0
- **Source:** https://www.shadertoy.com/view/tcccRl

### 4. Universe Ball
Spectacular gem-like sphere with cosmic colors and intricate procedural patterns, inspired by Jaenam's gem shaders.

- **Author:** diatribes 
- **License:** CC BY-NC-SA 3.0
- **Source:** https://www.shadertoy.com/view/WcGcWV

### 5. Fractal Land
Dynamic fractal landscape ("Fractal Cartoon") with animated waves, edge detection, and a rotating sun. Features an animated Nyan Cat with rainbow trail flying through the scene.

- **Author:** Kali
- **License:** CC BY-NC-SA 3.0 (default ShaderToy license)
- **Source:** https://www.shadertoy.com/view/XsBXWt

### 6. Fractal Pyramid
Hypnotic rotating fractal pyramid with colorful volumetric raymarching and smooth color gradients. Beautiful geometric patterns emerge from iterative transformations.

- **Author:** bradjamesgrant
- **License:** CC BY-NC-SA 3.0 (default ShaderToy license)
- **Source:** https://www.shadertoy.com/view/tsXBzS

### 7. Gemmarium
Stunning jellyfish-like gem with iridescent colors and volumetric clouds. Features a mirrored floor, animated orb, and mesmerizing procedural turbulence creating a "world in a gem" effect.

- **Author:** Jaenam
- **License:** CC BY-NC-SA 4.0
- **Source:** https://www.shadertoy.com/view/WftcWs
- **Date:** 2025-11-28


## Building & Running

### Desktop

```bash
# Compile shaders
dotnet build ShaderToyApp.csproj -t:CompileShaders

# Build application
dotnet build ShaderToyApp.csproj

# Run
dotnet run --project ShaderToyApp.csproj
```

### WebAssembly

```bash
# Prepare web build
dotnet run --project tools/SokolApplicationBuilder -- --task prepare --architecture web --path examples/ShaderToyApp

# Build for web
dotnet build ShaderToyAppWeb.csproj

# Serve the application
cd bin/Debug/net8.0/browser-wasm/AppBundle
python3 -m http.server 8080
```

Then open http://localhost:8080 in your browser.

### Android

```bash
# Build and install APK
dotnet run --project tools/SokolApplicationBuilder -- --task build --type release --architecture android --install --interactive --path examples/ShaderToyApp
```

### iOS

```bash
# Build and install
dotnet run --project tools/SokolApplicationBuilder -- --task build --type release --architecture ios --install --interactive --orientation landscape --path examples/ShaderToyApp
```

## Project Structure

```
ShaderToyApp/
├── Source/
│   ├── Program.cs              # Entry point
│   └── ShaderToyApp-app.cs     # Main application logic
├── shaders/
│   ├── raymarching.glsl        # Raymarching primitives shader
│   ├── proceduralocean.glsl    # Ocean shader
│   ├── stormytorus.glsl        # Stormy torus shader
│   ├── universeball.glsl       # Universe ball shader
│   ├── fractalland.glsl        # Fractal land shader
│   ├── fractalpyramid.glsl     # Fractal pyramid shader
│   └── gemmarium.glsl          # Gemmarium shader
├── Assets/                      # Application assets
└── LICENSE.md                   # License information
```

## Adding New Shaders

To add a new shader to the gallery:

1. **Create the shader file** in `shaders/yourshader.glsl`:
```glsl
@vs vs
layout(binding=0) uniform vs_params {
    float aspect;
    float time;
};

in vec2 position;
out vec2 uv;

void main() {
    gl_Position = vec4(position, 0.0, 1.0);
    uv.x = position.x * aspect;
    uv.y = position.y;
}
@end

@fs fs
layout(binding=1) uniform fs_params {
    vec2 iResolution;
    float iTime;
    vec4 iMouse;
};

in vec2 uv;
out vec4 frag_color;

void main() {
    vec2 fragCoord = (uv * 0.5 + 0.5) * iResolution;
    // Your shader code here
    frag_color = vec4(1.0);
}
@end

@program yourshader vs fs
```

2. **Update the application** in `Source/ShaderToyApp-app.cs`:
   - Add to `ShaderType` enum
   - Add to `shader_names` array
   - Create pipeline in `Init()`

3. **Compile and build**:
```bash
dotnet build ShaderToyApp.csproj -t:CompileShaders
dotnet build ShaderToyApp.csproj
```

## License

- **Application Framework**: MIT License
- **Raymarching Shader**: MIT License (Inigo Quilez)
- **Procedural Ocean Shader**: MIT License (afl_ext)
- **Stormy Torus Shader**: CC BY-NC-SA 4.0 (Jaenam) - **Non-Commercial Use Only**
- **Universe Ball Shader**: CC BY-NC-SA 3.0 - **Non-Commercial Use Only**
- **Fractal Land Shader**: CC BY-NC-SA 3.0 (Kali) - **Non-Commercial Use Only**
- **Fractal Pyramid Shader**: CC BY-NC-SA 3.0 (BigWings) - **Non-Commercial Use Only**
- **Gemmarium Shader**: CC BY-NC-SA 4.0 (Jaenam) - **Non-Commercial Use Only**

See [LICENSE.md](LICENSE.md) for complete license information.

**Important:** The Stormy Torus, Universe Ball, Fractal Land, Fractal Pyramid, and Gemmarium shaders are licensed under CC BY-NC-SA licenses, which restrict commercial use. If you intend to use this project commercially, you must remove these shaders or obtain explicit permission from their respective authors.

## Technologies

- [Sokol.NET](https://github.com/elix22/Sokol.NET) - C# bindings for sokol headers
- [sokol_gfx](https://github.com/floooh/sokol) - Modern cross-platform graphics API wrapper
- [sokol-shdc](https://github.com/floooh/sokol-tools) - Shader compiler
- [Dear ImGui](https://github.com/ocornut/imgui) - GUI library
- .NET 10.0 - Cross-platform framework
## Credits

- **Inigo Quilez** - Raymarching primitives shader
- **afl_ext** - Procedural ocean shader
- **Jaenam** - Stormy torus shader
- **Kali** - Fractal land shader
- **Martijn Steinrucken (BigWings)** - Fractal pyramid shader
- **Andre Weissflog (floooh)** - Sokol libraries
- **Omar Cornut** - Dear ImGui

## Contributing

Contributions are welcome! When adding new shaders:

1. Ensure the shader has a compatible license (MIT, CC0, or similar permissive license)
2. Include proper attribution and license information
3. Update LICENSE.md with the new shader's license
4. Test on multiple platforms if possible

## Support

For issues, questions, or suggestions, please open an issue on the [Sokol.NET repository](https://github.com/elix22/Sokol.NET/issues).
