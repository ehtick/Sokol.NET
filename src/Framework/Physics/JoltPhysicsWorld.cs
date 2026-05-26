using System;
using System.Collections.Generic;
using System.Numerics;

namespace GameEditor.Framework.Physics
{
    public sealed class JoltPhysicsWorld : IPhysicsWorld
    {
        const ushort ObjLayerNonMoving = 0;
        const ushort ObjLayerMoving    = 1;

        JPH.TempAllocatorImpl?                          _tempAlloc;
        JPH.JobSystemThreadPool?                       _jobSystem;
        JPH.PhysicsSystem?                             _physics;
        JPH.BodyInterface?                             _bodyInterface;
        JPH.ContactListenerTrampolineManaged?          _contactListener;
        ICollisionListener?                            _gameListener;

        // These must outlive _physics: the native PhysicsSystem stores raw pointers to them.
        JPH.BroadPhaseLayerInterfaceTable?             _bpInterface;
        JPH.ObjectLayerPairFilterTable?                _pairFilter;
        JPH.ObjectVsBroadPhaseLayerFilterTable?        _objVsBP;

        int _nextHandle = 1;
        readonly Dictionary<int, JPH.BodyID>  _handleToBodyId  = new();
        readonly Dictionary<uint, int>         _packedToHandle  = new();
        readonly Dictionary<uint, ushort>      _packedToLayer   = new();
        readonly Dictionary<uint, ushort>      _packedToMask    = new();
        readonly HashSet<uint>                 _sensors         = new();

        enum EvKind { CollisionEnter, CollisionStay, CollisionExit, TriggerEnter, TriggerExit }
        readonly record struct CollEv(uint PackedA, uint PackedB, ContactPoint Contact, EvKind Kind);
        readonly List<CollEv> _pending = new();
        readonly object _pendingLock   = new object();

        float _accumulator;
        const float FixedDt = 1f / 60f;

        // Jolt global state (factory, registered types) is a process-lifetime singleton.
        // Init once on first use; never call Shutdown mid-session (mirrors JoltPhysicsDemo pattern).
        static bool _joltGlobalInitialized = false;

        // ---- IPhysicsWorld --------------------------------------------------

        public unsafe void Initialize(Vector3 gravity)
        {
            if (!_joltGlobalInitialized)
            {
                JPH.Const_JoltHelpers.Init();
                _joltGlobalInitialized = true;
            }

            _tempAlloc = new JPH.TempAllocatorImpl(64 * 1024 * 1024);
            int numThreads = Math.Max(1, Environment.ProcessorCount - 1);
            _jobSystem = new JPH.JobSystemThreadPool(2048, 8, numThreads);

            _bpInterface = new JPH.BroadPhaseLayerInterfaceTable(2, 2);
            using var bp0 = new JPH.BroadPhaseLayer(0);
            using var bp1 = new JPH.BroadPhaseLayer(1);
            _bpInterface.MapObjectToBroadPhaseLayer(ObjLayerNonMoving, bp0);
            _bpInterface.MapObjectToBroadPhaseLayer(ObjLayerMoving,    bp1);

            _pairFilter = new JPH.ObjectLayerPairFilterTable(2);
            _pairFilter.EnableCollision(ObjLayerMoving,    ObjLayerNonMoving);
            _pairFilter.EnableCollision(ObjLayerMoving,    ObjLayerMoving);

            _objVsBP = new JPH.ObjectVsBroadPhaseLayerFilterTable(_bpInterface, 2, _pairFilter, 2);

            _physics = new JPH.PhysicsSystem();
            _physics.Init(65536, 0, 65536, 65536, _bpInterface, _objVsBP, _pairFilter);
            _physics.SetGravity(new JPH.Vec3(gravity.X, gravity.Y, gravity.Z));
            _bodyInterface = _physics.GetBodyInterface();

            SetupContactListener();
        }

        public void Step(float deltaTime)
        {
            if (_physics == null) return;

            _accumulator += deltaTime;
            while (_accumulator >= FixedDt)
            {
                _physics.Update(FixedDt, 1, _tempAlloc, _jobSystem);
                _accumulator -= FixedDt;
            }

            DispatchPendingEvents();
        }

