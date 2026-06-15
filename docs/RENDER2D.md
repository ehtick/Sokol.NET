# Sokol.Render2D — Framework GPU 2D Renderer + GPU Particle System (Design)

> A reusable **GPU 2D renderer** (`Sokol.SG` batched/instanced quads + triangles) and **GPU particle
> system** (instanced, tens of thousands of particles, additive/glow) shipped as a **Sokol.NET
> framework component** — not app-local. It is the optional high-throughput backend behind the
> *same* `ISceneRenderer` / `IParticleRenderer` seams that already drive the default NanoVG path, so
> any example can opt into it with **zero gameplay changes**. **Prism Rush** (JamboreeArcade) is the
> first consumer and the proving ground — its optional "M5" SG path, promoted here from app-local to a
> framework unit so every Arcade game (and future framework users) can reuse it.
>
> This is the **blueprint** — placement, scope, API, the GPU particle/scene paths, the
> **compositing** strategy (the real risk), the all-platform shader plan, and milestones. **No
> production code is written by this document.** It builds on two already-signed-off specs — keep them
> open: `examples/JamboreeArcade/docs/PARTICLE_SYSTEM.md` **§7.4** (the `SgParticleRenderer` sketch)
> and `examples/JamboreeArcade/docs/PRISM_RUSH.md` **§8** (`GuiSceneRenderer` vs `SgSceneRenderer`).
> Read **§5 (Compositing)** before writing a line — it is where this design earns its keep.

---

## 0. Status & how this fits the existing decisions

- PARTICLE_SYSTEM.md §7.3/§14.1 and PRISM_RUSH.md §8.3/§15.2 both **locked NanoVG as the default** and
  the SG backend as **optional, built only when a profile demands it or as the flagship showcase**.
  This doc does **not** revisit that. It specifies the SG backend itself and **promotes** it to the
  framework so the work is done once and shared, rather than copied per game.
- The simulation (particles) and the scene/gameplay code are **already backend-independent** behind
  `IParticleRenderer` and `ISceneRenderer`. This design adds a *second implementation* of each. The
  default NanoVG backends stay exactly as they are.
- Cross-platform shader surface is the headline cost the prior specs flagged ("shaders × 6"). §7
  handles it with the proven all-platform `sokol-shdc` harness already used by `src/Framework`.

---

## 1. Goals & non-goals

**Goals**
- A **GPU-instanced 2D particle renderer**: one instanced-quad pipeline, a per-particle instance
  stream `{pos, size, rot, rgba, uvRect}`, **one draw call per blend/texture batch**, comfortably
  **tens of thousands** of particles on a mid-tier GPU.
- **Full per-particle `vertexColor × texture`** — lifts NanoVG's documented alpha-only tint limit, so
  textured particles colour-tint over life (the "juice" the showcase wants).
- A **GPU 2D scene renderer**: batched filled quads / rounded quads / triangles / circles / lines /
  gradients for the obstacle scene + avatar + parallax, in a handful of draws.
- **Additive blending** (fire/glow/thrust) as a first-class, free feature (pipeline blend state); an
  **optional bloom/post tier** behind a flag for capable devices.
- **Drop-in behind the existing interfaces** — `SgSceneRenderer : ISceneRenderer` and
  `SgParticleRenderer : IParticleRenderer`. Gameplay/sim code is untouched.
- **All six targets from one shader source** (D3D11 / Metal×2 / GL / GLES3 / WebGL2) via a single
  `sokol-shdc` invocation per shader — **no per-platform `#if`** in the generated `.cs`.
- **Framework-level**, lean: depends only on `src/sokol` + `src/GUI`, consumable the same way examples
  already consume `src/GUI` (a source glob), with **no native-lib rebuild** required.

**Non-goals**
- **Not a replacement for NanoVG.** NanoVG stays the default; this is the opt-in high-count path. The
  HUD, chrome, text, and most UI keep rendering through `Sokol.GUI.Renderer`.
- **No 3D.** That is `src/Framework`'s `RenderingServer`. This is strictly 2D.
- **No new simulation.** The particle sim, presets, affectors, and texture cache are reused as-is
  (moved, not rewritten — §3).
- **No SDF text / font atlas of its own (v1).** Text stays NanoVG (composited on top). A future SDF
  text path is out of scope here.
