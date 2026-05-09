using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Sokol;
using static Sokol.SG;
using static Sokol.Utils;

/// <summary>
/// Port of Samples/Tests/Rig/SkeletonMapperTest.cpp.
/// Maps a high-detail (HD) animation skeleton onto a low-detail ragdoll skeleton
/// using SkeletonMapper.MapReverse each frame.
/// </summary>
class Demo_SkeletonMapper : DemoBase
{
    public override string Name     => "Skeleton Mapper";
    public override string Category => "Rig";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 5, Latitude  = 20, Longitude = 45,
        Center    = new Vector3(0, 1, 0),
        Aspect    = 60, NearZ = 0.1f, FarZ = 500.0f,
    };

    private JPH.Ragdoll?           _ragdoll;
    private JPH.SkeletalAnimation? _animation;
    private JPH.SkeletonMapper?    _mapper;
    private JPH.SkeletonPose?      _animatedPose;
    private JPH.SkeletonPose?      _ragdollPose;
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
        var settingsData = LoadAsset("Human.tof");
        settings = settingsData.RagdollSettingsLoadFromBuffer();
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

        // ── Load neutral ragdoll pose ─────────────────────────────────────
        JPH.SkeletalAnimation? neutralRagdoll;
        var neutralRagdollData = LoadAsset("Human/neutral.tof");
        neutralRagdoll = neutralRagdollData.SkeletalAnimationLoadFromBuffer();

        // ── Load HD animation skeleton ────────────────────────────────────
        JPH.Skeleton? animSkeleton;
        var animSkeletonData = LoadAsset("Human/skeleton_hd.tof");
        animSkeleton = animSkeletonData.SkeletonLoadFromBuffer();
        animSkeleton?.CalculateParentJointIndices();

        // ── Load HD neutral and jog animations ────────────────────────────
        JPH.SkeletalAnimation? neutralHD;
        var neutralHDData = LoadAsset("Human/neutral_hd.tof");
        neutralHD = neutralHDData.SkeletalAnimationLoadFromBuffer();

        var jogData = LoadAsset("Human/jog_hd.tof");
        _animation = jogData.SkeletalAnimationLoadFromBuffer();

        // ── Set up poses ─────────────────────────────────────────────────
        _animatedPose = new JPH.SkeletonPose();
        _animatedPose.SetSkeleton(animSkeleton);

        _ragdollPose = new JPH.SkeletonPose();
        _ragdollPose.SetSkeleton(settings.GetSkeleton());

        // Sample neutral poses to get T-pose matrices for mapper init
        neutralRagdoll?.Sample(0f, _ragdollPose);
        _ragdollPose.CalculateJointMatrices();

        neutralHD?.Sample(0f, _animatedPose);
        _animatedPose.CalculateJointMatrices();

        // ── Initialize skeleton mapper ────────────────────────────────────
        _mapper = new JPH.SkeletonMapper();
       _mapper.Initialize(_ragdollPose, _animatedPose);

        // ── Add ragdoll to physics ────────────────────────────────────────
        _ragdoll.SetPose(_ragdollPose);
        _ragdoll.AddToPhysicsSystem(JPH.EActivation.Activate);

        // ── Register bodies for rendering ─────────────────────────────────
        _ragdollBodyStart = bodies.Count;
        _ragdollBodyCount = (int)_ragdoll.GetBodyCount();
        var scale = new Vector3(0.06f, 0.15f, 0.06f);
        for (int i = 0; i < _ragdollBodyCount; i++)
            bodies.Add(new PhysicsBody { bodyId = _ragdoll.GetBodyID(i), shape = RenderShape.Capsule, scale = scale, color = GetDistinctColor(i) });

        neutralRagdoll?.Dispose();
        neutralHD?.Dispose();
    }

    public override unsafe void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        if (_ragdoll == null || _animation == null || _mapper == null ||
            _animatedPose == null || _ragdollPose == null) return;

        float duration = _animation.GetDuration();
        _time += dt;
        if (duration > 0f) _time %= duration;

        // Sample HD animation
        _animation.Sample(_time, _animatedPose);
        _animatedPose.CalculateJointMatrices();

        // Map animated HD pose onto ragdoll skeleton (reverse map: HD → ragdoll)
        var animMatrices    = _animatedPose.GetJointMatrices();
        var ragdollMatrices = _ragdollPose.GetJointMatrices();
        _mapper.MapReverse(animMatrices[(UIntPtr)0], ragdollMatrices[(UIntPtr)0]);

        _ragdollPose.CalculateJointStates();
        _ragdoll.DriveToPoseUsingMotors(_ragdollPose);

        // Draw animated (HD) skeleton at X=0, ragdoll skeleton offset to X=1
        DrawSkeletonPose(_animatedPose);
        DrawSkeletonPose(_ragdollPose, new System.Numerics.Vector3(1f, 0f, 0f));
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
        _animatedPose?.Dispose(); _animatedPose = null;
        _ragdollPose?.Dispose();  _ragdollPose  = null;
        _mapper?.Dispose();       _mapper       = null;
        _animation?.Dispose();    _animation    = null;
    }
}
