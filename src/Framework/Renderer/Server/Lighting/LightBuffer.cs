// LightBuffer.cs — CPU-side GpuLight[32] staging for the uniform block.
// Filled each frame by CollectLights(); passed to sg_apply_uniforms as a
// sg_range pointing into the pre-allocated fixed array.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GameEditor.Framework.Renderer.Server.Lighting
{
    /// <summary>
    /// Manages the GpuLight[MAX_LIGHTS] array written every frame.
    /// Zero allocations after construction.
    /// </summary>
    public sealed class LightBuffer
    {
        private readonly GpuLight[] _lights = new GpuLight[RenderingConstants.MAX_LIGHTS];
        private int _count;

        /// <summary>Number of lights staged in the current frame.</summary>
        public int Count => _count;

        /// <summary>Reset before calling <see cref="Add"/> for the new frame.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset() => _count = 0;

        /// <summary>Stage one light. Silently drops extras when capacity is full.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in Light light)
        {
            if (_count >= RenderingConstants.MAX_LIGHTS) return;
            _lights[_count++] = new GpuLight(in light);
        }

        /// <summary>
        /// Returns a <see cref="Span{T}"/> over the active lights for this frame.
        /// Span lifetime is valid until the next <see cref="Reset"/> call.
        /// </summary>
        public Span<GpuLight> ActiveSpan => _lights.AsSpan(0, _count);

        /// <summary>
        /// Pin the active slice into a <c>sg_range</c> for <c>sg_apply_uniforms</c>.
        /// The range remains valid for the current frame.
        /// </summary>
        public unsafe Sokol.SG.sg_range GetRange()
        {
            if (_count == 0)
            {
                // Return a valid (but zero-sized won't work) single zero entry.
                fixed (GpuLight* p = _lights)
                    return new Sokol.SG.sg_range { ptr = p, size = (nuint)(Unsafe.SizeOf<GpuLight>() * 1) };
            }
            fixed (GpuLight* p = _lights)
                return new Sokol.SG.sg_range
                {
                    ptr  = p,
                    size = (nuint)(Unsafe.SizeOf<GpuLight>() * _count)
                };
        }
    }
}