- **No determinism / networking.** Particles remain cosmetic & client-local (PARTICLE_SYSTEM.md §3);
  the SG backend changes nothing about that.

---

## 2. Placement & naming — **new lean sibling `src/Render2D/`** (recommended)

**Finding that drives this:** examples don't `ProjectReference` the framework DLL — JamboreeArcade
pulls framework code in as **source globs** (`src/GUI/**/*.cs`, `src/sokol/*.cs`) in its
`Directory.Build.props`. The existing `src/Framework` assembly (`GameEditor.Framework`) also drags in
**JoltPhysics, Frent ECS, and the whole 3D `RenderingServer`** — a heavy, wrong dependency for a 2D
party-game app.

**Recommendation:** a new sibling **`src/Render2D/`**, assembly **`Sokol.Render2D`**, namespace
**`Sokol.Render2D`** (consistent with `Sokol.GUI`, `Sokol.NearNet`), depending **only** on
`src/sokol` + `src/GUI`. It is:
- a real `Render2D.csproj` (Library, `PublishAot`) that **owns the all-platform shader-compile MSBuild
  targets** (copied from `Framework.csproj`'s proven pattern — §7) and can build/verify standalone; and
- a **source tree apps glob in** exactly like `src/GUI` — e.g. JamboreeArcade adds
  `<Compile Include="../../src/Render2D/**/*.cs">` to its `Directory.Build.props`. The committed,
  all-platform generated shader `.cs` files come along in the glob, so a consumer needs **no shader
  build of its own** for these shaders.

| Option | Dependency weight | Shader harness | Verdict |
|---|---|---|---|
| **`src/Render2D/` sibling** (rec.) | sokol + GUI only | its own (copied from Framework) | **Lean, matches how GUI is consumed** |
| Inside `src/Framework/Renderer/Render2D/` | pulls in Jolt + Frent + 3D server | reuse Framework's | Heavy for 2D consumers; reuses harness but couples to the editor stack |

> **Decision to confirm (D1).** Sibling `src/Render2D/` (recommended) vs. folding into `src/Framework`.
> The sibling is the lighter, cleaner home given the source-glob consumption model; the only "cost" is
> duplicating the ~10-line MSBuild shader-target block, which is self-contained and already templated.

---

## 3. Scope — what gets promoted to the framework

The reusable unit is **the whole particle module + the scene-renderer seam**, so every example shares
one implementation rather than the renderer consuming an app-local sim.

**Move into `src/Render2D/` (backend-independent, no logic change):**
- Particle sim & data: `Particle`, `EmitterConfig` (+ enums), `Emitter`, `ParticleSystem`,
  `Affectors`, `ParticlePresets`, `ParticleTextureCache`, `ParticleLayer`.
- Renderer seam: `IParticleRenderer`, `GuiParticleRenderer` (default, unchanged), **`SgParticleRenderer`** (new).
- Scene seam: a generalized **`IRender2D`** scene interface + **`GuiSceneRenderer`** (NanoVG) +
  **`SgSceneRenderer`** (new). Today's `JamboreeArcade.PrismRush.Rendering.ISceneRenderer` becomes a
  thin adapter over (or is migrated to) the framework `IRender2D`.

**Stays app-side (gameplay-specific):**
- Prism Rush's `Camera`, `PrismWorld`, `Physics2D`, level data, HUD. The gameplay `Camera` *produces*
  the ortho projection + world→screen the framework renderer consumes; it does not move.
- `Aabb` / `Vector2` math: the framework renderer takes `System.Numerics.Vector2` + a projection; the
  PrismRush `Aabb` stays in `Physics2D` and is converted at the call boundary.

**Refactor plan (surgical):**
1. `git mv` the particle files `examples/JamboreeArcade/Source/Particles/**` → `src/Render2D/Particles/**`,
   change namespace `JamboreeArcade.Particles` → `Sokol.Render2D.Particles`.
2. Repoint JamboreeArcade: drop the old `Source/Particles` from its compile set, add the
   `src/Render2D/**` glob, fix `using` namespaces (mechanical).
3. Generalize `ISceneRenderer` → `Sokol.Render2D.IRender2D` (screen-space primitives + a `Camera2D`
   seam); keep a `PrismRush` adapter so `PrismRushView` is untouched beyond the `using`.
4. Verify JamboreeArcade still builds & runs **identically on NanoVG** (no behaviour change) **before**
   adding any SG code. This is the de-risking checkpoint: promotion must be a no-op for the default path.

> **Decision to confirm (D2).** Promote the **whole** particle module (recommended — one shared
> implementation) vs. keep the sim app-local and promote only the renderer interfaces + SG backend.
> Recommendation: promote the whole module; it's already clean and app-independent.

---

## 4. Architecture overview

```
 Game / Prism Rush  (unchanged)
   World.Step(dt) ──────────────► ParticleLayer  (CPU sim: emit, integrate, affectors)   ← Sokol.Render2D
        │  events                      │  live set = ReadOnlySpan<Particle>
        ▼                              ▼
   IRender2D (scene primitives)   IParticleRenderer (draw the live set)
        │                              │
   ┌────┴───────────┐          ┌───────┴────────────┐
   │ GuiSceneRenderer│         │ GuiParticleRenderer │   DEFAULT (NanoVG, in the GUI pass)
   │ SgSceneRenderer │         │ SgParticleRenderer  │   OPTIONAL (Sokol.SG, in the UNDERLAY pass)
   └────────┬───────┘          └───────┬────────────┘
            └───────────┬──────────────┘
                        ▼
            Render2DSurface  ── owns the SG pipelines, dynamic buffers, ortho camera, atlas,
                                and the compositing contract (§5). One shared pass for scene+particles.
```

Two seams stay exactly where they are; the new backends slot in under them. The **only structural
difference** between the two backends is *which pass they draw in* (§5): the NanoVG backends draw
inside the app's GUI pass during `Widget.Draw`; the SG backends draw in a separate **underlay** the
frame composites *beneath* the NanoVG HUD/chrome.

---

## 5. Compositing — the crux

### 5.1 The constraint

The JamboreeArcade frame (`JamboreeArcade-app.cs`) draws **everything in one swapchain pass**:

```
sg_begin_pass(swapchain, CLEAR)
    _gui.Draw(w,h,dpi)   →   nvgBeginFrame …walk widget tree… nvgEndFrame
sg_end_pass(); sg_commit();
```

`Widget.Draw(Renderer r)` runs **inside an open NanoVG frame**. NanoVG (sokol backend) records its
draw list and **flushes it at `nvgEndFrame`** with its own pipelines/bindings. Therefore:

- **You cannot interleave raw `sg_*` draws mid-NanoVG-frame.** Any SG geometry must be issued either
  *before* `nvgBeginFrame` or in a *separate pass*.
- **NanoVG cannot sample an external `sg_image`.** Confirmed in `ext/nanovg/src/sokol_nanovg.h`: the
  backend allocates its own textures (`snvg__renderCreateTexture` → `sg_make_image`) and exposes **no**
  "wrap this `sg_image` as an `NVGimage`" entry point. So "render SG to an offscreen RT, then draw it as
  a NanoVG image under the HUD" would require **adding a C binding to `sokol_nanovg.h` + regenerating
  bindings + rebuilding the native `sokol` lib on all six platforms** — the heavy `ext/` path the
  project avoids.

### 5.2 Strategies

| | How | Native rebuild? | MSAA risk | Precedent |
|---|---|---|---|---|
| **C — offscreen RT + SG blit underlay (recommended)** | SG renders {bg+scene+particles} to an offscreen RT during `Tick`; at the **top of the existing swapchain pass**, a tiny SG fullscreen-blit copies it in; then `_gui.Draw` paints the NanoVG HUD on top (same pass). | **No** | **None** (one pass → one resolve) | Closest to **GameEditor** (offscreen 3D → composite in the swapchain pass), minus ImGui's image widget |
| **B — two swapchain passes** | Pass 1 (swapchain, CLEAR): SG scene+particles. Pass 2 (swapchain, **LOAD**): NanoVG HUD/chrome on top. | No | **Yes — verify** MSAA `sample_count=4` swapchain LOAD across GLES3/WebGL2 tile GPUs | sokol "3D then UI" idiom |
| **A — offscreen RT → NanoVG image** | SG → offscreen RT; draw it as a NanoVG image in `Widget.Draw`, HUD on top. | **Yes** (`sokol_nanovg.h` add + regen + 6× lib rebuild) | None | — |

### 5.3 Recommendation — **Strategy C**

Render the SG underlay (background gradient + parallax + scene geometry + particles) to a **single
offscreen render target** during the screen's `Tick()` (its own `sg_begin_pass`/`sg_end_pass`, before
the swapchain pass). Then, **inside the app's existing swapchain pass, before `_gui.Draw`**, issue one
fullscreen textured-quad blit of that RT into the swapchain. `_gui.Draw` then renders the NanoVG
HUD/chrome on top (the pass already holds the scene; NanoVG draws only where the HUD is). One swapchain
pass → **one MSAA resolve**, no external-image binding, no native rebuild — and it mirrors the proven
GameEditor offscreen-then-composite pattern.

