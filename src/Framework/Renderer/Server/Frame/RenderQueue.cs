using System;

namespace GameEditor.Framework.Renderer.Server
{
    /// <summary>
    /// Pre-sized, pre-allocated draw command list for a single frame.
    /// Backed by a fixed-size array allocated once in RenderingServer.Init().
    /// Zero heap allocation after construction.
    /// </summary>
    public sealed class RenderQueue
    {
        private readonly DrawCommand[] _commands;
        private int _count;

        public RenderQueue(int capacity)
        {
            _commands = new DrawCommand[capacity];
            _count    = 0;
        }

        /// <summary>Number of commands currently in the queue.</summary>
        public int Count => _count;

        /// <summary>Capacity of the backing array.</summary>
        public int Capacity => _commands.Length;

        /// <summary>Span over the active slice [0..Count).</summary>
        public Span<DrawCommand> ActiveSlice => _commands.AsSpan(0, _count);

        /// <summary>Full backing span (pre-allocated; use ActiveSlice for active commands).</summary>
        public Span<DrawCommand> BackingSpan => _commands.AsSpan();

        /// <summary>Append a draw command. Returns false if the queue is full.</summary>
        public bool TryAdd(in DrawCommand cmd)
        {
            if (_count >= _commands.Length) return false;
            _commands[_count++] = cmd;
            return true;
        }

        /// <summary>Reset the count to zero for the next frame. Does not clear the backing array.</summary>
        public void Reset() => _count = 0;
    }
}
