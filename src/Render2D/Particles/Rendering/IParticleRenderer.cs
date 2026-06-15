using Sokol.GUI;

namespace Sokol.Render2D.Particles.Rendering;

/// <summary>Pluggable particle backend. The simulation knows nothing about how particles are drawn,
/// so a GPU-instanced <c>Sokol.SG</c> backend (design §7.4) can drop in later without touching the
/// sim or any game code. A <see cref="ParticleSystem"/> brackets its homogeneous live set with one
/// <see cref="Begin"/>/<see cref="End"/> pair so blend state is set once per group.</summary>
public interface IParticleRenderer
{
    /// <summary>Set up state for one system's batch (e.g. blend mode from <paramref name="cfg"/>).</summary>
    void Begin(Renderer r, EmitterConfig cfg);

    /// <summary>Draw a single live particle.</summary>
    void Draw(Renderer r, in Particle p, EmitterConfig cfg);

    /// <summary>Restore state after the batch (resets blend so later UI draws normally).</summary>
    void End(Renderer r);
}