- **Ordering is safe.** The blit is a single self-contained SG draw (pipeline + bindings + 1 draw)
  completed *before* `nvgBeginFrame`. NanoVG's flush sets all of its own state from scratch, so the two
  don't depend on each other's pipeline state — the same way sokol_imgui lets you draw your own SG
  content then `simgui_render()` in one pass. The blit uses no depth/stencil writes, so NanoVG's
  stencil strokes are unaffected.
- **App-shell hook (generic, small).** Add an optional seam to `IAppScreen`, e.g.
  `IRender2DUnderlay? Underlay => null;`. Each frame, after `_gui.Update` and **before**
  `sg_begin_pass(swapchain)`, the shell calls `Underlay?.RenderOffscreen(w,h,dpi)` (the SG offscreen
  pass); then, just inside the swapchain pass, `Underlay?.Blit()`. Screens without an underlay (the
  whole catalog, every NanoVG game) return `null` → today's path, byte-for-byte unchanged.
- **Antialiasing of the SG scene.** v1: a **single-sample** offscreen RT (like `Framework`'s
  `OffscreenTarget`) plus **1-px edge feather in the shaders** (SDF-ish AA for circles/quad edges) —
  cheap and crisp enough for the neon look. If edges read jaggy on a target, upgrade the offscreen RT
  to MSAA + resolve (more setup, isolated to `Render2DSurface`). NanoVG HUD text stays crisp regardless.

