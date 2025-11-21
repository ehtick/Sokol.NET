# Sokol.NET

**Modern, cross-platform graphics and multimedia framework for C# with .NET NativeAOT**

Sokol.NET is a comprehensive C# binding and application framework built on top of the [Sokol headers](https://github.com/floooh/sokol), providing a modern, high-performance graphics API with support for desktop, mobile, and web platforms.

> **⚠️ Development Status**: This project is under ongoing development, primarily driven by the needs of my internal projects. Future development beyond my personal technical requirements will depend on public interest and community contributions.

## 🌐 Live Examples

**[✨ Try all 36 examples in your browser →](https://elix22.github.io/Sokol.NET/)**

Experience Sokol.NET's capabilities instantly with interactive WebAssembly examples. No installation required!

## 🎯 Features

- **Cross-Platform**: Deploy to Windows, macOS, Linux, Android, iOS, and WebAssembly from a single codebase
- **High Performance**: Leverages .NET NativeAOT for near-native performance with zero overhead
- **Modern Graphics**: Unified API supporting Direct3D 11, Metal, OpenGL, OpenGL ES, and WebGL
- **Rich Examples**: 36 example applications demonstrating various graphics techniques and features
- **Production Ready**: Includes ImGui, Assimp, glTF, Spine, Ozz animation, and more integrations

## 🚀 Supported Platforms

| Platform | Runtime | Graphics API | Status |
|----------|---------|--------------|--------|
| **Windows** | JIT/NativeAOT | Direct3D 11 | ✅ Full Support |
| **macOS** | JIT/NativeAOT | Metal | ✅ Full Support |
| **Linux** | JIT/NativeAOT | OpenGL | ✅ Full Support |
| **Android** | NativeAOT (Bionic) | OpenGL ES 3.0 | ✅ Full Support (APK/AAB) |
| **iOS** | NativeAOT | Metal | ✅ Full Support |
| **WebAssembly** | WASM | WebGL 2.0 | ✅ Full Support |

## 📦 What's Included

### Core Libraries
- **sokol_gfx**: Modern 3D graphics API abstraction
- **sokol_app**: Unified application/window management
- **sokol_audio**: Cross-platform audio playback
- **sokol_fetch**: Asynchronous resource loading
- **sokol_time**: High-precision timing
- **sokol_gl**: OpenGL 1.x style immediate mode rendering
- **sokol_gp**: 2D graphics painter API
- **sokol_debugtext**: Text rendering for debugging
- **sokol_shape**: Procedural 3D shape generation

### Integrated Libraries
- **Dear ImGui** (cimgui): Immediate mode GUI toolkit
- **cgltf**: glTF 2.0 loader
- **Basis Universal**: GPU texture compression
- **fontstash**: Dynamic font rendering
- **stb_image**: Image loading (PNG, JPG, etc.)
- **pl_mpeg**: MPEG1 video playback

### External Optional Libraries
These libraries are available as separate dynamic libraries that can be loaded when needed:
- **Assimp**: 3D model loading (40+ formats) - Load dynamically via native library configuration
  - **Note**: May have performance issues on low-tier Android devices. Consider using **GltfViewer** (SharpGLTF-based) or cgltf for better compatibility and performance.
- **Spine**: 2D skeletal animation runtime - Load dynamically via native library configuration
- **Ozz-animation**: 3D skeletal animation system - Load dynamically via native library configuration

See example projects (`assimp_simple`, `spine_simple`, `ozz_shdfeatures`) and their `Directory.Build.props` files for configuration details.

**Recommended**: For glTF 2.0 models, use the **GltfViewer** example - a production-ready, full-featured glTF viewer with PBR rendering, animations, and advanced material support.

## 🎮 Example Applications

The `examples/` folder contains 36 sample applications demonstrating various features:

### Graphics Fundamentals
- **[clear](examples/clear)** - Basic window and clear color
- **[cube](examples/cube)** - Rotating 3D cube with texture
- **[instancing](examples/instancing)** - Hardware instancing demonstration
- **[offscreen](examples/offscreen)** - Render-to-texture techniques
- **[mrt](examples/mrt)** - Multiple render targets
- **[shadows](examples/shadows)** - Shadow mapping implementation

### 2D Graphics
- **[sgl](examples/sgl)** - 2D immediate mode rendering
- **[sgl_lines](examples/sgl_lines)** - Line rendering techniques
- **[shapes_transform](examples/shapes_transform)** - 2D transformations
- **[dyntex](examples/dyntex)** - Dynamic texture updates

### Text & Fonts
- **[debugtext](examples/debugtext)** - Debug text overlay
- **[debugtext_context](examples/debugtext_context)** - Multiple text contexts
- **[fontstash](examples/fontstash)** - TrueType font rendering
- **[fontstash_layers](examples/fontstash_layers)** - Layered text effects

### 3D Models & Animation
- **[assimp_simple](examples/assimp_simple)** - Basic 3D model loading
- **[assimp_animation](examples/assimp_animation)** - Skeletal animation with Assimp
- **[assimp_scene](examples/assimp_scene)** - Complex scene loading
- **[cgltf](examples/cgltf)** - glTF 2.0 model loading
- **[GltfViewer](examples/GltfViewer)** - Full-featured glTF viewer
- **[ozz_shdfeatures](examples/ozz_shdfeatures)** - Ozz animation system with shader features

### 2D Animation
- **[spine_simple](examples/spine_simple)** - Basic Spine animation
- **[spine_skinsets](examples/spine_skinsets)** - Spine skin swapping
- **[spine_inspector](examples/spine_inspector)** - Spine animation debugger

### Textures & Materials
- **[loadpng](examples/loadpng)** - PNG texture loading
- **[basisu](examples/basisu)** - Basis Universal GPU textures
- **[cubemap_jpeg](examples/cubemap_jpeg)** - Cubemap textures
- **[cubemaprt](examples/cubemaprt)** - Render-to-cubemap
- **[miprender](examples/miprender)** - Mipmap generation
- **[vertextexture](examples/vertextexture)** - Vertex texture fetch
- **[texview](examples/texview)** - Texture viewer utility
- **[sdf](examples/sdf)** - Signed distance field rendering

### UI & Integration
- **[cimgui](examples/cimgui)** - Dear ImGui integration
- **[imgui_usercallback](examples/imgui_usercallback)** - Custom ImGui rendering

### Advanced
- **[drawcallperf](examples/drawcallperf)** - Draw call performance testing
- **[plmpeg](examples/plmpeg)** - MPEG1 video playback

## 📜 Spine License Notice

Some examples in this repository (`spine_simple`, `spine_skinsets`, `spine_inspector`) use the [Spine](http://esotericsoftware.com) runtime library. 

**Important for Users**: While you are free to evaluate and build these examples, if you wish to use Spine in your own projects, you will need to purchase a [Spine license](https://esotericsoftware.com/spine-purchase). The Spine Runtimes are covered by the [Spine Runtimes License Agreement](http://esotericsoftware.com/spine-runtimes-license).

This repository's maintainer has a valid Spine license for development and distribution of these examples.

## 🛠️ Prerequisites

- **.NET 10.0 SDK** or later
- **CMake 3.29.0** or later (required for building native libraries)
- **Visual Studio Code** (primary development IDE)
  - **Required** for Android and iOS development (build/install/debug)
  - Supports all platforms: Desktop, Web, Android, and iOS
  - Alternative IDEs (Visual Studio, Rider) support Desktop and Web only
- **wasm-tools-net8 workload** (for WebAssembly development): `dotnet workload install wasm-tools-net8`
- **Platform-specific toolchains**:
  - **Windows**: Visual Studio 2022 Build Tools
  - **macOS**: Xcode Command Line Tools
  - **Linux**: GCC/Clang
  - **Android**: Android SDK & NDK 25+
  - **iOS**: Xcode 14+

## 🏁 Quick Start

### 1. Clone and Register

```bash
git clone --recursive https://github.com/elix22/Sokol.NET.git
cd Sokol.NET

# Configure Git to automatically update submodules (IMPORTANT!)
git config submodule.recurse true

# Register the repository (creates ~/.sokolnet_config)
./register.sh  # macOS/Linux
# or
register.bat   # Windows
```

> **⚠️ Important**: The `git config submodule.recurse true` command is **required** to automatically update nested submodules (like `cimgui/imgui`) when you pull changes. Without this, you'll need to manually run `git submodule update --init --recursive` after every `git pull`.

### 2. Run Examples

📘 **[Complete VS Code Run Guide with Screenshots](docs/VSCODE_RUN_GUIDE.md)** - Get started instantly with step-by-step instructions for running applications on all platforms.

#### Using Visual Studio Code (Recommended)

VS Code is the primary development environment with full support for all platforms.

**Desktop & Web**: Press **F5** and select your platform and example.

**Android & iOS**: Use **Command Palette** (`Cmd+Shift+P` / `Ctrl+Shift+P`) → **Tasks: Run Task**:

**Android Tasks:**
- **Android: List Devices** - List all connected Android devices
- **Android: Build APK** - Build release/debug APK
- **Android: Build AAB** - Build release/debug Android App Bundle
- **Android: Install APK** - Build and install APK to selected device
- **Android: Install AAB** - Build and install AAB to selected device

**iOS Tasks:**
- **iOS: List Devices** - List all connected iOS devices
- **iOS: Build** - Build release/debug iOS app
- **iOS: Install** - Build and install to selected iOS device

#### Using Command Line or Other IDEs

**Desktop** (works with Visual Studio, Rider, or command line):
```bash
cd examples/cube
dotnet build cube.csproj -t:CompileShaders
dotnet build cube.csproj
dotnet run -p cube.csproj
```

**Note**: Visual Studio and Rider support Desktop and Web development only. For Android and iOS, use Visual Studio Code.

### 3. Create Your Own Project

📘 **[Complete Project Creation Guide](docs/CREATE_PROJECT.md)** - Learn how to create standalone Sokol.NET projects.

Create a new standalone project outside the repository:

**Using VS Code:**
1. Open Command Palette (`Cmd+Shift+P` / `Ctrl+Shift+P`)
2. Select **Tasks: Run Task** → **Create New Project**
3. Enter project name (e.g., `my_game`)
4. Enter destination path (must be outside this repository)

**Using Command Line:**
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task createproject \
  --project my_game \
  --destination /path/to/projects
```

The project will be created with full VS Code configuration, ready to open and run independently.

### 4. Build for Other Platforms

#### Android APK/AAB
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture android --type release \
  --path examples/cube --install --interactive
```

#### iOS
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture ios --type release \
  --path examples/cube --install --interactive
```

#### WebAssembly
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture web \
  --path examples/cube
```

See [docs/web-server-setup.md](docs/web-server-setup.md) for running WebAssembly apps locally.

## 📚 Documentation

Comprehensive documentation is available in the **[`docs/`](docs/)** folder:

### Getting Started
- **[Visual Studio Code Run Guide](docs/VSCODE_RUN_GUIDE.md)** ⭐ - Step-by-step guide with screenshots for running apps on all platforms
- **[Shader Programming Guide](docs/SHADER_GUIDE.md)** 🎨 - Complete guide to writing cross-platform shaders with sokol-shdc
- **[Creating New Projects](docs/CREATE_PROJECT.md)** 🚀 - How to create standalone Sokol.NET projects outside the repository
- **[Creating New Examples](docs/CREATE_EXAMPLE.md)** 🆕 - How to create new example projects from template

### Platform-Specific Guides
- **Android**
  - [Android Properties Configuration](docs/ANDROID_PROPERTIES.md)
  - [Android Screen Orientation](docs/ANDROID_SCREEN_ORIENTATION.md)
  - [Android Device Selection](docs/ANDROID_DEVICE_SELECTION.md)
  - [AAB Build Guide](docs/AAB_BUILD_GUIDE.md)
  - [Android Keyboard Implementation](docs/ANDROID_KEYBOARD_IMPLEMENTATION.md)

- **iOS**
  - [iOS Device Selection & Installation](docs/ios-device-selection.md)
  - [iOS Properties Configuration](docs/IOS_PROPERTIES.md)

- **WebAssembly**
  - [WebAssembly Local Server Setup](docs/web-server-setup.md)
  - [WebAssembly Browser Guide](docs/WEBASSEMBLY_BROWSER_GUIDE.md)
  - [Browser Cache Issues](docs/Browser-Cache-Issues.md)

### Build System & Deployment
- [Build System Documentation](docs/BUILD_SYSTEM.md) - Complete build system reference
- [Quick Build Reference](docs/QUICK_BUILD.md) - Common build commands
- [Sokol Application Builder](docs/SOKOL_APPLICATION_BUILDER.md) - Build tool documentation
- [Multi-Device Install](docs/MULTI_DEVICE_INSTALL.md) - Installing to multiple devices

### Configuration & Customization
- [App Icon Configuration](docs/APP_ICON.md)
- [Icon Quick Start](docs/ICON_QUICKSTART.md)
- [App Version Configuration](docs/APP_VERSION_CONFIGURATION.md)
- [Package Prefix Configuration](docs/PACKAGE_PREFIX_CONFIGURATION.md)
- [Project Template](docs/PROJECT_TEMPLATE.md)

### Advanced Topics
- [C Internal Wrappers Auto-Generation](docs/C-Internal-Wrappers-Auto-Generation.md)
- [WebAssembly Struct Return Workaround](docs/WebAssembly-Struct-Return-Workaround.md)
- [Output Path Implementation](docs/OUTPUT_PATH_IMPLEMENTATION.md)

📖 **[Full Documentation Index](docs/README.md)**

## 🏗️ Project Structure

```
Sokol.NET/
├── examples/          # 36 example applications
├── src/              # C# bindings and core libraries
├── ext/              # Native C/C++ dependencies
│   ├── sokol/       # Sokol headers
│   ├── cimgui/      # ImGui C bindings
│   ├── assimp/      # 3D model loader
│   ├── cgltf/       # glTF loader
│   ├── spine-c/     # Spine runtime
│   └── ozz-animation/ # Animation system
├── libs/             # Prebuilt native libraries
│   ├── windows/
│   ├── macos/
│   ├── linux/
│   ├── android/
│   ├── ios/
│   └── emscripten/
├── tools/            # Build tools and utilities
│   └── SokolApplicationBuilder/
├── bindgen/          # C# binding generator
├── docs/             # Documentation
└── templates/        # Project templates
```

## 🔧 Building Native Libraries

Native Sokol libraries are pre-built and included in the `libs/` folder. To rebuild:

### Windows
```powershell
.\scripts\build-vs2022-windows.ps1
```

### macOS
```bash
./scripts/build-xcode-macos.sh
```

### Linux
```bash
./scripts/build-linux-library.sh
```

### Android
```bash
./scripts/build-android-sokol-libraries.sh
```

### iOS
```bash
./scripts/build-ios-sokol-library.sh
```

See [docs/BUILD_SYSTEM.md](docs/BUILD_SYSTEM.md) for detailed build instructions.

## 🤝 Contributing

Contributions are welcome! Please ensure:
- Code follows existing style and conventions
- All platforms continue to build successfully
- Examples run without errors
- Documentation is updated for new features

## 📄 License

This project is licensed under the MIT License. See individual library folders for their respective licenses:
- Sokol: zlib/libpng license
- ImGui: MIT License
- Assimp: BSD 3-Clause License
- Spine: Spine Runtime License

## 🙏 Credits

Built on top of these excellent libraries:
- [Sokol](https://github.com/floooh/sokol) by Andre Weissflog
- [Dear ImGui](https://github.com/ocornut/imgui) by Omar Cornut
- [Assimp](https://github.com/assimp/assimp) - Open Asset Import Library
- [cgltf](https://github.com/jkuhlmann/cgltf) by Johannes Kuhlmann
- [Spine Runtimes](https://github.com/EsotericSoftware/spine-runtimes) by Esoteric Software
- [Ozz-animation](https://github.com/guillaumeblanc/ozz-animation) by Guillaume Blanc

## 📞 Support & Community

- **Issues**: [GitHub Issues](https://github.com/elix22/Sokol.NET/issues)
- **Author**: Eli Aloni (elix22)

---

**Get started now**: Clone the repository, run `./register.sh`, and explore the examples!

