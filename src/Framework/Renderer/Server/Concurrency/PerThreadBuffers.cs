namespace GameEditor.Framework.Renderer.Server.Concurrency
{
    /// <summary>
    /// Pre-allocated, thread-local scratch buffers used by <see cref="ParallelCuller"/>.
    /// Each worker thread gets its own <c>int[]</c> sized <c>MAX_DRAWS</c> so no heap
    /// allocation occurs on the hot culling path.
    /// </summary>
    internal static class PerThreadBuffers
    {
        // One int[] per OS thread — sized for the worst case where a single thread sees
        // all visible draw commands before the merge step.
        private static readonly ThreadLocal<int[]> _visibleScratch =
            new(() => new int[RenderingConstants.MAX_DRAWS]);

        /// <summary>Returns the calling thread's scratch buffer (never null).</summary>
        public static int[] GetScratch() => _visibleScratch.Value!;
    }
}