> **Decision to confirm (D3).** Compositing strategy: **C (offscreen + blit, recommended)** vs **B
> (two-pass swapchain — simpler data flow but unverified MSAA LOAD on tile GPUs)** vs **A (NanoVG
> external-image binding — needs an `ext/` change + 6-platform native rebuild)**. Recommendation: C.
> The MSAA-LOAD question is the single biggest unknown in B; C sidesteps it entirely.

### 5.4 Which pass each backend draws in

- **NanoVG backends** (`GuiSceneRenderer`, `GuiParticleRenderer`): unchanged — drawn during
  `PrismRushView.Draw(Renderer r)`, inside the GUI pass.
- **SG backends** (`SgSceneRenderer`, `SgParticleRenderer`): drawn during the underlay offscreen pass.
  So `PrismRushView` calls scene+particle rendering **into whichever pass the active backend owns**.
  The view already separates "draw scene/particles" from "draw HUD"; the HUD always stays NanoVG.

---

## 6. The SG renderers

### 6.1 `Render2DSurface` (shared core)

Owns everything stateful so the two SG renderers stay thin:
- the **offscreen RT** (recreated on resize), the **blit pipeline**, the **ortho projection** (from the
  gameplay `Camera`: world units → clip space, Y-up, logical-px aware via `UiMetrics`),
