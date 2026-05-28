using System.Numerics;

namespace GameEditor.Framework.Renderer.Server
{
    /// <summary>
    /// 24 B draw command. Fits two per 64 B cache line.
    /// Emitted during ECS extract; consumed by culling, LOD selection, and grouping.
    /// All fields are value types — this struct is never boxed on the hot path.
    /// </summary>
    public readonly struct DrawCommand
    {
        /// <summary>Index into MeshRegistry.</summary>
        public readonly ushort MeshId;

        /// <summary>Index into MaterialRegistry.</summary>
        public readonly ushort MaterialId;

        /// <summary>Selected LOD index (bits 0–3) + render flags (bits 4–15).</summary>
        public readonly ushort LodMask;

        /// <summary>Combined material+depth sort key for opaque / transparent ordering.</summary>
        public readonly ushort SortKey;

        /// <summary>Frent entity handle — used for picking and debug only; not on the hot path.</summary>
        public readonly uint EntityHandle;

        /// <summary>World-space bounds centre — used for view-depth sort of transparent objects.</summary>
        public readonly Vector3 BoundsCenter;

        /// <summary>World-space bounding sphere radius.</summary>
        public readonly float BoundsRadius;

        public DrawCommand(ushort meshId, ushort materialId, ushort lodMask, ushort sortKey,
                           uint entityHandle, Vector3 boundsCenter, float boundsRadius)
        {
            MeshId        = meshId;
            MaterialId    = materialId;
            LodMask       = lodMask;
            SortKey       = sortKey;
            EntityHandle  = entityHandle;
            BoundsCenter  = boundsCenter;
            BoundsRadius  = boundsRadius;
        }
    }
}
