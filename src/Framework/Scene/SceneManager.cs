using System.Collections.Generic;
using System.Numerics;
using Frent;
using Sokol;
using GameEditor.Framework.Renderer;
using GameEditor.Framework.Renderer.Server;
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

        // Async mesh loads in flight (keyed by MeshPath); avoids duplicate SFilesystem requests.
        private static readonly HashSet<string> _pendingMeshLoads = new(StringComparer.Ordinal);

        // ---- Physics -------------------------------------------------------
        private static IPhysicsWorld? _physics;
        private static readonly Dictionary<Entity, PhysicsBodyHandle>      _entityToHandle    = new();
        private static readonly Dictionary<int, Entity>                     _handleToEntity    = new();
        private static readonly Dictionary<Entity, CharacterHandle>         _entityToCharHandle = new();
        private static readonly Dictionary<int, Entity>                     _charHandleToEntity = new();
        private static readonly Dictionary<Entity, VehicleHandle>           _entityToVehicleHandle = new();
        private static readonly Dictionary<int, Entity>                     _vehicleHandleToEntity = new();

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
            PreloadSceneMeshes();
            EventBus.RaiseSceneLoaded();
            UndoStack.Clear();
            Logger.Info($"Scene loaded from {path}");
        }

        public static void LoadSceneFromAssetsAsync(string assetPath)
        {
            SFilesystem.LoadFileAsync(assetPath, (path, buffer, status) =>
            {
                if (status == SFileLoadStatus.Success)
                {
                    string json = System.Text.Encoding.UTF8.GetString(buffer);
                    EventBus.RaiseSceneUnloaded();
                    ActiveScene ??= new Scene("Untitled");
                    SceneSerializer.Deserialize(json, ActiveScene);
                    ActiveScene.FilePath = null; // Loaded from assets, not a file path
                    ActiveScene.IsDirty = false;
                    PreloadSceneMeshes();
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
                PreloadSceneMeshes();
                EventBus.RaiseSceneLoaded();
                Logger.Info("[SceneManager] Scene restored from play snapshot.");
            }

            UndoStack.Clear();
        }

        /// <summary>
        /// Pre-loads all OBJ meshes referenced by MeshRenderer components in the current scene.
        /// Desktop: synchronous via File.ReadAllBytes.
        /// Web / asset bundle: async via SFilesystem.LoadFileAsync.
        /// Must be called after scene deserialization, on the main thread.
        /// </summary>
        // ── Static initializer: subscribe to EventBus events ─────────────────────────────
        static SceneManager()
        {
            EventBus.ComponentChanged += OnComponentChanged;
        }

        /// <summary>
        /// Reacts to Inspector changes on MeshRenderer: if MaterialPath points to a .mtl file
        /// that has not been loaded yet, load and register it now.
        /// </summary>
        private static void OnComponentChanged(Entity id, string componentName)
        {
            if (componentName != nameof(MeshRenderer)) return;
            if (!ECSWorld.Instance.TryGetComponent<MeshRenderer>(id, out var mr)) return;
            if (string.IsNullOrEmpty(mr.MaterialPath)) return;
            if (!mr.MaterialPath.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase)) return;
            if (!System.IO.File.Exists(mr.MaterialPath)) return;

            // Use just the filename as the registry key prefix so it matches sub.MaterialKey
            // (which is built as "mtlLib#materialName" using the OBJ's raw mtllib filename).
            string mtlFileName = System.IO.Path.GetFileName(mr.MaterialPath);
            string basePath    = System.IO.Path.GetDirectoryName(mr.MaterialPath) ?? string.Empty;
            byte[] mtlBytes    = System.IO.File.ReadAllBytes(mr.MaterialPath);
            RenderingServer.Materials.LoadMtl(
                mtlFileName,
                mtlBytes,
                basePath,
                relPath =>
                {
                    string abs = System.IO.Path.IsPathRooted(relPath)
                        ? relPath
                        : System.IO.Path.Combine(basePath, relPath);
                    return System.IO.File.Exists(abs) ? System.IO.File.ReadAllBytes(abs) : null;
                });
        }

        private static void PreloadSceneMeshes()
        {
            var world = ECSWorld.Instance;
            foreach (Entity id in world.Entities)
            {
                if (!world.TryGetComponent<MeshRenderer>(id, out var mr)) continue;
                if (string.IsNullOrEmpty(mr.MeshPath)) continue;
                if (mr.MeshPath.StartsWith("prim:", StringComparison.Ordinal)) continue;

                string path = mr.MeshPath;
                if (RenderingServer.Meshes.GetByPath(path) != null) continue;
                if (_pendingMeshLoads.Contains(path)) continue;

                if (System.IO.File.Exists(path))
                {
                    // Desktop: synchronous load from absolute path.
                    RenderingServer.Meshes.Load(path, System.IO.File.ReadAllBytes(path));
                    // Auto-load the co-located .mtl file (if the OBJ references one).
                    TryLoadMeshMtlDesktop(path);
                }
                else
                {
                    // Web / asset bundle: derive Assets-relative path and load async.
                    string? relPath = ToAssetsRelativePath(path);
                    if (relPath == null)
                    {
                        Logger.Warning($"[SceneManager] Mesh not found: '{path}'");
                        continue;
                    }
                    _pendingMeshLoads.Add(path);
                    string captured = path;
                    SFilesystem.LoadFileAsync(relPath, (_, buffer, status) =>
                    {
                        _pendingMeshLoads.Remove(captured);
                        if (status == SFileLoadStatus.Success && buffer != null)
                            RenderingServer.Meshes.Load(captured, buffer);
                        else
                            Logger.Warning($"[SceneManager] Failed to load mesh: '{relPath}'");
                    });
                }
            }
        }

        /// <summary>
        /// After an OBJ mesh has been loaded into MeshRegistry, look up its MtlLib filename,
        /// find the co-located .mtl file on disk, and register all materials from it.
        /// Desktop-only (synchronous File I/O). No-op if MtlLib is empty or file not found.
        /// </summary>
        private static void TryLoadMeshMtlDesktop(string meshPath)
        {
            var meshRes = RenderingServer.Meshes.GetByPath(meshPath);
            if (meshRes == null || string.IsNullOrEmpty(meshRes.MtlLib)) return;

            string basePath   = System.IO.Path.GetDirectoryName(meshPath) ?? string.Empty;
            string mtlAbsPath = System.IO.Path.Combine(basePath, meshRes.MtlLib);
            if (!System.IO.File.Exists(mtlAbsPath)) return;

            byte[] mtlBytes = System.IO.File.ReadAllBytes(mtlAbsPath);
            RenderingServer.Materials.LoadMtl(
                meshRes.MtlLib,   // key prefix matches sub.MaterialKey ("mtllib#matName")
                mtlBytes,
                basePath,
                relPath =>
                {
                    string abs = System.IO.Path.IsPathRooted(relPath)
                        ? relPath
                        : System.IO.Path.Combine(basePath, relPath);
                    return System.IO.File.Exists(abs) ? System.IO.File.ReadAllBytes(abs) : null;
                });
        }

        private static string? ToAssetsRelativePath(string path)
        {
            const string marker = "/Assets/";
            string normalised = path.Replace('\\', '/');
            int idx = normalised.IndexOf(marker, StringComparison.Ordinal);
            return idx >= 0 ? normalised.Substring(idx + marker.Length) : null;
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

            // Post-step: sync character controller positions back to Transform.
            foreach (var (entity, charHandle) in _entityToCharHandle)
            {
                if (!world.TryGetComponent<Transform>(entity, out var tr)) continue;

                tr.Position = _physics.GetCharacterPosition(charHandle);
                tr.Rotation = _physics.GetCharacterRotation(charHandle);
                world.AddComponent(entity, tr);
            }

            // Post-step: sync vehicle chassis positions back to Transform.
            foreach (var (handle, entity) in _vehicleHandleToEntity)
            {
                var vHandle = new VehicleHandle(handle);
                if (!world.TryGetComponent<Transform>(entity, out var tr)) continue;
                tr.Position = _physics.GetVehiclePosition(vHandle);
                tr.Rotation = _physics.GetVehicleRotation(vHandle);
                world.AddComponent(entity, tr);
            }

            // Sync wheel follower entity transforms to wheel world transforms.
            foreach (Entity entity in world.Entities)
            {
                if (!world.TryGetComponent<WheelFollowerComponent>(entity, out var wf)) continue;
                if (wf.VehicleEntity.IsNull) continue;
                if (!world.TryGetComponent<Transform>(entity, out var wfTr)) continue;
                var wm = GetWheelWorldTransform(wf.VehicleEntity, wf.WheelIndex);
                wfTr.Position = new Vector3(wm.M41, wm.M42, wm.M43);
                wfTr.Rotation = Quaternion.CreateFromRotationMatrix(wm);
                world.AddComponent(entity, wfTr);
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

                // Build the shapes list, populating geometry for ConvexHull/Mesh from the mesh renderer.
                var shapes = rb.Shapes != null
                    ? new List<ShapeEntry>(rb.Shapes)
                    : new List<ShapeEntry> { ShapeEntry.Default() };

                if (world.TryGetComponent<MeshRenderer>(entity, out var mr) &&
                    PrimitiveMeshSpec.TryParse(mr.MeshPath, out var spec))
                {
                    for (int i = 0; i < shapes.Count; i++)
                    {
                        var entry = shapes[i];
                        if (entry.Shape == ColliderShape.ConvexHull && entry.MeshVertices == null)
                        {
                            entry.MeshVertices = PrimitiveMeshGeometry.GetHullPoints(spec);
                            shapes[i] = entry;
                        }
                        else if (entry.Shape == ColliderShape.Mesh && entry.MeshVertices == null)
                        {
                            (entry.MeshVertices, entry.MeshIndices) = PrimitiveMeshGeometry.GetMeshTriangles(spec);
                            shapes[i] = entry;
                        }
                    }
                }

                var desc = new BodyDesc(
                    tr.Position, tr.Rotation, tr.Scale,
                    rb.MotionType, rb.Mass, rb.UseGravity, shapes,
                    rb.Friction, rb.Restitution, rb.LinearDamping, rb.AngularDamping,
                    rb.IsTrigger, rb.Layer, rb.LayerMask);

                var handle = _physics.CreateBody(desc);
                _entityToHandle[entity] = handle;
                _handleToEntity[handle.Value] = entity;
            }

            Logger.Info($"[SceneManager] Physics initialized. {_entityToHandle.Count} bodies created.");

            // Create constraints after all bodies are registered.
            foreach (Entity entity in world.Entities)
            {
                if (!world.TryGetComponent<ConstraintComponent>(entity, out var cc)) continue;

                _entityToHandle.TryGetValue(cc.BodyA, out var handleA);
                _entityToHandle.TryGetValue(cc.BodyB, out var handleB);
                if (!handleA.IsValid || !handleB.IsValid) continue;

                var cdesc = new ConstraintDesc(
                    cc.Type,
                    handleA, handleB,
                    cc.LocalAnchorA, cc.LocalAnchorB,
                    cc.LocalAxisA, cc.LocalAxisB,
                    cc.MinLimit, cc.MaxLimit);

                var ch = _physics.CreateConstraint(cdesc);
                cc.RuntimeHandle = ch;
                world.AddComponent(entity, cc);
            }

            // Create character controllers.
            foreach (Entity entity in world.Entities)
            {
                if (!world.TryGetComponent<CharacterComponent>(entity, out var cc)) continue;
                if (!world.TryGetComponent<Transform>(entity, out var tr)) continue;

                var charDesc = new CharacterDesc
                {
                    Position           = tr.Position,
                    Rotation           = tr.Rotation,
                    Height             = cc.Height,
                    Radius             = cc.Radius,
                    MaxSlopeAngle      = cc.MaxSlopeAngle,
                    MaxStrength        = cc.MaxStrength,
                    Mass               = cc.Mass,
                    Friction           = cc.Friction,
                    GravityFactor      = cc.GravityFactor,
                    CollisionTolerance = cc.CollisionTolerance,
                    ShapeOffset        = cc.ShapeOffset,
                    IsKinematic        = cc.Mode == CharacterMode.Kinematic,
                    Layer              = cc.Layer,
                    LayerMask          = cc.LayerMask,
                };
                var charHandle = _physics.CreateCharacter(charDesc);
                _entityToCharHandle[entity] = charHandle;
                _charHandleToEntity[charHandle.Value] = entity;
            }

            // Create vehicle controllers.
            foreach (Entity entity in world.Entities)
            {
                if (!world.TryGetComponent<VehicleComponent>(entity, out var vc)) continue;
                if (!world.TryGetComponent<Transform>(entity, out var tr)) continue;

                var wheels = new VehicleWheelDesc[vc.Wheels.Length];
                for (int i = 0; i < vc.Wheels.Length; i++)
                {
                    var w = vc.Wheels[i];
                    wheels[i] = new VehicleWheelDesc
                    {
                        LocalPosition     = w.Position,
                        Radius            = w.Radius,
                        Width             = w.Width,
                        SuspMinLength     = w.SuspMinLength,
                        SuspMaxLength     = w.SuspMaxLength,
                        SuspFrequency     = w.SuspFrequency,
                        SuspDamping       = w.SuspDamping,
                        MaxSteerAngle     = w.MaxSteerAngle,
                        MaxHandBrakeTorque = w.MaxHandBrakeTorque,
                        IsDriven          = w.IsDriven,
                    };
                }
                var vdesc = new VehicleDesc
                {
                    Type              = vc.Type,
                    Position          = tr.Position,
                    Rotation          = tr.Rotation,
                    ChassisHalfExtent = vc.ChassisHalfExtent,
                    Mass              = vc.Mass,
                    COMOffsetY        = vc.COMOffsetY,
                    MaxEngineTorque   = vc.MaxEngineTorque,
                    ClutchStrength    = vc.ClutchStrength,
                    MaxRollAngle      = vc.MaxRollAngle,
                    Friction          = vc.Friction,
                    Wheels            = wheels,
                    Layer             = vc.Layer,
                    LayerMask         = vc.LayerMask,
                };
                var vHandle = _physics!.CreateVehicle(vdesc);
                vc.RuntimeHandle = vHandle;
                world.AddComponent(entity, vc);
                _entityToVehicleHandle[entity] = vHandle;
                _vehicleHandleToEntity[vHandle.Value] = entity;
            }
        }

        /// <summary>
        /// Computes sensible default <see cref="ShapeEntry"/> dimensions for <paramref name="shape"/>
        /// from the entity's mesh spec (if available).  Used by the inspector when adding shapes.
        /// </summary>
        public static ShapeEntry DefaultShapeEntry(ColliderShape shape, PrimitiveMeshSpec? spec)
        {
            var entry = ShapeEntry.Default(shape);
            if (!spec.HasValue) return entry;
            var s = spec.Value;
            switch (shape)
            {
                case ColliderShape.Box:
                    entry.HalfExtent = new Vector3(s.Width * 0.5f, s.Height * 0.5f, s.Depth * 0.5f);
                    break;
                case ColliderShape.Sphere:
                    entry.Radius = s.Radius;
                    break;
                case ColliderShape.Capsule:
                    entry.Radius     = s.Radius;
                    entry.HalfHeight = MathF.Max(0f, s.Height * 0.5f - s.Radius);
                    break;
                case ColliderShape.Cylinder:
                    entry.Radius     = s.Radius;
                    entry.HalfHeight = s.Height * 0.5f;
                    break;
            }
            return entry;
        }

        static void ShutdownPhysics()
        {
            // Destroy constraints before bodies.
            var world = ECSWorld.Instance;
            foreach (Entity entity in world.Entities)
            {
                if (!world.TryGetComponent<ConstraintComponent>(entity, out var cc)) continue;
                if (!cc.RuntimeHandle.IsValid) continue;
                _physics?.DestroyConstraint(cc.RuntimeHandle);
                cc.RuntimeHandle = ConstraintHandle.Invalid;
                world.AddComponent(entity, cc);
            }

            // Destroy character controllers.
            foreach (var (_, charHandle) in _entityToCharHandle)
                _physics?.DestroyCharacter(charHandle);
            _entityToCharHandle.Clear();
            _charHandleToEntity.Clear();

            // Destroy vehicle controllers.
            foreach (var (_, vHandle) in _entityToVehicleHandle)
                _physics?.DestroyVehicle(vHandle);
            _entityToVehicleHandle.Clear();
            _vehicleHandleToEntity.Clear();
            _setVehicleInputBridge       = null;
            _isWheelOnGroundBridge       = null;
            _getWheelRotationSpeedBridge  = null;
            _getWheelWorldTransformBridge = null;

            _physics?.Shutdown();
            _physics = null;
            _entityToHandle.Clear();
            _handleToEntity.Clear();
            Logger.Info("[SceneManager] Physics shutdown.");
        }

        // ---- Character controller helpers (called by GameBehaviour) --------

        public static bool MoveCharacter(Entity entity, Vector3 velocity)
        {
            if (_physics == null || !_entityToCharHandle.TryGetValue(entity, out var h)) return false;
            _physics.SetCharacterLinearVelocity(h, velocity);
            return true;
        }

        public static bool IsCharacterGrounded(Entity entity)
        {
            if (_physics == null || !_entityToCharHandle.TryGetValue(entity, out var h)) return false;
            return _physics.IsCharacterGrounded(h);
        }

        public static bool TryGetCharacterGroundNormal(Entity entity, out Vector3 normal)
        {
            normal = Vector3.Zero;
            if (_physics == null || !_entityToCharHandle.TryGetValue(entity, out var h)) return false;
            normal = _physics.GetCharacterGroundNormal(h);
            return true;
        }

        // ---- Vehicle controller helpers (called by GameBehaviour) ----------

        // Bridge delegates injected by GameAssemblyRunner when the game DLL is
        // loaded in an isolated AssemblyLoadContext (same pattern as Logger/Input).
        private static Func<Entity, float, float, float, float, bool>? _setVehicleInputBridge;
        private static Func<Entity, int, bool>?                         _isWheelOnGroundBridge;
        private static Func<Entity, int, float>?                        _getWheelRotationSpeedBridge;
        private static Func<Entity, int, Matrix4x4>?                    _getWheelWorldTransformBridge;

        public static void RegisterVehicleCallbacks(
            Func<Entity, float, float, float, float, bool> setVehicleInput,
            Func<Entity, int, bool>                         isWheelOnGround,
            Func<Entity, int, float>                        getWheelRotationSpeed,
            Func<Entity, int, Matrix4x4>                    getWheelWorldTransform)
        {
            _setVehicleInputBridge          = setVehicleInput;
            _isWheelOnGroundBridge          = isWheelOnGround;
            _getWheelRotationSpeedBridge     = getWheelRotationSpeed;
            _getWheelWorldTransformBridge    = getWheelWorldTransform;
        }

        public static bool SetVehicleInput(Entity entity, float steer, float throttle, float brake, float handBrake = 0f)
        {
            if (_setVehicleInputBridge != null) return _setVehicleInputBridge(entity, steer, throttle, brake, handBrake);
            if (_physics == null || !_entityToVehicleHandle.TryGetValue(entity, out var h)) return false;
            _physics.SetVehicleInput(h, steer, throttle, brake, handBrake);
            return true;
        }

        public static bool IsWheelOnGround(Entity entity, int wheelIndex)
        {
            if (_isWheelOnGroundBridge != null) return _isWheelOnGroundBridge(entity, wheelIndex);
            if (_physics == null || !_entityToVehicleHandle.TryGetValue(entity, out var h)) return false;
            return _physics.IsWheelOnGround(h, wheelIndex);
        }

        public static float GetWheelRotationSpeed(Entity entity, int wheelIndex)
        {
            if (_getWheelRotationSpeedBridge != null) return _getWheelRotationSpeedBridge(entity, wheelIndex);
            if (_physics == null || !_entityToVehicleHandle.TryGetValue(entity, out var h)) return 0f;
            return _physics.GetWheelRotationSpeed(h, wheelIndex);
        }

        public static Matrix4x4 GetWheelWorldTransform(Entity entity, int wheelIndex)
        {
            if (_getWheelWorldTransformBridge != null) return _getWheelWorldTransformBridge(entity, wheelIndex);
            if (_physics == null || !_entityToVehicleHandle.TryGetValue(entity, out var h)) return Matrix4x4.Identity;
            return _physics.GetWheelWorldTransform(h, wheelIndex);
        }

        public static PhysicsBodyHandle GetVehicleBodyHandle(Entity entity)
        {
            if (_physics == null || !_entityToVehicleHandle.TryGetValue(entity, out var h)) return PhysicsBodyHandle.Invalid;
            return _physics.GetVehicleBodyHandle(h);
        }
    }
}
