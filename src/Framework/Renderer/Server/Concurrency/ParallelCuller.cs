using System.Collections.Concurrent;
using System.Threading;

#if !WEB
using System.Collections.Generic;
using System.Threading.Tasks;
#endif

namespace GameEditor.Framework.Renderer.Server.Concurrency
{
    /// <summary>
    /// Parallel frustum culler.
    ///
    /// <see cref="Run"/> partitions the <paramref name="commands"/> array into 256-element
    /// chunks and tests each draw command against the view frustum on the thread pool.
    /// Each worker accumulates visible indices in a thread-local scratch buffer and then
    /// atomically reserves a slice of the shared <paramref name="output"/> array.
    ///
    /// On WebAssembly (<c>#if WEB</c>) the implementation falls back to a serial loop
    /// because the browser runtime has no thread support.
    ///
    /// Sokol <c>sg_*</c> calls must <b>not</b> be made from inside <see cref="Run"/>;
    /// it is pure CPU-side work that produces an index list consumed by the render loop.
    /// </summary>
    public static class ParallelCuller
    {
        private const int ChunkSize = 256;

        // Shared merge counter — reset to 0 at the start of each Run call.
        // Run is called sequentially once per view, so this is safe.
        private static int _mergeCount;

        /// <summary>
        /// Frustum-cull <paramref name="count"/> draw commands, writing the indices of
        /// visible commands into <paramref name="output"/>.
        /// </summary>
        /// <param name="frustum">View frustum for this render pass.</param>
        /// <param name="commands">Pre-allocated draw command array (capacity >= count).</param>
        /// <param name="count">Number of valid draw commands in <paramref name="commands"/>.</param>
        /// <param name="output">Destination for visible draw-command indices (capacity >= count).</param>
        /// <param name="visibleCount">Number of indices written to <paramref name="output"/>.</param>
        public static void Run(
            in Frustum    frustum,
            DrawCommand[] commands,
            int           count,
            int[]         output,
            out int       visibleCount)
        {
            _mergeCount = 0;

            if (count == 0)
            {
                visibleCount = 0;
                return;
            }

#if WEB
            // ── Serial path (WASM / no threads) ─────────────────────────────────────────
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                var sphere = new BoundingSphere(commands[i].BoundsCenter, commands[i].BoundsRadius);
                if (frustum.Intersects(sphere))
                    output[n++] = i;
            }
            visibleCount = n;
#else
            // ── Parallel path ────────────────────────────────────────────────────────────
            // Capture frustum for the lambda (readonly struct — cheap copy).
            Frustum capturedFrustum = frustum;

            Parallel.ForEach(
                Partitioner.Create(0, count, ChunkSize),
                // localInit: get the calling thread's pre-allocated scratch buffer.
                () => (buf: PerThreadBuffers.GetScratch(), n: 0),
                // body: cull each chunk into the local scratch.
                (range, _, local) =>
                {
                    int   localN   = local.n;
                    int[] localBuf = local.buf;
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        var sphere = new BoundingSphere(commands[i].BoundsCenter, commands[i].BoundsRadius);
                        if (capturedFrustum.Intersects(sphere))
                        {
                            if (localN < localBuf.Length)
                                localBuf[localN++] = i;
                        }
                    }
                    return (localBuf, localN);
                },
                // localFinally: atomically reserve a slice of the output array and copy.
                local =>
                {
                    if (local.n == 0) return;
                    int start = Interlocked.Add(ref _mergeCount, local.n) - local.n;
                    if (start + local.n <= output.Length)
                        System.Array.Copy(local.buf, 0, output, start, local.n);
                });

            visibleCount = _mergeCount;
#endif
        }
    }
}
