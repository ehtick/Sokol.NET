using System;
using System.Collections.Generic;
using Sokol.GUI;
using Sokol.Render2D.Particles.Rendering;

namespace Sokol.Render2D.Particles;

/// <summary>Owns the live <see cref="ParticleSystem"/> instances and the shared backend/RNG, and is
/// the single API games touch: <see cref="Spawn(EmitterConfig, Vector2, float)"/> to fire an effect,
/// <see cref="Update"/> each frame, <see cref="Render"/> in the board's pass. Cosmetic and
/// client-local — never networked. Finished systems self-retire so memory stays bounded.</summary>
public sealed class ParticleLayer : IDisposable, IParticleSpawnSink
{
    const int MaxSystems = 24;   // layer-wide cap; the oldest effect is dropped if exceeded

    readonly List<ParticleSystem>  _systems  = new();
    readonly Random                _rng      = new();
    readonly ParticleTextureCache  _textures = new();
    readonly GuiParticleRenderer   _backend;
    readonly List<string>          _preload  = new();   // keys to mirror into the SG cache when it spins up

    // Sokol.SG backend — created lazily on first RenderSg (so pure-NanoVG consumers pay nothing).
    SgParticleTextureCache? _sgTextures;
    SgParticleRenderer?     _sgBackend;
    readonly List<(EmitterConfig Cfg, Vector2 Pos, float Scale)> _deferred = new();   // sub-emitter (death) spawns

    public ParticleLayer() => _backend = new GuiParticleRenderer(_textures);

    public bool IsEmpty => _systems.Count == 0;

    /// <summary>Warm a sprite into <see cref="ParticleTextureCache"/> before it's first drawn (sprites
    /// load async, so preloading avoids a one-effect miss).</summary>
    public void Preload(string textureKey)
    {
        _textures.Preload(textureKey);
        _preload.Add(textureKey);
        _sgTextures?.Preload(textureKey);
    }

    /// <summary>Start an effect at <paramref name="position"/> (logical px, the layer's space).
    /// <paramref name="scale"/> uniformly scales spawned sizes/speeds so one preset fits any cell size.
    /// Returns the system so a continuous effect can be driven (body-following: set Origin/Direction
    /// each frame, then Stop() when done); fire-and-forget callers ignore it.</summary>
    public ParticleSystem Spawn(EmitterConfig cfg, Vector2 position, float scale = 1f)
    {
        if (_systems.Count >= MaxSystems) _systems.RemoveAt(0);
        var sys = new ParticleSystem(cfg, position, scale, _rng);
        _systems.Add(sys);
        return sys;
    }

    public ParticleSystem Spawn(EmitterConfig cfg, float x, float y, float scale = 1f)
        => Spawn(cfg, new Vector2(x, y), scale);

    public void Update(float dt)
    {
        for (int i = _systems.Count - 1; i >= 0; i--)
        {
            _systems[i].Update(dt, this);
            if (_systems[i].IsDone) _systems.RemoveAt(i);
        }
        if (_deferred.Count > 0)   // apply sub-emitter death-spawns after the iteration (no mid-loop mutation)
        {
            for (int i = 0; i < _deferred.Count; i++) { var d = _deferred[i]; Spawn(d.Cfg, d.Pos, d.Scale); }
            _deferred.Clear();
        }
    }

    void IParticleSpawnSink.SpawnDeferred(EmitterConfig cfg, Vector2 pos, float scale)
        => _deferred.Add((cfg, pos, scale));

    public void Render(Renderer r)
    {
        for (int i = 0; i < _systems.Count; i++)
            _systems[i].Render(r, _backend);
    }

    /// <summary>Render every system through the Sokol.SG backend into <paramref name="surface"/>'s
    /// particle batch (design §7.4). <paramref name="scale"/> maps the layer's logical px to the
    /// surface's physical-px target. Call between the surface's Begin/End so particles land in the
    /// offscreen pass, above the scene and below the NanoVG HUD.</summary>
    public void RenderSg(Render2DSurface surface, float scale, Vector2 offset = default)
    {
        if (_sgTextures == null)
        {
            _sgTextures = new SgParticleTextureCache();
            _sgBackend  = new SgParticleRenderer(_sgTextures);
            foreach (var k in _preload) _sgTextures.Preload(k);
        }
        _sgBackend!.Configure(surface, scale, offset);
        for (int i = 0; i < _systems.Count; i++)
            _systems[i].Render(null!, _sgBackend);
    }

    /// <summary>Drop every live effect (e.g. on a new game) so stale particles never reappear.</summary>
    public void Clear() => _systems.Clear();

    /// <summary>Free the shared sprite cache (NanoVG images). Call when the owning screen closes.</summary>
    public void Dispose()
    {
        _systems.Clear();
        _textures.Dispose();
        _sgTextures?.Dispose();
    }
}
