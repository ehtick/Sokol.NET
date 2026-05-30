using System;
using System.Runtime.CompilerServices;

namespace GameEditor.Framework.Renderer.Server.Lod
{
    /// <summary>
    /// Selects a LOD level for a single entity based on its screen-space coverage.
    ///
    /// Coverage ≈ worldRadius / distanceToCamera  (dimensionless; 1.0 = diameter equals
    /// camera distance, i.e. a very large or very close object).
    ///
    /// Without a <see cref="LodGroup"/> component, returns <c>0</c> (always draw at
    /// full detail).  A return value of <see cref="Skip"/> means the entity should be
    /// omitted from the draw list entirely (effectively free distance-based culling).
    /// </summary>
    public static class LodSelector
    {
        /// <summary>Sentinel returned by <see cref="Pick"/> when the entity should be skipped.</summary>
        public const int Skip = -1;

        /// <summary>
        /// Pick the appropriate LOD level for an entity given its world-space radius,
        /// camera distance, and optional LOD group definition.
        /// </summary>
        /// <param name="worldRadius">
        ///   Radius of the entity's world-space bounding sphere
        ///   (e.g. <c>Aabb.Extents.Length()</c> after transformation).
        /// </param>
        /// <param name="distToCamera">Distance from the entity centre to the camera.</param>
        /// <param name="previousLevel">
        ///   LOD level selected last frame; used for hysteresis to prevent flickering.
        ///   Pass <c>0</c> when no history is available.
        /// </param>
        /// <param name="group">
        ///   Optional LOD group attached to the entity.  Pass <c>null</c> to always
        ///   return LOD 0 (no distance culling).
        /// </param>
        /// <returns>
        ///   LOD index (≥ 0) to use, or <see cref="Skip"/> to omit the entity from the
        ///   draw list.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Pick(float worldRadius, float distToCamera, int previousLevel,
                               LodGroup? group = null)
        {
            // Coverage metric: simple radius / distance ratio.
            // Larger value → more of the screen occupied → use a finer LOD.
            float coverage = distToCamera > 0.001f ? worldRadius / distToCamera : float.MaxValue;

            if (group is { Levels: { Length: > 0 } levels })
            {
                // Walk levels from finest (0) to coarsest (N-1).
                // Switch to level i when coverage exceeds its threshold (plus hysteresis
                // to avoid flip-flopping near the boundary).
                int selected = Skip; // assume cull until a level claims it
                for (int i = 0; i < levels.Length; i++)
                {
                    float thr    = levels[i].ScreenCoverageThreshold;
                    float margin = levels[i].HysteresisMargin;

                    // Hysteresis: when moving away from a finer level (i < previousLevel)
                    // require coverage to drop below thr - margin; when moving toward finer
                    // (i >= previousLevel) accept above thr + margin.
                    float effectiveThr = (i < previousLevel)
                        ? thr - margin
                        : thr + margin;

                    if (coverage >= effectiveThr)
                    {
                        selected = i;
                        break; // first level whose threshold is met = finest qualifying level
                    }
                }
                return selected;
            }

            // No LOD group: always draw at LOD 0.
            return 0;
        }
    }
}
