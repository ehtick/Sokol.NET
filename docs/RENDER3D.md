# Sokol.Render3D — Framework GPU 3D Renderer (Design + Implementation Plan)

> **Status:** DESIGN — ready for milestone implementation once approved.
> **Created:** 2026-06-19 · **Owner:** Sokol.NET
> **Resolves:** `docs/RENDER3D_PHYSICS3D_KICKOFF.md` §5 **D1/D2/D5** for the renderer workstream.
> **Siblings:** `docs/PHYSICS3D.md` (the deterministic 3D physics engine) · `docs/ARCADE_3D_GAMES.md`
> (Bowling 3D + Darts 3D, the two proving-ground games) · `docs/RENDER2D.md` (the 2D analogue this mirrors).

---

## 0. Decisions carried in (from the kickoff §5)

| # | Decision | Resolution |
|---|---|---|
| **D1** | Render3D origin | **Lean clean-room renderer**, mirroring `src/Render2D`. *Not* an extraction of the GameEditor RenderingServer (`src/Framework/Renderer/Server` — PBR/IBL/CSM/skinning/glTF, editor + ECS coupled). The arcade games need a tiny subset; a small renderer is cheaper to build and to verify on six platforms, and stays decoupled. RenderingServer remains the heavyweight path; Render3D may borrow individual pieces (e.g. the Render2D bloom post) later. |
| **D2** | Home / packaging | **Example-local first.** Build under `examples/<app>/Source/Render3D/` exactly as `Physics2D` lives in the example today, iterate against real devices, **then promote** to a standalone `src/Render3D/` (`Sokol.Render3D`, AOT, hosting the all-platform `sokol-shdc` targets — modelled on `src/Render2D/Render2D.csproj`). |
| **D5** | Sim ⇄ render coupling | **Standalone, not ECS.** The game reads each `Body3`'s position + orientation each frame and feeds a draw-list. No Frent/ECS dependency (the editor's path); mirrors how the 2D arcade views read `PhysicsWorld` bodies. |

---

## 1. Goals & non-goals

### Goals (MVP)
- A reusable GPU 3D **scene renderer** any the reference app game (and, post-promotion, any example) can consume — the 3D analogue of `Render2DSurface`.
- **Primitive mesh library** — UV sphere, box, capsule, cylinder, plane/quad, cone — built procedurally on the CPU and uploaded once. (Bowling = sphere ball + capsule pins + box lane/walls; Darts = disk board + cone darts.)
- A **perspective camera**, **one directional light** (Lambert + ambient + Blinn–Phong specular), and **one directional shadow map** (PCF).
- **Instanced draw** for repeated meshes (10 identical pins) + immediate per-mesh draw for one-offs.
- **Composites under the Sokol.GUI / NanoVG HUD** via the existing offscreen→blit "SG underlay" contract (Render2D Strategy C), so the app frame loop drives it with no new plumbing.
- **All six platforms** from one `sokol-shdc` compile (`glsl430:hlsl5:metal_macos:metal_ios:glsl300es`); NativeAOT + the WASM struct-by-value wrapper workaround.

### Non-goals (MVP — deferred, not redesigned away)
- No PBR / IBL / metallic-roughness, no glTF import, no skinning / morph targets, no KHR transmission, no CSM cascades (a single shadow map only). *(All of that already exists in RenderingServer if a future consumer needs it.)*
- No transparency depth-sort (opaque + simple cutout alpha only), no post/bloom (the Render2D bloom can be layered in later as an optional tier).
- No ECS, no scene-graph/asset-database, no material editor. The game owns the draw-list.

> **Acceptance bar (project norm):** success is *visual + per-platform*, not a unit test. Every milestone below names what must be **seen** and on **which targets** (macOS Metal + ≥1 mobile + WASM at minimum; the full fleet before "done").

---

## 2. Placement & naming

```
examples/<app>/Source/Render3D/        ← lives here FIRST (D2)
  Render3DSurface.cs        ← the workhorse (offscreen 3D pass + shadow pass + blit)
  IRender3DUnderlay.cs      ← compositing contract (or reuse the shared SG-underlay; see §5)
  Camera3D.cs               ← view+proj, orbit/look-at, screen→world ray (aim/picking)
  Light.cs                  ← single directional light + ambient
  Mesh3D.cs                 ← GPU buffers (pos/normal/uv + indices), immutable, cached
  Primitives.cs             ← Sphere/Box/Capsule/Cylinder/Plane/Cone CPU builders
  Material3D.cs             ← base color, optional albedo view, specular/shininess, emissive, flags
  Render3DScene.cs          ← per-frame draw-list (mesh, material, world mat, tint) [optional sugar]
  shaders/
    scene3d.glsl            ← lit + shadowed mesh shader
    shadow3d.glsl           ← depth-only from the light
    blit3d.glsl             ← fullscreen composite into the swapchain
    compiled/               ← committed *_shader.cs (all five slang blobs, no per-platform #if)
```