- the **texture atlas** (`ParticleTextureCache` images packed into one atlas image so a whole particle
  batch is one draw; falls back to per-texture batches if an asset isn't atlased),
- the **dynamic vertex/instance buffers** (`stream_update`, sized to a pool cap; grown by recreate, not
  per-frame alloc), and the **scene + particle pipelines** (§6.2/§6.3).

### 6.2 `SgParticleRenderer : IParticleRenderer` — delivers PARTICLE_SYSTEM.md §7.4

One **instanced-quad** pipeline (mirrors `examples/instancing`):
- `vertex_buffers[0]` = a static unit quad (4 verts `{pos.xy, uv}`) + a 6-index buffer.
- `vertex_buffers[1]` = the **per-instance stream**: `{ Vector2 pos; float size; float rot; uint rgba;
  Vector4 uvRect; }` (≈ 32 B), `step_func = SG_VERTEXSTEP_PER_INSTANCE`.
- Two **blend pipelines** sharing the shader: **Additive** (`SRC_ALPHA, ONE`) and **Normal**
  (`SRC_ALPHA, ONE_MINUS_SRC_ALPHA`). No depth test/write (painter's order = emit order within a batch).
- Vertex shader: `clip = proj * vec4(quadPos*size rotated by rot + pos, 0, 1)`; pass `uv` (mapped into
  `uvRect`) and `rgba` to the fragment. Fragment: `frag = texture(atlas, uv) * vertexColor` with a 1-px
  edge feather for untextured glow/disc visuals (so circles are smooth without MSAA).

**Driving model fits the existing interface as-is.** A `ParticleSystem` brackets its homogeneous live
set with `Begin(cfg)…Draw(p)…End`. `SgParticleRenderer`:
- `Begin(cfg)` → select blend pipeline + atlas sub-rect for `cfg`, start appending to the instance buffer;
- `Draw(in p)` → append one instance `{p.Pos, sizeFromAgeLerp, p.Rotation, packRgba(colorFromAgeLerp),
  uvRectForFrame}` (no per-particle draw call);
- `End()` → `sg_update_buffer(instanceVbuf, batchRange)` + `sg_apply_pipeline` + `sg_apply_bindings`
  + **one** `sg_draw(0, 6, batchCount)`.

So **one draw call per (blend × texture) batch**; with a single atlas and additive-dominant effects
that's typically **1–3 draws for the entire particle field**, tens of thousands of instances.

### 6.3 `SgSceneRenderer : IRender2D` — the batched scene

The scene primitives (`Quad`/`RotatedSquare`/`Tri`/`Circle`/`Line`/gradient) are CPU-tessellated into a
**single dynamic coloured-triangle vertex buffer** (`{pos.xy, rgba}` or `{pos, uv, rgba}` when
textured) and drawn with **one alpha-blend pipeline** (a couple of draws if a texture/gradient batch
splits). Tessellation: quad→2 tris, rounded-quad→corner fans, circle→N-gon fan (N from radius), tri→1
tri, line→quad. Vector crispness comes from the 1-px shader feather (and the optional MSAA RT); an
SDF/analytic-AA path is a documented later optimization, not v1. The parallax + background gradient are
issued through the same renderer so the **entire** visual is in the offscreen RT and the NanoVG layer is
HUD-only (§5.3).

### 6.4 GPU post / bloom (optional tier)

Additive blend is core and free. **Bloom** = a bright-pass + separable Gaussian blur (½/¼-res ping-pong
RTs) + additive composite over the scene RT before the blit. Recommend: ship additive in the first SG
milestone; add bloom **behind a quality flag**, enabled only on capable tiers (off by default on
GLES3/WebGL2 — extra passes + fill cost on Mali-class GPUs). Chromatic aberration / vignette are
trivial follow-ons in the same composite shader if wanted.

> **Decision to confirm (D4).** Bloom in scope for the initial framework build, or a later tier?
> Recommendation: additive now; bloom as a flagged follow-on milestone (§9 M4).

---

## 7. Shaders — one source, all six platforms

Copy `src/Framework/Framework.csproj`'s harness into `Render2D.csproj` (the model the project already
ships): hand-written `.glsl` in `Render2D/shaders/`, generated `.cs` committed in
`Render2D/shaders/compiled/`, regenerated by MSBuild `Target`s with `Inputs`/`Outputs` (incremental) and
`DependsOnTargets="EnsureRender2DShaderOutDir"`. Each shader is **one** `sokol-shdc` call:

```
sokol-shdc --input shaders/particle.glsl --output shaders/compiled/particle_shader.cs \
           --module particle --slang "glsl430:hlsl5:metal_macos:metal_ios:glsl300es" \
           --reflection -f sokol_csharp
```

One generated `.cs` per shader holds **all five slang blobs**; sokol selects at runtime, so **no
per-platform `#if`** and consumers that glob the source get every platform for free. Shaders for v1:
`particle.glsl` (instanced), `scene.glsl` (coloured/ textured triangles), `blit.glsl` (fullscreen RT
composite); add `bloom_bright.glsl` + `bloom_blur.glsl` only if D4 says bloom is in.

**GLES3 data-texture rule** ([[reference_gles3_unfilterable_float_textures]]): if any path ever samples a
float data texture via `texelFetch` (e.g. a future GPU-sim buffer), it must be declared
`unfilterable_float` + `nonfiltering` or Mali-G52/low-tier GLES3 panics in `sg_apply_bindings`. The v1
instance-stream path uses a normal vertex buffer (not a data texture), so this only matters if a GPU
particle *simulation* is added later (explicitly out of scope — sim stays CPU).

---

## 8. API surface (consumer-facing)

```csharp
namespace Sokol.Render2D;

// Already-existing seams (promoted, unchanged behaviour on NanoVG):
public interface IParticleRenderer { void Begin(Renderer r, EmitterConfig cfg);
                                     void Draw(Renderer r, in Particle p, EmitterConfig cfg);
                                     void End(Renderer r); }
public interface IRender2D { /* Quad/RotatedSquare/Tri/Circle/Line/Gradient in world space + a Camera2D */ }

// New: the SG surface a screen owns when it opts into the GPU path.
public sealed class Render2DSurface : IDisposable {
    public void Resize(int pxW, int pxH);
    public SgSceneRenderer    Scene    { get; }   // : IRender2D
    public SgParticleRenderer Particles{ get; }   // : IParticleRenderer
    public void BeginUnderlay(in Matrix ortho);   // sg_begin_pass(offscreen) + set proj
    public void EndUnderlay();                     // sg_end_pass
    public void Blit();                            // fullscreen composite into the current pass
}

// Generic shell seam (one optional property on the app's screen interface):
public interface IRender2DUnderlay {
    void RenderOffscreen(float w, float h, float dpi); // builds the offscreen RT for this frame
    void Blit();                                       // composite it at the top of the swapchain pass
}
```

A consumer (Prism Rush) constructs a `Render2DSurface`, draws its scene+particles through
`Surface.Scene` / `Surface.Particles` in `RenderOffscreen`, returns the surface as its `IAppScreen.Underlay`,
and keeps drawing the **HUD** through the NanoVG `Renderer` as today. Selecting the backend is a single
toggle (NanoVG default; SG when opted in) — gameplay code never names sokol_gfx.

---

## 9. Milestones (framework-order, each verified on all six targets)

Per [[feedback_rendering_server_milestone_order]] discipline — fixed order, verify each before the next.

- **M0 — Promote & no-op.** Move the particle module + scene seam into `src/Render2D/` (§3),
  generalize `IRender2D`, repoint JamboreeArcade. **Verify:** JamboreeArcade builds and Prism Rush +
  every particle effect look **identical on NanoVG** on macOS + one device. *No SG code yet.* (This is
  the safety checkpoint: promotion must change nothing.)
- **M1 — Compositing skeleton.** `Render2DSurface` + offscreen RT + blit pipeline + the `IAppScreen.Underlay`
  shell hook + `blit.glsl`. Prism Rush renders its **background+scene only** via `SgSceneRenderer` under
  the NanoVG HUD. **Verify:** scene matches the NanoVG look; HUD composites on top; **MSAA/clear/resize
  correct on all six targets** (this proves the §5 strategy on real GLES3/WebGL2/Metal/D3D11).
- **M2 — GPU particles.** `SgParticleRenderer` + `particle.glsl` (instanced, additive + normal,
  atlas, `vertexColor × texture`). Route Prism Rush's particles through it. **Verify:** all presets
  render with full colour-over-life on textures; **tens of thousands** of instances at 60 fps on
  desktop and a stress count holds on the device; bounded memory, no per-frame GC.
- **M3 — Scene completeness + crispness.** Full `IRender2D` primitive set batched (rounded quads,
  triangles, circles, lines, gradients) + shader edge-feather AA. **Verify:** Prism Rush is visually at
  parity with (or better than) the NanoVG scene on all targets; the "max particles" showcase mode.
- **M4 — (optional) Bloom/post tier.** Bright-pass + blur + composite behind a quality flag, off by
  default on low tiers (D4). **Verify:** glow reads on capable devices; no regression / acceptable cost
  on Mali-G52; flag cleanly disables it.
- **M5 — Second consumer.** Adopt the SG path in one more Arcade game that wants heavy particles (e.g.
  Cannon Battle blasts) to prove reuse beyond Prism Rush. **Verify:** drop-in via the same seams, no
  gameplay change.

Acceptance bar throughout (project standard): **looks/plays right + stable fps + bounded memory**,
visually verified on macOS + at least one real device per milestone; M2/M3 additionally checked on a
low-end Android (Mali-G52) for the particle budget.

---

## 10. Performance & platform notes

- **Mali-G52 / low-tier GLES3:** additive overdraw is the limiter, not instance count — bound the
  screen-fill of bright sprites; prefer the atlas (one draw) over many texture switches; keep bloom off
  by default here. Pool caps from PARTICLE_SYSTEM.md §12 still apply.
- **WebGL2:** identical generated shaders; respect `#if WEB` UI-scale handling for the HUD
  ([[feedback_web_ui_scaling]]); the offscreen RT must be sized in **physical** px (retina/`dpi`).
- **DPI:** the ortho projection and all sizes go through `UiMetrics` ([[feedback_android_dpi_scaling]]);
  the offscreen RT is allocated at physical resolution, the blit maps it 1:1.
- **No threads.** Sim + instance-buffer fill on the frame thread; one `sg_update_buffer` per batch.
- **Resize/teardown.** `Render2DSurface.Resize` recreates the RT + atlas views; `Dispose` frees
  buffers/images/pipelines; finished particle systems self-retire (sim unchanged).

---

## 11. Decisions (signed off 2026-06-15)

- **D1 — Placement:** ✅ **new lean sibling `src/Render2D/`** (`Sokol.Render2D`, deps = sokol + GUI),
  consumed as a source glob like `src/GUI`. Not inside `src/Framework`.
- **D2 — Scope:** ✅ **promote the whole particle module** + the scene seam; M0 proves the NanoVG path
  is byte-identical before any SG code.
- **D3 — Compositing:** ✅ **Strategy C** — offscreen RT + a fullscreen SG blit at the top of the
  existing swapchain pass, NanoVG HUD on top. No native rebuild; one MSAA resolve. (§5)
- **D4 — Bloom:** ✅ **additive blending first** (core, free); bloom is a later flagged tier (M4), off
  by default on low-end GLES3/WebGL2.
- **D5 — Built-in sprites ship inside the assembly (baked-as-source):** ✅ the default particle textures
  (`particles/{smoke.png,flame.svg,blast_sheet.png}`) are baked to base64 in a generated `.cs`
  (`Render2DEmbeddedAssets`, regen via `scripts/gen-render2d-assets.py` from
  `src/Render2D/Assets/particles/`). Because consumers **source-glob** `src/Render2D/**/*.cs` (D1) rather
  than reference a built DLL, an `<EmbeddedResource>` would never reach them — but a baked `.cs` rides the
  same glob and compiles into every consumer on all six targets (NativeAOT/WASM) with **no asset-folder
  copy and no per-platform bundling**. **Priority = app-file-first, baked-in fallback:** both texture
  caches load via the async `SFilesystem` fetcher and, only if the app ships no file at that path, fall
  back to `Render2DEmbeddedAssets.TryGet` (decode in the fetch callback = frame boundary, outside any sg
  pass). So a consumer can **override/extend** the presets by shipping its own asset at the same path,
  while an app that ships **no** asset folder still gets the built-in sprites. Verified two ways on
  desktop: (a) deleting the app's `Assets/particles/` entirely → gallery still renders from the baked-in
  set; (b) restoring them → app loads its own copies again. JamboreeArcade keeps its own
  `Assets/particles/`; the baked-in set exists for future consumers.
- **Biggest technical unknown (retire early):** the Strategy-C compositing must be proven on **GLES3 +
  WebGL2** in **M1** — that's where Metal/desktop assumptions silently break (CLAUDE.md). M1 exists
  precisely to retire that risk before any particle work.

---

## 12. References (concepts/precedents in this repo — no code copied)

- `examples/JamboreeArcade/docs/PARTICLE_SYSTEM.md` §7.3/§7.4 — the sim/renderer split + the
  `SgParticleRenderer` sketch this realizes.
- `examples/JamboreeArcade/docs/PRISM_RUSH.md` §8 — `GuiSceneRenderer` vs `SgSceneRenderer` tradeoffs +
  the M5 decision this promotes.
- `examples/GameEditor` `GameEditor-app.cs` + `RenderingServer` — the offscreen-then-composite
  precedent for running SG under the GUI in one frame.
- `examples/instancing` — the instanced-quad pipeline + dynamic per-instance stream buffer pattern.
- `src/Framework/Framework.csproj` — the all-platform `sokol-shdc` shader-compile harness to copy.
```
