using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Sokol;
using static Sokol.SG;
using static Sokol.Utils;

/// <summary>
/// Port of Samples/Tests/Rig/PoweredRigTest.cpp.
/// Loads Human.tof as a dynamic ragdoll driven by motor constraints to follow
/// a sprint animation. The root translation is locked; root position comes from
/// GetRootTransform each frame.
/// </summary>
class Demo_PoweredRig : DemoBase
{
    public override string Name     => "Powered Rig";
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
    private float                  _time;
    private int                    _ragdollBodyStart = -1;
    private int                    _ragdollBodyCount = 0;
    private List<PhysicsBody>?     _bodies;

    public override unsafe void Init(JPH.BodyInterface bi, JPH.PhysicsSystem sys, List<PhysicsBody> bodies, Random random)
    {
        _bodies = bodies;
        AddFloor(bi, bodies);

        // ── Load ragdoll settings ─────────────────────────────────────────
        JPH.RagdollSettings? settings;
        var data = LoadAsset("Human.tof");
        settings = data.RagdollSettingsLoadFromBuffer();
        if (settings == null) return;

        RemapRagdollLayers(settings);
        settings.GetSkeleton()?.CalculateParentJointIndices();
        settings.Stabilize();
        settings.CalculateConstraintPriorities();
        settings.DisableParentChildCollisions();
        settings.CalculateBodyIndexToConstraintIndex();
        settings.CalculateConstraintIndexToBodyIdxPair();

        // ── Create ragdoll ────────────────────────────────────────────────
        _ragdoll = settings.CreateRagdoll(1, 0, sys);
        if (_ragdoll == null) return;

        // ── Load animation ────────────────────────────────────────────────
        var animData = LoadAsset("Human/sprint.tof");
        _animation = animData.SkeletalAnimationLoadFromBuffer();

        // ── Pose setup ────────────────────────────────────────────────────
        _pose = new JPH.SkeletonPose();
        _pose.SetSkeleton(settings.GetSkeleton());
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
        if (_ragdoll == null || _animation == null || _pose == null) return;

        float duration = _animation.GetDuration();
        _time += dt;
        if (duration > 0f) _time %= duration;

        _animation.Sample(_time, _pose);

        // Lock root translation to origin; get root position from physics
        var joint0 = _pose.GetJoint(0);
        using var zero = JPH.Vec3.SZero();
        joint0.mTranslation.Assign(zero);

        // Get root position from ragdoll and set as pose root offset
        using var rootPos = new JPH.Vec3(0f, 0f, 0f);
        using var rootRot = JPH.Quat.SIdentity();
        _ragdoll.GetRootTransform(rootPos, rootRot);
        joint0.mRotation.Assign(rootRot);
        _pose.SetRootOffset(rootPos);

        _pose.CalculateJointMatrices();
        _ragdoll.DriveToPoseUsingMotors(_pose);
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        if (_ragdoll != null)
        {
            if (_bodies != null && _ragdollBodyStart >= 0 && _ragdollBodyCount > 0)
            {
                _bodies.RemoveRange(_ragdollBodyStart, _ragdollBodyCount);
                _bodies = null;
            }
            _ragdollBodyStart = -1;
            _ragdollBodyCount = 0;
            _ragdoll.RemoveFromPhysicsSystem();
            _ragdoll.Dispose();
            _ragdoll = null;
        }
        _pose?.Dispose(); _pose = null;
        _animation?.Dispose(); _animation = null;
    }
}
