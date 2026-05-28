using System.Numerics;
using Sokol;

namespace GameEditor.Framework.Renderer.Server
{
    /// <summary>
    /// Describes a single camera + offscreen render target.
    /// Registered with RenderingServer.RegisterView(); identified by view index.
    /// </summary>
    public sealed class RenderView
    {
        /// <summary>Camera view matrix (world → view).</summary>
        public Matrix4x4 View;

        /// <summary>Camera projection matrix (view → clip).</summary>
        public Matrix4x4 Projection;

        /// <summary>Combined ViewProjection (updated by SubmitView).</summary>
        public Matrix4x4 ViewProj;

        /// <summary>Viewport rectangle in the offscreen target (pixels): X, Y, Width, Height.</summary>
        public (int X, int Y, int Width, int Height) Viewport;

        /// <summary>Sokol offscreen pass action (clear colours / depth).</summary>
        public SG.sg_pass_action PassAction;

        /// <summary>
        /// Sokol offscreen pass handle.
        /// May be default (id=0) to redirect to the swapchain (used in standalone examples).
        /// </summary>
        public SG.sg_attachments Attachments;

        /// <summary>Colour output image (sg_image). Can be bound to an ImGui texture slot.</summary>
        public SG.sg_image ColorTexture;

        /// <summary>Depth target image.</summary>
        public SG.sg_image DepthTexture;

        /// <summary>Camera world-space position — used for LOD and shadow projection.</summary>
        public Vector3 CameraPosition;

        /// <summary>Vertical FOV in radians — used for LOD screen-coverage computation.</summary>
        public float FovY = 0.785398f; // 45°

        /// <summary>Near clip distance.</summary>
        public float NearZ = 0.1f;

        /// <summary>Far clip distance.</summary>
        public float FarZ = 1000f;

        /// <summary>User-readable label for debug.</summary>
        public string Label = "view";

        /// <summary>When true this view is rendered every frame; when false it is skipped.</summary>
        public bool Active = true;
    }
}
