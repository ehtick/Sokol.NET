using System;
using System.Collections.Generic;
using System.Diagnostics;
using Frent;
using Xunit;

namespace Ecs.Tests
{
    /// <summary>
    /// Scale + stability tests for the parent→children index. These are the scenarios the index was
    /// added for: deleting a large subtree must be O(subtree), never a per-node world scan
    /// (the old O(subtree·N) cascade would stutter / hang at these sizes).
    /// </summary>
    public sealed class HierarchyStressTests : EcsTestBase
    {
        [Fact]
        public void DeepChain_DeleteRoot_DeletesAll_NoStackOverflow()
        {
            // A 50k-deep chain: root → c1 → c2 → … The cascade is iterative (worklist), so depth this
            // large must not blow the stack (a naive recursive cascade would StackOverflow here).
            const int depth = 50_000;
            Entity root = NewEntity("root");
            Entity cur = root;
            for (int i = 0; i < depth; i++)
                cur = NewChild(cur);

            Assert.Equal(depth + 1, AliveCount());

            World.DestroyEntity(root);

            Assert.Equal(0, AliveCount());
            Assert.Empty(World.Entities);
            AssertNoOrphans();
        }

        [Fact]
        public void WideTree_DeleteRoot_IsLinearNotQuadratic()
        {
            // One root with 100k direct children. Building (AddChildLink) and deleting (subtree walk +
            // one batched _entities removal) must both be ~O(n). The pre-index cascade was O(n²) here
            // (scan all entities per deleted node) and would take many seconds; assert a generous bound
            // that an O(n²) regression cannot meet but is immune to CI jitter.
            const int width = 100_000;
            Entity root = NewEntity("root");
            var children = new Entity[width];
            for (int i = 0; i < width; i++) children[i] = NewChild(root);

            Assert.Equal(width, World.GetChildren(root).Count);

            var sw = Stopwatch.StartNew();
            World.DestroyEntity(root);
            sw.Stop();

            Assert.Equal(0, AliveCount());
            Assert.Empty(World.Entities);
            Assert.True(sw.ElapsedMilliseconds < 4000,
                $"deleting a {width}-wide subtree took {sw.ElapsedMilliseconds} ms — possible O(n²) regression");
        }

        [Fact]
        public void DeleteSmallSubtree_InLargeWorld_LeavesEverythingElseIntact()
        {
            // Many unrelated entities + one small subtree. Deleting the subtree must remove exactly it
            // (5 nodes) and nothing else, and leave the index/entities consistent.
            const int bystanders = 40_000;
            for (int i = 0; i < bystanders; i++) NewEntity();

            Entity sroot = NewEntity("sroot");
            Entity s1 = NewChild(sroot), s2 = NewChild(sroot);
            Entity s1a = NewChild(s1), s1b = NewChild(s1);

            int before = AliveCount();
            World.DestroyEntity(sroot);

            Assert.Equal(before - 5, AliveCount());
            foreach (Entity e in new[] { sroot, s1, s2, s1a, s1b })
                Assert.False(e.IsAlive);
            AssertIndexConsistent();
            AssertNoOrphans();
        }

        [Fact]
        public void RandomChurn_IndexStaysConsistent()
        {
            // Deterministic fuzz: create / reparent (incl. to-null, and possibly cycles — all allowed
            // and exercised) / delete-subtree, verifying the full invariant after every single op.
            var rng = new Random(20260603);
            const int iterations = 4000;
            const int softCap = 600;

            // Seed a small forest.
            for (int i = 0; i < 24; i++) NewEntity();

            for (int it = 0; it < iterations; it++)
            {
                List<Entity> live = SnapshotAlive();
                int roll = rng.Next(100);

                if (live.Count == 0 || (roll < 45 && live.Count < softCap))
                {
                    // create — as a root or a child of a random existing entity
                    if (live.Count > 0 && rng.Next(2) == 0)
                        NewChild(live[rng.Next(live.Count)]);
                    else
                        NewEntity();
                }
                else if (roll < 70)
                {
                    // reparent a random entity to another random entity, or detach (null)
                    Entity child = live[rng.Next(live.Count)];
                    Entity? parent = rng.Next(4) == 0 ? null : live[rng.Next(live.Count)];
                    Reparent(child, parent);
                }
                else
                {
                    // delete a random entity (cascades its subtree)
                    World.DestroyEntity(live[rng.Next(live.Count)]);
                }

                AssertIndexConsistent();
                AssertNoOrphans();
            }
        }

        [Fact]
        public void RepeatedBuildAndTeardown_NoLeaks()
        {
            // Build and fully tear down a moderate tree many times; entity/index state must return to
            // empty each round (catches accumulating stale index entries or undeleted nodes).
            for (int round = 0; round < 50; round++)
            {
                Entity root = NewEntity("root");
                var mids = new List<Entity>();
                for (int m = 0; m < 20; m++)
                {
                    Entity mid = NewChild(root);
                    mids.Add(mid);
                    for (int leaf = 0; leaf < 20; leaf++) NewChild(mid);
                }

                Assert.Equal(1 + 20 + 20 * 20, AliveCount());   // root + mids + leaves = 421

                // Delete half the mid-subtrees individually, then the root takes the rest.
                for (int m = 0; m < mids.Count; m += 2) World.DestroyEntity(mids[m]);
                AssertNoOrphans();
                World.DestroyEntity(root);

                Assert.Equal(0, AliveCount());
                Assert.Empty(World.Entities);
                AssertIndexConsistent();
            }
        }

        private static List<Entity> SnapshotAlive()
        {
            var list = new List<Entity>(World.Entities.Count);
            foreach (Entity e in World.Entities) if (e.IsAlive) list.Add(e);
            return list;
        }
    }
}
