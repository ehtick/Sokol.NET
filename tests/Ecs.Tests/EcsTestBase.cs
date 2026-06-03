using System;
using System.Collections.Generic;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using Xunit;

// ECSWorld.Instance and the static EventBus are process-global singletons, so the tests must not run
// in parallel against each other. Each test resets via EcsTestBase's ctor (World.Clear()).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Ecs.Tests
{
    /// <summary>
    /// Shared scaffolding: resets the singleton world per test and provides helpers that mirror the
    /// production code paths which write Transform.Parent, plus an invariant checker that compares the
    /// maintained parent→children index against a brute-force scan.
    /// </summary>
    public abstract class EcsTestBase
    {
        protected static readonly ECSWorld World = ECSWorld.Instance;

        protected EcsTestBase() => World.Clear();   // xUnit news up the class per test → clean slate

        // ── entity construction ──────────────────────────────────────────────────────────────
        protected static Entity NewEntity(string name = "e") => World.CreateEntity(name);

        protected static Entity NewChild(Entity parent, string name = "c")
        {
            Entity e = World.CreateEntity(name);
            Reparent(e, parent);
            return e;
        }

        /// <summary>Production reparent path: copy Transform → set Parent → AddComponent. This is the
        /// chokepoint where the index is maintained (Hierarchy drag, glTF import, deserialize, ALC sync
        /// all funnel through it).</summary>
        protected static void Reparent(Entity child, Entity? parent)
        {
            World.TryGetComponent<Transform>(child, out var tr);
            tr.Parent = parent;
            World.AddComponent(child, tr);
        }

        /// <summary>Raw reparent that BYPASSES AddComponent (mutates the component ref in place) —
        /// simulates the duplicate path / any future raw write the index can't observe until
        /// RebuildHierarchyIndex.</summary>
        protected static void RawSetParent(Entity child, Entity? parent)
        {
            ref Transform tr = ref World.GetComponent<Transform>(child);
            tr.Parent = parent;
        }

        // ── assertions ───────────────────────────────────────────────────────────────────────

        /// <summary>The core stability invariant: the maintained index equals a brute-force scan of
        /// every alive entity's Transform.Parent, every list entry is alive, and no dead entity lingers
        /// in <see cref="ECSWorld.Entities"/>.</summary>
        protected static void AssertIndexConsistent()
        {
            var expected = new Dictionary<Entity, HashSet<Entity>>();
            foreach (Entity e in World.Entities)
            {
                Assert.True(e.IsAlive, "a dead entity is still listed in ECSWorld.Entities");
                if (World.TryGetComponent<Transform>(e, out var tr) && tr.Parent.HasValue)
                {
                    if (!expected.TryGetValue(tr.Parent.Value, out var set))
                        expected[tr.Parent.Value] = set = new HashSet<Entity>();
                    Assert.True(set.Add(e), "the same entity appears twice in ECSWorld.Entities");
                }
            }

            foreach (Entity p in World.Entities)
            {
                IReadOnlyList<Entity> indexed = World.GetChildren(p);
                expected.TryGetValue(p, out var exp);
                Assert.Equal(exp?.Count ?? 0, indexed.Count);   // no missing AND no stale/duplicate
                for (int i = 0; i < indexed.Count; i++)
                {
                    Assert.True(indexed[i].IsAlive, "the index lists a dead child");
                    Assert.NotNull(exp);
                    Assert.Contains(indexed[i], exp!);
                }
            }
        }

        protected static int AliveCount()
        {
            int n = 0;
            foreach (Entity e in World.Entities) if (e.IsAlive) n++;
            return n;
        }

        /// <summary>The original-bug assertion: no alive entity may reference a dead parent (an orphan).
        /// Deleting a parent must cascade to every descendant.</summary>
        protected static void AssertNoOrphans()
        {
            var alive = new HashSet<Entity>(World.Entities);
            foreach (Entity e in World.Entities)
                if (World.TryGetComponent<Transform>(e, out var tr) && tr.Parent.HasValue)
                    Assert.True(alive.Contains(tr.Parent.Value),
                        "orphan: an alive entity still points at a deleted parent");
        }

        /// <summary>Runs <paramref name="action"/> while counting EntityDestroyed events, returning the
        /// total and the per-entity counts (to assert exactly-once delivery).</summary>
        protected static (int Total, Dictionary<Entity, int> PerEntity) CaptureDestroyed(Action action)
        {
            int total = 0;
            var per = new Dictionary<Entity, int>();
            void Handler(Entity e) { total++; per[e] = per.TryGetValue(e, out int c) ? c + 1 : 1; }

            EventBus.EntityDestroyed += Handler;
            try { action(); }
            finally { EventBus.EntityDestroyed -= Handler; }
            return (total, per);
        }
    }
}
