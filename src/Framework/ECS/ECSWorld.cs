using System.Collections.Generic;
using Frent;
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

        public IReadOnlyList<Entity> Entities => _entities;

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