        public void Shutdown()
        {
            if (_contactListener != null)
            {
                _physics?.SetContactListener(null);
                _contactListener.Dispose();
                _contactListener = null;
            }

            foreach (var (handle, bodyId) in _handleToBodyId)
            {
                _bodyInterface?.RemoveBody(bodyId);
                _bodyInterface?.DestroyBody(bodyId);
            }

            _handleToBodyId.Clear();
            _packedToHandle.Clear();
            _packedToLayer.Clear();
            _packedToMask.Clear();
            _sensors.Clear();

            _physics?.Dispose();   _physics    = null;
            _objVsBP?.Dispose();   _objVsBP    = null;
            _pairFilter?.Dispose(); _pairFilter = null;
            _bpInterface?.Dispose(); _bpInterface = null;
            _jobSystem?.Dispose(); _jobSystem  = null;
            _tempAlloc?.Dispose(); _tempAlloc  = null;
            _bodyInterface = null;
            // Note: JPH.Const_JoltHelpers.Shutdown() is intentionally NOT called here.
            // The factory and registered types are process-lifetime singletons; calling
            // Shutdown() mid-session corrupts global state on subsequent play cycles.
        }

        public unsafe PhysicsBodyHandle CreateBody(BodyDesc desc)
        {
            if (_bodyInterface == null)
                throw new InvalidOperationException("JoltPhysicsWorld not initialized.");

            using var cs = new JPH.BodyCreationSettings();

            // Shape
            switch (desc.Shape)
            {
                case ColliderShape.Sphere:
                {
                    float r = MathF.Max(desc.Scale.X, MathF.Max(desc.Scale.Y, desc.Scale.Z)) * 0.5f;
                    using var ss = new JPH.SphereShapeSettings(r);
                    cs.SetShapeSettings(ss);
                    break;
                }
                case ColliderShape.Capsule:
                {
                    float radius     = MathF.Max(desc.Scale.X, desc.Scale.Z) * 0.5f;
                    float halfHeight = MathF.Max(0f, desc.Scale.Y - radius);
                    using var ss = new JPH.CapsuleShapeSettings(halfHeight, radius);
                    cs.SetShapeSettings(ss);
                    break;
                }
                case ColliderShape.Cylinder:
                {
                    float radius    = MathF.Max(desc.Scale.X, desc.Scale.Z) * 0.5f;
                    float halfHeight = desc.Scale.Y * 0.5f;
                    using var ss = new JPH.CylinderShapeSettings(halfHeight, radius);
                    cs.SetShapeSettings(ss);
                    break;
                }
                case ColliderShape.Plane:
                {
                    using var plane = new JPH.Plane(new JPH.Vec4(0f, 1f, 0f, 0f));
                    using var ss = new JPH.PlaneShapeSettings(plane);
                    cs.SetShapeSettings(ss);
                    break;
                }
                case ColliderShape.ConvexHull:
                {
                    Vector3 s = desc.Scale;
                    Vector3[] src = desc.MeshVertices ?? new Vector3[]
                    {
                        new(-0.5f,-0.5f,-0.5f), new(0.5f,-0.5f,-0.5f),
                        new( 0.5f, 0.5f,-0.5f), new(-0.5f, 0.5f,-0.5f),
                        new(-0.5f,-0.5f, 0.5f), new(0.5f,-0.5f, 0.5f),
                        new( 0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f),
                    };
                    var pts = new JPH.Vec3f[src.Length];
                    for (int i = 0; i < src.Length; i++)
                        pts[i] = new JPH.Vec3f(src[i].X * s.X, src[i].Y * s.Y, src[i].Z * s.Z);
                    using var hull = JPH.ConvexHullShapeSettingsFromPoints(pts, 0.05f);
                    cs.SetShapeSettings(hull);
                    break;
                }
                default: // Box
                {
                    using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(desc.Scale.X * 0.5f, desc.Scale.Y * 0.5f, desc.Scale.Z * 0.5f));
                    cs.SetShapeSettings(ss);
                    break;
                }
            }

            // Position and rotation
            cs.mPosition.Set(desc.Position.X, desc.Position.Y, desc.Position.Z);
            cs.mRotation.Set(desc.Rotation.X, desc.Rotation.Y, desc.Rotation.Z, desc.Rotation.W);

            // Motion type and layer
            switch (desc.MotionType)
            {
                case RigidbodyMotionType.Static:
                    cs.mMotionType  = JPH.EMotionType.Static;
                    cs.mObjectLayer = ObjLayerNonMoving;
                    break;
                case RigidbodyMotionType.Kinematic:
                    cs.mMotionType  = JPH.EMotionType.Kinematic;
                    cs.mObjectLayer = ObjLayerMoving;
                    break;
                default:
                    cs.mMotionType  = JPH.EMotionType.Dynamic;
                    cs.mObjectLayer = ObjLayerMoving;
                    break;
            }

