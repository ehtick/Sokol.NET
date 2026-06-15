using System;
using Sokol.GUI;

namespace Sokol.Render2D.Particles;

/// <summary>A per-frame, per-particle modifier (design §5.5) — composes on top of the built-in
/// gravity/drag integration to shape motion. Built-ins below cover the M3 set; effects list 1–3 of
/// them on their <see cref="EmitterConfig.Affectors"/>. Affectors are stateless and shared across a
/// preset's spawns, so they apply forces by mutating <see cref="Particle.Vel"/> in place.</summary>
public interface IAffector
{
    /// <summary><paramref name="origin"/> is the emitter's current position (logical px) — lets an
    /// affector be relative to a moving emitter (e.g. <see cref="Vortex"/>); ignore it otherwise.</summary>
    void Apply(ref Particle p, float dt, Vector2 origin);
}

/// <summary>A constant directional acceleration (px/s²) — a steady breeze that bends every particle.</summary>
public sealed class Wind : IAffector
{
    readonly Vector2 _accel;
    public Wind(Vector2 accel) => _accel = accel;
    public void Apply(ref Particle p, float dt, Vector2 origin) => p.Vel += _accel * dt;
}

/// <summary>A swirly sine "noise" field for wispy smoke. Position-coherent (nearby particles bend
/// together) and evolves as the particle drifts + ages, with a per-particle phase from
/// <see cref="Particle.Seed"/> so they don't all align. <paramref name="strength"/> is the force
/// amplitude (px/s²); <paramref name="frequency"/> sets the spatial scale of the swirls.</summary>
public sealed class Turbulence : IAffector
{
    const float Tau = MathF.PI * 2f;
    readonly float _strength, _freq;

    public Turbulence(float strength, float frequency = 0.02f)
    {
        _strength = strength;
        _freq     = frequency;
    }

    public void Apply(ref Particle p, float dt, Vector2 origin)
    {
        float ph = p.Seed * Tau;
        float ax = MathF.Sin(p.Pos.Y * _freq + ph) + MathF.Sin(p.Pos.X * _freq * 0.7f + p.Age * 2f + ph);
        float ay = MathF.Cos(p.Pos.X * _freq + ph * 1.3f) + MathF.Cos(p.Pos.Y * _freq * 0.6f + p.Age * 1.7f);
        p.Vel += new Vector2(ax, ay) * (_strength * dt);
    }
}

/// <summary>Pull toward / push away from a point, with an optional tangential component for a vortex.
/// <paramref name="radial"/> &gt; 0 repels (explosion shockwave), &lt; 0 attracts (implosion);
/// <paramref name="tangential"/> adds swirl (vortex). Centre is in the layer's coordinate space.</summary>
public sealed class RadialForce : IAffector
{
    readonly Vector2 _center;
    readonly float   _radial, _tangential;

    public RadialForce(Vector2 center, float radial, float tangential = 0f)
    {
        _center     = center;
        _radial     = radial;
        _tangential = tangential;
    }

    public void Apply(ref Particle p, float dt, Vector2 origin)
    {
        Vector2 d = p.Pos - _center;
        float len = d.Length;
        if (len < 0.001f) return;
        Vector2 dir = d / len;
        Vector2 tan = new(-dir.Y, dir.X);
        p.Vel += (dir * _radial + tan * _tangential) * dt;
    }
}

/// <summary>Divergence-free "curl noise" — swirly, organic motion with no sources or sinks, so it reads
/// more naturally than the sine <see cref="Turbulence"/> (fire licks, smoke curls, magic eddies). A
/// cheap 2D approximation: the curl of a time-evolving sine potential, with a per-particle phase from
/// <see cref="Particle.Seed"/>. <paramref name="strength"/> = force amplitude (px/s²),
/// <paramref name="frequency"/> = spatial scale, <paramref name="speed"/> = how fast the field churns.</summary>
public sealed class CurlNoise : IAffector
{
    const float Tau = MathF.PI * 2f;
    readonly float _strength, _freq, _speed;

    public CurlNoise(float strength, float frequency = 0.012f, float speed = 0.7f)
    {
        _strength = strength;
        _freq     = frequency;
        _speed    = speed;
    }

    public void Apply(ref Particle p, float dt, Vector2 origin)
    {
        float t = p.Age * _speed + p.Seed * Tau;
        float x = p.Pos.X * _freq, y = p.Pos.Y * _freq;
        // curl of the scalar potential φ = sin(x+t)·cos(y−t)  ⇒  v = (∂φ/∂y, −∂φ/∂x), divergence-free.
        float vx = -MathF.Sin(x + t) * MathF.Sin(y - t);
        float vy = -MathF.Cos(x + t) * MathF.Cos(y - t);
        p.Vel += new Vector2(vx, vy) * (_strength * dt);
    }
}

/// <summary>Emitter-relative swirl + pull — orbits particles around (and optionally draws them toward
/// or pushes them from) the emitter's <b>current</b> origin, so it follows a moving emitter, unlike
/// <see cref="RadialForce"/>'s fixed centre. Great for vortexes, galaxies, and black-hole pulls.
/// <paramref name="swirl"/> = tangential strength (1/s; sign sets direction), <paramref name="pull"/>
/// = radial strength (1/s; &gt;0 inward, &lt;0 outward).</summary>
public sealed class Vortex : IAffector
{
    readonly float _swirl, _pull;
    public Vortex(float swirl, float pull = 0f) { _swirl = swirl; _pull = pull; }

    public void Apply(ref Particle p, float dt, Vector2 origin)
    {
        Vector2 d = p.Pos - origin;
        float len = d.Length;
        if (len < 0.001f) return;
        Vector2 dir = d / len;
        Vector2 tan = new(-dir.Y, dir.X);
        // scale with radius for an orbital feel (outer particles sweep faster, inner ones tighten).
        p.Vel += (tan * (_swirl * len) - dir * (_pull * len)) * dt;
    }
}
