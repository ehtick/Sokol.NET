# Bowling 3D + Darts 3D — Arcade Games on Render3D + Physics3D (Design + Plan)

> **Status:** DESIGN — ready for milestone implementation once `docs/RENDER3D.md` + `docs/PHYSICS3D.md` are approved.
> **Created:** 2026-06-19 · **Owner:** Sokol.NET
> **Resolves:** `docs/RENDER3D_PHYSICS3D_KICKOFF.md` §5 **D6** (first proving ground).
> **Depends on:** `docs/RENDER3D.md` (`Render3DSurface`, primitives, camera, shadow) ·
> `docs/PHYSICS3D.md` (`PhysicsWorld3`, `Body3`, `Det3`, `DeterminismProbe3D`).

---

## 1. What these are

Two **new** the reference app games — the "2 additional games" — built as siblings of the existing 2D
`BowlingGame`/`DartsGame`, but presented in **3D** on the new framework:

| Existing (2D, shipped) | New (3D, this doc) | Engine used |
|---|---|---|
| Bowling (`Source/Arcade/Bowling/`, deterministic 2D `PhysicsWorld`) | **Bowling 3D** | `Physics3D` (full rigid-body, toppling pins) + `Render3D` |
| Darts (`Source/Arcade/Darts/`, integer-only, *no physics*) | **Darts 3D** | integer scoring reused + `Render3D` (presentation only) |

They are **additional** games in the catalogue, not replacements — the 2D versions stay. They prove the framework
end-to-end: Darts 3D validates `Render3D` + the net/chrome path with near-zero physics; Bowling 3D validates the
deterministic 3D rigid-body sim under real BLE input-replay.

---

## 2. Everything they reuse (don't rebuild)

These are ordinary `ArcadeGame`s — the entire surrounding system is unchanged:

