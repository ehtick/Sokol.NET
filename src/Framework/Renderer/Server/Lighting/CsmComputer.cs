// CsmComputer.cs — Cascaded Shadow Map split and VP matrix computation.
// Uses a practical split scheme (blend of logarithmic + uniform) to produce
// N tight orthographic VP matrices, one per cascade.
// Zero allocations per frame — outputs are pre-allocated fixed-size arrays.

using System;
using System.Numerics;

namespace GameEditor.Framework.Renderer.Server.Lighting
{
    /// <summary>
    /// Computes per-cascade split depths and tight orthographic VP matrices for
    /// Cascaded Shadow Maps (CSM / PSSM).
    ///
    /// Usage each frame:
    ///   csm.Compute(view, proj, near, shadowRange, lightDirection, sliceResolution, numCascades);
    ///   then upload CascadeVP[] into pbr_vs_params.csm_vp and
    ///   SplitDepths[] into pbr_csm_params.csm_split_depths.
    /// </summary>
    public sealed class CsmComputer
    {
        public const int MaxCascades = 4;

        // Blend toward logarithmic split (1.0) vs uniform split (0.0).
        // 0.75 works well for typical outdoor scenes.
        public float Lambda = 0.75f;

        // Additional pull-back factor for the light near-plane (fraction of cascade depth range).
        // Prevents casters behind the camera sub-frustum from dropping out of shadow.
        public float NearPullback = 0.1f;

        // Outputs — populated by Compute(), sized for MaxCascades.
        // Read only the first [cascadeCount] entries after calling Compute().
        public readonly Matrix4x4[] CascadeVP  = new Matrix4x4[MaxCascades];
        public readonly float[]     SplitDepths = new float[MaxCascades];

        // Reused per-frame scratch — avoids allocations.
        private readonly Vector3[] _corners = new Vector3[8];

        // ─────────────────────────────────────────────────────────────────────
        // NDC corners in clip space (shared across all cascades).
        private static ReadOnlySpan<Vector3> NdcCorners => new Vector3[]
        {
            new(-1f, -1f, -1f), new( 1f, -1f, -1f), new( 1f,  1f, -1f), new(-1f,  1f, -1f), // near
            new(-1f, -1f,  1f), new( 1f, -1f,  1f), new( 1f,  1f,  1f), new(-1f,  1f,  1f), // far
        };

