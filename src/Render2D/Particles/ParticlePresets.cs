using System;
using Sokol.GUI;

namespace Sokol.Render2D.Particles;

/// <summary>Built-in effects as ready-made <see cref="EmitterConfig"/> values (design §9). Immutable
/// and shared — passing one to <see cref="ParticleLayer.Spawn(EmitterConfig, Vector2, float)"/> never
/// allocates. M1 = Explosion/Sparks (dynamic); M2 adds Smoke + RocketThrust (textured).</summary>
public static class ParticlePresets
{
    /// <summary>Bundled sprite asset paths (copied to the output by the <c>Assets/**</c> glob).</summary>
    public const string SmokeTexture = "particles/smoke.png";
    public const string FlameTexture = "particles/flame.svg";
    public const string BlastTexture = "particles/blast_sheet.png";   // 4×4 flipbook

    // ── shared colour ramps (used by the gradient presets) ───────────────────────
    static readonly Gradient FireRamp = new(
        (0f,    UIColor.FromHex("#4A0C00")), (0.22f, UIColor.FromHex("#E03000")),
        (0.5f,  UIColor.FromHex("#FF8A1E")), (0.78f, UIColor.FromHex("#FFE07A")),
        (1f,    UIColor.FromHex("#FFF6C000")));
    static readonly Gradient SparkRamp = new(
        (0f, UIColor.FromHex("#FFFFFF")), (0.3f, UIColor.FromHex("#FFE27A")),
        (0.65f, UIColor.FromHex("#FF5CC8")), (1f, UIColor.FromHex("#7A3CFF00")));

    /// <summary>A warm one-shot fireball: a fast outward burst of additive glows fading orange→dark,
    /// with light gravity + drag so it puffs and settles. Pair with <see cref="Sparks"/>.</summary>
    public static readonly EmitterConfig Explosion = new()
    {
        Mode         = EmitMode.Burst,
        BurstCount   = 56,
        Shape        = EmitShape.Disc,
        ShapeRadius  = 6f,
        Life         = (0.35f, 0.7f),
        Speed        = (120f, 420f),
        StartSize    = (10f, 22f),
        EndSize      = (0f, 4f),
        Color0       = UIColor.FromHex("#FFE08A"),     // warm core
        Color1       = UIColor.FromHex("#E0401000"),   // → dark red, alpha 0 (fades out)
        Gravity      = new Vector2(0f, 40f),
        Drag         = 4f,
        Blend        = BlendMode.Additive,
        Visual       = ParticleShape.Glow,
        MaxParticles = 64,
    };

    /// <summary>Bright, thin gravity-pulled tracers thrown out from the impact point — short-lived
    /// line trails that arc and fall.</summary>
    public static readonly EmitterConfig Sparks = new()
    {
        Mode         = EmitMode.Burst,
        BurstCount   = 24,
        Shape        = EmitShape.Point,
        Life         = (0.2f, 0.45f),
        Speed        = (200f, 520f),
        StartSize    = (2f, 3.5f),       // line width
        EndSize      = (0.5f, 1f),
        Color0       = UIColor.FromHex("#FFF2C0"),
        Color1       = UIColor.FromHex("#FF7A1E00"),
        Gravity      = new Vector2(0f, 600f),
        Drag         = 0.5f,
        Blend        = BlendMode.Additive,
        Visual       = ParticleShape.LineTrail,
        TrailScale   = 0.025f,
        MaxParticles = 28,
    };

    /// <summary>Lingering grey smoke puff — a textured one-shot that drifts up (buoyant), expands, and
    /// fades over a couple of seconds. Pairs with <see cref="Explosion"/> over a wreck.</summary>
    public static readonly EmitterConfig Smoke = new()
    {
        Mode         = EmitMode.Burst,
        BurstCount   = 14,
        Shape        = EmitShape.Disc,
        ShapeRadius  = 8f,
        Life         = (1.4f, 2.6f),
        Speed        = (10f, 40f),
        StartSize    = (14f, 20f),
        EndSize      = (40f, 64f),                     // expands as it fades
        Color0       = UIColor.FromHex("#9A9A9AC0"),   // grey, alpha ~0.75 (texture is alpha-modulated)
        Color1       = UIColor.FromHex("#6E6E6E00"),   // → alpha 0
        Gravity      = new Vector2(0f, -22f),          // buoyant (drifts up)
        Drag         = 0.6f,
        Blend        = BlendMode.Normal,
        Visual       = ParticleShape.Texture,
        TextureKey   = SmokeTexture,
        Affectors    = new IAffector[] { new Turbulence(40f) },   // wisps as it rises
        MaxParticles = 20,
    };

