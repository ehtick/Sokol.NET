using System;
using Sokol.GUI;
using static Sokol.STM;

namespace Sokol.Render2D.Particles;

/// <summary>A transparent, non-interactive overlay widget that drives a <see cref="ParticleLayer"/>:
/// it advances the sim with a frame-rate-independent clock and renders it in the board's NanoVG pass
/// (design §8). Placed above the board but below the chrome menu, sized to fill, so effect coordinates
/// are in the same logical-px space as the board. Input falls straight through to the board beneath.</summary>
public sealed class ParticleHost : Widget
{
    readonly ParticleLayer _layer;
    double _last;   // stm seconds of the previous frame; 0 = not started

    public ParticleHost(ParticleLayer layer)
    {
        _layer = layer;
        Expand = true;
    }

    // Non-interactive: never claim a tap, so the board below receives every gesture.
    public override bool HitTest(Vector2 localPoint) => false;

    public override void Draw(Renderer r)
    {
        if (!Visible) return;

        // Self-clocked: dt comes from the monotonic timer (clamped, so a stall/resume can't teleport
        // particles) rather than the parameterless screen Tick. Deriving dt from the clock also makes
        // this robust to being drawn more than once per frame (the second dt ≈ 0).
        double now = stm_sec(stm_now());
        float  dt  = _last <= 0.0 ? 0f : (float)Math.Min(now - _last, 0.05);
        _last = now;

        _layer.Update(dt);
        _layer.Render(r);
    }
}
