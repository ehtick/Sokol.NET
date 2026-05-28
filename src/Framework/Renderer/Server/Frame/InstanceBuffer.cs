using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Sokol.SG;
using static Sokol.Utils;

namespace GameEditor.Framework.Renderer.Server
{
    /// <summary>
    /// Triple-buffered instance data buffer.
    /// Staging memory is allocated once via NativeMemory.Alloc (no GC pin, no LOH pressure).
    /// On each frame, the active ring slot is updated via sg_update_buffer.
    /// </summary>
    public sealed unsafe class InstanceBuffer : IDisposable
    {
        private static readonly int InstanceSize = Unsafe.SizeOf<InstanceData>();

        private readonly sg_buffer[] _ring = new sg_buffer[RenderingConstants.INSTANCE_RING_FRAMES];
        private InstanceData*        _stagingPtr;
        private readonly int         _maxInstancesPerDraw;
        private int                  _ringIndex;
        private int                  _stagingCount;
        private bool                 _disposed;

        public sg_buffer CurrentBuffer => _ring[_ringIndex];

        public InstanceBuffer(int maxInstancesPerDraw = RenderingConstants.MAX_INSTANCES_PER_DRAW)
        {
            _maxInstancesPerDraw = maxInstancesPerDraw;
            int stagingBytes     = maxInstancesPerDraw * InstanceSize;

            // Allocate unmanaged staging memory — survives across frames, never collected.
            _stagingPtr = (InstanceData*)NativeMemory.Alloc((nuint)stagingBytes);

            // Create ring of dynamic buffers.
            for (int i = 0; i < RenderingConstants.INSTANCE_RING_FRAMES; i++)
            {
                _ring[i] = sg_make_buffer(new sg_buffer_desc
                {
                    size   = (nuint)stagingBytes,
                    usage  = new sg_buffer_usage { vertex_buffer = true, stream_update = true },
                    label  = $"instance-ring-{i}"
                });
            }
        }

        /// <summary>Advance to the next ring slot at the start of a new frame.</summary>
        public void BeginFrame()
        {
            _ringIndex    = (_ringIndex + 1) % RenderingConstants.INSTANCE_RING_FRAMES;
            _stagingCount = 0;
        }

        /// <summary>Write one InstanceData into the staging buffer. Returns the staging offset.</summary>
        public bool TryAppend(in InstanceData data)
        {
            if (_stagingCount >= _maxInstancesPerDraw) return false;
            _stagingPtr[_stagingCount++] = data;
            return true;
        }

        /// <summary>
        /// Upload the staged data (0.._stagingCount) into the current ring buffer and return
        /// the byte offset of the first instance (always 0 for the simplified M1 path).
        /// </summary>
        public void Flush()
        {
            if (_stagingCount == 0) return;
            int byteCount = _stagingCount * InstanceSize;
            sg_update_buffer(_ring[_ringIndex], new sg_range
            {
                ptr  = _stagingPtr,
                size = (nuint)byteCount
            });
        }

        /// <summary>Number of instances staged since last BeginFrame / Reset.</summary>
        public int StagedCount => _stagingCount;

        /// <summary>Reset staged count without advancing the ring (use between draw groups within a frame).</summary>
        public void ResetStaging() => _stagingCount = 0;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = 0; i < _ring.Length; i++)
            {
                if (_ring[i].id != 0)
                {
                    sg_destroy_buffer(_ring[i]);
                    _ring[i] = default;
                }
            }

            if (_stagingPtr != null)
            {
                NativeMemory.Free(_stagingPtr);
                _stagingPtr = null;
            }
        }
    }
}
