// AnimationConstants.cs — ported as-is from examples/CGltfViewer/Source/AnimationConstants.cs.
// MAX_BONES raised to 128 to match RenderingConstants.MAX_BONES and the pbr_vs_uniforms.glsl
// finalBonesMatrices[MAX_BONES] uniform array (CGltfViewer used 100).

namespace GameEditor.Framework.Renderer.Server.Animation
{
    public static class AnimationConstants
    {
        public const int MAX_BONES = 128;
        public const int MAX_BONE_INFLUENCE = 4;
    }
}
