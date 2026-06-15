using System;
using Sokol.GUI;
using Sokol.Render2D.Particles.Rendering;

namespace Sokol.Render2D.Particles;

/// <summary>Receives sub-emitter spawns (a particle's death effect), deferred until after the update
/// loop so the owning <see cref="ParticleLayer"/> can add systems without mutating its list mid-iteration.</summary>
public interface IParticleSpawnSink
{
    void SpawnDeferred(EmitterConfig cfg, Vector2 pos, float scale);
}

/// <summary>One live effect instance: a fixed-capacity pool of <see cref="Particle"/>, an
/// <see cref="Emitter"/>, and an RNG. <see cref="Update"/> is pure CPU (emit → integrate → retire);
/// <see cref="Render"/> hands the live set to a backend. Reports <see cref="IsDone"/> once the emitter
/// has finished and no particles remain, so the layer can free it. Allocation-free after construction.</summary>
public sealed class ParticleSystem
{
    readonly EmitterConfig _cfg;
    readonly Emitter       _emitter;
    readonly Random        _rng;
    readonly Particle[]    _pool;
    readonly Vector2       _gravity;   // config gravity pre-scaled to the spawn scale
    readonly float         _scale;     // spawn scale (inherited by sub-emitter children)
    int _liveCount;

    public ParticleSystem(EmitterConfig cfg, Vector2 origin, float scale, Random rng)
    {
        _cfg     = cfg;
        _rng     = rng;
        _scale   = scale;
        _pool    = new Particle[Math.Max(1, cfg.MaxParticles)];
        _emitter = new Emitter(cfg) { Origin = origin, Scale = scale };
        _gravity = cfg.Gravity * scale;
    }

    public EmitterConfig Config => _cfg;
    public bool IsDone => _emitter.Finished && _liveCount == 0;

    // ── Body-following handle (for a continuous emitter that tracks a moving body) ──
    /// <summary>Move the emitter (e.g. to a projectile's position each frame).</summary>
    public Vector2 Origin    { get => _emitter.Origin;    set => _emitter.Origin = value; }
    /// <summary>Set the emission direction (e.g. the reverse of the body's velocity for thrust).</summary>
    public Vector2 Direction { get => _emitter.Direction; set => _emitter.Direction = value; }
    /// <summary>Stop emitting; existing particles finish out and the system then retires.</summary>
    public void Stop() => _emitter.Stop();

    public void Update(float dt, IParticleSpawnSink? sink = null)
    {
        if (dt <= 0f) return;

        // 1. emit into recycled slots (drop the overflow if the pool is full).
        int toEmit = _emitter.Step(dt);
        for (int i = 0; i < toEmit; i++)
        {
            int slot = FindFreeSlot();
            if (slot < 0) break;
            _emitter.Init(ref _pool[slot], _rng);
        }

        // 2. integrate + retire. Affectors apply forces (mutate Vel), then gravity + drag fold in.
        var affectors = _cfg.Affectors;
        int affectorCount = affectors?.Count ?? 0;
        var sub = _cfg.SubEmitter;            // spawned at each particle's death position
        Vector2 origin = _emitter.Origin;     // current emitter position (for emitter-relative affectors)
        float drag = MathF.Max(0f, 1f - _cfg.Drag * dt);
        _liveCount = 0;
        for (int i = 0; i < _pool.Length; i++)
        {
            ref Particle p = ref _pool[i];
            if (!p.Alive) continue;
            p.Age += dt;
            if (p.Age >= p.Life)
            {
                p.Alive = false;
                if (sub != null) sink?.SpawnDeferred(sub, p.Pos, _scale);
                continue;
            }
            for (int a = 0; a < affectorCount; a++) affectors![a].Apply(ref p, dt, origin);
            p.Vel       = (p.Vel + _gravity * dt) * drag;
            p.Pos      += p.Vel * dt;
            p.Rotation += p.AngularVel * dt;
            _liveCount++;
        }
    }

    public void Render(Renderer r, IParticleRenderer backend)
    {
        if (_liveCount == 0) return;
        backend.Begin(r, _cfg);
        for (int i = 0; i < _pool.Length; i++)
            if (_pool[i].Alive) backend.Draw(r, in _pool[i], _cfg);
        backend.End(r);
    }

    int FindFreeSlot()
    {
        for (int i = 0; i < _pool.Length; i++)
            if (!_pool[i].Alive) return i;
        return -1;
    }
}
