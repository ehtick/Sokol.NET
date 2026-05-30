using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace GameEditor.Framework.Renderer.Server
{
    /// <summary>
    /// Six-plane view frustum extracted from a combined view-projection matrix.
    ///
    /// Planes are inward-facing (a point satisfies <c>dot(Normal, P) + D >= 0</c> when inside).
    /// Normals are normalised so sphere-distance tests return values in world units.
    ///
    /// Extraction assumes <b>row-vector × matrix</b> convention (System.Numerics) and
    /// a depth clip range of <b>[0, 1]</b> (Metal / D3D11 / Sokol's unified NDC).
    /// </summary>
    public readonly struct Frustum
    {
        // ── Nested plane ────────────────────────────────────────────────────────────────

        private readonly struct FrustumPlane
        {
            public readonly Vector3 Normal;  // unit-length
            public readonly float   D;       // signed offset: dot(Normal,P)+D >= 0 ↔ inside

            public FrustumPlane(float nx, float ny, float nz, float d)
            {
                float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len > 1e-7f)
                {
                    float inv = 1f / len;
                    Normal = new Vector3(nx * inv, ny * inv, nz * inv);
                    D      = d * inv;
                }
                else
                {
                    Normal = Vector3.UnitX;
                    D      = d;
                }
            }
        }

        // ── Storage (6 planes inline — no heap) ─────────────────────────────────────────

        private readonly FrustumPlane _left, _right, _bottom, _top, _near, _far;

        private Frustum(FrustumPlane left, FrustumPlane right,
                        FrustumPlane bottom, FrustumPlane top,
                        FrustumPlane near, FrustumPlane far)
        {
            _left   = left;
            _right  = right;
            _bottom = bottom;
            _top    = top;
            _near   = near;
            _far    = far;
        }

        // ── Extraction ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Extract 6 inward-facing frustum planes from a combined view-projection matrix.
        ///
        /// Uses Gribb / Hartmann plane extraction for row-vector × matrix convention:
        /// <c>clip = [x,y,z,1] * vp</c>.
        /// Depth range assumed to be [0, 1].
        /// </summary>
        public static Frustum ExtractFromViewProj(in Matrix4x4 vp) => new Frustum(
            // Left:   clip.x + clip.w >= 0
            new FrustumPlane(vp.M11 + vp.M14, vp.M21 + vp.M24, vp.M31 + vp.M34, vp.M41 + vp.M44),
            // Right:  clip.w - clip.x >= 0
            new FrustumPlane(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41),
            // Bottom: clip.y + clip.w >= 0
            new FrustumPlane(vp.M12 + vp.M14, vp.M22 + vp.M24, vp.M32 + vp.M34, vp.M42 + vp.M44),
            // Top:    clip.w - clip.y >= 0
            new FrustumPlane(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42),
            // Near:   clip.z >= 0  (DX/Metal [0,1] depth)
            new FrustumPlane(vp.M13,           vp.M23,           vp.M33,           vp.M43),
            // Far:    clip.w - clip.z >= 0
            new FrustumPlane(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43));

        // ── Intersection — AABB (p-vertex / n-vertex method) ────────────────────────────

        /// <summary>
        /// Returns <c>true</c> if the AABB is not fully outside the frustum.
        /// Uses the p-vertex optimisation (one dot-product per plane, no corner enumeration).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Intersects(in Aabb aabb)
        {
            return PlaneTestAabb(in _left,   in aabb)
                && PlaneTestAabb(in _right,  in aabb)
                && PlaneTestAabb(in _bottom, in aabb)
                && PlaneTestAabb(in _top,    in aabb)
                && PlaneTestAabb(in _near,   in aabb)
                && PlaneTestAabb(in _far,    in aabb);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool PlaneTestAabb(in FrustumPlane p, in Aabb aabb)
        {
            // p-vertex: the corner most in the direction of the plane normal.
            float px = p.Normal.X >= 0f ? aabb.Max.X : aabb.Min.X;
            float py = p.Normal.Y >= 0f ? aabb.Max.Y : aabb.Min.Y;
            float pz = p.Normal.Z >= 0f ? aabb.Max.Z : aabb.Min.Z;
            // If the p-vertex is behind the plane, the AABB is fully outside.
            return p.Normal.X * px + p.Normal.Y * py + p.Normal.Z * pz + p.D >= 0f;
        }

        // ── Intersection — Bounding Sphere ───────────────────────────────────────────────

        /// <summary>
        /// Returns <c>true</c> if the sphere is not fully outside the frustum.
        /// Tests signed distance from the sphere centre to each plane; culls only when
        /// the sphere is entirely on the negative side of a plane.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Intersects(in BoundingSphere sphere)
        {
            float neg = -sphere.Radius;
            return SignedDist(in _left,   sphere.Center) >= neg
                && SignedDist(in _right,  sphere.Center) >= neg
                && SignedDist(in _bottom, sphere.Center) >= neg
                && SignedDist(in _top,    sphere.Center) >= neg
                && SignedDist(in _near,   sphere.Center) >= neg
                && SignedDist(in _far,    sphere.Center) >= neg;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SignedDist(in FrustumPlane p, Vector3 c) =>
            p.Normal.X * c.X + p.Normal.Y * c.Y + p.Normal.Z * c.Z + p.D;
    }
}