- **`ArcadeGame` base + `ShotInput`** (`Source/Net/ArcadeGame.cs`) — `Reset/Turn/Busy/Fire/Advance/GameOver/
  ScoreFor/ResultFor`; one shared game on both peers; the acting seat's compact integer packet re-simulates the
  turn on every device. Same `FixedDt` fixed-step discipline (and the **"`_accum -= FixedDt` inside the step
  loop"** rule — the documented arcade lesson, or a throw teleports).
- **The arcade net screen / harness** — the same path Bowling 2D and Darts 2D already run on (seed broadcast,
  per-turn `ShotInput`, turn handoff).
- **`GameChrome.Wrap`** (`Source/Widgets/GameChrome.cs`) — hamburger menu + floating top-right pill; full-bleed,
  every mode (the game-chrome refactor rule).
- **Group / Lobby invitability** — both register so they run **inline in `GroupLobbyScreen`** across all net models
  (the standing rule: *a game isn't done until it's invitable from the group*, not just code host/join).
- **HUD text via NanoVG only** — score/frame/501 text through `Sokol.GUI.Renderer.Draw`. **All shape rendering is
  on the GPU** (`Render3DSurface`) — the project rule (Render2D for 2D shapes, Render3D for 3D shapes, NanoVG = text
  only).
- **Particles** — pin-hit sparks reuse the 2D `Render2D` `ParticleLayer`, composited in the HUD/overlay above the
  3D scene (Render3D MVP has no particles).
- **Scoring logic** — Bowling 3D reuses the **pure** 10-frame strike/spare scoring from `BowlingGame.Score`; Darts
  3D reuses `DartsGame.Score/SectorNumber/Step501` **verbatim** (already pure-integer, fleet-safe).

> The only genuinely new code is: the 3D sim wiring (Bowling), the 3D views, and the `Render3D`/`Physics3D`
> components themselves (their own docs).

---

## 3. Bowling 3D

### 3.1 Sim — `BowlingGame3D : ArcadeGame`
Owns a `PhysicsWorld3` (`docs/PHYSICS3D.md`). World layout (lane long axis = world **Z**, up = world **Y**):
- **Lane** — a large static `Box`/`Plane` (Y up). **Gutters + back wall + side rails** — static boxes.
- **Ball** — a dynamic `Sphere` (`Mass ≈ 3`, low restitution), released at the foul line.
- **10 pins** — dynamic `Capsule`s upright in the standard triangle (reuse `BuildSpots`, now in the XZ plane).

`Fire(in ShotInput)`: place the ball at the release line, set `LinVel` from `Det.Dir(AimDeci) · SpeedFor(Power)`
down the lane; optional **hook** = a deterministic lateral/angular kick from `Spin`. `Advance(dt)`: accumulate and
fixed-step `PhysicsWorld3.Step`, apply lane drag each step, detect rest via body **sleeping**, then `ResolveThrow`.

**Pin "down" test (3D, more natural than 2D):** a pin counts as down when its **orientation tilts past ~θ from
vertical** (dot of its up-axis with world-up below a threshold) **or** it is displaced beyond its spot — replacing
the 2D pure-displacement test. Everything downstream (10-frame state machine, strike/spare, scoring) is the existing
`BowlingGame` logic, reused.

### 3.2 View — `BowlingView3D : Widget, IRender2DUnderlay` (SG underlay; see `RENDER3D.md §3`)
- **Camera** — `Camera3D` behind the bowler looking down the lane (perspective), optionally easing toward the ball
  during the roll.
- **`RenderOffscreen(pxW,pxH)`** — `Render3DSurface.Begin(...)` with the lane camera + a directional light/shadow
  fitted to the lane AABB; draw lane/gutter/wall boxes, **`DrawInstanced`** the 10 pin capsules (world matrix from
  each `Body3.Position`+`Orientation`), the ball sphere; `End()`.
- **`Blit()`** — composite under the HUD.
- **`Draw(Renderer)`** — NanoVG HUD (score/frame), the aim guide + `AimControl` (drag down the lane → `AimDeci`,
  release for power/hook), pin-hit `ParticleLayer` sparks. Reuses `BowlingView`'s HUD/aim code near-verbatim.

### 3.3 Net
Identical to Bowling 2D — `[AimDeci, Power, Spin]` per throw; both peers re-simulate. **Gated on
`DeterminismProbe3D`**: input-replay only on platforms whose on-device golden matches; otherwise active-auth for
this game on that platform (`PHYSICS3D.md §9`).

---

## 4. Darts 3D

Darts is the **lightweight** game — "no physics at all" in 2D. Darts 3D keeps that: **reuse `DartsGame`'s pure
integer scoring/flight state machine** (subclass or wrap it) and only swap the **view** to 3D.

### 4.1 Sim — reuse `DartsGame`
The score is fixed at `Fire` from `[angle, radius, seed]` via `DartsGame.Score` (pure integer — sector =
integer division of the deci-degree angle, ring = radius compares, jitter = `DetRng`). **No `Physics3D` needed.**
The dart's 3D travel is a **cosmetic parametric flight** toward the board plane (like the 2D `FlightDur` beat),
not a simulation — it can't affect the (already-decided) score, so it's free of determinism concerns.

> *Optional, not recommended for MVP:* fly a real ballistic `Body3` and intersect the board plane. Rejected because
> it adds a determinism surface for zero gameplay gain — the 2D design deliberately avoids physics here.

### 4.2 View — `DartsView3D : Widget, IRender2DUnderlay`
- **Camera** — facing the board straight-on (slight downward tilt).
- **`RenderOffscreen`** — a textured **dartboard disk** (wedge/ring texture, or a board mesh), 3D **cone** darts
  flying in along the parametric path and **sticking** at the scored point (mapped from `[angle, radius]` to the
  board plane), a small shadow.
- **`Draw`** — NanoVG HUD (501 remaining / practice score / BUST), the oscillating-crosshair aim from 2D darts
  projected onto the board, `AimControl`-style tap-to-lock.

### 4.3 Net
Identical to Darts 2D — integer-only, **always fleet-safe** (no probe gate).

---

## 5. Compositing & frame order (per game)

The app loop already drives this (`<app>-app.cs:123-149`) for any screen exposing an SG underlay:
```
view.RenderOffscreen(w,h)   // 3D: shadow pass → scene pass → resolve (its own sg passes)
begin swapchain pass
view.Blit()                 // 3D scene under the HUD
NanoVG: HUD text + aim guide + 2D particle sparks   // on top
end swapchain pass
```
No new app-loop code — the 3D views satisfy the same two-method contract as the 2D ones (`RENDER3D.md §3`).

---

## 6. Milestones (each with a visual/platform acceptance)

> Numbered `G3D-Mx`. Ordered to de-risk: **Darts 3D first** (exercises `Render3D` + net + chrome with ~no physics),
> then Bowling 3D solo, then Bowling 3D online. Each: macOS + ≥1 mobile + WASM before moving on; full fleet + a
> real BLE device pairing before "done".

- **G3D-M0 — Darts 3D (presentation-only).** Reuse `DartsGame`; `DartsView3D` renders a 3D board + flying cone
  darts; scoring identical to 2D; `GameChrome`; **group-invitable inline**. *Accept:* a full 501 + a solo practice
  play correctly in 3D, score matches the 2D game for the same inputs, invitable from `GroupLobbyScreen`, on the
  fleet. **(Validates Render3D + net + chrome before any 3D physics exists.)**
- **G3D-M1 — Bowling 3D solo.** `BowlingGame3D` on `PhysicsWorld3` + `BowlingView3D` on `Render3DSurface`
  (lit + shadowed lane, instanced pins, ball). *Accept:* a throw **knocks pins down with believable 3D toppling**,
  10-frame strike/spare scoring correct, runs at frame rate on **low-end Android**; depends on `P3D-M3` + `R3D-M3`.
- **G3D-M2 — Bowling 3D head-to-head over BLE.** Input-replay throws, **`DeterminismProbe3D`-gated**, `GameChrome`,
  lobby-invitable. *Accept:* on two paired devices, the same `[AimDeci,Power,Spin]` packet re-simulates to the
  **same final pin layout** (the determinism proof) and the same score; group flow works; active-auth fallback
  exercised on any non-matching platform.

---

## 7. Master sequencing (across all three docs)

```
1. R3D-M0/M1   (renderer: composited lit scene + primitives + camera + light)   ┐ in parallel
   P3D-M0      (Det3 + DeterminismProbe3D scaffold, golden green on fleet)        ┘
2. G3D-M0      Darts 3D            ← first game; proves Render3D + net + chrome end-to-end
3. P3D-M1..M3  (real rigid-body physics)   alongside   R3D-M2..M3 (shadows + instancing + textures)
4. G3D-M1      Bowling 3D solo     → G3D-M2  Bowling 3D online (probe-gated)
5. (later)     promote Render3D → src/Render3D, Physics3D → src/Physics3D   (kickoff D2 "promote later")
```

Acceptance is **visual + per-platform** throughout (CLAUDE.md §4 + the 6-platform project norm); the Bowling 3D
online milestone's acceptance is specifically *cross-device re-simulation agreement*, not just "it runs".

---

## 8. References

- Existing 2D games to mirror: `Source/Arcade/Bowling/` (`BowlingGame.cs`, `BowlingView.cs`, `BowlingDef.cs`),
  `Source/Arcade/Darts/` (`DartsGame.cs`, `DartsView.cs`, `DartsDef.cs`), `Source/Arcade/Pool/`, `Source/Arcade/Archery/`.
- `Source/Net/ArcadeGame.cs` — `ArcadeGame`/`ShotInput`. `Source/Widgets/GameChrome.cs` — chrome wrapper.
- `docs/RENDER3D.md`, `docs/PHYSICS3D.md` — the two framework components these games consume.
- Project rules applied: *every game group-invitable*; *Render3D for 3D shapes, NanoVG for text only*; *full-bleed
  GameChrome*; *6-platform verification before done*.