    /// <summary>Rocket/cannonball engine flame — a continuous, body-following additive stream emitted
    /// opposite the body's velocity in a tight cone, very short-lived so it forms a tail. Spawn it,
    /// then each frame set <see cref="ParticleSystem.Origin"/> = body pos and
    /// <see cref="ParticleSystem.Direction"/> = −velocity; call <see cref="ParticleSystem.Stop"/> when done.</summary>
    public static readonly EmitterConfig RocketThrust = new()
    {
        Mode          = EmitMode.Continuous,
        RatePerSecond = 90f,
        Duration      = 0f,                            // until Stop()
        Shape         = EmitShape.Point,
        Directional   = true,
        ConeHalfAngle = 15f * (MathF.PI / 180f),
        Life          = (0.12f, 0.28f),                // short → forms a tail
        Speed         = (40f, 120f),                   // opposite the body (Direction set per-frame)
        StartSize     = (6f, 12f),
        EndSize       = (1f, 3f),
        Color0        = UIColor.White,                 // texture is alpha-modulated; flame.svg carries the colour
        Color1        = UIColor.White.WithAlpha(0f),
        Drag          = 1f,
        Blend         = BlendMode.Additive,
        Visual        = ParticleShape.Texture,
        TextureKey    = FlameTexture,
        MaxParticles  = 64,
    };

    /// <summary>A textured fireball blast driven by a 4×4 sprite-sheet flipbook (<see cref="BlastTexture"/>):
    /// a few large additive quads, each randomly rotated, that play the explosion animation once over
    /// their life. A richer alternative/companion to the dynamic <see cref="Explosion"/>.</summary>
    public static readonly EmitterConfig Blast = new()
    {
        Mode          = EmitMode.Burst,
        BurstCount    = 5,
        Shape         = EmitShape.Disc,
        ShapeRadius   = 8f,
        Life          = (0.45f, 0.7f),                 // ≈ the flipbook's play time
        Speed         = (8f, 50f),
        StartSize     = (26f, 40f),
        EndSize       = (32f, 52f),                    // grows slightly; the sheet does the fade
        StartRotation = (0f, MathF.PI * 2f),
        Color0        = UIColor.White,                 // alpha 1 throughout — the sheet's late frames fade
        Color1        = UIColor.White,
        Blend         = BlendMode.Additive,
        Visual        = ParticleShape.Texture,
        TextureKey    = BlastTexture,
        SheetCols     = 4,
        SheetRows     = 4,
        SheetAnimate  = true,
        MaxParticles  = 8,
    };

    /// <summary>A continuous glow trail left behind a moving avatar — body-following: set
    /// <see cref="ParticleSystem.Origin"/> to the avatar each frame and <see cref="ParticleSystem.Direction"/>
    /// opposite its travel so the glow streams out behind it.</summary>
    public static readonly EmitterConfig Trail = new()
    {
        Mode          = EmitMode.Continuous,
        RatePerSecond = 80f,
        Duration      = 0f,                            // until Stop()
        Shape         = EmitShape.Point,
        Directional   = true,
        ConeHalfAngle = 0.6f,
        Life          = (0.22f, 0.5f),
        Speed         = (20f, 70f),
        StartSize     = (4f, 7f),
        EndSize       = (0f, 1.5f),
        Color0        = UIColor.FromHex("#6FE6FF"),    // cyan glow
        Color1        = UIColor.FromHex("#2A6CFF00"),
        Drag          = 1.5f,
        Blend         = BlendMode.Additive,
        Visual        = ParticleShape.Glow,
        MaxParticles  = 72,
    };

    /// <summary>A celebratory burst of spinning triangular confetti (dynamic, full colour tint). Spawn
    /// a few with different <c>Color0</c> for a multi-hue pop.</summary>
    public static readonly EmitterConfig Confetti = new()
    {
        Mode          = EmitMode.Burst,
        BurstCount    = 44,
        Shape         = EmitShape.Disc,
        ShapeRadius   = 24f,
        Life          = (1.1f, 2.0f),
        Speed         = (140f, 380f),
        StartSize     = (5f, 9f),
        EndSize       = (4f, 7f),
        StartRotation = (0f, MathF.PI * 2f),
        AngularVel    = (-8f, 8f),
        Color0        = UIColor.FromHex("#FFD54A"),
        Color1        = UIColor.FromHex("#FFD54A00"),
        Gravity       = new Vector2(0f, 260f),
        Drag          = 1.2f,
        Blend         = BlendMode.Normal,
        Visual        = ParticleShape.Triangle,
        MaxParticles  = 50,
    };

