using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Sokol;
using static Sokol.SG;
using static Sokol.Utils;

/// <summary>
/// Port of Samples/Tests/Rig/BigWorldTest.cpp.
/// Creates piles of ragdolls at increasing distances to test the physics system
/// with large world coordinates.
/// </summary>
class Demo_BigWorld : DemoBase
{
    public override string Name     => "Big World";
    public override string Category => "Rig";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 10, Latitude  = 20, Longitude = 45,
        Center    = new Vector3(0, 2, 0),
        Aspect    = 60, NearZ = 0.1f, FarZ = 100000.0f,
    };

    private const float VerticalSep  = 0.6f;
    private const int   PileSize     = 5;

    private readonly List<JPH.Ragdoll> _ragdolls = new();
    private int                        _ragdollBodyStart = -1;
    private int                        _ragdollBodyCount = 0;
    private List<PhysicsBody>?         _bodies;

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

        // ── Load one dead-pose animation ──────────────────────────────────
        JPH.SkeletalAnimation? deadPose;
        var animData = LoadAsset("Human/dead_pose1.tof");
        deadPose = animData.SkeletalAnimationLoadFromBuffer();

        var skeleton = settings.GetSkeleton();
        _ragdollBodyStart = bodies.Count;

        // ── Piles at three distances ──────────────────────────────────────
        float[] distances = { 0f, 1000f, 5000f };
        foreach (float dist in distances)
        {
            float inv = 1f / MathF.Sqrt(3f);
            float ox = dist * inv;
            float oz = dist * inv;

            for (int k = 0; k < PileSize; k++)
            {
                float y = 1.0f + k * VerticalSep;

                using var pose = new JPH.SkeletonPose();
                pose.SetSkeleton(skeleton);
                deadPose?.Sample(0f, pose);
                using var rootOff = new JPH.Vec3(ox, y, oz);
                pose.SetRootOffset(rootOff);
                pose.CalculateJointMatrices();

                var ragdoll = settings.CreateRagdoll(0, 0, sys);
                if (ragdoll == null) continue;
                ragdoll.SetPose(pose);
                ragdoll.DriveToPoseUsingMotors(pose);
                ragdoll.AddToPhysicsSystem(JPH.EActivation.Activate);

                int bodyCount = (int)ragdoll.GetBodyCount();
                var scale = new Vector3(0.06f, 0.15f, 0.06f);
                for (int b = 0; b < bodyCount; b++)
                    bodies.Add(new PhysicsBody { bodyId = ragdoll.GetBodyID(b), shape = RenderShape.Capsule, scale = scale, color = GetDistinctColor(b) });

                _ragdolls.Add(ragdoll);
            }
        }

        _ragdollBodyCount = bodies.Count - _ragdollBodyStart;
        settings.Dispose();
        deadPose?.Dispose();
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        foreach (var r in _ragdolls)
            r.RemoveFromPhysicsSystem();
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
        foreach (var r in _ragdolls)
            r.Dispose();
        _ragdolls.Clear();
    }
}
