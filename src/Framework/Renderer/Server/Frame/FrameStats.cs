namespace GameEditor.Framework.Renderer.Server
{
    /// <summary>
    /// Per-frame rendering statistics.
    /// All fields are plain integers updated during SubmitView; reset at BeginFrame().
    /// </summary>
    public struct FrameStats
    {
        /// <summary>Number of sg_draw calls issued this frame.</summary>
        public int DrawCalls;

        /// <summary>Total instances submitted across all draw calls this frame.</summary>
        public int InstancesSubmitted;

        /// <summary>Entities culled (frustum or outside LOD range) this frame.</summary>
        public int Culled;

        /// <summary>Number of LOD level switches that occurred this frame.</summary>
        public int LodSwitches;

        /// <summary>Number of shadow-pass draw calls issued this frame.</summary>
        public int ShadowDrawCalls;

        /// <summary>Instances silently dropped because the instance buffer was full (debug only).</summary>
        public int InstancesDropped;

        public void Reset()
        {
            DrawCalls          = 0;
            InstancesSubmitted = 0;
            Culled             = 0;
            LodSwitches        = 0;
            ShadowDrawCalls    = 0;
            InstancesDropped   = 0;
        }
    }
}
