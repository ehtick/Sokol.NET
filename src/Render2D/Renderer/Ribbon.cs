using System;
using Sokol.GUI;

namespace Sokol.Render2D;

/// <summary>A silky connected trail that follows a moving head point — a ring buffer of recent
/// positions rendered as a tapering, fading triangle strip (comet tails, light streaks, swooshes,
/// avatar trails). Push the head each frame, then Render into a <see cref="Render2DSurface"/>'s scene
/// batch. Unlike line-trail particles (discrete sprites), a ribbon is one continuous smooth band.</summary>
public sealed class Ribbon
{
    readonly Vector2[] _pts;
    int   _count;
    readonly float _minDistSq;

    public Ribbon(int maxPoints = 40, float minDistance = 5f)
    {
        _pts = new Vector2[Math.Max(2, maxPoints)];
        _minDistSq = minDistance * minDistance;
    }

    public void Clear() => _count = 0;

    /// <summary>Record the current head position. <c>_pts[0]</c> always tracks the smooth head; a new
    /// trail point is committed once the head has moved <see cref="_minDistSq"/> from the last committed
    /// point (<c>_pts[1]</c>) — measuring against the committed point, not the per-frame-updated head, is
    /// what lets the trail actually grow.</summary>
    public void Push(Vector2 head)
    {
        if (_count < 2)   // bootstrap a head + one committed point
        {
            for (int i = Math.Min(_count, _pts.Length - 1); i > 0; i--) _pts[i] = _pts[i - 1];
            _pts[0] = head;
            if (_count < _pts.Length) _count++;
            return;
        }
        Vector2 d = head - _pts[1];
        if (d.X * d.X + d.Y * d.Y >= _minDistSq)   // far enough → promote the head to a committed point
        {
            for (int i = Math.Min(_count, _pts.Length - 1); i > 0; i--) _pts[i] = _pts[i - 1];
            if (_count < _pts.Length) _count++;
        }
        _pts[0] = head;   // always follow the smooth head
    }

    /// <summary>Render as a tapering, fading strip (head→tail). <c>(pt + offset) * scale</c> maps the
    /// stored points into the surface's space (e.g. a scrolled gallery's physical px).</summary>
    public void Render(Render2DSurface s, UIColor head, UIColor tail, float width, float scale = 1f, Vector2 offset = default)
    {
        if (_count < 2) return;
        for (int i = 0; i < _count - 1; i++)
        {
            float t0 = (float)i / (_count - 1), t1 = (float)(i + 1) / (_count - 1);
            Vector2 a = (_pts[i] + offset) * scale, b = (_pts[i + 1] + offset) * scale;
            Vector2 dir = a - b;
            float len = MathF.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
            if (len < 0.001f) continue;
            Vector2 perp = new Vector2(-dir.Y, dir.X) / len;
            float w0 = width * scale * (1f - t0), w1 = width * scale * (1f - t1);
            Vector2 l0 = a + perp * w0, r0 = a - perp * w0, l1 = b + perp * w1, r1 = b - perp * w1;
            UIColor c0 = UIColor.Lerp(head, tail, t0), c1 = UIColor.Lerp(head, tail, t1);
            s.FillTriVc(l0, c0, r0, c0, r1, c1);
            s.FillTriVc(l0, c0, r1, c1, l1, c1);
        }
    }
}
