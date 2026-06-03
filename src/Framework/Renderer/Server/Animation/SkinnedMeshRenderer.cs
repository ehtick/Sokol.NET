// SkinnedMeshRenderer.cs — ECS component for one primitive of a GPU-skinned glTF character.
//
// Holds ONLY serializable keys — the live SkinnedMesh + CGltfAnimator live in
// SkinnedCharacterRegistry (keyed by CharacterKey) and are resolved at draw time. This keeps the
// component serializable, so skinned characters survive scene save/load and the Play→Stop snapshot
// (the registry is a static that outlives the round-trip).
// (Per [[project_animation_rendering_boundary]] this is the renderer-owned GPU side; a later stage
// moves the joints into real ECS entities.)

namespace GameEditor.Framework.Renderer.Server.Animation
{
    public struct SkinnedMeshRenderer
    {
        /// <summary>SkinnedCharacterRegistry key (the source glTF path) — shared by the character's primitives.</summary>
        public string CharacterKey;

        /// <summary>Running primitive index within the character (selects the SkinnedMesh in the registry entry).</summary>
        public int PrimIndex;

        /// <summary>PbrMaterial registry key ("path#material"); empty = fallback material.</summary>
        public string MaterialKey;

        public bool Visible;
        public bool ReceivesShadows;
        public bool CastsShadows;
    }
}
