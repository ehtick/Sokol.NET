# Sokol.Physics3D — Deterministic 3D Rigid-Body Engine (Design + Implementation Plan)

> **Status:** DESIGN — ready for milestone implementation once approved.
> **Created:** 2026-06-19 · **Owner:** Sokol.NET
> **Resolves:** `docs/RENDER3D_PHYSICS3D_KICKOFF.md` §5 **D3/D4/D5** for the physics workstream.
> **Siblings:** `docs/RENDER3D.md` (the renderer) · `docs/ARCADE_3D_GAMES.md` (Bowling 3D + Darts 3D).
> **Template:** `examples/JamboreeArcade/Source/Physics2D/` (`Det.cs`, `PhysicsWorld.cs`, `Body.cs`,
> `Shapes.cs`, `Collision.cs`, `DeterminismProbe.cs`) — the 2D engine this is the 3D analogue of.

---

## 0. Decisions carried in (from the kickoff §5)

| # | Decision | Resolution |
|---|---|---|
| **D3** | Physics MVP scope | **Full deterministic rigid-body.** Sphere + Capsule (dynamic) + Box/Plane (static) with **real angular dynamics** — quaternion orientation, per-shape inertia tensors, torque from off-centre contacts — so bowling pins **topple, spin and chain-react** in 3D. Gated behind a `DeterminismProbe3D` golden before any netplay is built on it. **Documented fallback** (§9) if a platform diverges: simplified position-only pins + scripted topple. |
| **D4** | Determinism from day 1 | **Yes.** These are BLE input-replay arcade games (`ArcadeGame`/`ShotInput`): a turn re-simulates from a compact integer packet on every device and must be **bit-identical** on arm64/x64/wasm × Metal/GLES3/WebGL2/D3D11. Determinism is the *reason* this is custom and not Jolt. Every math choice below is made for it. |
| **D5** | Sim ⇄ render coupling | **Standalone, not ECS.** The game owns the `PhysicsWorld3` + bodies, steps it at a fixed rate, and reads each body's `Position`+`Orientation` for the renderer. Mirrors the 2D arcade games. |

**Packaging (D2):** built **example-local first** at `examples/JamboreeArcade/Source/Physics3D/` (exactly where
`Physics2D` lives), promoted to `src/Physics3D/` (`Sokol.Physics3D`) once device-proven.

---

## 1. Goals & non-goals

### Goals (MVP)
- A deterministic 3D rigid-body world: gravity, linear + **angular** integration, a contact/impulse solver with
  **friction + restitution + torque**, sleeping, and per-step contacts the game reads — the lightweight Jolt
  substitute for *deterministic* arcade sims.
- **Shapes:** `Sphere`, `Capsule` (dynamic), `Box`, `Plane`/half-space (static world). That exact set covers
  Bowling (sphere ball, capsule pins, box lane + gutters + back wall) and is enough for Darts (which is near-ballistic).
- **`DeterminismProbe3D`** — a canonical rack-and-break scenario hashed to an FNV-1a golden, asserted headless in
  `tests/Physics3D.Tests` and shown on an on-device screen, exactly like the 2D `DeterminismProbe`.

### Non-goals (MVP — deferred, not designed away)
- No general convex (GJK/EPA) or triangle-mesh colliders — **only closed-form pair tests** (§4). No box–box
  (no two dynamic boxes ever meet: dynamics are spheres/capsules, world is static boxes/planes).
- No joints/constraints beyond contacts. No warm-starting in MVP (it adds contact-order sensitivity — see §3).
- No broadphase acceleration structure — body counts are ~12, so O(n²) AABB culling is fine; a deterministic
  sort-and-sweep is a documented later hook.
- No general CCD; the one fast body (the ball) is handled by **substepping + speculative contacts** (§5).

> **Acceptance bar:** the proof is the **golden hash matching across the fleet**, plus the visual check that pins
> topple/scatter believably. A platform whose golden differs cannot use input-replay for Bowling 3D → it uses the
> active-authoritative fallback for that game (Darts 3D is integer-only and always safe).

---

## 2. Why 3D determinism is harder than 2D (and the plan for each hazard)

The 2D engine proved that on .NET NativeAOT/RyuJIT, **`+ − × ÷` and `MathF.Sqrt` (IEEE-754 correctly-rounded) are
bit-identical across the fleet**, and that **`System.Numerics` arithmetic does not auto-contract to FMA** (.NET only
emits FMA via the explicit `MathF.FusedMultiplyAdd`, which we never call). The *only* 2D hazard was
`MathF.Sin/Cos`, replaced by `Det`'s integer-angle polynomial. 3D adds new hazards — each gets a concrete rule:

