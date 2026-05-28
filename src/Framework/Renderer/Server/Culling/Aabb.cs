using System.Numerics;
using System.Runtime.CompilerServices;

namespace GameEditor.Framework.Renderer.Server
{
    /// <summary>
    /// Axis-aligned bounding box. Readonly value type; never boxed on the hot path.
    /// </summary>
    public readonly struct Aabb
    {
        public readonly Vector3 Min;
        public readonly Vector3 Max;

        public Aabb(Vector3 min, Vector3 max) { Min = min; Max = max; }

        /// <summary>Centre of the box in world space.</summary>
        public Vector3 Center => (Min + Max) * 0.5f;

        /// <summary>Half-extents of the box.</summary>
        public Vector3 Extents => (Max - Min) * 0.5f;

        /// <summary>Transform this AABB by a matrix, producing a new world-space AABB.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Aabb Transform(in Matrix4x4 m)
        {
            // Arvo's method — transforms the extents and recentres.
            Vector3 c = Center;
            Vector3 e = Extents;

            // New centre = transform the old centre.
            var newCenter = Vector3.Transform(c, m);

            // New extents = |m| * old extents (axis columns absorbed).
            float ex = MathF.Abs(m.M11) * e.X + MathF.Abs(m.M21) * e.Y + MathF.Abs(m.M31) * e.Z;
            float ey = MathF.Abs(m.M12) * e.X + MathF.Abs(m.M22) * e.Y + MathF.Abs(m.M32) * e.Z;
            float ez = MathF.Abs(m.M13) * e.X + MathF.Abs(m.M23) * e.Y + MathF.Abs(m.M33) * e.Z;
            var newExtents = new Vector3(ex, ey, ez);

            return new Aabb(newCenter - newExtents, newCenter + newExtents);
        }

        public static Aabb Empty =>
            new Aabb(new Vector3(float.MaxValue), new Vector3(float.MinValue));
    }
}
