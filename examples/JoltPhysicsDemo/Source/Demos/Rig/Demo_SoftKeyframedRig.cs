using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Sokol;
using static Sokol.SG;
using static Sokol.Utils;

/// <summary>
/// Port of Samples/Tests/Rig/SoftKeyframedRigTest.cpp.
/// Loads Human.tof as a dynamic ragdoll driven soft-kinematically by a walk animation.
/// The ragdoll's linear velocity is supplemented each frame to cancel gravity.
/// </summary>
class Demo_SoftKeyframedRig : DemoBase
{
    public override string Name     => "Soft Keyframed Rig";
    public override string Category => "Rig";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 5, Latitude  = 20, Longitude = 45,
        Center    = new Vector3(0, 1, 0),
        Aspect    = 60, NearZ = 0.1f, FarZ = 500.0f,
    };

    private JPH.Ragdoll?           _ragdoll;
    private JPH.SkeletalAnimation? _animation;
    private JPH.SkeletonPose?      _pose;
    private JPH.PhysicsSystem?     _sys;
    private float                  _time;
    private int                    _ragdollBodyStart = -1;
    private int                    _ragdollBodyCount = 0;
    private List<PhysicsBody>?     _bodies;

    public override unsafe void Init(JPH.BodyInterface bi, JPH.PhysicsSystem sys, List<PhysicsBody> bodies, Random random)
    {
        _bodies = bodies;
        _sys    = sys;
        AddFloor(bi, bodies);

        // ── Brick wall ────────────────────────────────────────────────────
        CreateBrickWall(bi, bodies);

        // ── Static overhead bar ───────────────────────────────────────────
        using var barHalf = new JPH.Vec3(2.0f, 0.1f, 0.1f);
        using var barSS   = new JPH.BoxShapeSettings(barHalf, 0.01f);
        using var barCS   = new JPH.BodyCreationSettings();
        barCS.SetShapeSettings(barSS);
        barCS.mPosition.Set(0f, 1.5f, -2.0f);
        barCS.mMotionType  = JPH.EMotionType.Static;
        barCS.mObjectLayer = LayerNonMoving;
        var barBody = bi.CreateBody(barCS)!;
        bi.AddBody(barBody.GetID(), JPH.EActivation.DontActivate);
        bodies.Add(new PhysicsBody { bodyId = barBody.GetID(), shape = RenderShape.Box, scale = new Vector3(4f, 0.2f, 0.2f), color = new Vector3(0.6f, 0.6f, 0.6f) });

        // ── Load ragdoll settings ─────────────────────────────────────────
        JPH.RagdollSettings? settings;
        var data = LoadAsset("Human.tof");
        settings = data.RagdollSettingsLoadFromBuffer();
        if (settings == null) return;

        // Set max linear velocity on all parts to limit ragdoll velocity
        uint partCount = settings.mParts.Size().ToUInt32();
        for (uint i = 0; i < partCount; i++)
        {
            var part = settings.GetMutablePart(i);
            part.mMaxLinearVelocity = 10.0f;
        }

        RemapRagdollLayers(settings);
        settings.GetSkeleton()?.CalculateParentJointIndices();
        settings.Stabilize();
        settings.CalculateConstraintPriorities();
        settings.DisableParentChildCollisions();
        settings.CalculateBodyIndexToConstraintIndex();
        settings.CalculateConstraintIndexToBodyIdxPair();

        // ── Create ragdoll ────────────────────────────────────────────────
        _ragdoll = settings.CreateRagdoll(1, 0, sys);
        if (_ragdoll == null) { settings.Dispose(); return; }

        // ── Load animation ────────────────────────────────────────────────
        var animData = LoadAsset("Human/walk.tof");
        _animation = animData.SkeletalAnimationLoadFromBuffer();

        // ── Pose setup ────────────────────────────────────────────────────
        _pose = new JPH.SkeletonPose();
        _pose.SetSkeleton(settings.GetSkeleton());
        settings.Dispose();
        _animation?.Sample(0f, _pose);
        _pose.CalculateJointMatrices();
        _ragdoll.SetPose(_pose);
        _ragdoll.AddToPhysicsSystem(JPH.EActivation.Activate);

        // ── Register bodies for rendering ─────────────────────────────────
        _ragdollBodyStart = bodies.Count;
        _ragdollBodyCount = (int)_ragdoll.GetBodyCount();
        var scale = new Vector3(0.06f, 0.15f, 0.06f);
        for (int i = 0; i < _ragdollBodyCount; i++)
            bodies.Add(new PhysicsBody { bodyId = _ragdoll.GetBodyID(i), shape = RenderShape.Capsule, scale = scale, color = GetDistinctColor(i) });
    }

    public override unsafe void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        if (_ragdoll == null || _animation == null || _pose == null || _sys == null) return;

        float duration = _animation.GetDuration();

        // Sample pose at current time
        _animation.Sample(_time, _pose);
        _pose.CalculateJointMatrices();

        // Advance time
        _time += dt;
        if (duration > 0f) _time %= duration;

        // Sample again at new time for kinematics
        _animation.Sample(_time, _pose);
        _pose.CalculateJointMatrices();

        _ragdoll.DriveToPoseUsingKinematics(_pose, dt);

        // Cancel gravity: add (gravity * dt) as linear velocity impulse
        var g = _sys.GetGravity();
        using var impulse = new JPH.Vec3(g.GetX() * dt, g.GetY() * dt, g.GetZ() * dt);
        _ragdoll.AddLinearVelocity(impulse);
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        if (_ragdoll != null)
        {
            _ragdoll.RemoveFromPhysicsSystem();
            if (_bodies != null && _ragdollBodyStart >= 0 && _ragdollBodyCount > 0)
            {
                var ids = new JPH.BodyID[_ragdollBodyCount];
                for (int i = 0; i < _ragdollBodyCount; i++)
                    ids[i] = _bodies[_ragdollBodyStart + i].bodyId;
                sys.GetBodyInterface().DestroyBodies(ids);
                _bodies.RemoveRange(_ragdollBodyStart, _ragdollBodyCount);
                _bodies = null;
            }
            _ragdollBodyStart = -1;
            _ragdollBodyCount = 0;
            _ragdoll.Dispose();
            _ragdoll = null;
        }
        _pose?.Dispose(); _pose = null;
        _animation?.Dispose(); _animation = null;
        _sys = null;
    }

    private static unsafe void CreateBrickWall(JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        using var brickHalf = new JPH.Vec3(0.2f, 0.2f, 0.2f);
        using var brickSS   = new JPH.BoxShapeSettings(brickHalf, 0.01f);
        for (int row = 0; row < 3; row++)
        {
            int colStart = row / 2;
            int colEnd   = 10 - (row + 1) / 2;
            for (int col = colStart; col < colEnd; col++)
            {
                float x = -2.0f + col * 0.4f + ((row & 1) != 0 ? 0.2f : 0f);
                float y = 0.2f + row * 0.4f;
                float z = -2.0f;
                using var cs = new JPH.BodyCreationSettings();
                cs.SetShapeSettings(brickSS);
                cs.mPosition.Set(x, y, z);
                cs.mMotionType  = JPH.EMotionType.Dynamic;
                cs.mObjectLayer = LayerMoving;
                var body = bi.CreateBody(cs)!;
                bi.AddBody(body.GetID(), JPH.EActivation.DontActivate);
                bodies.Add(new PhysicsBody { bodyId = body.GetID(), shape = RenderShape.Box, scale = new Vector3(0.4f), color = new Vector3(0.7f, 0.6f, 0.5f) });
            }
        }
    }
}
