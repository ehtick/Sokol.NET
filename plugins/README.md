# Sokol.NET Plugins

Optional, self-contained add-ons that extend Sokol.NET applications.

## Available Plugins

| Plugin | Platforms | Description |
|--------|-----------|-------------|
| [Share](Share/README.md) | Android, iOS, macOS, Windows, Linux | Share score + generated image via native share sheet |

## Plugin Convention

Each plugin follows the same structure:

```
plugins/<PluginName>/
├── README.md
├── managed/             ← C# source (link into your .csproj)
├── native/
│   ├── CMakeLists.txt   ← Standalone shared library — builds independently of sokol
│   └── *.c / *.m        ← C / ObjC implementation
├── libs/                ← Build output (self-contained, never touches repo-root libs/)
│   ├── android/<abi>/<config>/lib<name>.so
│   ├── ios/<target>/<config>/<name>.framework
│   └── macos/<arch>/<config>/lib<name>.dylib
└── scripts/             ← One build script per platform
    ├── build-macos.sh
    ├── build-ios.sh
    ├── build-android.sh
    ├── build-linux.sh
    └── build-windows.ps1
```

### Opt-in steps

1. Add managed sources to your `.csproj`:
   ```xml
   <ItemGroup>
     <Compile Include="$(SokolNetHome)/plugins/<PluginName>/managed/*.cs" />
   </ItemGroup>
   ```
2. Build the native library using the platform script in `plugins/<PluginName>/scripts/`.
   Output lands in `plugins/<PluginName>/libs/` — independent of the main sokol library.
3. Add native library paths and any Gradle dependencies to your `Directory.Build.props`:
   ```xml
   <AndroidNativeLibrary_<name>Path>../../plugins/<PluginName>/libs/android</AndroidNativeLibrary_<name>Path>
   <IOSNativeLibrary_<name>Path>../../plugins/<PluginName>/libs/ios/arm64/release</IOSNativeLibrary_<name>Path>
   <AndroidGradleDependency_<key>>group:artifact:version</AndroidGradleDependency_<key>>
   ```
4. Add native library copy targets for macOS/Windows/Linux to your `.csproj` (see SmilingFruits for an example).
5. Copy any `platform/` files (e.g. Android FileProvider config) into your app's
   `platform/android/` folder.
