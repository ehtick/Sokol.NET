using Sokol.GUI;

namespace Sokol.Render2D.Particles;

/// <summary>One simulated particle — a kinematic point with size/colour-over-life, kept in a pooled
/// array (array-of-structs; cache-friendly at our counts). Dead slots are recycled by the owning
/// <see cref="ParticleSystem"/>. All positions/sizes are in logical pixels (the board's space).</summary>
public struct Particle
{
    public Vector2 Pos, Vel;        // logical px, px/s
    public float   Age, Life;       // seconds; dead when Age >= Life
    public float   Size0, Size1;    // start/end radius (or half-extent / line width); lerped by life
    public float   Rotation;        // radians (triangle/debris)
    public float   AngularVel;      // radians/s
    public UIColor Color0, Color1;  // start→end tint; alpha drives the fade
    public float   Seed;            // per-particle 0..1 for cheap variation
    public int     Frame;           // sprite-sheet cell (static-random; animated frames derive from T)
    public bool    Alive;

    /// <summary>Normalised age in 0..1 used to lerp size/colour.</summary>
    public readonly float T => Life > 0f ? Age / Life : 1f;

    /// <summary>Current radius / half-extent, interpolated from <see cref="Size0"/>→<see cref="Size1"/>.</summary>
    public readonly float Size => Size0 + (Size1 - Size0) * T;

    /// <summary>Current tint, interpolated from <see cref="Color0"/>→<see cref="Color1"/> (alpha included).</summary>
    public readonly UIColor Color => UIColor.Lerp(Color0, Color1, T);
}