**Shader compile (example-local):** add `sokol-shdc` targets mirroring `src/Render2D/Render2D.csproj`
(one call per shader, `--slang glsl430:hlsl5:metal_macos:metal_ios:glsl300es --reflection -f sokol_csharp`,
umbrella `CompileShaders` target). House them in `examples/<app>/Source/Render3D/Render3D.targets`
imported by the example's `Directory.Build.props`, or inline in the example csproj.

**On promotion (D2):** `src/Render3D/Render3D.csproj` (`AssemblyName Sokol.Render3D`, `PublishAot`, references
`src/sokol/sokol.csproj` and the Sokol.GUI source glob) — a near-copy of `Render2D.csproj`; the shader targets
move into it unchanged.

---

## 3. The compositing contract (the crux — reuse, don't reinvent)

The app frame loop already drives an **SG underlay** for every Render2D game
(`examples/<app>/Source/<app>-app.cs:123-149`):

```
underlay = _current?.Underlay;          // IRender2DUnderlay? on the active screen
underlay?.RenderOffscreen(w, h);        // BEFORE the swapchain pass — its own sg_begin/end_pass
... begin swapchain pass ...
underlay?.Blit();                       // composite the offscreen result, BEFORE nvgBeginFrame
... NanoVG HUD draws on top ...
```

`IRender2DUnderlay` (`src/Render2D/Renderer/IRender2DUnderlay.cs`) is **backend-agnostic** — its two methods
(`RenderOffscreen(pxW,pxH)` + `Blit()`) say nothing 2D-specific; they just mean *"run my SG world in an
offscreen pass, then blit it under the HUD."* A 3D view satisfies the identical contract.

**Recommendation:** Bowling 3D / Darts 3D views **implement the existing `IRender2DUnderlay`** (or a trivially
renamed `ISgUnderlay` if we choose to introduce one in the example). **Zero changes to the app loop.** The
2D-centric name is a cosmetic debt to clear at promotion time — call out an `ISgUnderlay { RenderOffscreen; Blit }`
that both `IRender2DUnderlay` and `IRender3DUnderlay` extend when Render3D moves to `src/`. Documenting it here so
the rename is a known follow-up, not a surprise.

> The internal pass order **inside** `RenderOffscreen` differs from 2D: 3D first renders the **shadow map**
> (its own offscreen depth pass), then the **scene** into the MSAA color+depth target, then `End()` resolves.
> `Blit()` is unchanged in spirit — one fullscreen draw into the active swapchain pass.

---

## 4. Architecture

### 4.1 `Render3DSurface` — the workhorse (mirror of `Render2DSurface`)

Owns all GPU resources and the per-frame passes. Lifetime: created lazily on first `RenderOffscreen` (same as
`Render2DSurface`), `Dispose()` releases targets/pipelines/buffers.

Resources:
- **Scene target:** offscreen color (RGBA8) + depth (DEPTH) images, `Samples = GfxQuality.SceneSamples` (MSAA),
  plus a resolve image when MSAA > 1. Recreated on size change (cache last px size).
- **Shadow target:** one depth image (`ShadowMapSize`, default 2048, configurable down to 1024 for low-end).
- **Pipelines:** `_scenePip` (scene3d, depth-test+write, backface cull), `_shadowPip` (shadow3d, depth-only,
  front-face cull or depth bias to fight acne), `_blitPip` (blit3d, no depth, fullscreen triangle).
- **Buffers:** per-mesh vertex/index buffers (owned by `Mesh3D`, not the surface) + a dynamic **instance buffer**
  (per-instance `mat4 world` + `vec4 tint`) for `DrawInstanced`.
- **Uniform buffers:** scene VS (`viewProj`, `lightViewProj`), scene FS (`lightDir`, `lightColor`, `ambient`,
  `camPos`, material params), shadow VS (`lightViewProj`).

API (consumer-facing):

