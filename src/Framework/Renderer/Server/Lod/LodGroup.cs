namespace GameEditor.Framework.Renderer.Server.Lod
{
    /// <summary>
    /// One level in a LOD ladder.  Levels should be sorted descending by
    /// <see cref="ScreenCoverageThreshold"/> (highest coverage = closest = LOD 0).
    ///
    /// Attach a <see cref="LodGroup"/> to an entity (Frent ECS component) alongside
    /// <see cref="GameEditor.Framework.ECS.Components.MeshRenderer"/> to enable
    /// per-entity LOD selection.  When no <see cref="LodGroup"/> is present the renderer
    /// treats the entity as single-level (always LOD 0) and never distance-culls it.
    /// </summary>
    public struct LodLevel
    {
        /// <summary>
        /// Coverage fraction at which the renderer switches <em>to</em> this level.
        /// Coverage ≈ worldRadius / distanceToCamera (dimensionless ratio in [0, ∞)).
        /// Level 0 threshold is typically 1.0 (covers at least its own diameter on screen).
        /// </summary>
        public float ScreenCoverageThreshold;

        /// <summary>
        /// Hysteresis margin to avoid LOD flickering near threshold boundaries.
        /// Switch <em>down</em> when coverage &lt; threshold − margin;
        /// switch <em>up</em>   when coverage &gt; threshold + margin.
        /// Default: 0.02 (2 % of screen height).
        /// </summary>
        public float HysteresisMargin;

        public LodLevel(float threshold, float hysteresis = 0.02f)
        {
            ScreenCoverageThreshold = threshold;
            HysteresisMargin        = hysteresis;
        }
    }

    /// <summary>
    /// ECS component that defines a LOD ladder for an entity.
    /// Levels must be sorted <b>descending</b> by <see cref="LodLevel.ScreenCoverageThreshold"/>
    /// (highest coverage = closest detail = index 0).
    ///
    /// Example (3-level object):
    /// <code>
    ///   Levels[0].ScreenCoverageThreshold = 0.10f  // high detail when close
    ///   Levels[1].ScreenCoverageThreshold = 0.02f  // medium detail
    ///   Levels[2].ScreenCoverageThreshold = 0.005f // low detail
    ///   // below 0.005 → <see cref="LodSelector.Skip"/> (cull)
    /// </code>
    /// </summary>
    public struct LodGroup
    {
        /// <summary>LOD levels sorted descending by ScreenCoverageThreshold.</summary>
        public LodLevel[] Levels;

        public LodGroup(params LodLevel[] levels)
        {
            System.Diagnostics.Debug.Assert(levels.Length <= 16,
                $"LodGroup: {levels.Length} levels exceeds the 4-bit sort-key capacity (max 16). Wrap-around would mis-group draw commands.");
            Levels = levels;
        }
    }
}