        /// <summary>
        /// Compute cascade VP matrices and split depths.
        /// </summary>
        /// <param name="cameraView">Camera view matrix.</param>
        /// <param name="cameraProj">Camera perspective projection matrix.</param>
        /// <param name="nearClip">Camera near plane distance.</param>
        /// <param name="shadowRange">Maximum shadow distance (camera-space Z). Must be &gt; nearClip.</param>
        /// <param name="lightDir">World-space direction the light is shining (normalised, points toward the scene).</param>
        /// <param name="shadowMapSize">Shadow atlas slice resolution (e.g. 2048) for texel snapping.</param>
        /// <param name="cascadeCount">Number of active cascades. Clamped to [1, MaxCascades].</param>
        public void Compute(
            in Matrix4x4 cameraView,
            in Matrix4x4 cameraProj,
            float nearClip,
            float shadowRange,
            in Vector3 lightDir,
            int shadowMapSize,
            int cascadeCount)
        {
            cascadeCount = Math.Clamp(cascadeCount, 1, MaxCascades);

            // Full camera frustum in world space — inverse of view * proj.
            Matrix4x4.Invert(cameraView * cameraProj, out var invVP);

            // Pre-compute all 8 full-frustum world-space corners.
            var ndcCorners = NdcCorners;
            for (int i = 0; i < 8; i++)
            {
                var c = ndcCorners[i];
                var w = Vector4.Transform(new Vector4(c.X, c.Y, c.Z, 1f), invVP);
                _corners[i] = new Vector3(w.X, w.Y, w.Z) / w.W;
            }

            // Light basis (right-handed, -Z toward scene).
            var lightFwd   = Vector3.Normalize(lightDir);
            var worldUp    = Math.Abs(Vector3.Dot(lightFwd, Vector3.UnitY)) < 0.999f
                           ? Vector3.UnitY
                           : Vector3.UnitX;
            var lightRight = Vector3.Normalize(Vector3.Cross(worldUp, lightFwd));
            var lightUp2   = Vector3.Cross(lightFwd, lightRight);

            float prevSplit = nearClip;
            float rangeInv  = 1f / (shadowRange - nearClip);

            // Hoisted out of the cascade loop: all 8 entries are overwritten every iteration,
            // so the buffer is reused rather than re-allocated on the stack per cascade.
            Span<Vector3> sliceCorners = stackalloc Vector3[8];

            for (int c = 0; c < cascadeCount; c++)
            {
                // ── PSSM split depth ──────────────────────────────────────────
                float t        = (c + 1f) / cascadeCount;
                float logSplit = nearClip * MathF.Pow(shadowRange / nearClip, t);
                float uniSplit = nearClip + (shadowRange - nearClip) * t;
                float splitDepth = Lambda * logSplit + (1f - Lambda) * uniSplit;
                SplitDepths[c] = splitDepth;

                // ── Sub-frustum corners in world space ────────────────────────
                // Lerp along the near-to-far segment for both planes of the slice.
                float tNear = (prevSplit  - nearClip) * rangeInv;
                float tFar  = (splitDepth - nearClip) * rangeInv;

                for (int i = 0; i < 4; i++)
                {
                    var n = _corners[i];
                    var f = _corners[i + 4];
                    sliceCorners[i]     = Vector3.Lerp(n, f, tNear);
                    sliceCorners[i + 4] = Vector3.Lerp(n, f, tFar);
                }

                // ── Bounding box in light space ───────────────────────────────
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;
                for (int i = 0; i < 8; i++)
                {
                    float lx = Vector3.Dot(sliceCorners[i], lightRight);
                    float ly = Vector3.Dot(sliceCorners[i], lightUp2);
                    float lz = Vector3.Dot(sliceCorners[i], lightFwd);
                    if (lx < minX) minX = lx;  if (lx > maxX) maxX = lx;
                    if (ly < minY) minY = ly;  if (ly > maxY) maxY = ly;
                    if (lz < minZ) minZ = lz;  if (lz > maxZ) maxZ = lz;
                }

                // Pull near plane back so geometry behind the slice can still cast shadows.
                float depthRange = maxZ - minZ;
                minZ -= depthRange * NearPullback;

                // ── Texel snapping (eliminates shimmering on camera movement) ──
                float worldPerTexelX = (maxX - minX) / shadowMapSize;
                float worldPerTexelY = (maxY - minY) / shadowMapSize;
                minX = MathF.Floor(minX / worldPerTexelX) * worldPerTexelX;
                minY = MathF.Floor(minY / worldPerTexelY) * worldPerTexelY;
                maxX = minX + MathF.Ceiling((maxX - minX) / worldPerTexelX) * worldPerTexelX;
                maxY = minY + MathF.Ceiling((maxY - minY) / worldPerTexelY) * worldPerTexelY;

                // ── Light view matrix ─────────────────────────────────────────
                // Place light eye at the far extent so the ortho near is 0.
                float centerLx = (minX + maxX) * 0.5f;
                float centerLy = (minY + maxY) * 0.5f;
                var lightEye   = lightRight * centerLx
                               + lightUp2   * centerLy
                               + lightFwd   * minZ;

                var lightView = new Matrix4x4(
                    lightRight.X, lightUp2.X, lightFwd.X, 0f,
                    lightRight.Y, lightUp2.Y, lightFwd.Y, 0f,
                    lightRight.Z, lightUp2.Z, lightFwd.Z, 0f,
                    -Vector3.Dot(lightRight, lightEye),
                    -Vector3.Dot(lightUp2,   lightEye),
                    -Vector3.Dot(lightFwd,   lightEye), 1f);

                var lightProj = Matrix4x4.CreateOrthographicOffCenter(
                    minX - centerLx, maxX - centerLx,
                    minY - centerLy, maxY - centerLy,
                    0f, maxZ - minZ);

                CascadeVP[c] = lightView * lightProj;
                prevSplit    = splitDepth;
            }
        }
    }
}
