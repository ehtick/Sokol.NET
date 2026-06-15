using System;
using Sokol.GUI;

namespace Sokol.Render2D.Particles;

/// <summary>Turns an <see cref="EmitterConfig"/> + elapsed time into new particles. Owns the spawn
/// accounting (a fractional accumulator so <see cref="EmitMode.Continuous"/> is frame-rate
/// independent) and the current transform — <see cref="Origin"/> and a uniform <see cref="Scale"/>
/// applied to every spawned size/speed/offset so one preset fits any board cell size.</summary>
public sealed class Emitter
{
    readonly EmitterConfig _cfg;
    float _age;
    bool  _bursted;
    bool  _stopped;
    float _accum;   // Continuous: carries the fractional remainder between frames

    public Emitter(EmitterConfig cfg) => _cfg = cfg;

    /// <summary>World position new particles spawn around (logical px).</summary>
    public Vector2 Origin { get; set; }

    /// <summary>Emission direction for a <see cref="EmitterConfig.Directional"/> config (e.g. set to the
    /// reverse of a body's velocity each frame for thrust). Ignored when the config is omnidirectional.</summary>
    public Vector2 Direction { get; set; } = -Vector2.UnitY;

    /// <summary>Uniform scale applied to spawned sizes / speeds / spawn offsets.</summary>
    public float Scale { get; set; } = 1f;

    /// <summary>Stop a continuous emitter (e.g. a thrust whose body has stopped); live particles
    /// finish out, then the system retires.</summary>
    public void Stop() => _stopped = true;

    /// <summary>True once the emitter will never spawn again (so the system can retire when empty).</summary>
    public bool Finished =>
        _stopped || (_cfg.Mode == EmitMode.Burst ? _bursted
                                                 : _cfg.Duration > 0f && _age >= _cfg.Duration);

    /// <summary>Advance the emitter clock and return how many particles to spawn this step.</summary>
    public int Step(float dt)
    {
        _age += dt;
        if (_stopped) return 0;
        if (_cfg.Mode == EmitMode.Burst)
        {
            if (_bursted) return 0;
            _bursted = true;
            return _cfg.BurstCount;
        }
        if (_cfg.Duration > 0f && _age >= _cfg.Duration) return 0;
        _accum += _cfg.RatePerSecond * dt;
        int n = (int)_accum;
        _accum -= n;
        return n;
    }

    /// <summary>Fill one freshly-recycled slot with a new particle's initial state.</summary>
    public void Init(ref Particle p, Random rng)
    {
        float s = Scale;

        Vector2 pos = Origin;
        float sr = _cfg.ShapeRadius * s;
        if (sr > 0f)
        {
            switch (_cfg.Shape)
            {
                case EmitShape.Disc:
                {
                    double a = rng.NextDouble() * Math.PI * 2.0;
                    float  r = MathF.Sqrt((float)rng.NextDouble()) * sr;   // sqrt = uniform over area
                    pos += new Vector2((float)Math.Cos(a) * r, (float)Math.Sin(a) * r);
                    break;
                }
                case EmitShape.Line:   // horizontal curtain (rain / snow / ground line)
                    pos += new Vector2(((float)rng.NextDouble() * 2f - 1f) * sr, 0f);
                    break;
                case EmitShape.Ring:   // on the circle edge (rings / portals)
                {
                    double a = rng.NextDouble() * Math.PI * 2.0;
                    pos += new Vector2((float)Math.Cos(a) * sr, (float)Math.Sin(a) * sr);
                    break;
                }
            }
        }

        double ang;
        if (_cfg.Directional)
        {
            double baseAng = Math.Atan2(Direction.Y, Direction.X);
            ang = baseAng + (rng.NextDouble() * 2.0 - 1.0) * _cfg.ConeHalfAngle;
        }
        else
        {
            ang = rng.NextDouble() * Math.PI * 2.0;   // omnidirectional spray
        }
        float speed = _cfg.Speed.Sample(rng) * s;

        p.Pos        = pos;
        p.Vel        = new Vector2((float)Math.Cos(ang) * speed, (float)Math.Sin(ang) * speed);
        p.Age        = 0f;
        p.Life       = MathF.Max(0.001f, _cfg.Life.Sample(rng));
        p.Size0      = _cfg.StartSize.Sample(rng) * s;
        p.Size1      = _cfg.EndSize.Sample(rng) * s;
        p.Rotation   = _cfg.StartRotation.Sample(rng);
        p.AngularVel = _cfg.AngularVel.Sample(rng);
        p.Color0     = _cfg.Color0;
        p.Color1     = _cfg.Color1;
        p.Seed       = (float)rng.NextDouble();
        int frames   = Math.Max(1, _cfg.SheetCols * _cfg.SheetRows);
        p.Frame      = (_cfg.SheetAnimate || frames <= 1) ? 0 : rng.Next(frames);   // animated frames derive from T
        p.Alive      = true;
    }
}
