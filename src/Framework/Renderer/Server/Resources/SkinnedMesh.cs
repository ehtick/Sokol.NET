using System;
using System.Numerics;
using static Sokol.SG;
using static Sokol.Utils;

namespace GameEditor.Framework.Renderer.Server.Resources
{
    /// <summary>
    /// 80 B per-vertex layout for the <c>pbr_skinning</c> pipeline (vertex buffer slot 0):
    /// position(12) + normal(12) + uv(8) + tangent(16) + joints(16) + weights(16).
    /// Joint indices are stored as floats — the shader reads them via <c>int(joints_0.x)</c>.
    /// </summary>
    public struct SkinnedVertex
    {
        public Vector3 Position; // @0
        public Vector3 Normal;   // @12
        public Vector2 Uv;       // @24
        public Vector4 Tangent;  // @32
        public Vector4 Joints;   // @48  (4 joint indices, as floats)
        public Vector4 Weights;  // @64  (4 blend weights)

        public const int SizeBytes = 12 + 12 + 8 + 16 + 16 + 16; // 80 B
    }

    /// <summary>
    /// GPU buffers for one skinned primitive. Unlike <see cref="MeshResource"/> (instanced,
    /// 48 B, ref-counted), a skinned mesh is drawn directly per character with its own bone
    /// matrices, so it is owned 1:1 by its renderer and not shared through MeshRegistry.
    /// </summary>
    public sealed class SkinnedMesh
    {
        public sg_buffer VertexBuffer;
        public sg_buffer IndexBuffer;
        public int       IndexCount;
        public Aabb      LocalBounds;

        public static unsafe SkinnedMesh Create(SkinnedVertex[] vertices, uint[] indices, in Aabb bounds, string label)
        {
            var m = new SkinnedMesh { IndexCount = indices.Length, LocalBounds = bounds };
            m.VertexBuffer = sg_make_buffer(new sg_buffer_desc
            {
                data  = SG_RANGE(vertices),
                label = label + "-skin-vbuf",
            });
            m.IndexBuffer = sg_make_buffer(new sg_buffer_desc
            {
                usage = new sg_buffer_usage { index_buffer = true },
                data  = SG_RANGE(indices),
                label = label + "-skin-ibuf",
            });
            return m;
        }

        public void Destroy()
        {
            if (VertexBuffer.id != 0) { sg_destroy_buffer(VertexBuffer); VertexBuffer = default; }
            if (IndexBuffer.id  != 0) { sg_destroy_buffer(IndexBuffer);  IndexBuffer  = default; }
        }
    }
}
