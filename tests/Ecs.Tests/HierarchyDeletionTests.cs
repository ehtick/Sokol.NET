using Frent;
using GameEditor.Framework.ECS.Components;
using Xunit;

namespace Ecs.Tests
{
    /// <summary>
    /// Correctness of cascade <c>DestroyEntity</c> + the parent→children index. The bug these guard:
    /// deleting a parent in the Hierarchy used to orphan its children (alive, dangling Parent, never
    /// ForgetEntity'd in the renderer).
    /// </summary>
    public sealed class HierarchyDeletionTests : EcsTestBase
    {
        [Fact]
        public void DeleteParent_DeletesDirectChildren()
        {
            Entity root = NewEntity("root");
            Entity a = NewChild(root), b = NewChild(root), c = NewChild(root);

            World.DestroyEntity(root);

            Assert.False(root.IsAlive);
            Assert.False(a.IsAlive);
            Assert.False(b.IsAlive);
            Assert.False(c.IsAlive);
            Assert.Equal(0, AliveCount());
            Assert.Empty(World.Entities);
            AssertIndexConsistent();
        }

        [Fact]
        public void DeleteParent_DeletesWholeDeepSubtree()
        {
            // root → a → a1 → a1x ; root → b
            Entity root = NewEntity("root");
            Entity a = NewChild(root, "a");
            Entity a1 = NewChild(a, "a1");
            Entity a1x = NewChild(a1, "a1x");
            Entity b = NewChild(root, "b");

            World.DestroyEntity(root);

            foreach (Entity e in new[] { root, a, a1, a1x, b })
                Assert.False(e.IsAlive);
            Assert.Empty(World.Entities);
            AssertIndexConsistent();
        }

        [Fact]
        public void DeleteSubtree_LeavesSiblingsAndAncestorsIntact()
        {
            Entity root = NewEntity("root");
            Entity branch = NewChild(root, "branch");
            Entity leaf1 = NewChild(branch, "leaf1");
            Entity leaf2 = NewChild(branch, "leaf2");
            Entity sibling = NewChild(root, "sibling");

            World.DestroyEntity(branch);

            Assert.True(root.IsAlive);
            Assert.True(sibling.IsAlive);
            Assert.False(branch.IsAlive);
            Assert.False(leaf1.IsAlive);
            Assert.False(leaf2.IsAlive);
            // branch is gone from root's child list; sibling remains.
            Assert.Equal(new[] { sibling }, System.Linq.Enumerable.ToArray(World.GetChildren(root)));
            AssertIndexConsistent();
        }

        [Fact]
        public void DeleteChild_RemovesItFromParentIndex_ButKeepsParent()
        {
            Entity root = NewEntity("root");
            Entity child = NewChild(root);

            World.DestroyEntity(child);

            Assert.True(root.IsAlive);
            Assert.False(child.IsAlive);
            Assert.Empty(World.GetChildren(root));
            AssertIndexConsistent();
        }

        [Fact]
        public void DeleteParent_RaisesDestroyedEventExactlyOncePerDescendant()
        {
            Entity root = NewEntity("root");
            Entity a = NewChild(root), b = NewChild(root);
            Entity a1 = NewChild(a);

            var (total, per) = CaptureDestroyed(() => World.DestroyEntity(root));

            Assert.Equal(4, total);                       // root, a, b, a1
            foreach (Entity e in new[] { root, a, b, a1 })
                Assert.Equal(1, per[e]);                  // each exactly once (renderer ForgetEntity)
        }

        [Fact]
        public void Reparent_ThenDeleteNewParent_DeletesMovedChild()
        {
            Entity a = NewEntity("a");
            Entity b = NewEntity("b");
            Entity child = NewChild(a, "child");

            Reparent(child, b);                           // move child from a → b
            Assert.Empty(World.GetChildren(a));
            Assert.Equal(new[] { child }, System.Linq.Enumerable.ToArray(World.GetChildren(b)));

            World.DestroyEntity(b);

            Assert.False(child.IsAlive);
            Assert.True(a.IsAlive);
            AssertIndexConsistent();
        }

