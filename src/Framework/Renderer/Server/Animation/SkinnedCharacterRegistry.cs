// SkinnedCharacterRegistry.cs — persistent home for the live GPU + animator objects of imported
// skinned characters, keyed by source glTF path. Mirrors MeshRegistry's role for static meshes:
// the SkinnedMeshRenderer component stores only serializable keys (path + primitive index), and the
// live SkinnedMesh / CGltfAnimator are resolved from here at draw time. Because the registry is a
// static that outlives the play/stop scene-snapshot round-trip, skinned characters survive Play→Stop
// (the deserialized component re-resolves the same registry entry).
using System.Collections.Generic;
using GameEditor.Framework.Renderer.Server.Resources;

namespace GameEditor.Framework.Renderer.Server.Animation
{
    public static class SkinnedCharacterRegistry
    {
        public sealed class Entry
        {
            /// <summary>Shared animator for the whole character (drives every primitive's bones).</summary>
            public CGltfAnimator? Animator;
            public int BoneCount;
            /// <summary>Skinned GPU mesh per running primitive index.</summary>
            public readonly Dictionary<int, SkinnedMesh> Meshes = new();

            public void DestroyMeshes()
            {
                foreach (var m in Meshes.Values) m.Destroy();
                Meshes.Clear();
            }
        }

        private static readonly Dictionary<string, Entry> _entries = new();

        /// <summary>Gets the entry for <paramref name="key"/>, creating it (and freeing any prior
        /// GPU meshes if it already existed, e.g. on re-import) so the caller can repopulate it.</summary>
        public static Entry GetOrCreateFresh(string key)
        {
            if (_entries.TryGetValue(key, out var e)) { e.DestroyMeshes(); e.Animator = null; }
            else _entries[key] = e = new Entry();
            return e;
        }

        public static bool TryGet(string key, out Entry entry) => _entries.TryGetValue(key, out entry!);

        /// <summary>Frees every character's GPU meshes and drops all entries (full reset).</summary>
        public static void Clear()
        {
            foreach (var e in _entries.Values) e.DestroyMeshes();
            _entries.Clear();
        }
    }
}