```csharp
var s = new Render3DSurface { Samples = GfxQuality.SceneSamples, ShadowMapSize = 2048 };

s.Begin(pxW, pxH, clearColor, in camera, in light);   // sizes targets, sets up both passes
s.Draw(mesh, in material, in worldMat, tint);          // one mesh (immediate)
s.DrawInstanced(mesh, in material, worldMats, tints);  // N identical meshes (the 10 pins)
s.End();                                               // shadow pass -> scene pass -> resolve

// ... later, inside the swapchain pass, before NanoVG ...
s.Blit();
```

Internally `Begin` records draws into two lists (shadow casters + scene items); `End` executes:
1. **Shadow pass** — bind `_shadowPip` + shadow depth target, draw every caster with `lightViewProj`.
2. **Scene pass** — bind `_scenePip` + MSAA color/depth, set scene uniforms (incl. the shadow map view +
   compare sampler), draw every item; resolve MSAA → resolve image.

`Blit` draws the resolve image fullscreen into the active swapchain pass.

### 4.2 `Camera3D`
- State: `Position`, `Target`, `Up`, `FovYDeg`, `Near`, `Far`, `Aspect`.
- `ViewProj` = `LookAt(pos,target,up) * Perspective(fov,aspect,near,far)` (right-handed, matches sokol/Metal NDC
  conventions used elsewhere — verify clip-space Z range per backend the same way RenderingServer does).
- Helpers the games need: `Orbit(yaw,pitch,dist,focus)` (Bowling chase cam), `ScreenRay(px, viewportPx)` →
  world ray (Darts board picking / aim projection).

### 4.3 `Mesh3D` + `Primitives`
- `Mesh3D` holds `sg_buffer` vbuf/ibuf + index count; built once from a CPU `MeshData { Vtx[] (pos,normal,uv); ushort[]/uint[] idx }`; immutable; the game caches one instance per shape and reuses it.
- `Primitives` (pure CPU generators, no determinism constraint — render-only):
  `Sphere(r, slices, stacks)`, `Box(half)`, `Capsule(r, halfHeight, slices, caps)`, `Cylinder(r, halfHeight, slices)`,
  `Plane(halfX, halfZ, [uvTile])`, `Cone(r, height, slices)`. Modest tessellation (low-end Android budget).
- Note: render meshes are **independent** of physics shapes. A pin's collider is a capsule (`Physics3D`); its mesh
  is a tessellated bowling-pin silhouette (or a capsule for MVP). The view maps `Body3.Position/Orientation` → a
  world matrix; mesh choice is cosmetic.

### 4.4 `Material3D`
Tiny by design: `BaseColor (vec4)`, `Albedo (sg_view?, optional)`, `SpecularStrength`, `Shininess`,
`Emissive (vec3)`, flags `Unlit`, `CastsShadow`, `ReceivesShadow`. No metallic/roughness/normal maps in MVP.

### 4.5 `Light`
One directional light: `Direction (normalized)`, `Color`, `Intensity`, `Ambient (vec3)`. The shadow ortho frustum
is fitted to a caller-supplied world AABB (the lane / the board) so the single map has tight texel density.
(Design leaves room for a small fixed light array later; MVP is one.)

---

## 5. Shaders — one source, six platforms

| Shader | VS | FS |
|---|---|---|
| `scene3d.glsl` | `world * pos` → world pos + world normal + uv + `lightViewProj * worldPos` (shadow coord); instanced variant reads `world`+`tint` from the instance stream | Lambert diffuse + ambient + Blinn–Phong spec, **shadow factor** via PCF (3×3) compare-sample of the shadow map, optional albedo texture, × vertex/instance tint |
| `shadow3d.glsl` | `lightViewProj * world * pos` | depth only (empty/clip) |
| `blit3d.glsl` | fullscreen triangle | sample resolve image, write straight through |