        [Fact]
        public void Reparent_ThenDeleteOldParent_DoesNotDeleteMovedChild()
        {
            Entity a = NewEntity("a");
            Entity b = NewEntity("b");
            Entity child = NewChild(a, "child");

            Reparent(child, b);                           // child now under b
            World.DestroyEntity(a);                       // deleting the OLD parent

            Assert.True(child.IsAlive);                   // child survived (it's under b now)
            Assert.True(b.IsAlive);
            Assert.Equal(new[] { child }, System.Linq.Enumerable.ToArray(World.GetChildren(b)));
            AssertIndexConsistent();
        }

        [Fact]
        public void ClearParent_DetachesFromCascade()
        {
            Entity root = NewEntity("root");
            Entity child = NewChild(root);

            Reparent(child, null);                        // clear parent
            Assert.Empty(World.GetChildren(root));

            World.DestroyEntity(root);

            Assert.True(child.IsAlive);                   // no longer a child → not cascaded
            AssertIndexConsistent();
        }

        [Fact]
        public void BulkDelete_ParentThenAlreadyDeletedChild_IsIdempotent()
        {
            Entity root = NewEntity("root");
            Entity child = NewChild(root);

            var (total, per) = CaptureDestroyed(() =>
            {
                World.DestroyEntity(root);                // cascades child
                World.DestroyEntity(child);               // child already dead → must be a no-op
            });

            Assert.Equal(2, total);                       // root + child, once each (no double-fire)
            Assert.Equal(1, per[child]);
            Assert.Empty(World.Entities);
        }

        [Fact]
        public void DeleteAlreadyDeadEntity_IsNoOp()
        {
            Entity e = NewEntity();
            World.DestroyEntity(e);

            var (total, _) = CaptureDestroyed(() => World.DestroyEntity(e));
            Assert.Equal(0, total);                       // no event, no throw
        }

        [Fact]
        public void ParentCycle_DeleteTerminatesAndDeletesBoth()
        {
            // A ↔ B mutual parenting (ECSWorld doesn't itself forbid cycles). The cascade must not
            // hang — the visited-set guard breaks the loop.
            Entity a = NewEntity("a");
            Entity b = NewEntity("b");
            Reparent(a, b);
            Reparent(b, a);

            World.DestroyEntity(a);

            Assert.False(a.IsAlive);
            Assert.False(b.IsAlive);
            Assert.Empty(World.Entities);
        }

        [Fact]
        public void RebuildHierarchyIndex_RecoversAfterRawParentWrite()
        {
            Entity a = NewEntity("a");
            Entity b = NewEntity("b");
            Entity child = NewChild(a, "child");

            RawSetParent(child, b);                       // bypasses AddComponent → index now stale
            // Index still thinks child is under a (stale) and doesn't know about b.
            Assert.Equal(new[] { child }, System.Linq.Enumerable.ToArray(World.GetChildren(a)));
            Assert.Empty(World.GetChildren(b));

            World.RebuildHierarchyIndex();                // full resync (the duplicate-path remedy)

            Assert.Empty(World.GetChildren(a));
            Assert.Equal(new[] { child }, System.Linq.Enumerable.ToArray(World.GetChildren(b)));
            AssertIndexConsistent();

            World.DestroyEntity(b);                        // now cascades correctly
            Assert.False(child.IsAlive);
            Assert.True(a.IsAlive);
        }

        [Fact]
        public void StaleIndex_DefensiveCheck_NeverDeletesANonChild()
        {
            // Raw-reparent child away from a WITHOUT rebuilding: a's index list still names child
            // (stale). Deleting a must NOT delete child, because DestroyEntity verifies the live link.
            Entity a = NewEntity("a");
            Entity b = NewEntity("b");
            Entity child = NewChild(a, "child");

            RawSetParent(child, b);                       // child.Parent == b now; index still lists it under a
            World.DestroyEntity(a);

            Assert.True(child.IsAlive);                   // defensive live-link verification saved it
            Assert.False(a.IsAlive);
        }

        [Fact]
        public void PlayStopRoundtrip_ClearResetsIndex()
        {
            Entity root = NewEntity("root");
            NewChild(root);
            NewChild(root);

            World.Clear();                                // simulates scene Stop (snapshot rebuild)

            Assert.Empty(World.Entities);
            Assert.Empty(World.GetChildren(root));        // index reset; old handle has no children
            AssertIndexConsistent();

            // Rebuild a fresh hierarchy and confirm cascade still works post-Clear.
            Entity r2 = NewEntity("r2");
            Entity c2 = NewChild(r2);
            World.DestroyEntity(r2);
            Assert.False(c2.IsAlive);
        }
    }
}
