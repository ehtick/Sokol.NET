using System.Numerics;

namespace GameEditor.Framework.Renderer.Server
{
    /// <summary>
    /// 80 B per-instance data uploaded to the per-instance vertex buffer (slot 1).
    /// Layout matches the vertex shader's instance attribute block.
    /// Never boxed; always passed by ref/in on the hot path.
    /// </summary>
    public readonly struct InstanceData
    {
        /// <summary>World-space model matrix (64 B). Read by the vertex shader as 4 × vec4 attributes.</summary>
        public readonly Matrix4x4 Model;

        /// <summary>
        /// Custom per-instance data (16 B): xyz = tint colour override, w = LOD fade / selection flag.
        /// Unused channels should be 0.
        /// </summary>
        public readonly Vector4 CustomData;

        public InstanceData(in Matrix4x4 model, Vector4 customData)
        {
            Model      = model;
            CustomData = customData;
        }

        public InstanceData(in Matrix4x4 model) : this(model, Vector4.One) { }
    }
}