| Hazard | Where it bites | Rule |
|---|---|---|
| **Quaternion → angle transcendentals** | `Quaternion.Slerp`, `CreateFromAxisAngle`, exact exp-map (`sin/cos\|ω\|dt`) | **Banned on the hot path.** Integrate orientation with the **first-order update + renormalize** (§3) — pure `+ − × ÷ √`. Aim/launch angles still go through `Det.Sin/Cos` (integer deci-degrees). |
| **`acos`/`atan2` in narrowphase** | angle between vectors, capsule axis tests | **Banned.** Use dot/cross/closest-point formulations (§4) — `+ − × ÷` + clamps only. |
| **`Vector3`/`Matrix4x4`/`Quaternion` ops** | integration, inertia rotation | **Allowed** — add/sub/mul, dot, cross, matrix-mul, and `Normalize` (via `1/√`) are deterministic on .NET (no auto-FMA). **But verify with the probe**, and keep the *solver inner loop* expressible as explicit scalar `float` math (like `Det`) so a SIMD surprise can be ruled out by swapping one helper, not a rewrite. |
| **Contact manifold order / epsilons** | which contacts, in which order, with what tie-breaks | **Fixed iteration order over a stable body list** (the 2D `ResolveCircles` discipline, extended to 3D); deterministic tie-breaks by body index; constants are compile-time `float` literals. |
| **Division by ~0** | closest-point denominators, normalize of a zero vector | Guard every divide with a fixed epsilon **branch** (same branch on every platform → same result); never rely on Inf/NaN propagation. |

---

## 3. Determinism toolkit — `Det3` (extends `Det`)

`Physics3D/Det3.cs` builds on the existing `Det` (`Det.Sin/Cos/Dir`, `DetRng`) and adds **pure** 3D helpers:

- **Orientation integration (first-order + renormalize)** — the chosen angular integrator (avoids all trig):
  ```
  // ω as a pure quaternion (0, ωx, ωy, ωz); semi-implicit, renormalize every step
  qDot = 0.5 * Quat(0, ω) ⊗ q
  q    = Normalize(q + qDot * dt)        // Normalize uses 1/√(dot) — deterministic
  ```
  Documented as THE integrator. Renormalization each step keeps `q` unit without `sin/cos`.
- **`Mat3 FromQuat(q)`** — rotation matrix from a quaternion (pure `+ − ×`), for rotating the body-space inertia.
- **World inverse inertia** — `Iinv_world = R · Iinv_body · Rᵀ` each step, with `Iinv_body` a **diagonal** (per-shape
  closed form, §4); pure matrix products.
- **Deterministic tangent basis** — given a contact normal `n`, pick the world axis least aligned with `n`
  (branch on `|n.x|,|n.y|,|n.z|`), `t1 = normalize(axis × n)`, `t2 = n × t1`. No `atan2`; identical branch → identical basis.
- **`DetRng`** — reused unchanged for any per-turn jitter (seeded integer xorshift).

No `System.Random`, no wall-clock, no `Stopwatch` anywhere in the sim.

---

## 4. Core types & narrowphase (mirror Physics2D)

```
Physics3D/
  Det3.cs               ← §3 toolkit (quat integrate, mat3-from-quat, inertia rotate, tangent basis)
  Shapes3.cs            ← Shape3 { Sphere(r) | Box(half) | Capsule(r, halfHeight) | Plane(n,d) }; closed-form inertia + AABB
  Body3.cs              ← the rigid body (state + material + filter + per-step output)
  Collision3.cs         ← closed-form narrowphase pairs (the determinism-critical file)
  PhysicsWorld3.cs      ← Step(): integrate → broadphase → narrowphase → solve → integrate → sleep
  DeterminismProbe3D.cs ← canonical scenario → FNV-1a golden (headless test + on-device screen)
```

### 4.1 `Body3`
State: `Vector3 Position`, `Quaternion Orientation`, `Vector3 LinVel`, `Vector3 AngVel`, `Shape3 Shape`,
`BodyType {Static,Kinematic,Dynamic}`, `float Mass`/`InvMass`, `Vector3 InvInertiaBody` (diagonal),
`Restitution`, `Friction`, `uint Layer/Mask`, `object? UserData`. Per-step output: `bool OnGround`, `Body3? Ground`,
`bool Sleeping`. Derived: world AABB (broadphase), `Mat3 InvInertiaWorld` (recomputed each step from orientation).
Static/Kinematic ⇒ `InvMass = 0`, `InvInertiaWorld = 0` (immovable, like the 2D `InvMass` rule).

### 4.2 Inertia tensors (closed form, body-space diagonal)
- Sphere: `I = 2/5 · m · r²` (isotropic).
- Capsule: cylinder + two hemispheres, standard closed form about the two principal axes (along-axis vs transverse).
- Box: `Ix = 1/12·m·(y²+z²)`, etc. (static-only in MVP, but defined).
All are compile-time-foldable `float` expressions → identical bits everywhere.

