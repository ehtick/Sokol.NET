# Code Obfuscation (Experimental)

> ⚠️ **This feature is still under active development and has NOT been fully verified.**
> The obfuscator has been smoke-tested on the `cube` example across Desktop, Android, iOS and Web,
> but it has **not** been validated on real applications that use reflection, serialization, JSON by
> member name, or dynamic type loading. Treat it as experimental: after enabling it, **test your app
> thoroughly on every target platform**, and keep the generated **symbol map** so you can translate
> obfuscated names in crash reports. Internals and the safety model are in
> [OBFUSCATION_TOOL_DESIGN.md](OBFUSCATION_TOOL_DESIGN.md).

## What it does

Sokol.NET can obfuscate your app's **managed IL before** the NativeAOT / WASM compiler runs, for all
targets (Desktop, Android, iOS, Web). Even with NativeAOT — which compiles IL to native code — the
binary still embeds reflection and stack-trace **name strings** and string **literals**; obfuscation
turns those into meaningless identifiers and hides the literals. Transforms:

- **Method renaming** — on by default; renames your app's methods to random identifiers.
- **String-literal encryption** — opt-in; hides string literals from `strings`/grep.
- **Type / field / property renaming** — opt-in, **higher risk** (see [Risks](#-risks--what-not-to-obfuscate)).

Only **your app's** code is touched. The Sokol.NET framework, interop entry points, P/Invoke and
`[UnmanagedCallersOnly]` callbacks are excluded automatically, so the app still starts and renders.

## Enabling it

Two things are required:

1. **Request obfuscation** — either
   - add `<Obfuscation>true</Obfuscation>` to your project's `Directory.Build.props` (recommended: a
     committed, per-project switch), **or**
   - pass `--obfuscate` to `SokolApplicationBuilder`.
2. **An `obfuscate.xml`** in the project folder — its presence supplies the config. If it is missing,
   the build proceeds **un-obfuscated** with a warning.

Obfuscation only runs on **release** builds, and only through `SokolApplicationBuilder`. A plain
`dotnet run` / `dotnet build` of the project (the JIT dev loop) is **never** obfuscated — which is
what you want while debugging.

### 1. `Directory.Build.props`

```xml
<Project>
  <PropertyGroup>
    <!-- Obfuscate release builds of this project (config comes from obfuscate.xml). -->
    <Obfuscation>true</Obfuscation>
  </PropertyGroup>
</Project>
```

Set it to `false` (or remove it) to turn obfuscation off again.

### 2. `obfuscate.xml` (the config)

Minimal — method renaming only:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Obfuscation>
  <Settings>
    <RenameMethods>true</RenameMethods>
  </Settings>
</Obfuscation>
```

Fuller example, with string encryption, the opt-in type/field/property pass, and some exclusions:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Obfuscation>
  <Settings>
    <RenameMethods>true</RenameMethods>
    <StringEncryption>on</StringEncryption>                        <!-- on | off -->
    <RenameTypesFieldsProperties>off</RenameTypesFieldsProperties> <!-- on | off (HIGHER RISK) -->
    <Seed>1234</Seed>            <!-- deterministic names; omit for random per build -->
    <EmitSymbolMap>true</EmitSymbolMap>
  </Settings>
  <Rules>
    <!-- Keep a namespace readable (e.g. code reached by reflection / serialization) -->
    <Exclude kind="namespace">MyGame.SaveData</Exclude>
    <!-- Keep a specific type and its members -->
    <Exclude kind="type">*.NetworkMessage</Exclude>
    <!-- Keep a string literal in plaintext (AdMob `ca-app-pub-*` ids are kept by default) -->
    <Exclude kind="string">https://api.example.com/*</Exclude>
  </Rules>
</Obfuscation>
```

Exclusion `kind` values: `namespace`, `type`, `method`, `file`, `folder` (patterns use `*`/`?`
globs), plus `string` / `string-regex` to keep specific string literals in plaintext. You can also
exclude a member in code with `[System.Reflection.Obfuscation(Exclude = true)]`.

## Building

With `<Obfuscation>true</Obfuscation>` set and `obfuscate.xml` present, your normal **release** build
commands obfuscate automatically:

```bash
# Desktop
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture desktop --type release --path examples/mygame

# Android
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture android --type release --path examples/mygame

# iOS  (use an ABSOLUTE --path; the team id owns the provisioning profile)
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture ios --type release \
  --path /Users/me/dev/examples/mygame --development-team <TEAM_ID>

# Web  (obfuscates the managed assembly shipped in _framework/ — the highest-value target)
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture web --path examples/mygame
```

If you prefer not to commit the switch, omit `<Obfuscation>` and add `--obfuscate` to the command
instead.

The build log prints the trigger and a summary, e.g.:

```
🔒 Obfuscation ENABLED (<Obfuscation> in Directory.Build.props) — config: .../obfuscate.xml
✅ Wrote 12 renamed method(s) + 3 type(s)/8 field(s)/2 properties + 40 encrypted string(s)
🗺️  Symbol map: obj/.../obfuscation.map.json
```

## ⚠️ Risks & what NOT to obfuscate

- **`RenameTypesFieldsProperties` breaks anything referenced BY NAME at runtime.** Renaming a
  property changes its `System.Text.Json` key; renaming a type breaks `Type.GetType("Ns.Type")` and
  `GetProperty("Name")`. If your app saves state, serializes to JSON by member name, or reflects over
  types, keep this pass **off** or `<Exclude>` the affected types/namespaces. Enum members and events
  are never renamed; virtual/interface members and interop are excluded automatically.
- **Always re-test on every target platform after enabling.** An app that works un-obfuscated can
  break when obfuscated if it depends on names at runtime — this is exactly the class of bug this
  experimental feature has not yet been exhaustively validated against.
- Keep the **symbol map** (`obj/.../obfuscation.map.json`, emitted when `EmitSymbolMap` is `true`) to
  translate obfuscated names back to originals in crash reports.

## Verifying it worked

In the native binary, reflection/stack-trace **names** are UTF-8 (so `strings` finds them) while
string **literals** are UTF-16. Check the built artifact
(`.so` / `.dylib` / iOS app binary / `_framework/*.wasm`):

```bash
# a renamed app class/method name should be GONE:
strings libmygame.so | grep -i MySecretClass       # (no output = it was renamed)

# the injected markers should be PRESENT in an obfuscated build:
strings libmygame.so | grep -iE '__Obfuscated__|__Strings__'
```

For string literals, grep with a UTF-16-LE search (`strings` alone won't see them):

```bash
python3 - <<'PY'
d = open("libmygame.so", "rb").read()
print("secret present:", b"my-secret-label".encode() if False else
      (d.count("my-secret-label".encode("utf-16-le")) > 0))
PY
```

## Reference

Design, the full safety model, and the complete config schema are in
[OBFUSCATION_TOOL_DESIGN.md](OBFUSCATION_TOOL_DESIGN.md).
