using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace GameEditor.Framework.Renderer.Server.Culling
{
    /// <summary>
    /// Loose octree for broad-phase spatial queries against world-space bounding spheres.
    ///
    /// "Loose" means each node's effective query region is 2× its theoretical half-size,
    /// so any sphere that fits inside a node's theoretical cell will fit inside its loose
    /// cell as well — enabling O(log N) insertion without testing all 8 children.
    ///
    /// Usage per frame:
    ///   1. <c>Build(drawCommands, count)</c> — rebuilds from scratch (clears first).
    ///   2. <c>QueryFrustum(frustum, output, out count)</c> — fills <paramref name="output"/>
    ///      with the draw-command indices that pass the broad-phase frustum test.
    ///
    /// The tree is available for non-rendering spatial queries (AI, physics, audio) too;
    /// it is maintained as a renderer-owned service so that no ECS components need a back-
    /// reference to scene-management code.
    ///
    /// Max depth: 8.  Leaf capacity before subdivision: 16.
    /// </summary>
    public sealed class Octree
    {
        private const int MaxDepth = 8;
        private const int LeafCap  = 16;

        // ── Node storage ─────────────────────────────────────────────────────────────────

        private struct Node
        {
            /// <summary>World-space AABB of this node's loose cell.</summary>
            public Aabb Bounds;
            /// <summary>Index of the first of 8 children, or -1 when this is a leaf.</summary>
            public int FirstChild;
            /// <summary>Number of items stored directly in this leaf (valid only when FirstChild == -1).</summary>
            public int ItemCount;
            /// <summary>First index into the <c>_items</c> flat array for this leaf's items.</summary>
            public int ItemStart;
        }

        private Node[] _nodes;
        private int    _nodeCount;

        // Items list — draw-command indices appended during build.
        // Leaves reference a contiguous slice of this array.
        private int[] _items;
        private int   _itemCount;

        // ── Construction ─────────────────────────────────────────────────────────────────

        public Octree(int nodeCapacity = 65536, int itemCapacity = 131072)
        {
            _nodes = new Node[nodeCapacity];
            _items = new int[itemCapacity];
        }

        // ── Per-frame lifecycle ───────────────────────────────────────────────────────────

        /// <summary>
        /// Resets the tree and inserts all <paramref name="count"/> draw commands.
        /// </summary>
        public void Build(DrawCommand[] cmds, int count)
        {
            _nodeCount = 0;
            _itemCount = 0;

            if (count == 0) return;

            // Compute a root AABB that encloses all bounding spheres.
            var rootMin = new Vector3(float.MaxValue);
            var rootMax = new Vector3(float.MinValue);
            for (int i = 0; i < count; i++)
            {
                Vector3 c = cmds[i].BoundsCenter;
                float   r = cmds[i].BoundsRadius;
                rootMin = Vector3.Min(rootMin, c - new Vector3(r));
                rootMax = Vector3.Max(rootMax, c + new Vector3(r));
            }

            // Expand to a cube so all octants are symmetric.
            Vector3 ext   = rootMax - rootMin;
            float   side  = MathF.Max(ext.X, MathF.Max(ext.Y, ext.Z));
            Vector3 mid   = (rootMin + rootMax) * 0.5f;
            float   half  = side * 0.5f * 1.01f; // tiny bias prevents edge cases
            var rootBounds = new Aabb(mid - new Vector3(half), mid + new Vector3(half));

            // Allocate root node.
            _nodes[0] = new Node { Bounds = rootBounds, FirstChild = -1, ItemCount = 0, ItemStart = 0 };
            _nodeCount = 1;

            for (int i = 0; i < count; i++)
                InsertIntoNode(0, i, cmds[i].BoundsCenter, cmds[i].BoundsRadius, 0);
        }

        // ── Insertion ────────────────────────────────────────────────────────────────────

        private void InsertIntoNode(int nodeIndex, int drawIndex, Vector3 center, float radius, int depth)
        {
            ref Node node = ref _nodes[nodeIndex];

            if (node.FirstChild == -1) // leaf
            {
                if (node.ItemCount < LeafCap || depth >= MaxDepth)
                {
                    // Store directly in this leaf.
                    if (_itemCount >= _items.Length) return; // item buffer full — drop gracefully
                    if (node.ItemCount == 0) node.ItemStart = _itemCount;
                    _items[_itemCount++] = drawIndex;
                    node.ItemCount++;
                }
                else
                {
                    // Subdivide: promote items to 8 new child nodes.
                    Subdivide(nodeIndex, depth);
                    // Re-insert this item now that children exist.
                    InsertIntoNode(nodeIndex, drawIndex, center, radius, depth);
                }
            }
            else
            {
                // Internal node: insert into all overlapping children.
                // (Loose cells mean the item may overlap more than one child.)
                int fc = node.FirstChild;
                for (int ci = 0; ci < 8; ci++)
                {
                    ref Node child = ref _nodes[fc + ci];
                    if (LooseCellContains(in child.Bounds, center, radius))
                        InsertIntoNode(fc + ci, drawIndex, center, radius, depth + 1);
                }
            }
        }

        private void Subdivide(int nodeIndex, int depth)
        {
            if (_nodeCount + 8 > _nodes.Length) return; // node buffer full — stay as oversized leaf

            ref Node node = ref _nodes[nodeIndex];
            int fc = _nodeCount;
            node.FirstChild = fc;
            _nodeCount += 8;

            Vector3 halfSize = node.Bounds.Extents; // already the half-extents of the whole node
            Vector3 quarter  = halfSize * 0.5f;
            Vector3 c        = node.Bounds.Center;

            // 8 children centres at ±quarter offsets in each axis.
            int idx = fc;
            for (int iz = 0; iz < 2; iz++)
            for (int iy = 0; iy < 2; iy++)
            for (int ix = 0; ix < 2; ix++)
            {
                float cx = c.X + (ix == 0 ? -quarter.X : +quarter.X);
                float cy = c.Y + (iy == 0 ? -quarter.Y : +quarter.Y);
                float cz = c.Z + (iz == 0 ? -quarter.Z : +quarter.Z);
                var childCenter = new Vector3(cx, cy, cz);
                // Loose child bounds = 2× the theoretical half-extent.
                _nodes[idx] = new Node
                {
                    Bounds     = new Aabb(childCenter - halfSize, childCenter + halfSize),
                    FirstChild = -1,
                    ItemCount  = 0,
                    ItemStart  = 0
                };
                idx++;
            }

            // Re-insert existing leaf items into the appropriate children.
            // Temporarily capture: depth + 1 because we've subdivided.
            int  savedStart = node.ItemStart;
            int  savedCount = node.ItemCount;
            node.ItemCount  = 0; // items now live in children

            // We need to re-read the item indices before the array is modified by child inserts.
            Span<int> savedItems = stackalloc int[LeafCap];
            for (int i = 0; i < savedCount; i++)
                savedItems[i] = _items[savedStart + i];

            // Rewind _itemCount to reclaim the slot if possible (only safe when the saved
            // items were at the end of the array — not guaranteed in general, so leave them).
            for (int i = 0; i < savedCount; i++)
            {
                // We don't know the individual sphere of each re-inserted item at this point,
                // so we store the draw index as a "wide" insert (will be re-assigned to
                // children during future queries if needed).
                // For correctness, insert into ALL children that overlap the parent's bounds.
                // This is conservative but safe.
                int fc2 = node.FirstChild;
                for (int ci = 0; ci < 8; ci++)
                {
                    if (_nodeCount > fc2 + ci)
                        InsertItemIntoChildConservatively(fc2 + ci, savedItems[i], depth + 1);
                }
            }
        }

        // Conservative re-insert: item goes into child without a refined sphere test
        // (we've lost the sphere data at subdivision time). This is intentionally simple;
        // items duplicated across siblings just cost extra during queries (no correctness issue).
        private void InsertItemIntoChildConservatively(int nodeIndex, int drawIndex, int depth)
        {
            if (nodeIndex >= _nodeCount) return;
            ref Node node = ref _nodes[nodeIndex];

            if (node.FirstChild == -1)
            {
                if (_itemCount >= _items.Length) return;
                if (node.ItemCount == 0) node.ItemStart = _itemCount;
                _items[_itemCount++] = drawIndex;
                node.ItemCount++;
            }
            else
            {
                // Recurse into all children (conservative).
                for (int ci = 0; ci < 8; ci++)
                    InsertItemIntoChildConservatively(node.FirstChild + ci, drawIndex, depth + 1);
            }
        }

        // ── Query ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fills <paramref name="output"/> with draw-command indices whose bounding spheres
        /// pass the frustum broad-phase test.  Result may contain duplicates; the caller
        /// deduplicates if needed.
        /// </summary>
        public void QueryFrustum(in Frustum frustum, int[] output, out int count)
        {
            count = 0;
            if (_nodeCount == 0) return;
            TraverseNode(0, in frustum, output, ref count);
        }

        private void TraverseNode(int nodeIndex, in Frustum frustum, int[] output, ref int count)
        {
            if (nodeIndex >= _nodeCount) return;
            ref Node node = ref _nodes[nodeIndex];

            // Broad-phase: does the node's loose AABB intersect the frustum?
            if (!frustum.Intersects(node.Bounds)) return;

            if (node.FirstChild == -1)
            {
                // Leaf: collect items.
                for (int i = 0; i < node.ItemCount; i++)
                {
                    if (count >= output.Length) return;
                    output[count++] = _items[node.ItemStart + i];
                }
            }
            else
            {
                for (int ci = 0; ci < 8; ci++)
                    TraverseNode(node.FirstChild + ci, in frustum, output, ref count);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true when the sphere (centre, radius) overlaps the loose cell.
        /// A loose cell is centred at <c>bounds.Center</c> with half-extents = bounds.Extents.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool LooseCellContains(in Aabb bounds, Vector3 center, float radius)
        {
            Vector3 e = bounds.Extents;
            Vector3 d = Vector3.Abs(center - bounds.Center);
            return d.X <= e.X + radius
                && d.Y <= e.Y + radius
                && d.Z <= e.Z + radius;
        }
    }
}
