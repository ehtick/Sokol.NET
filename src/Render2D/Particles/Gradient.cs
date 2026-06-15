using System;
using Sokol.GUI;

namespace Sokol.Render2D.Particles;

/// <summary>A multi-stop colour ramp sampled by a particle's normalised age (0..1). Set it on
/// <see cref="EmitterConfig.ColorGradient"/> to replace the simple <c>Color0→Color1</c> lerp with
/// richer transitions (e.g. fire: dark-red → red → orange → yellow → fade). Immutable and shared —
/// build once as a <c>static readonly</c>.</summary>
public sealed class Gradient
{
    readonly float[]   _t;
    readonly UIColor[] _c;

    public Gradient(params (float T, UIColor C)[] stops)
    {
        Array.Sort(stops, static (a, b) => a.T.CompareTo(b.T));
        _t = new float[stops.Length];
        _c = new UIColor[stops.Length];
        for (int i = 0; i < stops.Length; i++) { _t[i] = stops[i].T; _c[i] = stops[i].C; }
    }

    /// <summary>Colour at normalised age <paramref name="t"/> (clamped, linearly interpolated).</summary>
    public UIColor Sample(float t)
    {
        int n = _t.Length;
        if (n == 0) return UIColor.White;
        if (t <= _t[0])     return _c[0];
        if (t >= _t[n - 1]) return _c[n - 1];
        for (int i = 1; i < n; i++)
            if (t <= _t[i])
            {
                float f = (t - _t[i - 1]) / MathF.Max(1e-5f, _t[i] - _t[i - 1]);
                return UIColor.Lerp(_c[i - 1], _c[i], f);
            }
        return _c[n - 1];
    }
}
