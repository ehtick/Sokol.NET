# Sokol.Render3D + Deterministic 3D Physics — Planning Kickoff

> **Status:** ✅ PLANNED (2026-06-19) — this brief has been turned into design docs. Decisions §5 D1–D6 resolved.
> **Created:** 2026-06-19 · **Owner:** Sokol.NET
> **Goal of the fresh session:** turn this brief into (1) a design doc and (2) a milestone plan, after
> resolving the open decisions in §5. **Do not start coding until those design docs are approved.**
>
> ### → Design docs produced from this brief
> - **`docs/RENDER3D.md`** — `Sokol.Render3D` renderer design + milestones.
> - **`docs/PHYSICS3D.md`** — deterministic 3D physics design + milestones + `DeterminismProbe3D` plan.
> - **`docs/ARCADE_3D_GAMES.md`** — Bowling 3D + Darts 3D (the two new proving-ground games).
>
> ### §5 decisions resolved
> - **D1** Render3D origin → **lean clean-room** (mirror `src/Render2D`), not a RenderingServer extraction.
> - **D2** Home/packaging → **example-local first** (`examples/JamboreeArcade/Source/{Render3D,Physics3D}/`, like
>   `Physics2D`), promote to `src/Render3D` + `src/Physics3D` once device-proven.
> - **D3** Physics MVP → **full deterministic rigid-body** (sphere+capsule dynamic, box/plane static, real angular
>   toppling), with simplified-pin + active-auth fallbacks documented (`PHYSICS3D.md §9`).
> - **D4** Determinism → **yes, from day 1** (BLE input-replay; `DeterminismProbe3D` golden gates netplay).
> - **D5** Sim ⇄ render → **standalone, not ECS** (game reads `Body3` pos/orientation into a draw-list).
> - **D6** First proving ground → **two NEW games, Bowling 3D + Darts 3D**, alongside the existing 2D ones.

---

## 0. How to use this document

This is the entry point. The fresh session should:
1. Read this brief, then read the **prior art** in §3 (especially the RenderingServer plan and the 2D
   `Physics2D` + `Det.cs` engine) — they are the templates to mirror.
2. Resolve the **Open Decisions** in §5 *with the user* — they gate the whole architecture.
3. Produce the **deliverables** in §6 (a `RENDER3D.md` design + a `PHYSICS3D.md` design + milestones),
   modelled on the existing `RENDERING_SERVER_IMPLEMENTATION_PLAN.md` structure.

---

## 1. Goal & motivation

Build **two reusable Sokol.NET framework components**, the 3D analogues of the existing 2D stack:

- **`Sokol.Render3D`** — a reusable GPU 3D scene renderer (meshes, materials, lights, cameras, shadows)
  any app/example can consume. The 3D counterpart of `Sokol.Render2D`.
- **A custom *deterministic* 3D rigid-body physics engine** — bit-identical results across every platform,
  the 3D counterpart of `examples/JamboreeArcade/Source/Physics2D/` + `Det.cs`.

**Why custom & deterministic (not Jolt):** the repo already ships Jolt (`src/JoltPhysicsSharp`,
`ext/JoltPhysics`, `examples/JoltPhysics*`), but Jolt is **not** bit-deterministic across architectures.
The networked game model used by the arcade games (input-replay / lockstep, design §4.5d) re-simulates a
turn on *every* device from a compact input packet, so the sim must be byte-identical on
arm64 / x64 / wasm and across Metal/GLES/WebGL/D3D. **2D already proved this works** — the
`DeterminismProbe` golden hashed identically across the whole device fleet. The 3D engine extends that
property into rigid-body dynamics. Jolt stays as the *non-deterministic* fallback / editor-physics path.

---

## 2. The two workstreams

### 2a. `Sokol.Render3D` (GPU 3D renderer)
- **Rough scope** (refine in planning): scene/draw-list, camera, mesh+material, lighting, shadows, and the
  all-platform shader pipeline. MVP vs full feature set = a milestone decision.
- **Major prior art:** the GameEditor **RenderingServer is already a near-complete 3D renderer** — PBR,
  glTF import, IBL, CSM shadows, GPU skinning + morph targets, transparency, KHR transmission. The central
  question (see §5 **D1**) is whether `Sokol.Render3D` *extracts/generalizes* RenderingServer or is a leaner
  clean-room renderer.

### 2b. Deterministic 3D physics
- **Rough scope** (refine in planning): rigid bodies (sphere / box / capsule / convex / mesh?), broadphase,
  narrowphase + contact manifolds, a contact/constraint solver, integration, sleeping.
- **Determinism is the hard part in 3D.** Extend the `Det.cs` approach: only IEEE-754 `+ − × ÷ √` on the hot
  path; **no libm `sin/cos/acos/atan2`** (replace with the integer-angle polynomial trig already in `Det`);
  integer RNG (`DetRng`); a **fixed solver iteration order** over a stable body list. New 3D hazards to design
  around: quaternion integration/normalization, rotation matrices, and **manifold generation** (clip/closest-
  point routines often use transcendentals or order-dependent epsilons). Ship a `DeterminismProbe` golden like
  the 2D one and verify it across the fleet.

---

## 3. Prior art in THIS repo (read before planning)

