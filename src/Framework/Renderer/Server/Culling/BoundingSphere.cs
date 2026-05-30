using System.Numerics;

namespace GameEditor.Framework.Renderer.Server
{
    /// <summary>
    /// Bounding sphere defined by a world-space centre and a radius.
    /// 16-byte value type; never boxed on the hot path.
    /// </summary>
    public readonly struct BoundingSphere
    {
        public readonly Vector3 Center;
        public readonly float   Radius;

        public BoundingSphere(Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        /// <summary>Build a bounding sphere that tightly wraps an AABB.</summary>
        public static BoundingSphere FromAabb(in Aabb aabb) =>
            new BoundingSphere(aabb.Center, aabb.Extents.Length());
    }
}
