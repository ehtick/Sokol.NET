using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.Renderer;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Physics;
using GameEditor.Framework.Scripting;

namespace GameEditor.Framework.Scene
{
    public static class SceneManager
    {
        public static Scene? ActiveScene { get; private set; }
        public static PlayModeState PlayMode { get; private set; } = PlayModeState.Stopped;

        // JSON snapshot of the scene captured when Play() is called; restored on Stop().
        private static string? _playSnapshot;

        // ---- Physics -------------------------------------------------------
        private static IPhysicsWorld? _physics;
        private static readonly Dictionary<Entity, PhysicsBodyHandle> _entityToHandle = new();
        private static readonly Dictionary<int, Entity> _handleToEntity = new();

        private sealed class PhysicsCollisionDispatcher : ICollisionListener
        {
            public void OnCollisionEnter(PhysicsBodyHandle a, PhysicsBodyHandle b, ContactPoint contact)
            {
                if (!_handleToEntity.TryGetValue(a.Value, out var eA) ||
                    !_handleToEntity.TryGetValue(b.Value, out var eB)) return;
                ScriptSystem.DispatchCollisionEnter(eA, eB);
                ScriptSystem.DispatchCollisionEnter(eB, eA);
            }

            public void OnCollisionStay(PhysicsBodyHandle a, PhysicsBodyHandle b)
            {
                if (!_handleToEntity.TryGetValue(a.Value, out var eA) ||
                    !_handleToEntity.TryGetValue(b.Value, out var eB)) return;
                ScriptSystem.DispatchCollisionStay(eA, eB);
                ScriptSystem.DispatchCollisionStay(eB, eA);
            }

            public void OnCollisionExit(PhysicsBodyHandle a, PhysicsBodyHandle b)
            {
                if (!_handleToEntity.TryGetValue(a.Value, out var eA) ||
                    !_handleToEntity.TryGetValue(b.Value, out var eB)) return;
                ScriptSystem.DispatchCollisionExit(eA, eB);
                ScriptSystem.DispatchCollisionExit(eB, eA);
            }

            public void OnTriggerEnter(PhysicsBodyHandle trigger, PhysicsBodyHandle other)
            {
                if (!_handleToEntity.TryGetValue(trigger.Value, out var eT) ||
                    !_handleToEntity.TryGetValue(other.Value, out var eO)) return;
                ScriptSystem.DispatchTriggerEnter(eT, eO);
                ScriptSystem.DispatchTriggerEnter(eO, eT);
            }

            public void OnTriggerExit(PhysicsBodyHandle trigger, PhysicsBodyHandle other)
            {
                if (!_handleToEntity.TryGetValue(trigger.Value, out var eT) ||
                    !_handleToEntity.TryGetValue(other.Value, out var eO)) return;
                ScriptSystem.DispatchTriggerExit(eT, eO);
                ScriptSystem.DispatchTriggerExit(eO, eT);
            }
        }

        public static void NewScene(string name = "Untitled")
        {
            ActiveScene?.Clear();
            EventBus.RaiseSceneUnloaded();
            ActiveScene = new Scene(name);
            EventBus.RaiseSceneLoaded();
            UndoStack.Clear();
        }

        public static void SaveScene(string path)
        {
            if (ActiveScene == null) return;
            string json = SceneSerializer.Serialize(ActiveScene);
            SokolFile.WriteAllText(path, json);
            ActiveScene.FilePath = path;
            ActiveScene.IsDirty = false;
            Logger.Info($"Scene saved to {path}");
        }

        public static void LoadScene(string path)
        {
            if (!SokolFile.Exists(path))
            {
                Logger.Warning($"Scene file not found: {path}");
                return;
            }
            EventBus.RaiseSceneUnloaded();
            ActiveScene ??= new Scene("Untitled");
            string json = SokolFile.ReadAllText(path);
            SceneSerializer.Deserialize(json, ActiveScene);
            ActiveScene.FilePath = path;
            ActiveScene.IsDirty = false;
            EventBus.RaiseSceneLoaded();
            UndoStack.Clear();
            Logger.Info($"Scene loaded from {path}");
        }

