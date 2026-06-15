using System;
using System.Collections.Generic;
using Sokol.GUI;

namespace Sokol.Render2D.Particles;

/// <summary>How an emitter releases particles.</summary>
public enum EmitMode
{
    /// <summary>One-shot — release <see cref="EmitterConfig.BurstCount"/> particles on the first step.</summary>
    Burst,
    /// <summary>Steady stream — <see cref="EmitterConfig.RatePerSecond"/> particles/s (frame-rate independent).</summary>
    Continuous,
}

/// <summary>Spatial shape the spawn position is sampled from.</summary>
public enum EmitShape
{
    /// <summary>All particles start at the emitter origin.</summary>
    Point,
    /// <summary>Start at a uniform random point within <see cref="EmitterConfig.ShapeRadius"/> of the origin.</summary>
    Disc,
    /// <summary>Start on a horizontal segment of half-width <see cref="EmitterConfig.ShapeRadius"/>
    /// (rain / snow curtains, ground lines).</summary>
    Line,
    /// <summary>Start on the circle edge of radius <see cref="EmitterConfig.ShapeRadius"/> (rings, portals).</summary>
    Ring,
}

/// <summary>How a particle is drawn by <see cref="Rendering.IParticleRenderer"/>.</summary>
public enum ParticleShape
{
    /// <summary>Soft radial-gradient disc (additive fire/spark core).</summary>
    Glow,
    /// <summary>Hard filled circle (puffs, dots).</summary>
    Disc,
    /// <summary>Filled triangle, rotated by <see cref="Particle.Rotation"/> (debris/confetti).</summary>
    Triangle,
    /// <summary>A line from the position back along the velocity (sparks/tracers).</summary>
    LineTrail,
    /// <summary>A sprite from <see cref="EmitterConfig.TextureKey"/> (PNG/JPEG/SVG), centred + rotated;
    /// modulated by alpha only (NanoVG limit) so textured particles fade but don't colour-tint.</summary>
    Texture,
    /// <summary>Hollow ring / halo (bubbles, shockwave rings, portals).</summary>
    Ring,
    /// <summary>Five-point star (sparkles, magic bursts, twinkles).</summary>
    Star,
}

/// <summary>An inclusive [Min,Max] range sampled per particle. A bare tuple <c>(a, b)</c> converts
/// to one implicitly, so presets read like the design doc (<c>Life = (0.35f, 0.7f)</c>).</summary>
public readonly record struct FloatRange(float Min, float Max)
{
    public float Sample(Random rng) => Min + (float)rng.NextDouble() * (Max - Min);
    public static implicit operator FloatRange((float min, float max) t) => new(t.min, t.max);
}

/// <summary>
/// The data definition of an effect — everything an <see cref="Emitter"/> needs to spawn and evolve
/// particles. Built-in effects are <see cref="ParticlePresets"/> factory values; nothing here is
/// per-frame allocated. Ranges are sampled per particle from the system RNG.
/// </summary>
public sealed record EmitterConfig
{
    // ── emission ──────────────────────────────────────────────────────────────
    public EmitMode  Mode          { get; init; } = EmitMode.Burst;
    public int       BurstCount    { get; init; } = 32;     // Burst
    public float     RatePerSecond { get; init; } = 0f;     // Continuous
    public float     Duration      { get; init; } = 0f;     // Continuous lifetime in s (0 = until system disposed)
    public EmitShape Shape         { get; init; } = EmitShape.Point;
    public float     ShapeRadius   { get; init; } = 0f;     // for Disc
    /// <summary>When true, velocity is emitted within <see cref="ConeHalfAngle"/> of the emitter's
    /// runtime <c>Direction</c> (set per-frame for body-following thrust); else omnidirectional.</summary>
    public bool      Directional   { get; init; } = false;
    public float     ConeHalfAngle { get; init; } = 0f;     // radians, used when Directional

    // ── initial particle state (ranges) ─────────────────────────────────────────
    public FloatRange Life          { get; init; } = (0.4f, 0.8f);
    public FloatRange Speed         { get; init; } = (60f, 200f);   // px/s, emitted in a random direction
    public FloatRange StartSize     { get; init; } = (8f, 14f);
    public FloatRange EndSize       { get; init; } = (0f, 2f);
    public FloatRange StartRotation { get; init; } = (0f, 0f);
    public FloatRange AngularVel    { get; init; } = (0f, 0f);
    public UIColor    Color0        { get; init; } = UIColor.White;
    public UIColor    Color1        { get; init; } = UIColor.White.WithAlpha(0f);

    // ── simulation ──────────────────────────────────────────────────────────────
    public Vector2   Gravity { get; init; } = Vector2.Zero;   // px/s² (negative Y = buoyant)
    public float     Drag    { get; init; } = 0f;             // 1/s velocity damping
    public BlendMode Blend   { get; init; } = BlendMode.Normal;

    // ── appearance ──────────────────────────────────────────────────────────────
    public ParticleShape Visual     { get; init; } = ParticleShape.Disc;
    /// <summary>Asset path for <see cref="ParticleShape.Texture"/> (e.g. "particles/smoke.png" or a
    /// ".svg"); resolved lazily through <see cref="ParticleTextureCache"/>. Null for dynamic visuals.</summary>
    public string?       TextureKey { get; init; } = null;
    /// <summary>Sprite-sheet grid for <see cref="ParticleShape.Texture"/> (1×1 = a single image).</summary>
    public int           SheetCols  { get; init; } = 1;
    public int           SheetRows  { get; init; } = 1;
    /// <summary>When true, advance the sheet frame over the particle's life (a flipbook); else each
    /// particle picks one random static frame at spawn.</summary>
    public bool          SheetAnimate { get; init; } = false;
    /// <summary>LineTrail length in seconds of travel: tail = Pos − Vel·TrailScale.</summary>
    public float         TrailScale { get; init; } = 0.03f;
    /// <summary>Optional per-frame motion modifiers (turbulence, wind, vortex…). See <see cref="IAffector"/>.</summary>
    public IReadOnlyList<IAffector>? Affectors { get; init; } = null;
    public int           MaxParticles { get; init; } = 96;   // pool cap for one system

    /// <summary>Optional multi-stop colour ramp sampled by particle age — overrides
    /// <see cref="Color0"/>/<see cref="Color1"/> when set (richer transitions: fire, magic, plasma).</summary>
    public Gradient?      ColorGradient { get; init; } = null;

    /// <summary>Optional effect spawned at each particle's <b>death</b> position — chained effects like a
    /// firework shell that bursts into stars. Keep the parent's particle count low (sub-emitters
    /// multiply systems). The child inherits the parent's spawn scale.</summary>
    public EmitterConfig? SubEmitter { get; init; } = null;
}
