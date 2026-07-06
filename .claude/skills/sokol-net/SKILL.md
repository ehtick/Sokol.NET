---
name: sokol-net
description: >
  Sokol.NET framework development — use for ANY work in this repo: creating or debugging
  examples, building/running for desktop/Android/iOS/Web, writing or compiling shaders,
  adding a new C/C++ library under ext/ and generating its C# bindings with bindgen,
  rebuilding native libraries, and developing the framework itself under src/ (sokol
  bindings, Sokol.GUI, Framework/Render2D, imgui). Read BEFORE building, running, or
  editing bindings — the build system and binding generator have non-obvious rules.
---

# Sokol.NET Development

C# binding + application framework over the Sokol headers. One C# codebase targets
Windows (D3D11), macOS/iOS (Metal), Linux (OpenGL/glsl430), Android (GLES3), and
WebAssembly (WebGL2) via .NET NativeAOT. Home directory: `/Users/elialoni/Development/Sokol.NET`.

## Hard rules (violations break the build or other platforms)

1. **NEVER hand-edit `src/sokol/generated/*.cs`.** It is generated from C headers.
   Change the C header (or `bindgen/`) and run `./scripts/generate-bindings.sh`.
2. **A change to shared C# or `ext/` can silently break a different platform.** State which
   platforms you verified on. WASM and Android GLES3 are the usual silent casualties.
3. **UI must fit a phone** (~360 logical px wide): overflowable content goes in a
   `ScrollView` (`src/GUI/Widgets/ScrollView.cs`). See root `CLAUDE.md` §5 — mandatory.
4. **Shaders compile via MSBuild, never by invoking sokol-shdc directly:**
   `dotnet build examples/<name>/<name>.csproj -t:CompileShaders`.
5. Some directories under `examples/` and `ext/` are **private repositories** containing their
   own `CLAUDE.md`. If a directory's `CLAUDE.md` declares it private/proprietary, follow that
   file's rules first — and never `git add` such directories into this repo (no `git add -A`
   anywhere; stage exact paths only).

## Repo map

```
src/sokol/            C# bindings: generated/ (26 files, DO NOT EDIT) + hand-written helpers
src/GUI/              Sokol.GUI — retained-mode widget toolkit (NOT Dear ImGui)
src/Framework/        Higher-level engine pieces (Assets, ECS, Scene, Renderer, Physics…)
src/Render2D/         GPU 2D renderer + particle system
src/imgui/            Dear ImGui bindings (from ext/cimgui)
ext/                  C/C++ submodules + ext/CMakeLists.txt (ONE file builds all native libs)
ext/sokol.c           Unified C compilation unit (includes the header implementations)
bindgen/              Python binding generator (gen.py = registry, gen_csharp.py = emitter)
libs/<plat>/<arch>/   Pre-built native libraries (committed; rebuild only when ext/ changes)
tools/SokolApplicationBuilder/   Cross-platform build orchestrator
tools/sokol-tools/    sokol-shdc shader compiler binaries
scripts/              build-*, generate-bindings.sh, add-project-to-vscode-config.*
docs/                 Deep guides — check here FIRST for any subsystem (list below)
```

One-time setup: `./register.sh` (writes `~/.sokolnet_config/sokolnet_home`) and
`git submodule update --init --recursive`.

## Building & running

`tools/SokolApplicationBuilder` is the orchestrator. Invocation pattern:

```bash
dotnet run --project tools/SokolApplicationBuilder -- --task <task> --architecture <arch> [--type release|debug] --path examples/<name>
```

| Task | Purpose |
|---|---|
| `prepare` | compile shaders + copy assets (run before first desktop `dotnet run`) |
| `build` | full build (+deploy/run on device for android/ios) |
| `run` / `clean` / `cleanall` | as named |
| `createproject --project my_app --destination <dir>` | scaffold a standalone project (docs/CREATE_PROJECT.md) |
| `listdevices` | enumerate attached Android/iOS devices (docs/DEVICE_LISTING.md) |

Architectures: `desktop` (RID defaults to host), `android`, `ios`, `web`.