            if (!desc.UseGravity)
                cs.mGravityFactor = 0f;

            cs.mFriction = Math.Clamp(desc.Friction, 0f, 1f);
            cs.mRestitution = Math.Clamp(desc.Restitution, 0f, 1f);
            cs.mLinearDamping = Math.Max(0f, desc.LinearDamping);
            cs.mAngularDamping = Math.Max(0f, desc.AngularDamping);

            cs.mIsSensor = desc.IsTrigger;

            var activation = desc.IsStatic ? JPH.EActivation.DontActivate : JPH.EActivation.Activate;
            var bodyId = _bodyInterface.CreateAndAddBody(cs, activation);

            int handle = _nextHandle++;
            uint packed = bodyId.GetIndexAndSequenceNumber();
            _handleToBodyId[handle] = bodyId;
            _packedToHandle[packed] = handle;
            _packedToLayer[packed] = desc.Layer;
            _packedToMask[packed] = desc.LayerMask;
            if (desc.IsTrigger)
                _sensors.Add(packed);

            return new PhysicsBodyHandle(handle);
        }

        public void DestroyBody(PhysicsBodyHandle handle)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            uint packed = bodyId.GetIndexAndSequenceNumber();
            _bodyInterface?.RemoveBody(bodyId);
            _bodyInterface?.DestroyBody(bodyId);
            _handleToBodyId.Remove(handle.Value);
            _packedToHandle.Remove(packed);
            _packedToLayer.Remove(packed);
            _packedToMask.Remove(packed);
            _sensors.Remove(packed);
        }

        public void SetPosition(PhysicsBodyHandle handle, Vector3 position)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            using var pos = new JPH.Vec3(position.X, position.Y, position.Z);
            _bodyInterface?.SetPosition(bodyId, pos, JPH.EActivation.Activate);
        }

        public void SetRotation(PhysicsBodyHandle handle, Quaternion rotation)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            using var rot = new JPH.Quat(rotation.X, rotation.Y, rotation.Z, rotation.W);
            _bodyInterface?.SetRotation(bodyId, rot, JPH.EActivation.Activate);
        }

        public void MoveKinematic(PhysicsBodyHandle handle, Vector3 targetPosition, Quaternion targetRotation, float deltaTime)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            using var pos = new JPH.Vec3(targetPosition.X, targetPosition.Y, targetPosition.Z);
            using var rot = new JPH.Quat(targetRotation.X, targetRotation.Y, targetRotation.Z, targetRotation.W);
            _bodyInterface?.MoveKinematic(bodyId, pos, rot, deltaTime);
        }

        public Vector3 GetPosition(PhysicsBodyHandle handle)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return Vector3.Zero;
            using var pos = _bodyInterface!.GetPosition(bodyId);
            return new Vector3(pos.GetX(), pos.GetY(), pos.GetZ());
        }

        public Quaternion GetRotation(PhysicsBodyHandle handle)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return Quaternion.Identity;
            using var rot = _bodyInterface!.GetRotation(bodyId);
            return new Quaternion(rot.GetX(), rot.GetY(), rot.GetZ(), rot.GetW());
        }

        public void SetLinearVelocity(PhysicsBodyHandle handle, Vector3 velocity)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            using var v = new JPH.Vec3(velocity.X, velocity.Y, velocity.Z);
            _bodyInterface?.SetLinearVelocity(bodyId, v);
        }

        public Vector3 GetLinearVelocity(PhysicsBodyHandle handle)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return Vector3.Zero;
            using var v = _bodyInterface!.GetLinearVelocity(bodyId);
            return new Vector3(v.GetX(), v.GetY(), v.GetZ());
        }

        public void SetAngularVelocity(PhysicsBodyHandle handle, Vector3 velocity)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            using var v = new JPH.Vec3(velocity.X, velocity.Y, velocity.Z);
            _bodyInterface?.SetAngularVelocity(bodyId, v);
        }

        public Vector3 GetAngularVelocity(PhysicsBodyHandle handle)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return Vector3.Zero;
            using var v = _bodyInterface!.GetAngularVelocity(bodyId);
            return new Vector3(v.GetX(), v.GetY(), v.GetZ());
        }

        public void AddForce(PhysicsBodyHandle handle, Vector3 force)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            using var f = new JPH.Vec3(force.X, force.Y, force.Z);
            _bodyInterface?.AddForce(bodyId, f);
        }

        public void AddImpulse(PhysicsBodyHandle handle, Vector3 impulse)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            using var i = new JPH.Vec3(impulse.X, impulse.Y, impulse.Z);
            _bodyInterface?.AddImpulse(bodyId, i);
        }

        public void AddTorque(PhysicsBodyHandle handle, Vector3 torque)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            using var t = new JPH.Vec3(torque.X, torque.Y, torque.Z);
            _bodyInterface?.AddTorque(bodyId, t, JPH.EActivation.Activate);
        }

        public unsafe bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit)
        {
            hit = default;
            if (_physics == null) return false;

            var dir = Vector3.Normalize(direction) * maxDistance;
            using var rayOrigin = new JPH.Vec3(origin.X, origin.Y, origin.Z);
            using var rayDir    = new JPH.Vec3(dir.X, dir.Y, dir.Z);
            using var ray       = new JPH.RRayCast(rayOrigin, rayDir);
            using var result    = new JPH.RayCastResult();

            var npq   = _physics.GetNarrowPhaseQuery();
            bool found = npq.CastRay(ray, result);
            if (!found) return false;

            uint packed = result.mBodyID.GetIndexAndSequenceNumber();
            _packedToHandle.TryGetValue(packed, out int handleId);

            float fraction = result.mFraction;
            Vector3 hitPoint = origin + Vector3.Normalize(direction) * (maxDistance * fraction);

            hit = new RaycastHit(new PhysicsBodyHandle(handleId), hitPoint, Vector3.Normalize(direction), maxDistance * fraction);
            return true;
        }

        public int OverlapSphere(Vector3 center, float radius, List<PhysicsBodyHandle> results, int maxResults = 64)
        {
            results.Clear();
            if (_bodyInterface == null || radius <= 0f || maxResults <= 0) return 0;

            using var c = new JPH.Vec3(center.X, center.Y, center.Z);
            using var query = new JPH.AABox(c, radius);
            return OverlapAabb(query, results, maxResults);
        }

        public int OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, List<PhysicsBodyHandle> results, int maxResults = 64)
        {
            results.Clear();
            if (_bodyInterface == null || maxResults <= 0) return 0;

            var rotMat = Matrix4x4.CreateFromQuaternion(rotation);
            Vector3 hx = Vector3.TransformNormal(new Vector3(halfExtents.X, 0f, 0f), rotMat);
            Vector3 hy = Vector3.TransformNormal(new Vector3(0f, halfExtents.Y, 0f), rotMat);
            Vector3 hz = Vector3.TransformNormal(new Vector3(0f, 0f, halfExtents.Z), rotMat);
            Vector3 ext = Abs(hx) + Abs(hy) + Abs(hz);

            Vector3 min = center - ext;
            Vector3 max = center + ext;
            using var minV = new JPH.Vec3(min.X, min.Y, min.Z);
            using var maxV = new JPH.Vec3(max.X, max.Y, max.Z);
            using var query = new JPH.AABox(minV, maxV);
            return OverlapAabb(query, results, maxResults);
        }

        public void SetCollisionListener(ICollisionListener? listener)
        {
            _gameListener = listener;
        }

        // ---- Contact listener -----------------------------------------------

        unsafe void SetupContactListener()
        {
            _contactListener = new JPH.ContactListenerTrampolineManaged();

            _contactListener.SetOnContactAdded((b1, b2, manifold, settings) =>
            {
                if (_gameListener == null) return;

                uint pA = b1.GetID().GetIndexAndSequenceNumber();
                uint pB = b2.GetID().GetIndexAndSequenceNumber();

                bool aIsSensor = _sensors.Contains(pA);
                bool bIsSensor = _sensors.Contains(pB);

                ContactPoint contact = default;
                if (manifold != null)
                {
                    using var n = manifold.mWorldSpaceNormal;
                    using var o = manifold.mBaseOffset;
                    contact = new ContactPoint(
                        new Vector3(o.GetX(), o.GetY(), o.GetZ()),
                        new Vector3(n.GetX(), n.GetY(), n.GetZ()),
                        0f);
                }

                lock (_pendingLock)
                {
                    if (aIsSensor || bIsSensor)
                        _pending.Add(new CollEv(pA, pB, contact, EvKind.TriggerEnter));
                    else
                        _pending.Add(new CollEv(pA, pB, contact, EvKind.CollisionEnter));
                }
            });

            _contactListener.SetOnContactPersisted((b1, b2, manifold, settings) =>
            {
                if (_gameListener == null) return;

                uint pA = b1.GetID().GetIndexAndSequenceNumber();
                uint pB = b2.GetID().GetIndexAndSequenceNumber();

                bool aIsSensor = _sensors.Contains(pA);
                bool bIsSensor = _sensors.Contains(pB);
                if (aIsSensor || bIsSensor) return;

                ContactPoint contact = default;
                if (manifold != null)
                {
                    using var n = manifold.mWorldSpaceNormal;
                    using var o = manifold.mBaseOffset;
                    contact = new ContactPoint(
                        new Vector3(o.GetX(), o.GetY(), o.GetZ()),
                        new Vector3(n.GetX(), n.GetY(), n.GetZ()),
                        0f);
                }

                lock (_pendingLock)
                {
                    _pending.Add(new CollEv(pA, pB, contact, EvKind.CollisionStay));
                }
            });

            _contactListener.SetOnContactRemoved(pair =>
            {
                if (_gameListener == null) return;

                uint pA = pair.GetBody1ID().GetIndexAndSequenceNumber();
                uint pB = pair.GetBody2ID().GetIndexAndSequenceNumber();

                bool aIsSensor = _sensors.Contains(pA);
                bool bIsSensor = _sensors.Contains(pB);

                lock (_pendingLock)
                {
                    if (aIsSensor || bIsSensor)
                        _pending.Add(new CollEv(pA, pB, default, EvKind.TriggerExit));
                    else
                        _pending.Add(new CollEv(pA, pB, default, EvKind.CollisionExit));
                }
            });

            _contactListener.SetOnContactValidate((b1, b2, baseOffset, collisionResult) =>
            {
                uint pA = b1.GetID().GetIndexAndSequenceNumber();
                uint pB = b2.GetID().GetIndexAndSequenceNumber();
                if (ShouldCollide(pA, pB))
                    return JPH.ValidateResult.AcceptAllContactsForThisBodyPair;
                return JPH.ValidateResult.RejectAllContactsForThisBodyPair;
            });

            _physics!.SetContactListener(_contactListener.Inner);
        }

        int OverlapAabb(JPH.Const_AABox queryAabb, List<PhysicsBodyHandle> results, int maxResults)
        {
            int count = 0;
            foreach (var pair in _handleToBodyId)
            {
                if (count >= maxResults) break;

                using var ts = _bodyInterface!.GetTransformedShape(pair.Value);
                using var bodyAabb = ts.GetWorldSpaceBounds();
                if (!bodyAabb.Overlaps(queryAabb))
                    continue;

                results.Add(new PhysicsBodyHandle(pair.Key));
                count++;
            }
            return count;
        }

        static Vector3 Abs(Vector3 v)
            => new Vector3(MathF.Abs(v.X), MathF.Abs(v.Y), MathF.Abs(v.Z));

        static ushort LayerBit(ushort layer)
            => (ushort)(1u << (layer & 15));

        bool ShouldCollide(uint packedA, uint packedB)
        {
            if (!_packedToLayer.TryGetValue(packedA, out ushort layerA) ||
                !_packedToLayer.TryGetValue(packedB, out ushort layerB))
                return true;

            _packedToMask.TryGetValue(packedA, out ushort maskA);
            _packedToMask.TryGetValue(packedB, out ushort maskB);
            if (maskA == 0) maskA = 0xFFFF;
            if (maskB == 0) maskB = 0xFFFF;

            bool aAcceptsB = (maskA & LayerBit(layerB)) != 0;
            bool bAcceptsA = (maskB & LayerBit(layerA)) != 0;
            return aAcceptsB && bAcceptsA;
        }

        void DispatchPendingEvents()
        {
            if (_gameListener == null) return;

            List<CollEv> toDispatch;
            lock (_pendingLock)
            {
                if (_pending.Count == 0) return;
                toDispatch = new List<CollEv>(_pending);
                _pending.Clear();
            }

            foreach (var ev in toDispatch)
            {
                _packedToHandle.TryGetValue(ev.PackedA, out int hA);
                _packedToHandle.TryGetValue(ev.PackedB, out int hB);
                var a = new PhysicsBodyHandle(hA);
                var b = new PhysicsBodyHandle(hB);

                switch (ev.Kind)
                {
                    case EvKind.CollisionEnter:  _gameListener.OnCollisionEnter(a, b, ev.Contact); break;
                    case EvKind.CollisionStay:   _gameListener.OnCollisionStay(a, b);               break;
                    case EvKind.CollisionExit:   _gameListener.OnCollisionExit(a, b);              break;
                    case EvKind.TriggerEnter:    _gameListener.OnTriggerEnter(a, b);               break;
                    case EvKind.TriggerExit:     _gameListener.OnTriggerExit(a, b);                break;
                }
            }
        }
    }
}