    /// <summary>Fast additive horizontal streaks for high-speed segments — reposition
    /// <see cref="ParticleSystem.Origin"/> across the right edge each frame and set Direction left.</summary>
    public static readonly EmitterConfig SpeedLines = new()
    {
        Mode          = EmitMode.Continuous,
        RatePerSecond = 55f,
        Duration      = 0f,
        Shape         = EmitShape.Point,
        Directional   = true,
        ConeHalfAngle = 0.05f,
        Life          = (0.22f, 0.42f),
        Speed         = (700f, 1100f),
        StartSize     = (1.5f, 3f),
        EndSize       = (0.5f, 1f),
        Color0        = UIColor.FromHex("#EAF4FF"),
        Color1        = UIColor.FromHex("#9FCBFF00"),
        Blend         = BlendMode.Additive,
        Visual        = ParticleShape.LineTrail,
        TrailScale    = 0.03f,
        MaxParticles  = 48,
    };

    /// <summary>Gentle drifting ambient motes (themed: tint via <c>with</c>). Spawn once across the
    /// view (Disc, scale ≈ screen) and keep its Origin on the view centre; Turbulence makes it wisp.</summary>
    public static readonly EmitterConfig Ambient = new()
    {
        Mode          = EmitMode.Continuous,
        RatePerSecond = 16f,
        Duration      = 0f,
        Shape         = EmitShape.Disc,
        ShapeRadius   = 1f,
        Life          = (2.6f, 5f),
        Speed         = (3f, 16f),
        StartSize     = (2f, 5f),
        EndSize       = (1f, 3f),
        Color0        = UIColor.FromHex("#FFFFFF"),
        Color1        = UIColor.FromHex("#FFFFFF00"),
        Gravity       = new Vector2(0f, -10f),
        Drag          = 0.4f,
        Blend         = BlendMode.Additive,
        Visual        = ParticleShape.Glow,
        Affectors     = new IAffector[] { new Turbulence(14f), new Wind(new Vector2(8f, 0f)) },
        MaxParticles  = 64,
    };

    /// <summary>A small additive sparkle for collecting a pickup.</summary>
    public static readonly EmitterConfig Pickup = new()
    {
        Mode         = EmitMode.Burst,
        BurstCount   = 14,
        Shape        = EmitShape.Disc,
        ShapeRadius  = 4f,
        Life         = (0.3f, 0.6f),
        Speed        = (40f, 160f),
        StartSize    = (3f, 6f),
        EndSize      = (0f, 1f),
        Color0       = UIColor.FromHex("#FFE27A"),
        Color1       = UIColor.FromHex("#FFB02E00"),
        Drag         = 3f,
        Blend        = BlendMode.Additive,
        Visual       = ParticleShape.Glow,
        MaxParticles = 18,
    };

    // ── Variety pack (showcased in the Particle Gallery) ──────────────────────────

    /// <summary>A rising, flickering flame — buoyant additive glows in a cone, wisped by turbulence.</summary>
    public static readonly EmitterConfig Fire = new()
    {
        Mode = EmitMode.Continuous, RatePerSecond = 130f, Shape = EmitShape.Disc, ShapeRadius = 11f,
        Directional = true, ConeHalfAngle = 0.5f, Life = (0.4f, 0.9f), Speed = (40f, 120f),
        StartSize = (10f, 18f), EndSize = (0f, 4f),
        ColorGradient = FireRamp,                      // dark-red → red → orange → yellow → fade
        Gravity = new Vector2(0f, -120f), Drag = 1.4f, Blend = BlendMode.Additive, Visual = ParticleShape.Glow,
        Affectors = new IAffector[] { new CurlNoise(90f) }, MaxParticles = 220,   // organic flame licks
    };

