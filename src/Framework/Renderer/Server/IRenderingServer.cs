using System.Numerics;

namespace GameEditor.Framework.Renderer.Server
{
    /// <summary>
    /// Public façade for RenderingServer — the only surface game code and editor code call.
    /// All rendering goes through this interface; no direct sg_* calls from outside Server/.
    /// </summary>
    public interface IRenderingServer
    {
        /// <summary>
        /// Initialize GPU resources. Called once after sg_setup().
        /// All backing arrays are allocated here; zero allocation after this point on the hot path.
        /// </summary>
        void Init();

        /// <summary>Destroy all GPU resources. Called before sg_shutdown().</summary>
        void Shutdown();

        /// <summary>
        /// Must be called at the start of every application frame, before any SubmitView calls.
        /// Rotates the instance buffer ring and resets per-frame counters.
        /// </summary>
        void BeginFrame();

        /// <summary>
        /// Render a single view with the given view-projection matrix.
        /// Issues the ECS extract, optional culling, grouping, and all draw calls for this view.
        /// </summary>
        /// <param name="viewProj">Combined view × projection matrix (row-major, matches sokol convention).</param>
        /// <param name="viewIndex">Index into the registered view array (0 for the default scene view).</param>
        void SubmitView(in Matrix4x4 viewProj, int viewIndex = 0);

        /// <summary>
        /// Must be called at the end of every application frame, after all SubmitView calls.
        /// Flushes per-frame stats.
        /// </summary>
        void EndFrame();

        /// <summary>Register a RenderView slot and return its index (0-based).</summary>
        int RegisterView(RenderView view);

        /// <summary>Remove a previously registered view slot.</summary>
        void UnregisterView(int viewIndex);

        /// <summary>Read-only access to per-frame statistics (draw calls, instances, culled, etc.).</summary>
        ref readonly FrameStats Stats { get; }
    }
}
