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
        readonly Dictionary<int, JPH.Body>    _handleToBody    = new();
        readonly Dictionary<uint, int>         _packedToHandle  = new();
        readonly Dictionary<uint, ushort>      _packedToLayer   = new();
        readonly Dictionary<uint, ushort>      _packedToMask    = new();
        readonly HashSet<uint>                 _sensors         = new();

        int _nextConstraintHandle = 1;
        readonly Dictionary<int, JPH.TwoBodyConstraint> _constraintMap = new();

        // ── Character controllers ─────────────────────────────────────────────
        JPH.TempAllocatorImpl? _charTempAlloc;
        int _nextCharHandle = 1;
        readonly Dictionary<int, CharState> _charStates = new();

        private sealed class CharState : IDisposable
        {
            public required JPH.CharacterVirtual     Character;
            public required JPH.RotatedTranslatedShape RotTransShape;
            public required JPH.CapsuleShape           CapsuleShapeObj;
            public Vector3 DesiredVelocity;
            private bool _disposed;
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Character.Dispose();
                RotTransShape.Dispose();
                CapsuleShapeObj.Dispose();
            }
        }

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
                if (_charStates.Count > 0)
                    StepAllCharacters(FixedDt);
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

            foreach (var (_, constraint) in _constraintMap)
                _physics?.RemoveConstraint(constraint);
            _constraintMap.Clear();

            foreach (var state in _charStates.Values)
                state.Dispose();
            _charStates.Clear();
            _charTempAlloc?.Dispose(); _charTempAlloc = null;

            foreach (var (handle, bodyId) in _handleToBodyId)
            {
                _bodyInterface?.RemoveBody(bodyId);
                _bodyInterface?.DestroyBody(bodyId);
            }

            _handleToBodyId.Clear();
            _handleToBody.Clear();
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

            // ── Shape ──────────────────────────────────────────────────────────
            var shapes = desc.Shapes;
            bool anyMesh = false;
            foreach (var se in shapes)
                if (se.Shape == ColliderShape.Mesh) { anyMesh = true; break; }

            if (shapes.Count == 1)
            {
                BuildShapeAndCall(shapes[0], desc.Scale, shapeSS => cs.SetShapeSettings(shapeSS));
            }
            else if (shapes.Count > 1)
            {
                using var scs = new JPH.StaticCompoundShapeSettings();
                JPH.CompoundShapeSettings compound = scs;
                foreach (var entry in shapes)
                {
                    var e = entry;
                    using var pos = new JPH.Vec3(e.Offset.X, e.Offset.Y, e.Offset.Z);
                    using var rot = new JPH.Quat(e.OffsetRotation.X, e.OffsetRotation.Y, e.OffsetRotation.Z, e.OffsetRotation.W);
                    var capturedPos = pos; var capturedRot = rot;
                    BuildShapeAndCall(e, desc.Scale, shapeSS => compound.AddShape(capturedPos, capturedRot, shapeSS));
                }
                cs.SetShapeSettings((JPH.Const_StaticCompoundShapeSettings)scs);
            }
            else
            {
                // Empty shapes list — fallback to unit box
                using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(desc.Scale.X * 0.5f, desc.Scale.Y * 0.5f, desc.Scale.Z * 0.5f));
                cs.SetShapeSettings(ss);
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

            // MeshShape is static-only in Jolt — override regardless of MotionType
            if (anyMesh)
            {
                cs.mMotionType  = JPH.EMotionType.Static;
                cs.mObjectLayer = ObjLayerNonMoving;
            }

            var activation = (cs.mMotionType == JPH.EMotionType.Static) ? JPH.EActivation.DontActivate : JPH.EActivation.Activate;

            if (!desc.UseGravity)
                cs.mGravityFactor = 0f;

            cs.mFriction = Math.Clamp(desc.Friction, 0f, 1f);
            cs.mRestitution = Math.Clamp(desc.Restitution, 0f, 1f);
            cs.mLinearDamping = Math.Max(0f, desc.LinearDamping);
            cs.mAngularDamping = Math.Max(0f, desc.AngularDamping);

            cs.mIsSensor = desc.IsTrigger;

            var body   = _bodyInterface.CreateBody(cs);
            if (body == null)
                throw new InvalidOperationException("JoltPhysicsWorld: CreateBody returned null.");
            var bodyId = body.GetID();
            _bodyInterface.AddBody(bodyId, activation);

            int handle = _nextHandle++;
            uint packed = bodyId.GetIndexAndSequenceNumber();
            _handleToBodyId[handle] = bodyId;
            _handleToBody[handle]   = body;
            _packedToHandle[packed] = handle;
            _packedToLayer[packed] = desc.Layer;
            _packedToMask[packed] = desc.LayerMask;
            if (desc.IsTrigger)
                _sensors.Add(packed);

            return new PhysicsBodyHandle(handle);
        }

        /// <summary>
        /// Builds a Jolt shape-settings object for <paramref name="entry"/> (scaled by
        /// <paramref name="scale"/>) and synchronously passes a non-owning
        /// <see cref="JPH.Const_ShapeSettings"/> view to <paramref name="consume"/>.
        /// The underlying native object is valid for the duration of the callback only.
        /// </summary>
        private static unsafe void BuildShapeAndCall(ShapeEntry entry, Vector3 scale, Action<JPH.Const_ShapeSettings> consume)
        {
            switch (entry.Shape)
            {
                case ColliderShape.Sphere:
                {
                    float r = entry.Radius * MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z));
                    using var ss = new JPH.SphereShapeSettings(r);
                    consume(ss);
                    break;
                }
                case ColliderShape.Capsule:
                {
                    float radius     = entry.Radius * MathF.Max(scale.X, scale.Z);
                    float halfHeight = MathF.Max(0f, entry.HalfHeight * scale.Y);
                    using var ss = new JPH.CapsuleShapeSettings(halfHeight, radius);
                    consume(ss);
                    break;
                }
                case ColliderShape.Cylinder:
                {
                    float radius     = entry.Radius * MathF.Max(scale.X, scale.Z);
                    float halfHeight = entry.HalfHeight * scale.Y;
                    using var ss = new JPH.CylinderShapeSettings(halfHeight, radius);
                    consume(ss);
                    break;
                }
                case ColliderShape.Plane:
                {
                    using var plane = new JPH.Plane(new JPH.Vec4(0f, 1f, 0f, 0f));
                    using var ss = new JPH.PlaneShapeSettings(plane);
                    consume(ss);
                    break;
                }
                case ColliderShape.ConvexHull:
                {
                    Vector3 s = scale;
                    Vector3[] src = entry.MeshVertices ?? new Vector3[]
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
                    consume(hull);
                    break;
                }
                case ColliderShape.Mesh:
                {
                    if (entry.MeshVertices != null && entry.MeshIndices != null)
                    {
                        Vector3 s = scale;
                        var pts = new JPH.Vec3f[entry.MeshVertices.Length];
                        for (int i = 0; i < pts.Length; i++)
                        {
                            Vector3 v = entry.MeshVertices[i];
                            pts[i] = new JPH.Vec3f(v.X * s.X, v.Y * s.Y, v.Z * s.Z);
                        }
                        using var mesh = JPH.MeshShapeSettingsFromIndexedMesh(pts, entry.MeshIndices);
                        consume(mesh);
                    }
                    else
                    {
                        // Geometry not yet populated — fallback to box
                        using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(scale.X * 0.5f, scale.Y * 0.5f, scale.Z * 0.5f));
                        consume(ss);
                    }
                    break;
                }
                default: // Box
                {
                    using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(
                        entry.HalfExtent.X * scale.X,
                        entry.HalfExtent.Y * scale.Y,
                        entry.HalfExtent.Z * scale.Z));
                    consume(ss);
                    break;
                }
            }
        }

        public void DestroyBody(PhysicsBodyHandle handle)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            uint packed = bodyId.GetIndexAndSequenceNumber();
            _bodyInterface?.RemoveBody(bodyId);
            _bodyInterface?.DestroyBody(bodyId);
            _handleToBodyId.Remove(handle.Value);
            _handleToBody.Remove(handle.Value);
            _packedToHandle.Remove(packed);
            _packedToLayer.Remove(packed);
            _packedToMask.Remove(packed);
            _sensors.Remove(packed);
        }

        public ConstraintHandle CreateConstraint(ConstraintDesc desc)
        {
            if (_physics == null || _bodyInterface == null)
                throw new InvalidOperationException("JoltPhysicsWorld not initialized.");

            // Resolve bodies; BodyA/BodyB.Invalid means world anchor — use the static world body.
            if (!_handleToBody.TryGetValue(desc.BodyA.Value, out var bodyA) ||
                !_handleToBody.TryGetValue(desc.BodyB.Value, out var bodyB))
                return ConstraintHandle.Invalid;

            JPH.TwoBodyConstraint? constraint = null;

            using var a1 = new JPH.Vec3(desc.LocalAnchorA.X, desc.LocalAnchorA.Y, desc.LocalAnchorA.Z);
            using var a2 = new JPH.Vec3(desc.LocalAnchorB.X, desc.LocalAnchorB.Y, desc.LocalAnchorB.Z);
            using var ax1 = new JPH.Vec3(
                desc.LocalAxisA == System.Numerics.Vector3.Zero ? 0f : desc.LocalAxisA.X,
                desc.LocalAxisA == System.Numerics.Vector3.Zero ? 1f : desc.LocalAxisA.Y,
                desc.LocalAxisA == System.Numerics.Vector3.Zero ? 0f : desc.LocalAxisA.Z);
            using var ax2 = new JPH.Vec3(
                desc.LocalAxisB == System.Numerics.Vector3.Zero ? 0f : desc.LocalAxisB.X,
                desc.LocalAxisB == System.Numerics.Vector3.Zero ? 1f : desc.LocalAxisB.Y,
                desc.LocalAxisB == System.Numerics.Vector3.Zero ? 0f : desc.LocalAxisB.Z);

            switch (desc.Type)
            {
                case ConstraintType.Fixed:
                {
                    using var s = new JPH.FixedConstraintSettings();
                    s.mPoint1.Set(a1.GetX(), a1.GetY(), a1.GetZ());
                    s.mPoint2.Set(a2.GetX(), a2.GetY(), a2.GetZ());
                    s.mAxisX1.Set(1f, 0f, 0f); s.mAxisY1.Set(0f, 1f, 0f);
                    s.mAxisX2.Set(1f, 0f, 0f); s.mAxisY2.Set(0f, 1f, 0f);
                    constraint = s.Create(bodyA, bodyB);
                    break;
                }
                case ConstraintType.Point:
                {
                    using var s = new JPH.PointConstraintSettings();
                    s.mPoint1.Set(a1.GetX(), a1.GetY(), a1.GetZ());
                    s.mPoint2.Set(a2.GetX(), a2.GetY(), a2.GetZ());
                    constraint = s.Create(bodyA, bodyB);
                    break;
                }
                case ConstraintType.Hinge:
                {
                    using var s = new JPH.HingeConstraintSettings();
                    s.mPoint1.Set(a1.GetX(), a1.GetY(), a1.GetZ());
                    s.mPoint2.Set(a2.GetX(), a2.GetY(), a2.GetZ());
                    s.mHingeAxis1.Set(ax1.GetX(), ax1.GetY(), ax1.GetZ());
                    s.mHingeAxis2.Set(ax2.GetX(), ax2.GetY(), ax2.GetZ());
                    s.mLimitsMin = desc.MinLimit;
                    s.mLimitsMax = desc.MaxLimit;
                    constraint = s.Create(bodyA, bodyB);
                    break;
                }
                case ConstraintType.Slider:
                {
                    using var s = new JPH.SliderConstraintSettings();
                    s.mPoint1.Set(a1.GetX(), a1.GetY(), a1.GetZ());
                    s.mPoint2.Set(a2.GetX(), a2.GetY(), a2.GetZ());
                    s.mSliderAxis1.Set(ax1.GetX(), ax1.GetY(), ax1.GetZ());
                    s.mSliderAxis2.Set(ax2.GetX(), ax2.GetY(), ax2.GetZ());
                    s.mLimitsMin = desc.MinLimit;
                    s.mLimitsMax = desc.MaxLimit;
                    constraint = s.Create(bodyA, bodyB);
                    break;
                }
                case ConstraintType.Distance:
                {
                    using var s = new JPH.DistanceConstraintSettings();
                    s.mPoint1.Set(a1.GetX(), a1.GetY(), a1.GetZ());
                    s.mPoint2.Set(a2.GetX(), a2.GetY(), a2.GetZ());
                    s.mMinDistance = desc.MinLimit;
                    s.mMaxDistance = desc.MaxLimit < desc.MinLimit ? desc.MinLimit : desc.MaxLimit;
                    constraint = s.Create(bodyA, bodyB);
                    break;
                }
                case ConstraintType.Cone:
                {
                    using var s = new JPH.ConeConstraintSettings();
                    s.mPoint1.Set(a1.GetX(), a1.GetY(), a1.GetZ());
                    s.mPoint2.Set(a2.GetX(), a2.GetY(), a2.GetZ());
                    s.mTwistAxis1.Set(ax1.GetX(), ax1.GetY(), ax1.GetZ());
                    s.mTwistAxis2.Set(ax2.GetX(), ax2.GetY(), ax2.GetZ());
                    s.mHalfConeAngle = Math.Max(0f, desc.MaxLimit);
                    constraint = s.Create(bodyA, bodyB);
                    break;
                }
                case ConstraintType.SwingTwist:
                {
                    using var s = new JPH.SwingTwistConstraintSettings();
                    s.mPosition1.Set(a1.GetX(), a1.GetY(), a1.GetZ());
                    s.mPosition2.Set(a2.GetX(), a2.GetY(), a2.GetZ());
                    s.mTwistAxis1.Set(ax1.GetX(), ax1.GetY(), ax1.GetZ());
                    s.mTwistAxis2.Set(ax2.GetX(), ax2.GetY(), ax2.GetZ());
                    s.mNormalHalfConeAngle = Math.Max(0f, desc.MaxLimit);
                    s.mPlaneHalfConeAngle  = Math.Max(0f, desc.MaxLimit);
                    s.mTwistMinAngle = desc.MinLimit;
                    s.mTwistMaxAngle = desc.MaxLimit;
                    constraint = s.Create(bodyA, bodyB);
                    break;
                }
                case ConstraintType.SixDOF:
                {
                    using var s = new JPH.SixDOFConstraintSettings();
                    s.mPosition1.Set(a1.GetX(), a1.GetY(), a1.GetZ());
                    s.mPosition2.Set(a2.GetX(), a2.GetY(), a2.GetZ());
                    constraint = s.Create(bodyA, bodyB);
                    break;
                }
                default:
                    return ConstraintHandle.Invalid;
            }

            if (constraint == null) return ConstraintHandle.Invalid;

            _physics.AddConstraint(constraint);
            int ch = _nextConstraintHandle++;
            _constraintMap[ch] = constraint;
            return new ConstraintHandle(ch);
        }

        public void DestroyConstraint(ConstraintHandle handle)
        {
            if (!_constraintMap.TryGetValue(handle.Value, out var constraint)) return;
            _physics?.RemoveConstraint(constraint);
            _constraintMap.Remove(handle.Value);
        }

        public void SetConstraintEnabled(ConstraintHandle handle, bool enabled)
        {
            if (!_constraintMap.TryGetValue(handle.Value, out var constraint)) return;
            constraint.SetEnabled(enabled);
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
            // MeshShape bodies are forced to Static — guard against calling MoveKinematic on them
            if (_bodyInterface?.GetMotionType(bodyId) != JPH.EMotionType.Kinematic) return;
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

        // ── Character controller implementation ───────────────────────────────

        public CharacterHandle CreateCharacter(CharacterDesc desc)
        {
            if (_physics == null)
                throw new InvalidOperationException("JoltPhysicsWorld not initialized.");

            _charTempAlloc ??= new JPH.TempAllocatorImpl(4 * 1024 * 1024);

            // Build capsule: feet at the character's position, top at position + height.
            // RotatedTranslatedShape offsets the capsule so its base aligns with position.
            float halfHeight = MathF.Max(0f, desc.Height * 0.5f - desc.Radius);
            var capsule   = new JPH.CapsuleShape(halfHeight, desc.Radius);
            using var offset   = new JPH.Vec3(0f, halfHeight + desc.Radius, 0f);
            using var identRot = JPH.Quat.SIdentity();
            var rotTrans = new JPH.RotatedTranslatedShape(offset, identRot, capsule);

            using var settings = new JPH.CharacterVirtualSettings();
            settings.mMaxSlopeAngle  = desc.MaxSlopeAngle;
            settings.mMaxStrength    = desc.MaxStrength;
            settings.mMass           = desc.Mass;
            settings.mInnerBodyLayer = ObjLayerMoving;
            settings.SetShape((JPH.Const_RotatedTranslatedShape)rotTrans);

            // SupportingVolume: treat contact points at or below -radius as ground support.
            var supVol = settings.mSupportingVolume;
            using var axisY = new JPH.Vec3(0f, 1f, 0f);
            supVol.SetNormal(axisY);
            supVol.SetConstant(-desc.Radius);

            using var pos = new JPH.Vec3(desc.Position.X, desc.Position.Y, desc.Position.Z);
            using var rot = new JPH.Quat(desc.Rotation.X, desc.Rotation.Y, desc.Rotation.Z, desc.Rotation.W);

            var character = new JPH.CharacterVirtual(settings, pos, rot, 0, _physics);

            int h = _nextCharHandle++;
            _charStates[h] = new CharState
            {
                Character       = character,
                RotTransShape   = rotTrans,
                CapsuleShapeObj = capsule,
                DesiredVelocity = Vector3.Zero,
            };
            return new CharacterHandle(h);
        }

        public void DestroyCharacter(CharacterHandle handle)
        {
            if (!_charStates.TryGetValue(handle.Value, out var state)) return;
            state.Dispose();
            _charStates.Remove(handle.Value);
        }

        public void SetCharacterLinearVelocity(CharacterHandle handle, Vector3 velocity)
        {
            if (_charStates.TryGetValue(handle.Value, out var state))
                state.DesiredVelocity = velocity;
        }

        public Vector3 GetCharacterPosition(CharacterHandle handle)
        {
            if (!_charStates.TryGetValue(handle.Value, out var state)) return Vector3.Zero;
            using var p = state.Character.GetPosition();
            return new Vector3(p.GetX(), p.GetY(), p.GetZ());
        }

        public Quaternion GetCharacterRotation(CharacterHandle handle)
        {
            if (!_charStates.TryGetValue(handle.Value, out var state)) return Quaternion.Identity;
            using var r = state.Character.GetRotation();
            return new Quaternion(r.GetX(), r.GetY(), r.GetZ(), r.GetW());
        }

        public void SetCharacterPosition(CharacterHandle handle, Vector3 position)
        {
            if (!_charStates.TryGetValue(handle.Value, out var state)) return;
            using var p = new JPH.Vec3(position.X, position.Y, position.Z);
            state.Character.SetPosition(p);
        }

        public bool IsCharacterGrounded(CharacterHandle handle)
        {
            if (!_charStates.TryGetValue(handle.Value, out var state)) return false;
            return state.Character.GetGroundState() == JPH.CharacterBase.EGroundState.OnGround;
        }

        public Vector3 GetCharacterGroundNormal(CharacterHandle handle)
        {
            if (!_charStates.TryGetValue(handle.Value, out var state)) return Vector3.UnitY;
            using var n = state.Character.GetGroundNormal();
            return new Vector3(n.GetX(), n.GetY(), n.GetZ());
        }

        private void StepAllCharacters(float dt)
        {
            if (_physics == null || _charTempAlloc == null) return;

            using var gravVec = _physics.GetGravity();
            var g  = new Vector3(gravVec.GetX(), gravVec.GetY(), gravVec.GetZ());
            var up = Vector3.UnitY;

            using var extSettings = new JPH.CharacterVirtual.ExtendedUpdateSettings();
            using var bpFilter    = _physics.GetDefaultBroadPhaseLayerFilter(ObjLayerMoving);
            using var layerFilter = _physics.GetDefaultLayerFilter(ObjLayerMoving);
            using var bodyFilter  = new JPH.BodyFilter();
            using var shapeFilter = new JPH.ShapeFilter();

            foreach (var state in _charStates.Values)
            {
                var ch = state.Character;

                ch.UpdateGroundVelocity();

                using var curVelJph = ch.GetLinearVelocity();
                var curVel  = new Vector3(curVelJph.GetX(), curVelJph.GetY(), curVelJph.GetZ());
                float vertV = Vector3.Dot(curVel, up);

                bool onGround = ch.GetGroundState() == JPH.CharacterBase.EGroundState.OnGround;

                Vector3 newVel;
                if (onGround && vertV <= 0f)
                {
                    // Follow ground platform velocity (moving platforms work automatically)
                    using var gVelJph = ch.GetGroundVelocity();
                    newVel = new Vector3(gVelJph.GetX(), gVelJph.GetY(), gVelJph.GetZ());
                }
                else
                {
                    // In air: preserve vertical, apply gravity
                    newVel  = vertV * up;
                    newVel += g * dt;
                }

                // Apply caller-supplied horizontal/jump velocity
                using var gNormJph = ch.GetGroundNormal();
                bool tooSteep = onGround && ch.IsSlopeTooSteep(gNormJph);
                if (!tooSteep)
                {
                    newVel.X += state.DesiredVelocity.X;
                    newVel.Z += state.DesiredVelocity.Z;
                    // Jump: caller sets positive Y via SetCharacterLinearVelocity
                    if (state.DesiredVelocity.Y > 0f)
                        newVel.Y = state.DesiredVelocity.Y;
                }

                using var newVelJph = new JPH.Vec3(newVel.X, newVel.Y, newVel.Z);
                ch.SetLinearVelocity(newVelJph);

                ch.ExtendedUpdate(dt, gravVec, extSettings, bpFilter, layerFilter, bodyFilter, shapeFilter, _charTempAlloc);

                // Desired velocity is consumed each physics step; caller refreshes it every frame.
                state.DesiredVelocity = Vector3.Zero;
            }
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