### 4.3 Narrowphase pair matrix (closed-form ONLY — the new determinism risk)
The dynamic set is **{Sphere, Capsule}**, the static set is **{Box, Plane}**. That bounds the pairs to closed-form,
GJK-free tests, every one expressible with `+ − × ÷ √` + clamps:

| Pair | Method | Used for |
|---|---|---|
| Sphere–Plane | signed distance along plane normal | ball on lane / floor |
| Sphere–Box | closest point on AABB/OBB to centre | ball vs gutter wall |
| Sphere–Sphere | centre distance | (general; ball vs ball if ever) |
| Sphere–Capsule | closest point on capsule segment to centre | ball hits a pin |
| Capsule–Plane | each endpoint vs plane (deepest) | a pin (standing/toppled) on the lane |
| Capsule–Box | segment-vs-OBB closest point | a pin against a wall/gutter |
| **Capsule–Capsule** | **closest points of two segments** (Ericson, clamped params; one guarded divide) | **pin ↔ pin** (the chain reaction) |

> Capsule–capsule closest-segment-points is the only non-trivial one and is the classic deterministic routine —
> pure `+ − × ÷` with parameter clamps and a single epsilon-guarded denominator. It is the file to scrutinise in
> review and the reason `DeterminismProbe3D` has a dedicated rack-break variant.

### 4.4 `PhysicsWorld3.Step(dt)` (fixed order — the 2D discipline, in 3D)
```
0. (kinematic carry — only if a game needs moving platforms; omit for Bowling/Darts)
1. integrate velocities: LinVel += Gravity*dt (Dynamic, non-sleeping); recompute InvInertiaWorld from orientation
2. broadphase: O(n²) world-AABB overlap test over the STABLE body list → candidate pairs (deterministic order)
3. narrowphase: closed-form pair tests (§4.3) → Contact3 { a, b, worldPoint, normal, penetration } list (stable order)
4. solve: sequential impulse (Gauss–Seidel), FIXED iteration count, over contacts in stable order —
          normal impulse (restitution) + clamped friction impulse on the §3 tangent basis, applying BOTH
          linear AND angular (r × impulse via InvInertiaWorld) so off-centre hits create torque (topple)
5. integrate positions: Position += LinVel*dt; Orientation = Det3 first-order update + renormalize (§3)
6. positional correction: split-impulse / Baumgarte push-out along the normal (penetration slop + factor)
7. sleeping: lin²+ang² below threshold for N consecutive steps → Sleeping=true (wake on a new contact impulse)
```
Every loop iterates the insertion-ordered `_bodies`/`_contacts` (never reordered mid-step); tie-breaks by index.
**No warm-starting in MVP** — it requires persistent contact IDs and reintroduces order sensitivity; revisit only
if the rack won't settle in the iteration budget (it should, like the 2D break does in `BodyIters`).

---

## 5. Anti-tunneling for the fast ball (deterministic)

A bowling ball at full power covers a large fraction of a pin's radius per 1/240 s step → naive discrete contacts
can miss a pin. Two deterministic mitigations, both pure arithmetic:
1. **Substepping** — `Step(dt)` runs `N` internal sub-steps (e.g. 2–4) at `dt/N`; the game already accumulates and
   fixed-steps (`Advance` in `BowlingGame`), so this is an internal loop count, fully deterministic.
2. **Speculative contacts** — in narrowphase, expand the sphere's effective contact distance by `|relVel|·subDt`
   so an *approaching* pin within the step's reach generates a contact this step (the solver then resolves it
   without penetration). No transcendentals; identical on every platform.

MVP: ship substepping (simplest, obviously deterministic); add speculative contacts at P3D-M3 if tunnelling
survives substepping at max power. Pick the fixed rate (240 Hz like 2D, or 360 Hz) and **pin it in the probe**.

---

## 6. `DeterminismProbe3D` (the proof harness)

Self-contained (engine + `Det3` + `System.Numerics` only) so it runs headless **and** on-device, like the 2D probe:

- **Scenario:** a sphere struck along the lane into a **triangle rack of 10 capsules** standing on a plane inside
  4 static box walls; fixed `[aim, power]`; a fixed lane-drag each step; run `Steps` at `FixedDt`.
- **Hash:** FNV-1a over the **raw float bits** of every body's `Position (3)` + `Orientation (4)` + `LinVel (3)` +
  `AngVel (3)` at the end (mirrors `DeterminismProbe.Mix` over `SingleToInt32Bits`).
- **Golden:** pinned on macOS arm64, asserted by `tests/Physics3D.Tests`, and displayed on an on-device determinism
  screen (extend the existing arcade determinism screen). A second probe variant exercises a **single off-centre
  capsule topple** so an angular-only divergence is caught even if the rack happens to agree.
