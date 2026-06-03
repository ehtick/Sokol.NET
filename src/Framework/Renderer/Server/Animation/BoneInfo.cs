// BoneInfo.cs — ported as-is from examples/CGltfViewer/Source/BoneInfo.cs.
using System.Numerics;

namespace GameEditor.Framework.Renderer.Server.Animation
{
    public struct BoneInfo
    {
        // id is index in finalBoneMatrices
        public int Id;

        // offset matrix transforms vertex from model space to bone space (inverse bind matrix)
        public Matrix4x4 Offset;
    }
}