| What | Where | Why it matters |
|---|---|---|
| **Sokol.Render2D** framework component | `src/Render2D/` (`Render2D.csproj`, `Renderer/Render2DSurface.cs`, `Particles/`) | The **template** for a new `src/Render3D/`: `AssemblyName Sokol.Render2D`, AOT, hosts the all-platform `sokol-shdc` compile targets. Mirror its csproj + layout. |
| **Deterministic 2D physics** | `examples/JamboreeArcade/Source/Physics2D/` (`Aabb`, `Shapes`, `Body`, `Collision`, `PhysicsWorld`) | The engine to mirror in 3D. Pure `+−×÷√`, sequential over stable body order. |
| **Determinism toolkit** | `Physics2D/Det.cs` (polynomial sin/cos on integer deci-degrees + `DetRng` xorshift), `Physics2D/DeterminismProbe.cs` (canonical scenario → FNV-1a golden) | The exact recipe + the proof harness; extend both to 3D. |
| **Existing 3D renderer + its plan** | `examples/GameEditor/` RenderingServer; `examples/GameEditor/docs/RENDERING_SERVER_IMPLEMENTATION_PLAN.md` (+ M1–M3 reviews) | **Primary reference.** Reuse its doc *structure* (Goals/Non-Goals, Reference Impls, Architecture: threading / GC / Sokol thread-safety / resource lifetimes, Milestones). |
| **Existing framework seams** | `src/Framework/` already has `Physics/`, `Renderer/`, `Scene/`, `ECS/` | Decide (§5 **D2**) whether 3D lives here or as standalone `Sokol.Render3D`/`Sokol.Physics3D`. |
| **Jolt bindings** | `src/JoltPhysicsSharp/`, `ext/JoltPhysics/`, `examples/JoltPhysics*`, `docs/JOLT_*.md` | API-shape reference + the *non-deterministic* fallback. NOT the deterministic engine. |
| **SG render-path abstraction (small)** | `examples/JamboreeArcade/Source/Arcade/PrismRush/Rendering/` (`ISceneRenderer`, `SgSceneRenderer`, `GuiSceneRenderer`) | A lightweight example of an SG render path + a GUI fallback. |

---

## 4. Cross-cutting constraints (carry into the plan)

- **6 platforms:** D3D11 (Win) · Metal (macOS/iOS) · OpenGL (Linux) · GLES3 (Android) · WebGL2 (WASM).
  Assumptions don't hold across them — verify on all, per project norm.
- **NativeAOT + WASM struct-by-value** P/Invoke workaround (auto-generated C wrappers) —
  `docs/C-Internal-Wrappers-Auto-Generation.md`.
- **Shaders:** authored in the sokol `.glsl` dialect, cross-compiled by `sokol-shdc`; framework components host
  the all-platform compile targets (`Framework.csproj` / `Render2D.csproj` model:
  `--slang glsl430:hlsl5:metal_macos:metal_ios:glsl300es`).
- **Determinism (physics):** IEEE-754 `+−×÷√` are safe; libm transcendentals and FMA contraction are **not**;
  integer RNG only; fixed iteration order. 3D rotation + manifold generation are the new risks.
- **Compositing:** SG-3D under / over Sokol.GUI (NanoVG) — precedent = RenderingServer + the Render2D
  underlay pattern (`RenderOffscreen`/`Blit` beneath NanoVG text/HUD).

---

## 5. Open decisions — resolve FIRST (these gate everything)

- **D1 — Render3D origin:** *Extract/generalize the GameEditor RenderingServer* into `Sokol.Render3D`, or
  build a *leaner clean-room* renderer? (RenderingServer is feature-rich but editor-coupled.)
- **D2 — Home/packaging:** Standalone `src/Render3D/` + `src/Physics3D/` (`Sokol.Render3D` / `Sokol.Physics3D`,
  mirroring `src/Render2D`), or fold into the existing `src/Framework/{Renderer,Physics}`?
- **D3 — Physics scope (MVP):** which shapes (sphere/box/capsule/convex/mesh)? joints/constraints? sleeping?
  CCD? Define the MVP vs the full target.
- **D4 — Determinism from day 1?** Is the 3D engine deterministic-for-netplay from the start (like Physics2D),
  or general-purpose first with determinism retrofitted? (Large architectural impact — affects every math
  choice.)
- **D5 — Physics ⇄ render coupling:** ECS-driven (Frent / `src/Framework/ECS`) or standalone, and how transforms
  sync between sim and renderer.
- **D6 — First proving ground:** which app validates it — a JamboreeArcade 3D game, a GameEditor runtime path,
  or a fresh minimal sample?

---

## 6. Deliverables of the planning session

1. **`docs/RENDER3D.md`** — `Sokol.Render3D` design + milestones (model on `RENDER2D.md` / the RenderingServer plan).
2. **`docs/PHYSICS3D.md`** — deterministic 3D physics design + milestones + the `DeterminismProbe` plan.
3. **Milestone order**, each with an explicit acceptance check (visual/platform, per project norm — not just
   "make it work").

---

## 7. START HERE (fresh session)

Read §3 (especially `RENDERING_SERVER_IMPLEMENTATION_PLAN.md` and `Physics2D/` + `Det.cs`) → resolve §5 D1–D6
with the user → write the §6 design docs → get approval → only then begin milestone implementation.
