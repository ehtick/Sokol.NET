using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Sokol;
using static Sokol.SG;
using static Sokol.Utils;

/// <summary>
/// Port of Samples/Tests/Rig/RigPileTest.cpp.
/// Stacks 20 ragdolls (4 piles of 5) in dead-pose animations.
/// </summary>
class Demo_RigPile : DemoBase
{
    public override string Name     => "Rig Pile";
    public override string Category => "Rig";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 10, Latitude  = 20, Longitude = 45,
        Center    = new Vector3(0, 2, 0),
        Aspect    = 60, NearZ = 0.1f, FarZ = 500.0f,
    };

    private const float HorizontalSep = 4.0f;
    private const float VerticalSep   = 0.6f;
    private const int   RagdollsPerPile = 5;

    private readonly List<JPH.Ragdoll>  _ragdolls  = new();
    private int                          _ragdollBodyStart = -1;
    private int                          _ragdollBodyCount = 0;
    private List<PhysicsBody>?           _bodies;

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

        // ── Load 4 dead-pose animations ───────────────────────────────────
        var deadPoses = new JPH.SkeletalAnimation?[4];
        for (int i = 0; i < 4; i++)
        {
            var animData = LoadAsset($"Human/dead_pose{i + 1}.tof");
            deadPoses[i] = animData.SkeletalAnimationLoadFromBuffer();
        }

        var skeleton = settings.GetSkeleton();

        _ragdollBodyStart = bodies.Count;
        uint groupId = 1;

        // ── 2×2 grid of piles ────────────────────────────────────────────
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 2; col++)
            {
                float baseX = (col - 0.5f) * HorizontalSep;
                float baseZ = (row - 0.5f) * HorizontalSep;

                for (int k = 0; k < RagdollsPerPile; k++)
                {
                    float y = 1.0f + k * VerticalSep;

                    // Pick random dead pose
                    int poseIdx = random.Next(4);
                    using var pose = new JPH.SkeletonPose();
                    pose.SetSkeleton(skeleton);
                    deadPoses[poseIdx]?.Sample(0f, pose);
                    pose.CalculateJointMatrices();

                    // Apply random Y rotation to root offset
                    float angle = (float)(random.NextDouble() * 2.0 * System.Math.PI);
                    using var rootOff = new JPH.Vec3(baseX, y, baseZ);
                    pose.SetRootOffset(rootOff);
                    pose.CalculateJointMatrices();

                    var ragdoll = settings.CreateRagdoll(groupId, 0, sys);
                    groupId++;
                    if (ragdoll == null) continue;
                    ragdoll.SetPose(pose);
                    ragdoll.DriveToPoseUsingMotors(pose);
                    ragdoll.AddToPhysicsSystem(JPH.EActivation.Activate);

                    // Register bodies
                    int bodyCount = (int)ragdoll.GetBodyCount();
                    var scale = new Vector3(0.06f, 0.15f, 0.06f);
                    for (int b = 0; b < bodyCount; b++)
                        bodies.Add(new PhysicsBody { bodyId = ragdoll.GetBodyID(b), shape = RenderShape.Capsule, scale = scale, color = GetDistinctColor(b) });

                    _ragdolls.Add(ragdoll);
                }
            }
        }

        _ragdollBodyCount = bodies.Count - _ragdollBodyStart;

        foreach (var anim in deadPoses) anim?.Dispose();
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        if (_bodies != null && _ragdollBodyStart >= 0 && _ragdollBodyCount > 0)
        {
            _bodies.RemoveRange(_ragdollBodyStart, _ragdollBodyCount);
            _bodies = null;
        }
        _ragdollBodyStart = -1;
        _ragdollBodyCount = 0;
        foreach (var r in _ragdolls)
        {
            r.RemoveFromPhysicsSystem();
            r.Dispose();
        }
        _ragdolls.Clear();
    }
}
