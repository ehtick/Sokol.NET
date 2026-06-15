using System;
using Sokol.GUI;

namespace Sokol.Render2D.Particles.Rendering;

/// <summary>Default backend (design §7.2): draws each particle through the existing
/// <see cref="Sokol.GUI.Renderer"/> (NanoVG) so effects composite in the board's own frame with the
/// right z-order and no extra pass. Maps each <see cref="ParticleShape"/> to primitive calls.</summary>
public sealed class GuiParticleRenderer : IParticleRenderer
{
    readonly ParticleTextureCache? _textures;

    public GuiParticleRenderer(ParticleTextureCache? textures = null) => _textures = textures;

    public void Begin(Renderer r, EmitterConfig cfg) => r.SetBlend(cfg.Blend);

    // Reset to Normal so the chrome menu / status pill that draw after the particles are unaffected.
    public void End(Renderer r) => r.SetBlend(BlendMode.Normal);

    public void Draw(Renderer r, in Particle p, EmitterConfig cfg)
    {
        float size = p.Size;
        UIColor c  = cfg.ColorGradient is { } grad ? grad.Sample(p.T) : p.Color;
        if (c.A <= 0.003f) return;

        switch (cfg.Visual)
        {
            case ParticleShape.Glow:
                if (size <= 0.1f) return;
                var paint = r.RadialGradient(p.Pos, 0f, size, c, c.WithAlpha(0f));
                r.FillCircleWithPaint(p.Pos.X, p.Pos.Y, size, paint);
                break;

            case ParticleShape.Disc:
                if (size <= 0.1f) return;
                r.FillCircle(p.Pos.X, p.Pos.Y, size, c);
                break;

            case ParticleShape.Triangle:
                if (size <= 0.1f) return;
                r.Save();
                r.Translate(p.Pos);
                r.Rotate(p.Rotation);
                r.FillTriangle(
                    new Vector2(0f, -size),
                    new Vector2(size * 0.866f, size * 0.5f),
                    new Vector2(-size * 0.866f, size * 0.5f),
                    c);
                r.Restore();
                break;

            case ParticleShape.LineTrail:
                Vector2 tail = p.Pos - p.Vel * cfg.TrailScale;
                r.DrawLine(p.Pos.X, p.Pos.Y, tail.X, tail.Y, MathF.Max(1f, size), c);
                break;

            case ParticleShape.Ring:
                if (size <= 0.1f) return;
                r.StrokeCircle(p.Pos.X, p.Pos.Y, size, MathF.Max(1.5f, size * 0.22f), c);
                break;

            case ParticleShape.Star:
                // NanoVG fallback: a soft glow (the SG backend renders a true 5-point star).
                if (size <= 0.1f) return;
                var starPaint = r.RadialGradient(p.Pos, 0f, size, c, c.WithAlpha(0f));
                r.FillCircleWithPaint(p.Pos.X, p.Pos.Y, size, starPaint);
                break;

            case ParticleShape.Texture:
                if (size <= 0.1f) return;
                var img = _textures?.Get(cfg.TextureKey);
                if (img is not { IsValid: true }) return;   // still loading / missing → skip this frame
                int frames = Math.Max(1, cfg.SheetCols * cfg.SheetRows);
                int frame  = cfg.SheetAnimate ? Math.Clamp((int)(p.T * frames), 0, frames - 1) : p.Frame;
                // NanoVG modulates a sprite by alpha only (no per-particle tint), so pass the faded
                // alpha; rotation comes from the transform (image-pattern angle is fixed at 0).
                r.Save();
                r.Translate(p.Pos);
                r.Rotate(p.Rotation);
                r.DrawImageFrame(img, new Rect(-size, -size, size * 2f, size * 2f), cfg.SheetCols, cfg.SheetRows, frame, c.A);
                r.Restore();
                break;
        }
    }
}
