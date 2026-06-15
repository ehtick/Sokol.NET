using System;
using Sokol.GUI;

namespace Sokol.Render2D.Particles.Rendering;

/// <summary>GPU-instanced particle backend (design §7.4): the same simulation the
/// <see cref="GuiParticleRenderer"/> draws through NanoVG, emitted instead as instances into a
/// <see cref="Render2DSurface"/> (one draw per blend+texture batch, tens of thousands of particles).
/// Each <see cref="ParticleSystem"/> is homogeneous, so blend/texture/shape are per-batch.
/// <para>Particles live in the layer's logical-px space; <see cref="Configure"/>'s <c>scale</c> maps
/// them to the surface's physical-px target. Full <c>vertexColor × texture</c> lifts NanoVG's
/// alpha-only tint limit (textured particles now colour over life).</para></summary>
public sealed class SgParticleRenderer : IParticleRenderer
{
    readonly SgParticleTextureCache _tex;
    Render2DSurface _surface = null!;
    float   _scale = 1f;
    Vector2 _offset;          // logical-px pan (e.g. a scrolled gallery) applied before scaling

    int  _mode;
    bool _skip;

    public SgParticleRenderer(SgParticleTextureCache tex) => _tex = tex;

    /// <summary>Target surface, logical→physical scale, and a logical-px offset (for a scrolled view).</summary>
    public void Configure(Render2DSurface surface, float scale, Vector2 offset)
    {
        _surface = surface; _scale = scale; _offset = offset;
    }

    public void Begin(Renderer r, EmitterConfig cfg)
    {
        _skip = false;
        Sokol.SG.sg_view tex = default;
        switch (cfg.Visual)
        {
            case ParticleShape.Glow:      _mode = 0; break;
            case ParticleShape.Disc:      _mode = 1; break;
            case ParticleShape.Triangle:  _mode = 2; break;
            case ParticleShape.Ring:      _mode = 4; break;
            case ParticleShape.Star:      _mode = 5; break;
            case ParticleShape.LineTrail: _mode = 0; break;   // elongated soft glow = a streak
            case ParticleShape.Texture:
                _mode = 3;
                tex = _tex.Get(cfg.TextureKey);
                if (tex.id == 0) { _skip = true; return; }     // sprite still loading → skip this frame
                break;
        }
        _surface.BeginParticleBatch(cfg.Blend, tex);
    }

    public void Draw(Renderer r, in Particle p, EmitterConfig cfg)
    {
        if (_skip) return;
        UIColor c = cfg.ColorGradient is { } grad ? grad.Sample(p.T) : p.Color;
        if (c.A <= 0.003f) return;

        float size = p.Size * _scale;
        Vector2 pos = (p.Pos + _offset) * _scale;
        float hw, hh, rot;
        float u0 = 0f, v0 = 0f, u1 = 1f, v1 = 1f;

        switch (cfg.Visual)
        {
            case ParticleShape.LineTrail:
            {
                Vector2 vel = p.Vel * _scale;
                float   len = MathF.Sqrt(vel.X * vel.X + vel.Y * vel.Y) * cfg.TrailScale;   // tail = pos − vel·TrailScale
                hw  = MathF.Max(len, 1f) * 0.5f;
                hh  = MathF.Max(1f, size) * 0.5f;
                rot = MathF.Atan2(vel.Y, vel.X);
                pos -= vel * (cfg.TrailScale * 0.5f);          // centre the streak between head and tail
                break;
            }
            case ParticleShape.Texture:
            {
                if (size <= 0.1f) return;
                hw = hh = size; rot = p.Rotation;
                int cols = Math.Max(1, cfg.SheetCols), rows = Math.Max(1, cfg.SheetRows);
                int frames = cols * rows;
                int frame  = cfg.SheetAnimate ? Math.Clamp((int)(p.T * frames), 0, frames - 1) : p.Frame;
                int col = frame % cols, row = (frame / cols) % rows;
                u0 = (float)col / cols; u1 = (float)(col + 1) / cols;
                v0 = (float)row / rows; v1 = (float)(row + 1) / rows;
                break;
            }
            default:
                if (size <= 0.1f) return;
                hw = hh = size; rot = p.Rotation;
                break;
        }

        _surface.PushParticle(pos, hw, hh, rot, c, u0, v0, u1, v1, _mode);
    }

    public void End(Renderer r) { if (!_skip) _surface.EndParticleBatch(); }
}
