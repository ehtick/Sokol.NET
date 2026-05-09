# JoltPhysicsDemo

An interactive physics sample browser built on top of [JoltPhysics](https://github.com/jrouwe/JoltPhysics) and [Sokol.NET](https://github.com/elialoni/Sokol.NET). It bundles over 100 runnable demos organized into categories, all selectable from a live in-app panel. The app targets **Desktop** (macOS, Windows, Linux), **Android**, **iOS**, and **WebAssembly** from a single C# codebase using .NET NativeAOT.

## Screenshots

| Pyramid (1 241 bodies) | Mass Spawn (10k bodies) |
|---|---|
| ![Pyramid demo](screenshots/Screenshot%202026-05-09%20at%2020.00.43.png) | ![Mass Spawn demo](screenshots/Screenshot%202026-05-09%20at%2020.00.55.png) |

| Crater | Conveyor Belt |
|---|---|
| ![Crater demo](screenshots/Screenshot%202026-05-09%20at%2020.00.59.png) | ![Conveyor Belt demo](screenshots/Screenshot%202026-05-09%20at%2020.01.14.png) |

| Tank (Tracked Vehicle) | SoftBody: Vs Fast Moving |
|---|---|
| ![Tank demo](screenshots/Screenshot%202026-05-09%20at%2020.03.07.png) | ![SoftBody Vs Fast Moving demo](screenshots/Screenshot%202026-05-09%20at%2020.03.14.png) |

| Character Virtual | Kinematic Rig |
|---|---|
| ![Character Virtual demo](screenshots/Screenshot%202026-05-09%20at%2020.03.49.png) | ![Kinematic Rig demo](screenshots/Screenshot%202026-05-09%20at%2020.04.09.png) |

| Boat (Water — transparent, Fresnel shading) | |
|---|---|
| ![Boat demo](screenshots/Screenshot%202026-05-09%20at%2020.04.36.png) | |

## Demo Categories

### General
Core rigid-body scenarios covering the fundamentals of the physics engine.

| Demo | Description |
|---|---|
| Simple | Single rigid body falling onto a floor — minimal hello-world setup |
| Stack | Column of stacked boxes testing stacking stability |
| Wall | A wall of bricks hit by a projectile |
| Pyramid | Large layered pyramid of boxes (1 200+ bodies) |
| Islands | Isolated islands of bodies that go to sleep independently |
| Friction | Side-by-side blocks with varying friction coefficients |
| Restitution | Bouncing balls with different restitution (bounciness) values |
| Mass Spawn (10k bodies) | Stress test: 10 000 bodies spawned simultaneously |
| Dominos | Chain of domino tiles |
| Avalanche | Avalanche of boxes cascading down a slope |
| Crater | Projectile impacts leaving a crater in a pile of bodies |
| Stairs | Character-style stair-climbing test using rigid bodies |
| Kinematic | Kinematic body pushing dynamic bodies |
| Damping | Linear and angular damping comparison |
| Heavy On Light | Heavy object resting on a stack of lighter ones |
| Gravity Factor | Per-body gravity scale override |
| Funnel | Bodies funneled through a narrow opening |
| High Speed (CCD) | Fast-moving projectile with continuous collision detection |
| Gyroscopic Force | Gyroscopic torque on a spinning body |
| Bowling | Bowling pins and ball |
| Billiards | Billiard balls on a table |
| Big vs Small | Interaction between very large and very small bodies |
| Change Motion Type | Switching a body between Static / Kinematic / Dynamic at runtime |
| ActivateDuringUpdate | Bodies woken mid-step when a kinematic body moves through them |
| ChangeMotionQuality | Toggling discrete ↔ linear-cast quality per body |
| Contact Listener | Custom contact listener that filters and modifies contacts |
| Conveyor Belt | Kinematic conveyor belt using surface velocity |
| Sensor | Sensor bodies that detect overlaps without generating contact forces |
| Contact Manifold | Visualising the contact manifold points |
| Center Of Mass | Offset center of mass and its effect on rotation |
| Change Shape | Swapping a body's collision shape at runtime |
| Modify Mass | Runtime mass and inertia override |
| Change Object Layer | Moving a body between object layers at runtime |

### Constraints
Joints and motors connecting pairs of bodies.

| Demo | Description |
|---|---|
| Wrecking Ball | Heavy ball on a fixed-point constraint swinging into a wall |
| Newton's Cradle | Classic five-ball pendulum using distance constraints |
| Seesaw | Two bodies on a fixed-pivot seesaw |
| Chain Pendulum | Multi-link chain using hinge constraints |
| Elevator | Platform driven by a motor on a slider constraint |
| Mace | Rigid mace built from a chain of distance constraints |
| Point Constraint | Ball-and-socket joint examples |
| Hinge Constraint | Single-axis revolute joints |
| Cone Constraint | Hinge with angular limits forming a cone |
| Pulley | Two bodies connected by a virtual pulley |
| Spring | Distance constraint with spring/damper settings |
| Swing Twist | Swing-twist constraint for shoulder/hip joints |
| Gear Constraint | Gear ratio between two hinged bodies |
| Rack And Pinion | Rack-and-pinion mechanical linkage |
| Distance Constraint | Fixed-length rope between two bodies |
| Fixed Constraint | Fully rigid weld joint |
| Slider Constraint | Prismatic joint with optional motor and limits |
| Powered Hinge | Hinge driven by a position or velocity motor |
| Powered Slider | Slider driven by a position or velocity motor |
| SwingTwist Friction | Swing-twist with angular friction |

### Shapes
Collision shape showcase — each demo isolates a single shape type.

| Shape | Notes |
|---|---|
| Box | Axis-aligned box |
| Sphere | Perfect sphere |
| Capsule | Capsule (cylinder + two hemispheres) |
| Cylinder | Flat-capped cylinder |
| Tapered Capsule | Variable-radius capsule |
| Tapered Cylinder | Frustum / truncated cone |
| Offset Center Of Mass | Compound with displaced COM |
| Convex Hull | Arbitrary convex hull from point cloud |
| Rotated & Translated | Shape transform offset within a body |
| Static Compound | Immutable compound of sub-shapes |
| Triangle | Single triangle primitive |
| Plane | Infinite plane |
| Empty | Zero-volume placeholder shape |
| Mutable Compound | Compound whose sub-shapes can be added/removed at runtime |

### Vehicles
Wheeled and tracked vehicle controllers.

| Demo | Description |
|---|---|
| Vehicle Constraint | Generic multi-wheel vehicle constraint |
| Motorcycle | Two-wheeled motorcycle with lean physics |
| Car (SixDOF Constraint) | Car built from six-degrees-of-freedom constraints |
| Tank (Tracked Vehicle) | Differential-steering tracked vehicle |
| Vehicle Stress | Stress test with many vehicles |

### Soft Body
Position-based soft-body simulation.

| Demo | Description |
|---|---|
| Shapes | Soft body in various shapes |
| Vs Fast Moving | Soft body hit by a high-speed rigid projectile (CCD) |
| Friction | Friction between a soft body and rigid surfaces |
| Restitution | Bouncing soft body |
| Pressure | Inflatable soft body with internal pressure |
| Gravity Factor | Per-soft-body gravity scale |
| Force | External force applied to soft body vertices |
| Kinematic | Kinematic vertices driving the soft body |
| Update Position | Moving the body's anchor point at runtime |
| Stress Test | Many soft bodies active simultaneously |
| LRA Constraint | Long-range attachment constraints |
| Bend Constraint | Bending resistance between faces |
| Cosserat Rod | Rod-like soft body with twist stiffness |
| Contact Listener | Custom contact callbacks on soft bodies |
| Sensor | Soft body interacting with sensor volumes |
| Custom Update | Per-vertex custom position update callback |
| Skinned Constraint | Soft body skinned to a rigid skeleton |
| Vertex Radius | Non-zero vertex radius for soft bodies |

### Character
Kinematic and virtual character controllers for first/third-person movement.

| Demo | Description |
|---|---|
| Character | Kinematic character controller with WASD + jump/crouch |
| Character Virtual | Constraint-based virtual character controller |

Both character demos support the **virtual joystick** with A/D continuous steering and correct backward-movement inversion.

### Rig
Articulated ragdoll / skeletal rigs.

| Demo | Description |
|---|---|
| Create Rig | Build a ragdoll programmatically |
| Load Rig | Load a ragdoll from file |
| Load Save Rig | Round-trip save/load of a ragdoll |
| Load Save Binary Rig | Binary-format ragdoll save/load |
| Kinematic Rig | Rig driven by keyframed animation |
| Soft Keyframed Rig | Soft-body style keyframing with contact response |
| Powered Rig | Motor-driven joints tracking a target pose |
| Rig Pile | Pile of ragdolls stress test |
| Big World | Large world with many concurrently active rigs |
| Skeleton Mapper | Retargeting between two different skeletal hierarchies |

### Water
Buoyancy and water interaction.

| Demo | Description |
|---|---|
| Water Shapes | Various rigid shapes floating in a water volume |
| Boat | Boat hull with wave-based buoyancy |

## Controls

### Desktop (Keyboard & Mouse)
| Input | Action |
|---|---|
| Left-drag | Orbit camera |
| Right-drag / Scroll | Zoom |
| W / A / S / D | Move character / steer vehicle |
| Arrow keys | Steer tank |
| Space | Jump (character demos) |
| Left Shift | Crouch (character demos) |
| Right Shift | Brake (tank demo) |
| P or Pause button | Pause / resume simulation |
| R or Reset button | Reset current demo |

### Mobile / Touch (Android & iOS)
Virtual controls are enabled by default on Android and iOS.

| Control | Action |
|---|---|
| Left joystick (bottom-left) | Move / steer |
| Action buttons (bottom-right) | Jump, Crouch, Brake — context-dependent |
| One-finger drag (outside controls) | Orbit camera |
| Pinch | Zoom |

Multi-touch is fully supported: the joystick finger and button fingers are tracked independently.

## Stats Panel (top-right)

| Field | Description |
|---|---|
| FPS | Current frames per second |
| Bodies | Active body count in the physics world |
| Cam | World-space camera eye position |
| Start paused | Begin the next demo in a paused state |
| Lock camera | Prevent camera from moving |
| Virtual controls | Toggle on-screen joystick and buttons |
| Flip joy side | Move joystick to the right and buttons to the left |
| Pause / Resume | Toggle simulation pause |
| Reset | Restart the current demo |

## Building

```bash
# Desktop (JIT — for fast iteration)
dotnet run --project examples/JoltPhysicsDemo/JoltPhysicsDemo.csproj

# Prepare assets / shaders
dotnet run --project tools/SokolApplicationBuilder -- --task prepare --architecture desktop --path examples/JoltPhysicsDemo

# WebAssembly
dotnet run --project tools/SokolApplicationBuilder -- --task build --architecture web --path examples/JoltPhysicsDemo

# Android APK
dotnet run --project tools/SokolApplicationBuilder -- --task build --type release --architecture android --subtask apk --path examples/JoltPhysicsDemo

# iOS
dotnet run --project tools/SokolApplicationBuilder -- --task build --type release --architecture ios --path examples/JoltPhysicsDemo
```

## Architecture

```
JoltPhysicsDemo-app.cs      ← app shell: init, frame, event, UI, virtual controls
Camera.cs                   ← orbit / first-person camera + touch handling
VirtualControlsTypes.cs     ← VirtualControlsType enum (None / WASD / Arrows)
Source/Demos/               ← one file per demo, all inherit DemoBase
  DemoBase.cs               ← shared lifecycle, key state, virtual key mapping
  Rig/                      ← ragdoll / skeleton demos
  SoftBody/                 ← soft-body demos
  Water/                    ← buoyancy demos
```

Each demo implements:
- `Activate()` — one-time setup: create bodies, constraints, etc.
- `Update(dt)` — per-frame logic (input, motor targets, etc.)
- `Deactivate()` / `Cleanup()` — teardown

The `VirtualControls` and `VirtualActionButtons` properties on `DemoBase` declare which on-screen controls to show; the app shell draws them and injects synthetic key presses into the shared key-state table.
