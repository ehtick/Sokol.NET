using System;
using System.Collections.Generic;

namespace GameEditor.Framework.Renderer.Server.Animation
{
    /// <summary>
    /// Holds node-TRS animation clips per imported glTF, keyed by glTF path then by node name.
    /// Populated whenever a glTF is imported OR preloaded (so it survives a cold scene reload), and
    /// queried to (re)attach <see cref="NodeAnimationPlayer"/> components to the matching entities.
    /// Mirrors how <c>SkinnedCharacterRegistry</c> lets deserialized skinned entities re-resolve their
    /// animator by key. Node name is the natural key because the importer names each node's entity
    /// after the node (and the scene serializer round-trips that name via <c>NameTag</c>).
    /// </summary>
    public static class NodeAnimationRegistry
    {
        // glTF path (the part of a MeshRenderer.MeshPath before '#') → (node name → clip).
        private static readonly Dictionary<string, Dictionary<string, NodeAnimationClip>> _byPath
            = new(StringComparer.Ordinal);

        public static void Register(string gltfPath, Dictionary<string, NodeAnimationClip> nodeClips)
            => _byPath[gltfPath] = nodeClips;

        public static bool TryGetForPath(string gltfPath, out Dictionary<string, NodeAnimationClip> clips)
            => _byPath.TryGetValue(gltfPath, out clips!);

        /// <summary>Returns the clip for <paramref name="nodeName"/> of <paramref name="gltfPath"/>, or null.</summary>
        public static NodeAnimationClip? Resolve(string gltfPath, string nodeName)
            => _byPath.TryGetValue(gltfPath, out var m) && m.TryGetValue(nodeName, out var c) ? c : null;

        /// <summary>
        /// Returns the clip for the first registered animated node named <paramref name="nodeName"/>
        /// across all glTFs, or null. Used to re-attach players to scene entities by name — the
        /// animated node entity may have no MeshRenderer of its own (multi-primitive nodes put the
        /// mesh on child entities), so it can't be matched via MeshRenderer→glTF path.
        /// </summary>
        public static NodeAnimationClip? ResolveAnyByName(string nodeName)
        {
            foreach (var perNode in _byPath.Values)
                if (perNode.TryGetValue(nodeName, out var c)) return c;
            return null;
        }
    }
}