        public static void LoadSceneFromAssetsAsync(string assetPath)
        {
            GameFileSystem.Instance.LoadFile(assetPath, (path, buffer, status) =>
            {
                if (status == FileLoadStatus.Success)
                {
                    string json = System.Text.Encoding.UTF8.GetString(buffer);
                    EventBus.RaiseSceneUnloaded();
                    ActiveScene ??= new Scene("Untitled");
                    SceneSerializer.Deserialize(json, ActiveScene);
                    ActiveScene.FilePath = null; // Loaded from assets, not a file path
                    ActiveScene.IsDirty = false;
                    EventBus.RaiseSceneLoaded();
                    UndoStack.Clear();
                    Logger.Info($"Scene loaded from assets: {assetPath}");
                }
                else
                {
                    Logger.Warning($"Failed to load scene from assets: {assetPath}");
                }
            });

        }

        public static void SetPlayMode(PlayModeState state)
        {
            PlayMode = state;
            EventBus.RaisePlayModeChanged(state);
        }

        /// <summary>
        /// Saves a scene snapshot, populates the ScriptSystem from current entities,
        /// starts all behaviours and transitions to Playing state.
        /// </summary>
        public static void Play()
        {
            if (PlayMode == PlayModeState.Playing) return;

            if (ActiveScene == null)
            {
                Logger.Warning("[SceneManager] No active scene to play.");
                return;
            }

            if (PlayMode == PlayModeState.Stopped)
            {
                // Snapshot the scene so we can restore it on Stop()
                if (ActiveScene != null)
                {
                    _playSnapshot = SceneSerializer.Serialize(ActiveScene);
                    Logger.Info("[SceneManager] Play snapshot saved.");
                }

                // Populate script behaviours from current scene entities
                ScriptSystem.PopulateFromScene(ECS.ECSWorld.Instance);

                SetPlayMode(PlayModeState.Playing);

                // Initialize physics
                InitializePhysics();

                // Start all registered behaviours
                ScriptSystem.StartAll();
                Logger.Info($"[SceneManager] Play started. {ScriptSystem.Count} script(s) running.");
            }
            else if (PlayMode == PlayModeState.Paused)
            {
                // Resume without restarting scripts
                SetPlayMode(PlayModeState.Playing);
            }
        }

        /// <summary>Freezes script execution without destroying behaviours.</summary>
        public static void Pause()
        {
            if (PlayMode != PlayModeState.Playing) return;
            SetPlayMode(PlayModeState.Paused);
        }

        /// <summary>
        /// Stops all scripts, transitions to Stopped state and restores the pre-play
        /// scene snapshot so the editor sees the original unmodified scene.
        /// </summary>
        public static void Stop()
        {
            if (PlayMode == PlayModeState.Stopped) return;

            // Destroy all running scripts
            ScriptSystem.StopAll();

            // Shutdown physics
            ShutdownPhysics();

            SetPlayMode(PlayModeState.Stopped);

            // Restore the pre-play snapshot
            if (_playSnapshot != null && ActiveScene != null)
            {
                EventBus.RaiseSceneUnloaded();
                SceneSerializer.Deserialize(_playSnapshot, ActiveScene);
                ActiveScene.IsDirty = false;
                _playSnapshot = null;
                EventBus.RaiseSceneLoaded();
                Logger.Info("[SceneManager] Scene restored from play snapshot.");
            }

            UndoStack.Clear();
        }

