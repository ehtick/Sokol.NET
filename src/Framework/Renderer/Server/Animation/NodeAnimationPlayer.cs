namespace GameEditor.Framework.Renderer.Server.Animation
{
    /// <summary>
    /// ECS component: plays a <see cref="NodeAnimationClip"/> onto the owning entity's
    /// <c>Transform</c> each frame (rigid/node animation, e.g. the ChronographWatch hands).
    /// Advanced by <c>RenderingServer.UpdateNodeAnimations</c> at <c>BeginFrame</c>, before
    /// <c>SubmitView</c> reads the Transform. Independent of the skinned-character path.
    /// </summary>
    public struct NodeAnimationPlayer
    {
        public NodeAnimationClip Clip;
        public float Time;     // seconds into the clip
        public bool  Playing;
        public bool  Loop;
    }
}