    /// <summary>A water fountain — a tight upward jet that arcs back down under gravity.</summary>
    public static readonly EmitterConfig Fountain = new()
    {
        Mode = EmitMode.Continuous, RatePerSecond = 170f, Shape = EmitShape.Point,
        Directional = true, ConeHalfAngle = 0.26f, Life = (0.9f, 1.5f), Speed = (260f, 360f),
        StartSize = (4f, 7f), EndSize = (1f, 3f),
        Color0 = UIColor.FromHex("#9FE8FF"), Color1 = UIColor.FromHex("#2A6CFF00"),
        Gravity = new Vector2(0f, 520f), Drag = 0.1f, Blend = BlendMode.Additive, Visual = ParticleShape.Glow,
        MaxParticles = 340,
    };

    /// <summary>A firework pop — an outward burst of twinkling stars that fall + spin.</summary>
    public static readonly EmitterConfig Fireworks = new()
    {
        Mode = EmitMode.Burst, BurstCount = 60, Shape = EmitShape.Point, Life = (0.7f, 1.3f), Speed = (150f, 340f),
        StartSize = (5f, 10f), EndSize = (0f, 2f), StartRotation = (0f, MathF.PI * 2f), AngularVel = (-3f, 3f),
        Color0 = UIColor.FromHex("#FFE27A"), Color1 = UIColor.FromHex("#FF3CA000"),
        Gravity = new Vector2(0f, 170f), Drag = 1.2f, Blend = BlendMode.Additive, Visual = ParticleShape.Star,
        MaxParticles = 64,
    };

    /// <summary>Swirling magic sparkles — spinning stars drifting up, wisped by turbulence.</summary>
    public static readonly EmitterConfig MagicSparkle = new()
    {
        Mode = EmitMode.Continuous, RatePerSecond = 75f, Shape = EmitShape.Disc, ShapeRadius = 28f,
        Life = (0.5f, 1.2f), Speed = (10f, 60f), StartSize = (4f, 9f), EndSize = (0f, 2f),
        StartRotation = (0f, MathF.PI * 2f), AngularVel = (-4f, 4f),
        Color0 = UIColor.FromHex("#E0B0FF"), Color1 = UIColor.FromHex("#5AC8FF00"),
        Gravity = new Vector2(0f, -20f), Drag = 1.2f, Blend = BlendMode.Additive, Visual = ParticleShape.Star,
        Affectors = new IAffector[] { new Turbulence(34f) }, MaxParticles = 170,
    };

    /// <summary>Falling rain — a wide top curtain of fast downward streaks (<see cref="EmitShape.Line"/>;
    /// the gallery widens <c>ShapeRadius</c> to the view). Position the Origin along the top edge.</summary>
    public static readonly EmitterConfig Rain = new()
    {
        Mode = EmitMode.Continuous, RatePerSecond = 230f, Shape = EmitShape.Line, ShapeRadius = 200f,
        Life = (0.5f, 0.8f), Speed = (0f, 0f), StartSize = (1.5f, 2.5f), EndSize = (1f, 2f),
        Color0 = UIColor.FromHex("#BFE0FF"), Color1 = UIColor.FromHex("#7FB0FF80"),
        Gravity = new Vector2(40f, 1400f), Blend = BlendMode.Normal, Visual = ParticleShape.LineTrail, TrailScale = 0.05f,
        MaxParticles = 300,
    };

    /// <summary>Drifting snow — a wide top curtain of slow tumbling flakes (turbulence + wind).</summary>
    public static readonly EmitterConfig Snow = new()
    {
        Mode = EmitMode.Continuous, RatePerSecond = 95f, Shape = EmitShape.Line, ShapeRadius = 200f,
        Life = (2.0f, 3.5f), Speed = (10f, 30f), StartSize = (3f, 6f), EndSize = (3f, 6f),
        Color0 = UIColor.FromHex("#FFFFFF"), Color1 = UIColor.FromHex("#FFFFFFA0"),
        Gravity = new Vector2(0f, 60f), Drag = 0.8f, Blend = BlendMode.Normal, Visual = ParticleShape.Disc,
        Affectors = new IAffector[] { new Turbulence(40f), new Wind(new Vector2(20f, 0f)) }, MaxParticles = 240,
    };

    /// <summary>Rising bubbles — hollow rings that float up and wobble.</summary>
    public static readonly EmitterConfig Bubbles = new()
    {
        Mode = EmitMode.Continuous, RatePerSecond = 26f, Shape = EmitShape.Disc, ShapeRadius = 32f,
        Life = (1.4f, 2.6f), Speed = (8f, 24f), StartSize = (6f, 16f), EndSize = (8f, 20f),
        Color0 = UIColor.FromHex("#BFEFFFB0"), Color1 = UIColor.FromHex("#BFEFFF00"),
        Gravity = new Vector2(0f, -50f), Drag = 0.5f, Blend = BlendMode.Normal, Visual = ParticleShape.Ring,
        Affectors = new IAffector[] { new Turbulence(20f) }, MaxParticles = 80,
    };