        public static bool GetMainCameraMatrices(int width, int height,out Matrix4x4 viewProj, out Vector3 eyePos)
        {
            viewProj = Matrix4x4.Identity;
            eyePos = Vector3.Zero;
            if (width <= 0 || height <= 0) return false;

            var world = ECSWorld.Instance;
            foreach (Entity id in world.Entities)
            {
                if (!world.TryGetComponent<CameraComponent>(id, out var cam) || !cam.IsMain) continue;
                if (cam.NearZ <= 0f || cam.FarZ <= cam.NearZ) continue; // skip degenerate projection
                if (!world.TryGetComponent<Transform>(id, out var tr)) continue;

                Matrix4x4 rotMat = Matrix4x4.CreateFromQuaternion(tr.Rotation);

                // Use the rotation matrix's +Z column as forward (matches gizmo convention)
                Vector3 forward = new Vector3(rotMat.M31, rotMat.M32, rotMat.M33);
                Vector3 up = new Vector3(rotMat.M21, rotMat.M22, rotMat.M23);

                eyePos = tr.Position;
                Matrix4x4 view = Matrix4x4.CreateLookAt(eyePos, eyePos + forward, up);
                Matrix4x4 proj;
                if (cam.IsOrthographic)
                {
                    float orthoH = MathF.Max(0.01f, cam.OrthoSize > 0f ? cam.OrthoSize : 5f);
                    float orthoW = orthoH * ((float)width / height);
                    proj = Matrix4x4.CreateOrthographicOffCenter(-orthoW, orthoW, -orthoH, orthoH, cam.NearZ, cam.FarZ);
                }
                else
                {
                    float fov = MathF.Max(1f, cam.Fov) * MathF.PI / 180f;
                    proj = Matrix4x4.CreatePerspectiveFieldOfView(fov, (float)width / height, cam.NearZ, cam.FarZ);
                }
                viewProj = view * proj;
                return true;
            }
            return false;
        }

        public static void UpdatePhysics(float deltaTime)
        {
            if (_physics == null || PlayMode != PlayModeState.Playing) return;

            var world = ECSWorld.Instance;

            // Pre-step: push Transform → Jolt for kinematic bodies so scripts can drive them.
            foreach (var (entity, handle) in _entityToHandle)
            {
                if (!world.TryGetComponent<RigidbodyComponent>(entity, out var rb)) continue;
                if (rb.MotionType != RigidbodyMotionType.Kinematic) continue;
                if (!world.TryGetComponent<Transform>(entity, out var tr)) continue;

                // MoveKinematic computes implicit velocity so the body correctly pushes dynamic bodies.
                _physics.MoveKinematic(handle, tr.Position, tr.Rotation, deltaTime);
            }

            _physics.Step(deltaTime);

            // Post-step: sync physics positions/rotations back to Transform for dynamic bodies only.
            // Kinematic bodies are script-authoritative — skip them to avoid Quaternion→Euler round-trip drift.
            foreach (var (entity, handle) in _entityToHandle)
            {
                if (!world.TryGetComponent<RigidbodyComponent>(entity, out var rb)) continue;
                if (rb.IsStatic || rb.MotionType == RigidbodyMotionType.Kinematic) continue;
                if (!world.TryGetComponent<Transform>(entity, out var tr)) continue;

                var pos = _physics.GetPosition(handle);
                var rot = _physics.GetRotation(handle);

                tr.Position = pos;
                tr.Rotation = rot;
                world.AddComponent(entity, tr);
            }
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out Entity hitEntity, out RaycastHit hit)
        {
            hitEntity = default;
            hit = default;
            if (_physics == null) return false;
            if (!_physics.Raycast(origin, direction, maxDistance, out hit)) return false;
            if (!hit.Body.IsValid) return false;
            return _handleToEntity.TryGetValue(hit.Body.Value, out hitEntity);
        }

        public static bool TryGetLinearVelocity(Entity entity, out Vector3 velocity)
        {
            velocity = Vector3.Zero;
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            velocity = _physics.GetLinearVelocity(handle);
            return true;
        }

        public static bool SetLinearVelocity(Entity entity, Vector3 velocity)
        {
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            _physics.SetLinearVelocity(handle, velocity);
            return true;
        }

        public static bool TryGetAngularVelocity(Entity entity, out Vector3 velocity)
        {
            velocity = Vector3.Zero;
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            velocity = _physics.GetAngularVelocity(handle);
            return true;
        }

        public static bool SetAngularVelocity(Entity entity, Vector3 velocity)
        {
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            _physics.SetAngularVelocity(handle, velocity);
            return true;
        }