Fast paths:
```bash
# Desktop, JIT (day-to-day dev loop — no NativeAOT wait):
dotnet run --project examples/cube/cube.csproj
# Web build, then serve:
dotnet run --project tools/SokolApplicationBuilder -- --task build --architecture web --path examples/cube
dotnet serve --directory examples/cube/bin/Release/net10.0/browser-wasm/AppBundle
```
`.vscode/tasks.json` has 500+ pre-made tasks (one per example × platform).

## Creating a new example

Follow `docs/CREATE_EXAMPLE.md`. Anatomy of `examples/<name>/` (copy from `examples/cube`):

- `<name>.csproj` (desktop/mobile) **and** `<name>web.csproj` (WASM) — two csprojs per example.
- `Source/` — code. Entry: a `sokol_main()` returning `sapp_desc` with Init/Frame/Event/Cleanup
  callbacks; platform mains in `Program.cs`.
- `shaders/*.glsl` — Sokol cross-compiled dialect; compiled output lands in `shaders/compiled/`.
- `Assets/`, `Directory.Build.props` (per-example options: optional dynamic libs, package prefix,
  icons — docs/ANDROID_PROPERTIES.md, IOS_PROPERTIES.md, APP_ICON.md), `wwwroot/` (web shell).

After creating: register VS Code tasks with `./scripts/add-project-to-vscode-config.sh <name>`,
run `prepare`, then verify **desktop first, then web** (web breaks most often), then mobile.
For a project OUTSIDE the repo use `--task createproject` instead (template in docs/PROJECT_TEMPLATE.md).

## Shaders

- Write once in Sokol's `.glsl` dialect; `-t:CompileShaders` runs sokol-shdc for every backend:
  `glsl430` (Linux), `hlsl5` (D3D11), `metal_macos`/`metal_ios`, `glsl300es` (Android/WASM).
- Full guide: `docs/SHADER_GUIDE.md`. Gotcha: RGBA32F/data textures read with `texelFetch` MUST
  be declared unfilterable-float + a nonfiltering sampler in sokol-shdc, or Mali GPUs panic.

## Debugging examples

| Target | How |
|---|---|
| Desktop | `dotnet run` (JIT — real breakpoints work); native crashes: `lldb -- dotnet run …`; sokol log → stderr |
| Android | `adb logcat -s <AppTag>:V *:E` (sokol log goes to logcat); `--task build` installs+launches; device pick: docs/ANDROID_DEVICE_SELECTION.md |
| iOS | `xcrun devicectl` install/launch; logs in Console.app / `devicectl` output |
| Web | browser devtools console; stale-build confusion → docs/Browser-Cache-Issues.md; struct-return crashes → see bindings section |

Debug vs Release matters: NativeAOT Debug codegen is drastically slower — perf symptoms seen only
in Debug are usually codegen, not your change. Graphics acceptance is visual: build + launch and
give the user repro steps; the user supplies screenshots (Claude cannot screencapture here).

Known platform traps (verified, keep in mind when a bug "makes no sense"):
- `sapp_quit()` is broken on stock Android — this repo's `sokol_app.h` defers shutdown post-frame
  and calls `finishAndRemoveTask()` via JNI.
- NanoVG/`Screen.Instance.Update()` needs **logical** pixels (`sw/dpr, sh/dpr`) — passing physical
  clips drawing to the top-left quadrant.
- `ma_sound_stop()` (MiniAudio) is not immediate; to silence a node instantly use
  `ma_sound_uninit()` + free.

## Adding a new ext/ library + C# bindings (the bindgen workflow)

Checklist — all steps required, in order:

1. **Vendor the source:** `git submodule add <url> ext/<lib>` (or copy single-header libs in).
2. **Compile it natively:** add it to `ext/CMakeLists.txt` (the ONE CMake file; follow the
   existing per-platform `if (WIN32/APPLE/ANDROID/Emscripten/LINUX)` guards). If the lib is C++
   or must ship separately (like Assimp/Spine/Ozz), model it as an **optional dynamic library**
   configured via the example's `Directory.Build.props` instead of bundling into `sokol.c`.