    /// <summary>Floating embers — small glowing sparks rising from a fire, carried by turbulence + wind.</summary>
    public static readonly EmitterConfig Embers = new()
    {
        Mode = EmitMode.Continuous, RatePerSecond = 52f, Shape = EmitShape.Disc, ShapeRadius = 24f,
        Life = (0.8f, 1.8f), Speed = (10f, 50f), StartSize = (2f, 5f), EndSize = (0f, 1.5f),
        Color0 = UIColor.FromHex("#FFB24A"), Color1 = UIColor.FromHex("#FF3C0000"),
        Gravity = new Vector2(0f, -60f), Drag = 0.6f, Blend = BlendMode.Additive, Visual = ParticleShape.Glow,
        Affectors = new IAffector[] { new Turbulence(45f), new Wind(new Vector2(15f, 0f)) }, MaxParticles = 120,
    };

    /// <summary>A quick twinkle pop — stars that flash in place and fade.</summary>
    public static readonly EmitterConfig Twinkle = new()
    {
        Mode = EmitMode.Burst, BurstCount = 26, Shape = EmitShape.Disc, ShapeRadius = 30f,
        Life = (0.5f, 1.1f), Speed = (0f, 20f), StartSize = (3f, 8f), EndSize = (0f, 1f),
        StartRotation = (0f, MathF.PI * 2f), Color0 = UIColor.FromHex("#FFF6C0"), Color1 = UIColor.FromHex("#FFD54A00"),
        Drag = 2f, Blend = BlendMode.Additive, Visual = ParticleShape.Star, MaxParticles = 30,
    };

    /// <summary>The star burst a firework shell releases on death (white→gold→pink stars that fall).</summary>
    public static readonly EmitterConfig FireworkBurst = new()
    {
        Mode = EmitMode.Burst, BurstCount = 54, Shape = EmitShape.Point, Life = (0.6f, 1.2f), Speed = (130f, 300f),
        StartSize = (4f, 9f), EndSize = (0f, 1.5f), StartRotation = (0f, MathF.PI * 2f), AngularVel = (-4f, 4f),
        ColorGradient = SparkRamp, Gravity = new Vector2(0f, 200f), Drag = 1.3f,
        Blend = BlendMode.Additive, Visual = ParticleShape.Star, MaxParticles = 64,
    };

    /// <summary>A firework — a glowing shell that rises, then sub-emitter-bursts into
    /// <see cref="FireworkBurst"/> stars at its apex (the headline sub-emitter showcase).</summary>
    public static readonly EmitterConfig Firework = new()
    {
        Mode = EmitMode.Burst, BurstCount = 1, Shape = EmitShape.Point, Directional = true, ConeHalfAngle = 0.12f,
        Life = (0.7f, 0.95f), Speed = (320f, 400f), StartSize = (5f, 8f), EndSize = (3f, 5f),
        Color0 = UIColor.FromHex("#FFF2C0"), Color1 = UIColor.FromHex("#FFB04A"),
        Gravity = new Vector2(0f, 230f), Drag = 0.4f, Blend = BlendMode.Additive, Visual = ParticleShape.Glow,
        SubEmitter = FireworkBurst, MaxParticles = 4,
    };

    /// <summary>A swirling galaxy — stars spawned on a ring, orbited and drawn inward by an
    /// emitter-relative <see cref="Vortex"/> (so it follows the emitter), spiralling into the centre.</summary>
    public static readonly EmitterConfig Galaxy = new()
    {
        Mode = EmitMode.Continuous, RatePerSecond = 110f, Shape = EmitShape.Ring, ShapeRadius = 70f,
        Life = (1.6f, 3.2f), Speed = (0f, 8f), StartSize = (3f, 7f), EndSize = (0f, 1.5f),
        StartRotation = (0f, MathF.PI * 2f), ColorGradient = SparkRamp,
        Blend = BlendMode.Additive, Visual = ParticleShape.Star,
        Affectors = new IAffector[] { new Vortex(swirl: 2.0f, pull: 0.35f) }, MaxParticles = 320,
    };
}
