using System.Collections.Generic;
using Frent;
using Frent.Core;
using Frent.Systems;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.ECS
{
    public sealed class ECSWorld : IDisposable
    {
        private World _world;
        private readonly List<Entity> _entities = new();

        public static ECSWorld Instance { get; private set; } = new ECSWorld();

        private ECSWorld() { _world = new World(); }

        public Entity CreateEntity(string name = "Entity")
        {
            var e = _world.Create(
                new NameTag { Name = name },
                new ActiveFlag { Active = true },
                Transform.Default);
            _entities.Add(e);
            EventBus.RaiseEntityCreated(e);
            return e;
        }

        public void DestroyEntity(Entity e)
        {
            _entities.Remove(e);
            EventBus.RaiseEntityDestroyed(e);
            if (e.IsAlive) e.Delete();
        }

        // Return the concrete List<Entity> to avoid IReadOnlyList<T> generic interface
        // dispatch, which causes a "function signature mismatch" crash in WASM NativeAOT.
        public List<Entity> Entities => _entities;
        
        // ── Name lookup with ALC bridge ───────────────────────────────────────
        // When the game DLL runs in an isolated AssemblyLoadContext its ECSWorld
        // singleton has an empty _entities list.  GameAssemblyRunner calls
        // RegisterFindByNameCallback so lookups are forwarded to the host world.

        private static Func<string, Entity?>? _findByNameBridge;

        public static void RegisterFindByNameCallback(Func<string, Entity?> fn)
            => _findByNameBridge = fn;

        /// <summary>
        /// Returns the first entity whose <see cref="NameTag.Name"/> equals
        /// <paramref name="name"/>, or <see langword="null"/> if not found.
        /// </summary>
        public Entity? FindEntityByName(string name)
        {
            if (_findByNameBridge != null) return _findByNameBridge(name);
            foreach (var e in _entities)
                if (TryGetComponent<NameTag>(e, out var tag) && tag.Name == name)
                    return e;
            return null;
        }

        /// <summary>
        /// Replaces the internal entity ordering with <paramref name="orderedEntities"/>.
        /// Entities not present in the input are appended in their existing relative order.
        /// </summary>
        public void SetEntityOrder(IEnumerable<Entity> orderedEntities)
        {
            var seen = new HashSet<Entity>();
            var next = new List<Entity>(_entities.Count);

            foreach (var e in orderedEntities)
            {
                if (!e.IsAlive) continue;
                if (!_entities.Contains(e)) continue;
                if (seen.Add(e)) next.Add(e);
            }

            // Keep any entities we didn't receive (defensive fallback).
            foreach (var e in _entities)
                if (seen.Add(e)) next.Add(e);

            _entities.Clear();
            _entities.AddRange(next);
        }

        public void AddComponent<T>(Entity e, T component) where T : struct
        {
            if (e.Has<T>())
                e.Get<T>() = component;
            else
                e.Add(component);
        }

        public bool HasComponent<T>(Entity e) where T : struct
            => e.Has<T>();

        public ref T GetComponent<T>(Entity e) where T : struct
            => ref e.Get<T>();

        public bool TryGetComponent<T>(Entity e, out T component) where T : struct
        {
            if (e.TryGet<T>(out var r))
            {
                component = r.Value;
                return true;
            }
            component = default;
            return false;
        }

        /// <summary>
        /// Zero-copy component access for hot paths: returns a writable <see cref="Ref{T}"/>
        /// into archetype storage in a single lookup. Unlike <see cref="TryGetComponent{T}"/>
        /// it does not copy the struct out, and unlike <see cref="AddComponent{T}"/> it needs
        /// no copy-back — mutate through <c>component.Value</c> directly.
        /// Valid only while no structural change (add/remove/create/delete) occurs.
        /// </summary>
        public bool TryGetComponentRef<T>(Entity e, out Ref<T> component) where T : struct
            => e.TryGet<T>(out component);

        /// <summary>
        /// Returns a cached Frent <see cref="Frent.Systems.Query"/> over entities that have both
        /// <typeparamref name="T1"/> and <typeparamref name="T2"/>. The query is built once per
        /// component-set and reused; its <c>Enumerate</c>/<c>EnumerateWithEntities</c> enumerators
        /// are <see langword="ref"/> structs, so iterating allocates nothing. Prefer this over
        /// scanning <see cref="Entities"/> + per-entity <see cref="TryGetComponent{T}"/> on
        /// per-frame paths.
        /// </summary>
        public Query Query<T1, T2>() => _world.Query<T1, T2>();

        /// <inheritdoc cref="Query{T1,T2}()"/>
        public Query Query<T1, T2, T3>() => _world.Query<T1, T2, T3>();

        public void RemoveComponent<T>(Entity e) where T : struct
        {
            if (e.Has<T>()) e.Remove<T>();
        }

        public IEnumerable<T> GetAllComponents<T>() where T : struct
        {
            var result = new List<T>();
            foreach (var chunk in _world.Query<T>().EnumerateChunks<T>())
                foreach (ref var c in chunk.Span)
                    result.Add(c);
            return result;
        }

        /// <summary>
        /// Returns <see langword="true"/> if at least one entity currently carries
        /// component <typeparamref name="T"/>.  Uses the Frent chunk enumerator
        /// (a <see langword="ref"/> struct — zero allocation) for an O(archetypes) check.
        /// </summary>
        public bool HasAnyComponent<T>() where T : struct
        {
            foreach (var chunk in _world.Query<T>().EnumerateChunks<T>())
                if (chunk.Span.Length > 0) return true;
            return false;
        }

        public void Clear()
        {
            var copy = new List<Entity>(_entities);
            foreach (var e in copy)
            {
                EventBus.RaiseEntityDestroyed(e);
                if (e.IsAlive) e.Delete();
            }
            _entities.Clear();
            _world.Dispose();
            _world = new World();
        }

        public void Dispose() => _world.Dispose();
    }
}