        public static bool AddForce(Entity entity, Vector3 force)
        {
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            _physics.AddForce(handle, force);
            return true;
        }

        public static bool AddImpulse(Entity entity, Vector3 impulse)
        {
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            _physics.AddImpulse(handle, impulse);
            return true;
        }

        public static bool AddTorque(Entity entity, Vector3 torque)
        {
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            _physics.AddTorque(handle, torque);
            return true;
        }

        public static int OverlapSphere(Vector3 center, float radius, List<Entity> results, int maxResults = 64)
        {
            results.Clear();
            if (_physics == null) return 0;

            var handles = new List<PhysicsBodyHandle>(maxResults);
            int hitCount = _physics.OverlapSphere(center, radius, handles, maxResults);
            for (int i = 0; i < hitCount; i++)
            {
                if (_handleToEntity.TryGetValue(handles[i].Value, out var entity))
                    results.Add(entity);
            }
            return results.Count;
        }

        public static int OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, List<Entity> results, int maxResults = 64)
        {
            results.Clear();
            if (_physics == null) return 0;

            var handles = new List<PhysicsBodyHandle>(maxResults);
            int hitCount = _physics.OverlapBox(center, halfExtents, rotation, handles, maxResults);
            for (int i = 0; i < hitCount; i++)
            {
                if (_handleToEntity.TryGetValue(handles[i].Value, out var entity))
                    results.Add(entity);
            }
            return results.Count;
        }

        public static bool TeleportBody(Entity entity, Vector3 position, Quaternion rotation)
        {
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;

            _physics.SetPosition(handle, position);
            _physics.SetRotation(handle, rotation);

            var world = ECSWorld.Instance;
            if (!world.TryGetComponent<Transform>(entity, out var tr)) return true;

            tr.Position = position;
            tr.Rotation = rotation;
            world.AddComponent(entity, tr);
            return true;
        }

        static void InitializePhysics()
        {
            _physics = new JoltPhysicsWorld();
            _physics.Initialize(new Vector3(0f, -9.81f, 0f));
            _physics.SetCollisionListener(new PhysicsCollisionDispatcher());

            var world = ECSWorld.Instance;
            foreach (Entity entity in world.Entities)
            {
                if (!world.TryGetComponent<RigidbodyComponent>(entity, out var rb)) continue;
                if (!world.TryGetComponent<Transform>(entity, out var tr)) continue;

                System.Numerics.Vector3[]? meshVerts = null;
                uint[]? meshIndices = null;
                if (rb.Shape == ColliderShape.ConvexHull &&
                    world.TryGetComponent<MeshRenderer>(entity, out var mr) &&
                    PrimitiveMeshSpec.TryParse(mr.MeshPath, out var spec))
                {
                    meshVerts = SceneRenderer.GetHullPoints(spec);
                }
                else if (rb.Shape == ColliderShape.Mesh &&
                    world.TryGetComponent<MeshRenderer>(entity, out var mr2) &&
                    PrimitiveMeshSpec.TryParse(mr2.MeshPath, out var spec2))
                {
                    (meshVerts, meshIndices) = SceneRenderer.GetMeshTriangles(spec2);
                }

                var desc = new BodyDesc(
                    tr.Position, tr.Rotation, tr.Scale,
                    rb.MotionType, rb.Mass, rb.UseGravity,
                    rb.Friction, rb.Restitution, rb.LinearDamping, rb.AngularDamping,
                    rb.Shape, rb.IsTrigger, rb.Layer, rb.LayerMask, meshVerts, meshIndices);

                var handle = _physics.CreateBody(desc);
                _entityToHandle[entity] = handle;
                _handleToEntity[handle.Value] = entity;
            }

            Logger.Info($"[SceneManager] Physics initialized. {_entityToHandle.Count} bodies created.");
        }

        static void ShutdownPhysics()
        {
            _physics?.Shutdown();
            _physics = null;
            _entityToHandle.Clear();
            _handleToEntity.Clear();
            Logger.Info("[SceneManager] Physics shutdown.");
        }
    }
}