3. **Register in the generator:** add one row to the `tasks` list in `bindgen/gen.py`:
   `[ '../ext/<lib>/<header>.h', '<prefix_>', ['<dep_prefix_', …] ]` — prefix = the C symbol
   prefix to bind; dep prefixes = other bound libs whose types appear in this header.
   Output goes to `src/sokol/generated/` by default (per-prefix overrides exist in
   `gen_csharp.py`).
4. **Generate:** `./scripts/generate-bindings.sh`. This also regenerates the **struct-return
   wrapper headers** (`ext/sokol_csharp_internal_wrappers.h` and per-lib variants): C functions
   returning structs **by value cannot be P/Invoked from WASM**, so the generator auto-creates
   `*_internal(<args>, out*)` C wrappers. A big new lib may need its own
   `gen_c_<lib>_wrappers_header` output wired in `gen.py` (copy the box2d/miniaudio pattern)
   and that header `#include`d in the lib's compilation unit.
5. **Rebuild native libs** for every platform you claim support for (next section) — the
   wrappers are C code; bindings without rebuilt libs = `EntryPointNotFoundException`.
6. **Smoke-test per platform**, WASM explicitly — it is the platform the wrappers exist for.
   Read `docs/C-Internal-Wrappers-Auto-Generation.md` + `docs/WebAssembly-Struct-Return-Workaround.md`
   before touching any of this machinery.

## Rebuilding native libraries (only when ext/ C/C++ changed)

```bash
./scripts/build-xcode-macos.sh              # macOS
./scripts/build-ios-sokol-library.sh all    # iOS
./scripts/build-android-sokol-libraries-via-apk.sh  # Android (multi-arch: builds the `clear`
                                            # example APK debug+release and extracts the .so libs
                                            # into libs/android/ — NOT build-android-sokol-libraries.sh)
./scripts/build-web-library.sh              # Emscripten/WASM
./scripts/build-linux-library.sh            # Linux
.\scripts\build-vs2022-windows.ps1          # Windows
```
Outputs land in `libs/<platform>/<arch>/` and are committed. Full doc: `docs/BUILD_SYSTEM.md`.

## Framework development in src/

- **`src/sokol`**: hand-written helpers wrap the generated bindings — extend via new hand-written
  files, never inside `generated/`.
- **`src/GUI` (Sokol.GUI)** — retained-mode widgets, NanoVG-drawn. Rules:
  - There is ONE shared tween engine (`AnimationManager`) — never create a parallel one; never
    `Unregister` a tween from its own callback.
  - Validate clicks with `HitTest(LocalPosition)`, not `IsHovered`.
  - A core GUI change must not break other consumers: rebuild all GUI consumers and
    regression-check the GUIDemo example's tabs before calling it done.
  - Button labels must never clip in any language — auto-size from `MeasureText`, never
    `Label.Length` heuristics.
- **Rendering split (standing rule):** game/app views draw all 2D **shapes** on a
  `Render2DSurface` (GPU); NanoVG `Draw()` is for **text only**.
- **DPI:** `sapp_dpi_scale()` lies on some Androids (returns 1.0) — scale UI from logical width
  (`UiMetrics` pattern); on WASM `sapp_width()` is physical/retina.

## Key docs index (read the doc before reinventing)

`docs/BUILD_SYSTEM.md` · `docs/SOKOL_APPLICATION_BUILDER.md` · `docs/CREATE_EXAMPLE.md` ·
`docs/CREATE_PROJECT.md` · `docs/SHADER_GUIDE.md` · `docs/C-Internal-Wrappers-Auto-Generation.md` ·
`docs/WEBASSEMBLY_BROWSER_GUIDE.md` · `docs/SOKOL_FILESYSTEM.md` · `docs/RENDER2D.md` ·
`docs/RENDER3D.md` · `docs/PHYSICS3D.md` · `docs/VSCODE_RUN_GUIDE.md` · `docs/QUICK_BUILD.md` ·
`docs/GITHUB_PAGES_SETUP.md` (web deploy; macOS case-insensitivity breaks .wasm renames).

## Definition of done

State acceptance criteria up front (root `CLAUDE.md` §4). A framework/example change is done when
it **builds and behaves on every platform it touches** — at minimum desktop + web for rendering
work, plus one real mobile device for UI/input work — and you have named the platforms verified.