- **Gate:** Bowling 3D enables input-replay netplay only on platforms whose on-device number matches the golden;
  others fall back to active-auth. (Re-pin only when the scenario is deliberately changed.)

---

## 7. Net integration (reuse `ShotInput` exactly)

No new wire format. `Physics3D` plugs into the existing `ArcadeGame`/`ShotInput` input-replay path
(`examples/JamboreeArcade/Source/Net/ArcadeGame.cs`):
- **Bowling 3D:** `AimDeci` = lane direction, `Power` = release speed, `Spin` = optional hook (a deterministic
  lateral/angular kick at release), `Seed` = unused/lane jitter. Both peers `Fire` the same packet and `Advance`
  the same `PhysicsWorld3` → identical pin layout.
- **Darts 3D:** stays integer-only (the 2D `DartsGame` scoring is reused verbatim); the engine is barely involved
  — see `docs/ARCADE_3D_GAMES.md`.

---

## 8. Milestones (each with the golden + a visual check)

> Numbered `P3D-Mx`, example-local (D2). Golden must match on macOS + ≥1 mobile + WASM at each step; full fleet
> before "done".

- **P3D-M0 — `Det3` + probe scaffold.** Quaternion first-order integrate + renormalize, `Mat3.FromQuat`, inertia
  rotate, tangent basis; `DeterminismProbe3D` hashing a trivial free-falling **and** spinning body; headless test
  pins a golden; on-device screen shows it. *Accept:* golden matches macOS + 1 mobile + web; a torque-free spinning
  body keeps a unit quaternion over thousands of steps.
- **P3D-M1 — Bodies + spheres + static box/plane + linear solver.** `Body3`, sphere/plane/box narrowphase,
  sequential-impulse **linear** solve, positional correction. *Accept:* a ball rolls on a plane, bounces off box
  walls, comes to rest; probe (sphere-only variant) golden stable across fleet.
- **P3D-M2 — Angular dynamics + capsules + friction.** Inertia tensors, torque application, capsule shape +
  capsule–plane/–capsule/sphere–capsule, friction impulses. *Accept:* an off-centre push **topples a single
  capsule**; a sphere **knocks a standing capsule down**; the rack-break probe golden matches across the fleet
  (the core determinism proof).
- **P3D-M3 — Sleeping + anti-tunneling + full rack.** Rest detection ends a throw; substepping (+ speculative
  contacts if needed) so a max-power ball never tunnels. *Accept:* a full 10-pin break settles, no tunnelling at max
  power on **all** targets, golden stable.
- **P3D-M4 (later) — Promote to `src/Physics3D`.** Optional deterministic sort-and-sweep broadphase; convex hull
  deferred. *Accept:* both 3D games build against `Sokol.Physics3D`; goldens unchanged.

---

## 9. Documented fallback (if a platform diverges) — the D3 safety valve

If `DeterminismProbe3D` cannot be made to match on a target despite the rules above (a libm leaking into a pair test,
a SIMD lowering surprise), that **game** degrades gracefully rather than the engine being abandoned:
1. **Per-platform:** Bowling 3D uses **active-player-authoritative** netplay on the offending platform (the acting
   device sends the resolved final pin transforms instead of everyone re-simulating) — the same documented fallback
   the 2D games define. Solo + single-platform play is unaffected.
2. **Whole-feature (last resort):** the **simplified-pin** model (kickoff D3 option B) — pins translate via
   sphere/capsule collisions but topple via a **scripted, deterministic-by-construction** animation rather than true
   angular simulation, shrinking the determinism surface to translation only. Kept as a design option, not built
   unless M2's angular golden proves unstable across the fleet.

---

## 10. References (precedents mirrored, no code copied)

- `examples/JamboreeArcade/Source/Physics2D/` — `Det.cs` (polynomial trig + `DetRng`), `PhysicsWorld.cs`
  (fixed-order `ResolveCircles` impulse solver), `Body.cs`/`Shapes.cs`/`Collision.cs`, `DeterminismProbe.cs`
  (the exact golden-harness recipe extended here).
- `examples/JamboreeArcade/Source/Net/ArcadeGame.cs` — `ArcadeGame`/`ShotInput` input-replay model reused unchanged.
- `docs/RENDER3D_PHYSICS3D_KICKOFF.md` §2b/§4/§5 — the determinism constraints and the open decisions resolved here.
- Ericson, *Real-Time Collision Detection* — closest-point routines (segment–segment, point–OBB) used in §4.3
  (algorithms only; implemented fresh with the determinism rules).
- `src/JoltPhysicsSharp/` — API-shape reference and the **non-deterministic** fallback path; NOT this engine.