**Platform risks to verify (don't assume parity):**
- **Depth-texture compare sampling** (the shadow map) differs subtly across GLES3 / WebGL2 / D3D11 / Metal. Use a
  sokol depth pixel format + a comparison sampler; verify PCF works on **WebGL2 + Android GLES3** specifically
  (the usual failure surface). Fallback if a target misbehaves: render linear depth into a color target and compare
  manually (no compare sampler).
- **NDC depth range / handedness** — match what RenderingServer's shaders already do per backend; re-verify culling
  winding on D3D11 vs GL.
- **Unfilterable-float data textures** — if any (e.g. an instance-data texture path on GLES3), apply the project's
  known lesson (`unfilterable_float` + `nonfiltering` in sokol-shdc) to avoid Mali-G52 `sg_apply_bindings` panics.

---

## 6. Milestones (each with an explicit visual/platform acceptance)

> Numbered `R3D-Mx`. Built example-local (D2). "Fleet" = macOS Metal + Win D3D11 + Linux GL + Android GLES3 +
> iOS Metal + WASM WebGL2; each milestone must pass macOS + ≥1 mobile + WASM before moving on, full fleet before "done".

- **R3D-M0 — Scaffolding + compositing.** Source/Render3D skeleton, shader compile wired, `Render3DSurface` draws
  **one lit spinning cube** offscreen and blits it **under a NanoVG label**.
  *Accept:* cube renders + rotates with correct depth, HUD text on top, on macOS + 1 mobile + web.
- **R3D-M1 — Primitives + camera + directional light.** Sphere/box/capsule/cylinder/plane/cone builders; `Camera3D`
  framing; Lambert+ambient+spec.
  *Accept:* a **bowling lane (box) with 10 capsule pins + a sphere ball** renders correctly framed and shaded; an
  orbit camera reads well; fleet-verified.
- **R3D-M2 — Directional shadow map + PCF.** Single shadow map fitted to the lane AABB.
  *Accept:* pins + ball cast soft shadows on the lane; **explicitly confirmed on Android GLES3 + WASM WebGL2**
  (the depth-compare risk surface).
- **R3D-M3 — Instancing + materials/textures + background.** `DrawInstanced` for the 10 pins; albedo texture on the
  lane/board; gradient/skybox clear.
  *Accept:* 10 instanced pins + a textured lane at frame rate on **low-end Android**; no per-instance draw-call cliff.
- **R3D-M4 (later) — Promote to `src/Render3D`.** Move the component, introduce `ISgUnderlay`, optional Render2D
  bloom tier. *Accept:* both 3D games build against `Sokol.Render3D`; all examples still build.

---

## 7. Performance & platform notes

- **Body/draw counts are tiny** (~12 dynamic + a handful of static), so the renderer is fill/shadow bound, not
  draw-call bound — keep tessellation and shadow-map size modest; one light, one shadow map.
- **MSAA** via `GfxQuality.SceneSamples` (the project's existing quality knob); resolve pinned `sample_count=1` like
  the RenderingServer RT images.
- **WASM:** struct-by-value returns go through the auto-generated C wrappers
  (`docs/C-Internal-Wrappers-Auto-Generation.md`); no special action beyond building the bindings.
- **Particles/FX:** pin-hit sparks stay in the **2D** layer (`Render2D` `ParticleLayer`) composited in the HUD /
  a 2D underlay over the 3D scene — Render3D MVP has no GPU particles (no need to duplicate Render2D's system).

---

## 8. Open questions (carry into review, none block M0)

1. **Shadow technique fallback** — if GLES3/WebGL2 compare-sampling is fiddly, ship linear-depth-in-color + manual
   compare from the start? (Decide at R3D-M2 against real devices.)
2. **`ISgUnderlay` now or at promotion?** Introducing the shared interface immediately avoids the
   `IRender2DUnderlay`-named-but-3D oddity but touches `src/Render2D`. Recommend deferring to R3D-M4 (D2 is
   "example-local first"; minimize `src/` churn until proven).
3. **Pin mesh fidelity** — capsule mesh (trivial, matches the collider) for MVP vs a proper pin silhouette mesh.
   Recommend capsule at M1, swap to a pin profile at R3D-M3 (cosmetic, no sim impact).

---

## 9. References (precedents in this repo — patterns mirrored, no code copied)

- `src/Render2D/` — the structural template (csproj, shader targets, `Render2DSurface`, `IRender2DUnderlay`).
- `docs/RENDER2D.md` §5 (Strategy C compositing) — the underlay model reused verbatim.
- `src/Framework/Renderer/Server/` (RenderingServer) — the heavyweight 3D renderer NOT used here; reference for
  backend-specific shadow/NDC/depth handling and the offscreen→blit-under-NanoVG precedent.
- `examples/<app>/Source/Arcade/PrismRush/Rendering/` (`ISceneRenderer`/`SgSceneRenderer`) — a tiny
  precedent for an SG render path behind an interface.
- `examples/<app>/Source/<app>-app.cs:123-149` — the live underlay drive loop.
