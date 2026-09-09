# Native AOT Obfuscation Tool — Design & Implementation

> ⚠️ **Experimental — still under active development and NOT fully verified.** Implemented and
> smoke-tested on the `cube` example across Desktop/Android/iOS/Web, but not yet validated on real
> apps that use reflection, serialization, or dynamic type loading. See the user guide,
> [OBFUSCATION.md](OBFUSCATION.md), for how to enable and use it in a project.

**Status:** Implemented (experimental / not fully verified). Method rename + string encryption + opt-in type/field/property rename are working; control-flow obfuscation (§6.4) is not yet built.
**Scope:** A code-obfuscation step that runs **before** the Native AOT / WASM compilation performed by `SokolApplicationBuilder`, for **all** Sokol.NET targets: Desktop (D3D11/Metal/OpenGL), Android (GLES3), iOS (Metal), and Web (WebGL2).
**Owner:** Eli Aloni

---

## 1. TL;DR

We add a small **IL-level obfuscator** (built on [dnlib](https://github.com/0xd4d/dnlib)) that rewrites the **compiled managed assembly** in the window between the C# compile and the native/WASM AOT compiler. It is shipped as a new project `tools/SokolObfuscator/`, exposed both as:

1. a **standalone CLI** (same `CommandLineParser` conventions as `SokolApplicationBuilder`), and
2. an **MSBuild target** injected into the existing `dotnet publish` / `dotnet build` invocations, activated by the already-present `--obfuscate` flag on `SokolApplicationBuilder`.

**Ordinary builds never obfuscate.** Obfuscation runs only through a dedicated `obfuscate-*` VS Code task (§8.5), and only when the project folder contains an **`obfuscate.xml`** — its *presence* is the on-switch and its *contents* are the config. No `obfuscate.xml` ⇒ no obfuscation, even when the obfuscate task runs.

Primary transform is **method renaming** to random valid identifiers. Also supported: **string-literal encryption**, **type/field/property renaming**, and (optional, lowest priority) **control-flow obfuscation**. Configuration is the **`obfuscate.xml`** file, which selects which methods / files / folders / types are included or excluded. It is optional for the *standalone CLI* (sensible defaults apply), but for the *build/task integration* its presence in the project folder is what enables obfuscation and supplies the config. CLI flags and the standard `[System.Reflection.Obfuscation]` attribute complement it.

The pipeline seam is the key idea:

```
  C# source ──► Roslyn compile ──► App.dll (IL, in obj/)
                                        │
                          ┌─────────────▼──────────────┐
                          │  SokolObfuscator (dnlib)    │  ◄── rename methods/types,
                          │  rewrites App.dll in place  │      encrypt strings, (ctrl-flow)
                          └─────────────┬──────────────┘
                                        │
                     ┌──────────────────┴───────────────────┐
                     ▼                                        ▼
        ILC (NativeAOT)                         ILLink trim + Mono-WASM AOT
   Desktop / Android / iOS                              Web (browser-wasm)
        native .exe/.so/.dylib                    dotnet.wasm + managed *.dll
```

Everything downstream (trimming, ILC, WASM AOT, packaging) is unchanged — it just consumes an already-obfuscated assembly.

---

## 2. Background: what obfuscation buys you on each target

This is **not** a normal .NET obfuscation scenario, and it is important to be honest about what the tool does and does not protect. The threat model differs sharply per target because the shipped artifact differs.

| Target | AOT toolchain | What actually ships to the user | Is IL recoverable? | Obfuscation value |
|---|---|---|---|---|
| **Desktop** | ILC (NativeAOT), `PublishAot=true` | Native `.exe` / `.dll` (machine code) | No IL in the binary | **Low–moderate** — sanitizes residual name metadata + strings |
| **Android** | ILC (NativeAOT), `-r linux-bionic-*`, `BuildAsLibrary` | Native `lib<App>.so` per ABI | No IL in the binary | **Low–moderate** — same as desktop |
| **iOS** | ILC (NativeAOT), `-r ios-arm64`, `BuildAsLibrary` | Native `lib<App>.dylib` | No IL in the binary | **Low–moderate** — same as desktop |
| **Web** | **Mono-WASM AOT** (`RunAOTCompilation=true`, `browser-wasm`) | `dotnet.wasm` **+ the managed `*.dll` assemblies** in `_framework/` | **YES — the managed IL is downloadable and decompilable (ILSpy/dnSpy)** | **HIGH** — this is the real win |

Two consequences drive the whole design:

1. **NativeAOT already provides strong protection.** ILC compiles IL → native machine code; there is *no IL in the shipped binary*, so a decompiler cannot reconstruct C#. See the referenced Stack Overflow discussion and ByteHide's AOT notes. **BUT** ILC still embeds *name strings* for two reasons: (a) **reflection metadata** for types/members that survive trimming, and (b) **stack-trace metadata** (`StackTraceSupport`) so exceptions print readable frames. These names are plain strings in the binary and are trivially recoverable with `strings`/grep. Pre-AOT renaming means those residual strings become meaningless (`a7Kx`, `Qm3`, …). String literals also survive verbatim in the data section, hence string encryption.

2. **Web is the outlier and the strongest reason to obfuscate.** Mono-WASM AOT is a *JIT-avoidance* optimization — the managed assemblies are still shipped and loaded by the Mono runtime. Anyone can open DevTools, download `_framework/<App>.dll`, and decompile it back to near-original C#. For Web, IL obfuscation is the same high-value protection you'd get on a classic .NET app.

**Framing adopted (per decision): defense-in-depth.** The tool renames to sanitize residual metadata **and** encrypts strings, and this document also lists the AOT/trimming knobs (§10) that reduce leakage. We do not oversell it: on native targets the marginal gain over "already native" is modest; on Web it is substantial.

### Non-goals

- **We do not obfuscate native machine code.** No post-ILC binary packing/mutation. (ByteHide's optional "post-compilation" step is explicitly out of scope; its anti-debugger / invalid-metadata / invalid-code protections are documented by ByteHide as *incompatible* with NativeAOT and are not attempted here.)
- **We are not a DRM / anti-tamper / licensing system.** String "encryption" is obfuscation, not encryption-at-rest — the key and decryptor live in the binary.
- **We do not touch the auto-generated bindings or the Sokol.NET framework by default** (see scoping, §7). Only the application assembly is obfuscated unless explicitly widened.

---

## 3. Goals

1. Obfuscate the application's managed code **before** AOT/WASM compilation, transparently, for **all four** target families.
2. **Method renaming** to random valid identifiers (letters + digits) — the primary feature.
3. Optional **string encryption**, **type/field/property renaming**, **control-flow obfuscation**.
4. **Configurable** via an optional XML file: include/exclude by **method**, **file**, **folder**, **type**, **namespace**. XML is not mandatory — sensible defaults + CLI flags + `[Obfuscation]` attribute work without it.
5. **CLI-driven**, using the same conventions as `SokolApplicationBuilder` (`CommandLineParser`), and **integrated into** `SokolApplicationBuilder` via the existing `--obfuscate` flag.
6. **Never break the build or the app.** Interop, reflection, serialization, entry points, and trimming roots must survive untouched. Correctness beats aggressiveness.
7. **Debuggable failures in the field:** emit an original→obfuscated **symbol map** so crash stack traces can be de-obfuscated.

---

## 4. Where obfuscation runs (the pipeline seam)

### 4.1 The constraint

`SokolApplicationBuilder` invokes a **single** `dotnet publish` (native) or `dotnet build` (web) per target. That one process runs *source → IL → trim → AOT/ILC → native/wasm* end-to-end. There is no natural gap for the builder to "reach in" between the C# compile and ILC from the outside. Concretely, the current invocations are:

- **Desktop** — [`DesktopAppBuilder.cs:94`](../tools/SokolApplicationBuilder/Source/DesktopAppBuilder.cs) — `dotnet publish -f <tfm> <proj> -r <rid> -c <cfg> -p:PublishAot=true …`
- **Android** — [`AndroidAppBuilder.cs:1384`](../tools/SokolApplicationBuilder/Source/AndroidAppBuilder.cs) — per-ABI loop, `dotnet publish <proj> -r linux-bionic-{arm64,arm,x64} -c <cfg> -p:BuildAsLibrary=true …`
- **iOS** — [`IOSAppBuilder.cs:400`](../tools/SokolApplicationBuilder/Source/IOSAppBuilder.cs) — `dotnet publish <proj> -r ios-arm64 -c <cfg> -p:BuildAsLibrary=true …`
- **Web** — [`WebAppBuilder.cs:135`](../tools/SokolApplicationBuilder/Source/WebAppBuilder.cs) — `dotnet build -f <tfm> <proj> -c <cfg> -p:DefineConstants="WEB" -o <out>` (drives the `browser-wasm` / `RunAOTCompilation` project)

### 4.2 The chosen mechanism: injected MSBuild target

The obfuscator runs **inside** each of those builds as an MSBuild target, injected via `CustomAfterMicrosoftCommonTargets`. `SokolApplicationBuilder` appends a few `-p:` properties to the command line it already constructs; nothing about the per-platform command otherwise changes.

Properties appended when `--obfuscate` is set:

```
-p:SokolObfuscate=true
-p:SokolObfuscationConfig=<abs path to obfuscate.xml>        (auto-discovered from the project folder)
-p:SokolObfuscatorTool=<abs path to SokolObfuscator.dll>
-p:CustomAfterMicrosoftCommonTargets=<abs path to Sokol.Obfuscation.targets>
```

`Sokol.Obfuscation.targets` (shipped in `tools/SokolObfuscator/` and copied to the builder's output) defines:

```xml
<Project>
  <!--
    Runs after the managed assembly is emitted by CoreCompile, and BEFORE the
    trimmer / ILC / Mono-WASM AOT consume it. @(IntermediateAssembly) is the
    just-built managed DLL in obj/. Rewriting it in place means every downstream
    step (trim, ILC, WASM AOT, bundling) operates on the obfuscated IL.
  -->
  <Target Name="SokolObfuscateManagedAssembly"
          AfterTargets="Compile"
          BeforeTargets="ILLink;IlcCompile;_WasmAotCompileApp"
          Condition="'$(SokolObfuscate)' == 'true'">
    <PropertyGroup>
      <_SokolObfArgs>--input "@(IntermediateAssembly->'%(FullPath)')"</_SokolObfArgs>
      <_SokolObfArgs Condition="'$(SokolObfuscationConfig)' != ''">$(_SokolObfArgs) --config "$(SokolObfuscationConfig)"</_SokolObfArgs>
      <_SokolObfArgs>$(_SokolObfArgs) --rid "$(RuntimeIdentifier)" --map "$(IntermediateOutputPath)obfuscation.map.json"</_SokolObfArgs>
    </PropertyGroup>
    <Message Importance="high" Text="🔒 Obfuscating @(IntermediateAssembly->'%(Filename)%(Extension)') …" />
    <Exec Command="dotnet &quot;$(SokolObfuscatorTool)&quot; $(_SokolObfArgs)" />
  </Target>
</Project>
```

**Why this seam works for every target:**

- The obfuscator rewrites the assembly *after* it exists (`AfterTargets="Compile"`) and *before* any consumer (`BeforeTargets`).
- For **ILC** targets the trimmer (`ILLink`) then `IlcCompile` read the (now obfuscated) intermediate assembly and produce native code whose embedded reflection/stack-trace names are obfuscated.
- For **Web/Mono-WASM** the trimmer + `_WasmAotCompileApp` + the app-bundle packaging read the obfuscated assembly, so the `*.dll` shipped in `_framework/` is obfuscated.
- Renaming happens **before trimming**, so the obfuscator must be **trimming-aware** (§7) — it must not rename anything the trimmer roots or the reflection stack resolve by name.

> **Acceptance note (SDK-version sensitivity):** the exact target names in `BeforeTargets` (`ILLink`, `IlcCompile`, `_WasmAotCompileApp`) are SDK-internal and can drift across .NET releases. The correctness requirement is only *"runs after the assembly is emitted and before it is consumed."* Part of bring-up (§9, §11) is to verify, on the pinned SDK (`net10.0`), that the obfuscated assembly is the one ILC/WASM actually ingest — by diffing `strings` output of the native binary / the shipped `.dll` before vs. after.

### 4.3 Alternatives considered and rejected

- **Source-level rewrite (Roslyn).** Rewriting the C# source tree into a temp copy before compiling. Rejected: fragile against source generators, `partial` classes, and the two generated-code trees (`src/sokol/generated/`, per-example `Source/Generated/`); and "which files/folders" is the *only* thing it does better, which we recover at IL level via the PDB (§7.3). It also cannot see what trimming will keep.
- **Post-native binary obfuscation.** Packing/mutating the `.so`/`.dylib`/`.exe`/`.wasm`. Rejected: platform-specific, brittle, breaks code-signing / notarization / store review, and buys little once code is already native.
- **Two-phase build orchestrated by the builder** (build managed-only → obfuscate → `dotnet publish --no-build`). Rejected: `dotnet publish -p:PublishAot` re-runs the build and would overwrite the obfuscated assembly; `--no-build` does not compose cleanly with cross-RID AOT/WASM publishes.

---

## 5. Architecture

### 5.1 Components

```
tools/
  SokolObfuscator/                     ← NEW project (console app + library)
    SokolObfuscator.csproj             ← references dnlib, CommandLineParser
    Program.cs                         ← CLI entry (CommandLineParser, mirrors builder)
    Source/
      ObfuscationOptions.cs            ← CLI options model
      ObfuscationConfig.cs             ← XML config model + loader/merger
      Engine/
        ObfuscationEngine.cs           ← orchestrates the passes over one assembly
        NamingScheme.cs                ← random valid-identifier generator (+ seed)
        SymbolMap.cs                   ← original→obfuscated map writer (JSON)
        MemberGraph.cs                 ← vtable/interface/override groups, ref fixups
      Passes/
        RenameMethodsPass.cs
        RenameTypesFieldsPropsPass.cs
        EncryptStringsPass.cs
        ControlFlowPass.cs             ← optional / phase 3
      Safety/
        ExclusionRules.cs              ← the "never rename" analysis (interop/reflection…)
    Sokol.Obfuscation.targets          ← MSBuild injection (see §4.2)
    templates/obfuscate.xml            ← annotated example config

tools/SokolApplicationBuilder/
    Source/Options.cs                  ← add --obfuscation-config (reuse existing --obfuscate)
    Source/ObfuscationInjection.cs     ← NEW: builds the -p: args, resolves tool paths
    Source/DesktopAppBuilder.cs        ← append obfuscation args to publish cmd
    Source/AndroidAppBuilder.cs        ← append obfuscation args to publish cmd
    Source/IOSAppBuilder.cs            ← append obfuscation args to publish cmd
    Source/WebAppBuilder.cs            ← append obfuscation args to build cmd
```

### 5.2 Why dnlib (not Mono.Cecil)

Both were on the table. **dnlib** is chosen as the primary engine, specifically because the code is **.NET 10** and function-pointer-heavy:

- **Modern-metadata robustness.** dnlib (latest 4.5.0, 2025) is purpose-built to read/write arbitrary and current .NET assemblies; its NuGet compatibility is computed through .NET 10. It handles the constructs this repo actually emits — `delegate* unmanaged<…>` function pointers, the **185** `[UnmanagedCallersOnly]` callbacks, static-abstract interface members — which is the exact area where the public `Mono.Cecil` 0.11.6 (targets `netstandard1.3`, infrequent releases) has historically been weakest. Since the tool's core job is writing assemblies *back* correctly, that risk would sit on the critical path.
- **Battle-tested writer.** dnlib's writer is the one behind **ConfuserEx**, **dnSpyEx**, and **de4dot** — production .NET obfuscators. dnlib exists precisely because Cecil was not robust enough for this class of work.
- **First-class portable PDBs.** dnlib reads/writes Windows *and* portable PDBs, which the file/folder scoping feature depends on (§7.3).
- MIT-licensed.

`Mono.Cecil` remains a viable fallback — cleaner, smaller API, and the .NET trimmer (ILLink) is built on a Microsoft-maintained *fork* of it, so the Cecil codebase is demonstrably capable of the latest metadata. The catch is only the **public release cadence**. The pass interfaces are written so the metadata backend is swappable, but dnlib is the default.

### 5.3 The obfuscator as both CLI and MSBuild callee

`SokolObfuscator` is a **console app**. The injected MSBuild target invokes it with `<Exec>dotnet SokolObfuscator.dll --input … --config …</Exec>`. A developer can invoke the *exact same* command by hand for debugging or CI. This satisfies both requirements: "part of `SokolApplicationBuilder`" (the builder ships it and toggles it) **and** "CLI in the same way as `SokolApplicationBuilder`" (identical `CommandLineParser` conventions).

---

## 6. Transformations

### 6.1 Method renaming (primary)

**What:** rename each in-scope, non-excluded method to a random valid identifier.

**Naming scheme:** identifiers match `^[A-Za-z][A-Za-z0-9]{N}$` (letter first so the name is a valid, greppable-nuisance identifier; letters + digits per the requirement). Default length 8–12, uniqueness enforced per declaring type (overloads keep distinct signatures, so name collisions across overloads are allowed by IL but we still generate unique names for readability of the map). A `--seed <int>` (or `<Seed>` in XML) makes runs reproducible; omit for per-build randomness. Optional `--dictionary` mode can draw from a confusing-but-legal set (`l1`, `I1`, `O0`) if desired — off by default.

**How (dnlib):** iterate `MethodDef`s; for direct calls, in-module method references resolve to the same `MethodDef`, so changing `.Name` updates call sites. Generic instantiations, delegate / function-pointer address-of (`ldftn`/`ldvirtftn`), and explicit interface implementations (`MethodImpl`s) are fixed up via `MemberGraph`.

**Correctness rules (must-follow):**
- **Override/interface groups renamed together, or not at all.** A method that overrides a base or implements an interface member from a *non-obfuscated* assembly (framework/BCL) **must not be renamed** — its name/slot must match the external declaration. `MemberGraph` computes these groups; any group touching an external member is excluded wholesale.
- **Property/event accessors** are renamed consistently with (or excluded together with) their property/event.
- Anything caught by the **safety analysis** (§7) is excluded regardless of scope.

### 6.2 String-literal encryption

**What (as implemented):** each `ldstr "plain"` in an *in-scope* method body is rewritten to `ldstr "<base64>"; call string SokolObfuscator.__Strings__::D(string)`, where the injected decryptor `D` does base64-decode → XOR (per-build key, seeded) → UTF-8. The plaintext never appears in the shipped artifact.

**Scope & exclusions (owner requirement — some strings must stay plaintext):**
- Only **in-scope (app) method bodies** are processed — the *same* namespace/folder scope as renaming, so framework strings (`Sokol*`/`Imgui*`/`src/**`) are never touched.
- **String-VALUE excludes** keep specific literals plaintext regardless of scope: `<Exclude kind="string">glob</Exclude>` and `<Exclude kind="string-regex">regex</Exclude>` (CLI: `--exclude string:glob` / `--exclude string-regex:re`). A **built-in default excludes `ca-app-pub-*`** (AdMob ids). Trivial literals (empty, length < 2) are skipped.
- Config strings injected into the native Info.plist / AndroidManifest — e.g. `IOSInfoPlistKey_GADApplicationIdentifier` in `Directory.Build.props` ([IOSAppBuilder.cs](../tools/SokolApplicationBuilder/Source/IOSAppBuilder.cs)) — are **not `ldstr` in the IL**, so string encryption never touches them (verified).

**Why it matters for AOT:** string literals otherwise survive **verbatim** in the native binary's data section and in the WebCIL `.wasm`; `strings`/grep reveals URLs, keys, format strings, gameplay secrets. Encryption defeats casual/`grep`-level inspection. It is **obfuscation, not security** — the key + decryptor ship in the binary.

**Correctness rules:**
- Only `ldstr` in **method bodies** is rewritten. Attribute arguments, `[DllImport]` `EntryPoint` strings, and resources are **metadata**, not `ldstr`, so they are naturally untouched.
- App-scope-only processing keeps **trimmer feature-switch / substitution** strings (framework/BCL) out of reach; list any *app-side* switch/reflection literal as a `string` exclude.
- Toggle with `<StringEncryption>on|off</StringEncryption>` or `--string-encryption on|off` (default off).

**Verified (2026-09-09, desktop):** on `cube`, 10 app strings encrypted; the sentinel `Initialize() Enter` is absent from the managed `.dll` (UTF-16) **and** the desktop **native** binary (UTF-16 + UTF-8); the app **runs and prints the decrypted string** under both JIT and NativeAOT; and a `string:*Initialize*` exclude keeps it plaintext.

### 6.3 Type / field / property renaming (optional, on by config)

**What:** rename types, fields, properties, enum members in scope.

**Why gated:** broader metadata sanitization, but higher risk — any of these reached by **reflection** (`Type.GetType("Ns.MyType")`, `GetProperty("Name")`), **JSON/serialization by member name**, **JSImport/JSExport** name mapping, or **XAML/data-binding-style** lookups will break. Given the codebase surface (`DynamicallyAccessedMembers` present, a `JSImport`), this pass is **off by default** and must be opted into, with the safety layer (§7) doing the heavy lifting. `[Obfuscation]`-attributed and config-excluded members are honored.

### 6.4 Control-flow obfuscation (optional, phase 3, lowest priority)

**What:** opaque predicates + basic-block flattening in method bodies to frustrate decompilation of the *Web* `.dll`.

**Why last / lowest value:** on native targets the code is already machine code, so decompilation is not the threat; the value is Web-only, and it fights ILC/JIT optimizations and inlining, increases size, and risks subtle bugs. Ships as `--control-flow off|low|medium|high`, default `off`. Never applied to methods excluded by the safety layer, to `[UnmanagedCallersOnly]` methods, or to anything performance-critical the config marks.

---

## 7. Safety: the "never obfuscate" analysis (correctness core)

This is the part that keeps the app working. Before any pass runs, `ExclusionRules` builds the set of members that **must not** be renamed/rewritten. Scope defaults (§7.1) already exclude the framework; these rules apply on top, within whatever scope is active.

### 7.1 Default scope = the app's own code (by namespace / source folder)

> **Corrected by the Phase-0 spike (2026-09-09).** Sokol.NET examples do **not** ship the framework as a separate assembly. Each example's `Directory.Build.props` compiles the framework source directly into the single example assembly (`<Compile Include="../../src/sokol/*.cs" />`, `.../generated/*.cs`, `../../src/imgui/*.cs`, …). Measured on a real net10 `cube.dll`: **730 types / 2534 methods / 2221 `[DllImport]`s**, all in the one assembly. An *assembly*-level scope therefore cannot separate app code from framework code.

Scope is defined by **namespace / source folder**, not by assembly. By default the obfuscator processes only the app's own code and excludes the framework: everything whose declaring namespace is `Sokol.*`, `Imgui.*`, `Ozz.*`, `JoltPhysicsSharp.*`, etc., or whose PDB source document resides under `src/`. These ship as default `exclude` rules in the built-in config; a project may override them in `obfuscate.xml`.

The generated bindings (`src/sokol/generated/*.cs`) are almost entirely `[DllImport]` P/Invoke and are **additionally** caught by the always-exclude safety rules (§7.2) — which is why the Phase-0 spike auto-excluded all 2221 of them regardless of scope. Obfuscating the framework itself is possible but higher-risk and must be opted into and re-verified per platform.

### 7.2 Members always excluded (even inside scope)

| Category | Why | How detected |
|---|---|---|
| `[UnmanagedCallersOnly]` methods | Address taken by native code; `EntryPoint=` is an exported ABI symbol | attribute scan |
| `[DllImport]` / `[LibraryImport]` methods | P/Invoke; `EntryPoint` binds a native symbol | attribute scan |
| `[JSExport]` / `[JSImport]` members | JS↔managed binding by name | attribute scan |
| **Application entry points — `Main`, `AndroidMain`, `IOSMain`** | Native launchers invoke them by name / exported symbol; renaming the method **or** altering the `[UnmanagedCallersOnly(EntryPoint="…")]` string ⇒ **the app fails to start** | built-in name list **+** assembly entry-point token **+** `[UnmanagedCallersOnly]`/`EntryPoint` pin |
| Module initializers (`[ModuleInitializer]`) & static constructors | Runtime-invoked ABI | metadata flags / attribute |
| Overrides/impls of **external** virtuals/interfaces | vtable/interface slot must match external name | `MemberGraph` |
| `[DynamicDependency(...)]` targets & `TrimmerRootDescriptor` members | Referenced **by string**; renaming → trimmed away → runtime failure | attribute + root-descriptor XML scan |
| `[DynamicallyAccessedMembers]`-reachable members | Reflection contract the trimmer honors | attribute scan (conservative) |
| Members read by **reflection/serialization by name** | `GetMethod("X")`, JSON member names, etc. | heuristic + explicit config/attribute (cannot be fully inferred — see risk R3) |
| Anything marked `[System.Reflection.Obfuscation(Exclude=true)]` | Explicit developer opt-out | attribute scan |
| `[UnmanagedFunctionPointer]` delegates used as native callbacks | ABI shape | attribute scan |

> **⛔ Startup ABI — hard rule (non-overridable).** Every Sokol.NET app's `Source/Program.cs` defines three entry points the native launchers call: **`Main`** (plain managed entry point — desktop/web), **`AndroidMain`** (`[UnmanagedCallersOnly(EntryPoint = "AndroidMain")]`, returns the app-desc pointer — the Android `lib<App>.so` loader resolves the symbol `AndroidMain`), and **`IOSMain`** (`[UnmanagedCallersOnly(EntryPoint = "IOSMain")]` — the iOS launcher resolves `IOSMain`). The obfuscator ships a **built-in, non-overridable exclusion** for these three *by name*, in addition to detecting them structurally, and it **never** rewrites an `[UnmanagedCallersOnly(EntryPoint=…)]` string (those are attribute metadata, not body `ldstr`, so string encryption already leaves them alone — §6.2). Renaming any of them, or touching the `"AndroidMain"`/`"IOSMain"` symbol strings, means the app will not start on that platform. A config file **cannot** re-include them. Verified in acceptance (§9, checks 1–2, 4).

### 7.3 Include / exclude by method, file, folder, type, namespace

The requirement to select **files** and **folders** is honored at IL level via the **portable PDB**: each method's sequence points map to a **source document path**. The obfuscator loads the PDB alongside the assembly and, for each method, derives its primary source file; folder/file rules are glob-matched against that path (relative to the project root). Requirements & fallback:

- The build must emit a **portable PDB** for file/folder rules to work. Sokol.NET example csproj already uses `TrimmerRemoveSymbols=false`; the injected target additionally ensures `DebugType=portable` + `DebugSymbols=true` are in effect for the obfuscated configuration (the map/PDB stay build-side; symbols can still be stripped from the *native* output, §10).
- If no PDB is present, file/folder rules are inert and the tool logs a warning; **type/namespace/method** rules still work (they come from metadata, not the PDB).

Selection primitives (all support `*`/`**` globs and may be `include` or `exclude`):

- **method** — by full signature or `Type::Method` or wildcard (`*::Update`).
- **type** / **namespace** — metadata names (`MyGame.Physics.*`).
- **file** — source file glob (`**/Secrets.cs`).
- **folder** — source folder glob (`Source/Net/**`).

**Precedence (highest wins):**
1. `[Obfuscation]` attribute / always-excluded safety rules (§7.2) — cannot be overridden.
2. CLI explicit include/exclude.
3. XML `exclude` rules.
4. XML `include` rules.
5. Defaults (in-scope = included; framework/generated = excluded).

---

## 8. Configuration

### 8.1 XML schema (optional)

For the **standalone CLI**, XML is **not mandatory** — with no config, defaults apply: app assembly only, method rename on, string encryption `light`, type/field/property rename off, control-flow off, framework & generated code excluded. For the **build/task integration** the file is **required and named `obfuscate.xml`** in the project folder: its presence is what turns obfuscation on (§8.4). It is auto-discovered there; `--obfuscation-config <path>` overrides the location/name.

Annotated example (`tools/SokolObfuscator/templates/obfuscate.xml`, copied into a new project's folder by the scaffolder):

```xml
<?xml version="1.0" encoding="utf-8"?>
<Obfuscation>
  <!-- Global switches (overridable per-scope and by CLI) -->
  <Settings>
    <RenameMethods>true</RenameMethods>
    <RenameTypesFieldsProperties>false</RenameTypesFieldsProperties>
    <StringEncryption>off</StringEncryption>     <!-- on | off -->
    <ControlFlow>off</ControlFlow>               <!-- off | low | medium | high -->
    <Seed></Seed>                                <!-- empty = random per build -->
    <IdentifierLength>10</IdentifierLength>
    <EmitSymbolMap>true</EmitSymbolMap>
  </Settings>

  <!-- What to process. Default scope is the app assembly only. -->
  <Scope>
    <Assembly include="true">lib*</Assembly>       <!-- the app assembly -->
    <!-- <Assembly include="true">Sokol.GUI</Assembly>   opt-in, re-verify! -->
  </Scope>

  <!-- Inclusion / exclusion. Later, more specific rules win per §7.3 precedence. -->
  <Rules>
    <!-- keep an entire folder readable (e.g. code hit by reflection/serialization) -->
    <Exclude kind="folder">Source/Serialization/**</Exclude>
    <Exclude kind="file">**/SaveGame.cs</Exclude>
    <Exclude kind="namespace">MyGame.Net.Wire.*</Exclude>
    <Exclude kind="type">MyGame.Config.Settings</Exclude>
    <Exclude kind="method">*::ToString</Exclude>

    <!-- string-VALUE excludes: keep these literals plaintext (never encrypted) -->
    <Exclude kind="string">ca-app-pub-*</Exclude>              <!-- AdMob ids (also a built-in default) -->
    <Exclude kind="string-regex">^https?://</Exclude>          <!-- e.g. keep URLs readable -->

    <!-- force-include something the defaults would skip -->
    <Include kind="folder">Source/Secret/**</Include>
    <Include kind="method">MyGame.Crypto.KeyDerivation::Derive</Include>
  </Rules>
</Obfuscation>
```

### 8.2 In-source opt-out via attribute

The standard BCL attribute is honored, so a developer can exclude a member without editing XML:

```csharp
using System.Reflection;

[Obfuscation(Exclude = true, Feature = "renaming")]     // don't rename
public void ReflectedByName() { … }

[Obfuscation(Exclude = true)]                            // exclude from all passes
public sealed class SerializedByMemberName { … }
```

### 8.3 CLI options (standalone, `CommandLineParser` — mirrors the builder)

```
dotnet SokolObfuscator.dll
  --input <path>                 (required) managed assembly to rewrite in place
  --config <path.xml>            (optional) XML config
  --rid <runtime-identifier>     (optional) informational / per-RID rules
  --map <path.json>              (optional) write original→obfuscated symbol map
  --seed <int>                   (optional) reproducible naming
  --rename-methods <bool>
  --rename-types <bool>
  --string-encryption off|light|full
  --control-flow off|low|medium|high
  --include <kind:pattern>       repeatable (kind = method|type|namespace|file|folder)
  --exclude <kind:pattern>       repeatable
  --identifier-length <int>
  --verbose
  --dry-run                      analyze + write map, do NOT modify the assembly
```

### 8.4 `SokolApplicationBuilder` integration and the `obfuscate.xml` gate

**Ordinary build/run tasks never obfuscate** — not even in release. There is no global auto-on. Obfuscation engages only when **both** hold:

1. it was **requested** — EITHER the build was invoked with `--obfuscate` (the dedicated task supplies it, §8.5) OR the project's `Directory.Build.props` declares `<Obfuscation>true</Obfuscation>` (see below), **and**
2. the project folder contains an **`obfuscate.xml`**.

If it was requested but `obfuscate.xml` is missing, the build proceeds **without** obfuscation and logs a prominent warning (`obfuscate.xml not found — skipping obfuscation`). This is the literal rule: *if obfuscation is requested and the project folder contains `obfuscate.xml`, the release build is obfuscated based upon `obfuscate.xml`; otherwise it is not.*

**Per-project opt-in via `Directory.Build.props`.** So that a project can be obfuscated on every release build **without** remembering the `--obfuscate` flag, `ObfuscationInjection` also reads `<project>/Directory.Build.props` and treats `<Obfuscation>true</Obfuscation>` (any PropertyGroup) as equivalent to `--obfuscate`. The value is read as raw XML by the builder — it does **not** evaluate MSBuild conditions — so it turns obfuscation on for every platform regardless of which PropertyGroup holds it. `obfuscate.xml` is still required (it supplies the config). Note this is a **builder-side** switch: it works through `SokolApplicationBuilder` (desktop/android/ios/web); a raw `dotnet build`/`dotnet run` of the csproj (the JIT dev loop) does **not** obfuscate — which is desirable for day-to-day debugging. Set the property to `false` (or remove it) to disable.

The `--obfuscate` flag **already exists** in [`Options.cs`](../tools/SokolApplicationBuilder/Source/Options.cs) (currently unused). We wire it up and add one optional override:

```
[Option("obfuscate", …)]           public bool Obfuscate { get; set; }          // existing — activates the path
[Option("obfuscation-config", …)]  public string ObfuscationConfig { get; set; } // NEW — optional override; default = <project>/obfuscate.xml
```

Resolution: when `--obfuscate` is set, the builder resolves the config as `--obfuscation-config` if given, else `<project>/obfuscate.xml`, and obfuscation runs **iff that file exists**.

**Recommendation:** obfuscation is meaningful only for `--type release`; on `debug` the builder ignores `--obfuscate` with a note (renamed symbols + encrypted strings make Debug diagnostics painful and there is no shipping value in a debug build).

Usage (identical shape to today's commands, one flag added; each requires `examples/cube/obfuscate.xml` to be present, else it builds un-obfuscated):

```bash
# Desktop
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture desktop --type release --path examples/cube --obfuscate

# Android (obfuscation applied for every ABI in the publish loop)
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture android --type release --path examples/cube --obfuscate

# iOS
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture ios --type release --path examples/cube --obfuscate

# Web (obfuscates the managed .dll that ships in _framework/ — highest value)
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture web --path examples/cube --obfuscate
```

Each `*AppBuilder` calls a shared helper `ObfuscationInjection.AppendArgs(opts, ref command)` that, **when obfuscation is active** (flag set + `obfuscate.xml` present + release), appends the `-p:` properties from §4.2 to the `dotnet publish`/`dotnet build` string it already builds. Nothing else in the per-platform packaging/signing path changes.

### 8.5 VS Code `tasks.json` — the dedicated obfuscation action

Obfuscation is exposed as its **own** task, separate from the normal `prepare-*` / build / run tasks, so a developer explicitly chooses an obfuscated build (this is *the* answer to "should release builds auto-obfuscate?" — **no**, it is a deliberate action). Tasks are ordinary shell entries that invoke the builder, matching the existing 500+:

```jsonc
{
  "label": "obfuscate-cube",                     // desktop release, obfuscated
  "type": "shell",
  "command": "dotnet",
  "args": ["run", "--project", "${workspaceFolder}/tools/SokolApplicationBuilder", "--",
           "--task", "build", "--architecture", "desktop", "--type", "release",
           "--path", "${workspaceFolder}/examples/cube", "--obfuscate"],
  "problemMatcher": "$msCompile"
},
{
  "label": "obfuscate-cube-web",
  "type": "shell",
  "command": "dotnet",
  "args": ["run", "--project", "${workspaceFolder}/tools/SokolApplicationBuilder", "--",
           "--task", "build", "--architecture", "web",
           "--path", "${workspaceFolder}/examples/cube", "--obfuscate"],
  "problemMatcher": "$msCompile"
}
```

Running `obfuscate-cube` performs a release build that obfuscates **iff** `examples/cube/obfuscate.xml` exists (§8.4). One `obfuscate-<example>` (plus `-web`, and per other platforms as needed) is added per example.

**Generation & the commit rule.** These entries are generated by the same mechanism that already adds the `prepare-*` tasks when scaffolding an example — [`CreateExampleTask.UpdateTasksJson`](../tools/SokolApplicationBuilder/Source/CreateExampleTask.cs) (extend its inserted template; `DeleteExampleTask` removes them). `.vscode/tasks.json` is a **local developer file that is never committed** (it names private example trees), so the `obfuscate-*` tasks are a local-dev convenience produced by the scaffolder / a one-time migration for existing examples — not a checked-in change.

### 8.7 Forcing a fresh compile every obfuscated build (the stale-marker trap)

The obfuscator rewrites `@(IntermediateAssembly)` **in place** and stamps an idempotency marker (`SokolObfuscator.__Obfuscated__`) so a doubled pipeline (notably the wasm publish, which fires the pipeline twice) can't re-obfuscate and corrupt the map (§6.1). That marker creates a trap for incremental builds: if the **sources are unchanged**, `CoreCompile` is skipped, the previous build's already-marked assembly is reused, and the `AfterTargets="Compile"` step sees the marker and skips — so a changed `obfuscate.xml` / tool / seed does **not** take effect, and an earlier *partial* obfuscation (e.g. rename-only, from before string encryption was added) ships silently. The builder does **not** clean (`ObfuscationInjection` only appends `-p:` args).

The fix lives in `Sokol.Obfuscation.targets` as target **`SokolObfuscateForceRecompile`** (`BeforeTargets="CoreCompile"`, `Condition="'$(SokolObfuscate)'=='true'"`): it deletes `$(IntermediateOutputPath)$(TargetName)$(TargetExt)` (+ its `.pdb` and the stale `obfuscation.map.json`) so `CoreCompile`'s up-to-date check fails and it always recompiles a clean, unmarked assembly, which the obfuscation step then rewrites fresh. It is RID/project-agnostic (uses MSBuild's own `$(IntermediateOutputPath)`/`$(TargetName)`) and runs **only** when obfuscating, so ordinary builds keep full incrementality. Verified: two consecutive obfuscated desktop builds with no source change both re-obfuscate (no `already obfuscated … skipping`); the Android publish recompiles fresh on `linux-bionic-arm64`.

---

## 9. Correctness & acceptance criteria

Per the project's coding principles, "done" is defined up front, and for this project success is **runtime/visual on real devices**, not just unit tests.

**Automated (host, in CI):**
1. `--dry-run` on every example assembly produces a map and reports the exclusion set; the exclusion set contains **`Main`, `AndroidMain`, `IOSMain`**, and **every** `[UnmanagedCallersOnly]`, `[DllImport]`, entry point, and external-override method (assert the three entry points are present by name, and counts > 0 where expected).
2. Round-trip: obfuscate `<App>.dll`, then reload it and verify (a) it still verifies (`ilverify` / load), (b) no excluded member changed name, (c) all `EntryPoint=` strings intact, (d) **`Main`, `AndroidMain`, `IOSMain` are present with unchanged names and the `"AndroidMain"`/`"IOSMain"` exported symbols are byte-for-byte intact**.
3. `strings` diff: build one example twice (with/without `--obfuscate`); assert chosen sentinel method names / string literals are **present** in the plain binary and **absent** in the obfuscated one (native targets: the `.so`/binary; Web: `_framework/*.dll`).

**Manual / device (the real bar — mandatory before "done"):**
4. Each obfuscated example **launches and runs correctly** on: at least one **Desktop** (macOS Metal), one **Android** (GLES3) device, one **iOS** (Metal) device, and **Web** (WebGL2 in a browser). Rendering + input behave identically to the non-obfuscated build.
5. Web: confirm in DevTools that the downloaded `_framework/<App>.dll` decompiles to **obfuscated** names.
6. A deliberately-thrown exception's stack trace can be **de-obfuscated** using the emitted `obfuscation.map.json`.
7. **Small-screen UI unaffected** — obfuscation must not change layout/behavior; re-verify a UI-heavy example on a phone-sized target.

> Because a shared/IL change "can silently break a different target platform," acceptance explicitly names all four target families; a pass on macOS is not a pass for Android GLES3 / WASM.

---

## 10. Defense-in-depth: AOT/trimming knobs (documentation, not code)

Renaming + string encryption reduce *what leaks*; these existing MSBuild/ILC knobs reduce *how much name metadata is embedded in the first place*. The doc records them so a shipping build can be hardened; the tool does not force them (some trade diagnosability).

- **`IlcGenerateStackTraceData=false`** (or `StackTraceSupport=false`) — stops ILC embedding method-name strings for stack traces. Biggest single reduction of residual names in native output. Trade-off: exceptions print addresses, not names → rely on the symbol map.
- **`StripSymbols=true` / `RemoveSections=true`** — already used by the Android release-hardening path ([`AndroidAppBuilder.cs`](../tools/SokolApplicationBuilder/Source/AndroidAppBuilder.cs)); strips the ELF symbol table. Recommend the equivalent for desktop/iOS release.
- **`TrimMode=full`** (vs the current `partial`) — trims more aggressively, so fewer members retain reflection metadata (hence fewer names). Trade-off: higher risk of trimming something reflection needs; must be validated per app.
- **`InvariantGlobalization=true`** — already set for Web; reduces ICU data / some strings.
- **Web:** `WasmNativeStrip=true`, and serve `_framework` with cache/obfuscated file names as desired. The managed `.dll` is the exposed artifact, which is exactly why IL obfuscation matters most here.
- **Keep `DebugType=portable` build-side** so the obfuscator can honor file/folder rules and emit a usable map, while still stripping symbols from the *shipped native* artifact.

---

## 11. Implementation plan (phased)

Each phase is independently shippable and separately device-verified.

**Phase 0 — Scaffolding**
1. Create `tools/SokolObfuscator/` console project; add to `Sokol.NET.sln`. Reference `dnlib`, `CommandLineParser`.
2. CLI (`Program.cs`, `ObfuscationOptions.cs`) + `--dry-run` that loads an assembly and prints its members. → *verify:* runs on `examples/cube` `obj/.../cube.dll`.

**Phase 1 — Safety analysis + method renaming (the core deliverable)**
3. `ExclusionRules` (§7.2) + `MemberGraph` (override/interface groups, ref fixups).
4. `RenameMethodsPass` + `NamingScheme` + `SymbolMap`.
5. XML config model + CLI include/exclude + `[Obfuscation]` attribute + PDB file/folder mapping.
6. `Sokol.Obfuscation.targets` + `ObfuscationInjection` wiring in all four `*AppBuilder`s; hook up existing `--obfuscate` (auto-discovers `<project>/obfuscate.xml`, no-op + warning if absent), add optional `--obfuscation-config` override, and generate the dedicated `obfuscate-*` tasks in `.vscode/tasks.json` via `CreateExampleTask.UpdateTasksJson` (§8.5).
   → *verify:* all four targets build **and run on device**; `strings` diff shows sentinel names gone; map de-obfuscates a stack trace. **Web decompile check.**

**Phase 2 — String encryption — DONE (2026-09-09)**
7. `StringEncryptor` (XOR + base64) + injected `SokolObfuscator.__Strings__::D` decryptor; app-scope only; string-VALUE excludes (`string` / `string-regex`, default `ca-app-pub-*`); `<StringEncryption>on|off</StringEncryption>` / `--string-encryption`.
   → *verified:* sentinel absent from managed `.dll` (UTF-16) and desktop **native** binary (UTF-16+UTF-8); app runs + decrypts under JIT **and** NativeAOT; `string:` exclude keeps a literal plaintext. Remaining: on-device (Android/iOS) + Web spot-check (same managed→native flow as Phase 1).

**Phase 3 — Type/field/property renaming (opt-in) — IN PROGRESS (2026-09-09)**
8. `RenamePass` (`Source/RenamePass.cs`), default off, gated by `<RenameTypesFieldsProperties>on</…>` / `--rename-types on`, safety-gated. Renames types (collapsing the namespace of top-level types), fields, and properties **with their get_/set_ accessors**, sharing one `NameGenerator` with the method pass. Runs **after** string encryption (which scopes on the original type names) and before the idempotency marker. Left untouched by design: enum members + `value__` (Enum.Parse/ToString), events; excluded automatically: out-of-scope/framework types, `SokolObfuscator.*` helpers, `[Obfuscation(Exclude)]`/`[DynamicDependency]`, property accessors that are virtual/override/interface-linked, and — conservatively — the whole TYPE NAME of any type that declares startup/interop ABI (entry point, `Main`/`AndroidMain`/`IOSMain`, P/Invoke, `[UnmanagedCallersOnly]`/`[LibraryImport]`/JS interop) so the native launcher's host type stays resolvable.
   → *verified so far:* cube (no reflection/serialization) obfuscated with Phase 3 **on** — runs on Desktop JIT (+ string-decrypt) with `_state` struct, 19 fields, the shader class, and `<PrivateImplementationDetails>`+RVA fields renamed while `CubeSapp`/`MainClass`/`Program` type names are kept; Desktop NativeAOT + Android/Web spot-checks in progress.
   → *remaining:* an app that uses reflection/serialization still works with the pass **off**; with it **on** only after excluding the reflected members. (Deferred: full `MemberGraph` to rename app-only virtual/interface groups — the pass currently skips all virtuals/overrides.)

**Phase 4 — Control-flow (optional)**
9. `ControlFlowPass`, default off. → *verify:* Web decompile is materially harder; no regression on native; perf within budget.

**Files changed in `SokolApplicationBuilder`:** `Options.cs` (+1 option), the four `*AppBuilder.cs` (append args), new `ObfuscationInjection.cs`. No change to the per-platform packaging/signing logic.

---

### 11.1 Implementation findings (Phase 0–1, host + device — 2026-09-09)

Verified on macOS (desktop NativeAOT + JIT), an **Android** device (Galaxy Z Flip4, arm64), the **iOS** device (iPhone SE 2G), and the **Web** (mono-wasm) artifact. Findings that shaped or constrain the implementation:

1. **The framework is compiled into the app assembly** (§7.1) — scope by namespace/folder, not by assembly. Measured on `cube.dll`: 730 types / 2221 `[DllImport]`s in one assembly; default scoping leaves the app's own code (3 methods in cube).
2. **A clean build is required.** The injected target hooks `AfterTargets="Compile"`; an *incremental* build that skips Compile would skip obfuscation. Obfuscated release builds must compile fresh — the builder/`obfuscate-*` task cleans first.
3. **`--path` must be absolute (iOS).** The iOS builder's pre-existing `CompileShaders` step joins a *relative* `projectDir` with itself as the working directory and fails `MSB1009` when `--path` is relative — aborting *before* the app publish, so obfuscation never runs. Use an absolute `--path` (a known builder quirk, unrelated to obfuscation).
4. **Web (mono-wasm) ships the managed assembly** as a WebCIL `_framework/cubeweb.wasm` — obfuscation sanitizes it (verified: `sokol_main` / `CreateAppDesc` absent, framework `LoadFileSync` preserved). This is the **highest-value** target — the IL is downloadable in the browser. Verified by publishing the `browser-wasm` project (`cubeweb.csproj`) directly with the injected target. The *builder's* web task (`WebAppBuilder`) has a **separate** pre-existing prerequisite — `<project>/script/project_vars.sh` plus `~/.urhonet_config` → `template/Web` scaffolding — that a plain (non-obfuscated) web build fails on identically; that is a web-build-setup concern, not obfuscation.
5. **Two correctness guards the web publish exposed** (both fixed and verified on the wasm publish): (a) **idempotency** — a `dotnet publish` runs the build pipeline more than once (wasm), firing the `AfterTargets="Compile"` step twice; the obfuscator now stamps a marker type (`SokolObfuscator.__Obfuscated__`) and a repeated pass **skips**, so the symbol map stays keyed by *original* names; (b) **nested-type namespace** — compiler-generated closures/display classes have an *empty* `Namespace`, so scope rules resolve the namespace from the **outermost** enclosing type, keeping framework closures (e.g. `Sokol.SFilesystem/<>c__DisplayClass…::<LoadFileSync>b__0`) inside the framework exclusion.

**Per-target proof:** the sentinels `sokol_main` / `CreateAppDesc` are present in the un-obfuscated output and **absent** after obfuscation — confirmed on the desktop Mach-O executable, the Android `libcube.so` (aarch64 ELF), the iOS `cube.framework` binary (Mach-O arm64), and the Web WebCIL `cubeweb.wasm`. The obfuscated app runs on all four real targets: desktop (native + JIT), **Android on device (Z Flip4)** — rendered its first frame, **iOS on device (iPhone SE 2G, iOS 26.6.1)** — installed and launched, process alive, and **Web in a browser** — the obfuscated cube renders (rotating cube, owner-verified) from the obfuscated `cubeweb.wasm`. Each build emits an `obfuscation.map.json` symbol map keyed by original names.

> **iOS signing note (§11.1.5).** The iOS build must use the **development team that owns the signing profiles** for the `com.elix22.*` bundle ids — here `U627U428U4` (the team every other iOS example caches in `~/.Sokol.NET-cache/*.teamid`; its `com.elix22.*` / `*` wildcard profiles sign any bundle id). Passing a *different* team (e.g. a bare `Apple Development` cert with no matching profile) fails at `No profiles for '<id>'` — a signing-setup mistake, not obfuscation. The builder's `build` produced the signed `.app`; the on-device install+launch was done with `ios-deploy` (install) + `xcrun devicectl device process launch` (ios-deploy could not auto-launch without the iOS 26 DeveloperDiskImage).

## 12. Risks & mitigations

| # | Risk | Mitigation |
|---|---|---|
| R1 | MSBuild seam target names drift across SDK versions → obfuscator runs too late/early | Bring-up verification (§4.2 note) via `strings`/decompile diff; pin to `net10.0`; the invariant ("after emit, before consume") is what we assert in CI |
| R2 | Renaming a member the **trimmer** roots by string → trimmed away → runtime crash | Trimming-aware exclusions (§7.2): parse `[DynamicDependency]`, root descriptors, `[DynamicallyAccessedMembers]`; default app-only scope; device acceptance gate |
| R3 | Reflection/serialization **by name** cannot be fully inferred statically | Conservative defaults (type/field rename off), `[Obfuscation]` + config exclusions, documented guidance, device tests on reflection-using examples (e.g. scene loading, save games) |
| R4 | External-override renamed → broken vtable/interface dispatch | `MemberGraph` excludes any group touching an external member |
| R5 | String encryption breaks a feature-switch / culture / interop string | Only rewrite body `ldstr`; skip metadata/attribute strings; safety-flag known switch strings; per-scope `off` |
| R6 | Obfuscated Debug build is undebuggable | Engage only on `--type release`; always emit the symbol map |
| R7 | Control-flow fights ILC/JIT, bloats size, subtle bugs | Off by default; Web-only value; perf budget in acceptance |
| R8 | Widening scope to the framework re-introduces mass interop breakage | Framework/generated excluded by default; widening requires explicit opt-in + full four-platform re-verify |

---

## 13. Open questions / future work

- **Symbol-map delivery:** where do shipped-build maps live so field crashes can be symbolicated? (Proposal: archive `obfuscation.map.json` per release alongside build artifacts.)
- **Assembly-level renaming caveats on Web:** whether to also rename the assembly file name in `_framework/` (affects the boot manifest) — deferred.
- **Virtualization / anti-tamper:** explicitly out of scope now; revisit only if a concrete threat justifies the cost and the native-code reality (§2) is accounted for.
- ~~**`--obfuscate` default for release**~~ — **Resolved (owner):** ordinary release builds never obfuscate. Obfuscation is a dedicated `obfuscate-*` task, gated on the presence of `obfuscate.xml` in the project folder and driven by it (§8.4–8.5).

---

## 14. References

- ByteHide Shield — Native AOT protection (Pre-AOT protection, trimming-aware transforms, recommended vs. prohibited protections): <https://docs.bytehide.com/platforms/dotnet/products/shield/frameworks/aot/native>
- "Can I protect my application using AOT compilation?" — <https://stackoverflow.com/questions/63219698/can-i-protect-my-application-using-aot-compilation>
- Curated list of .NET obfuscators (IL-level, Cecil/dnlib): <https://github.com/NotPrab/.NET-Obfuscator>
- dnlib (primary engine; ConfuserEx/dnSpyEx/de4dot) — <https://github.com/0xd4d/dnlib>
- Mono.Cecil (fallback) — <https://github.com/jbevain/cecil>
- Related in-repo: [`docs/BUILD_SYSTEM.md`](BUILD_SYSTEM.md), [`docs/SOKOL_APPLICATION_BUILDER.md`](SOKOL_APPLICATION_BUILDER.md), [`docs/WEBASSEMBLY_BROWSER_GUIDE.md`](WEBASSEMBLY_BROWSER_GUIDE.md)
