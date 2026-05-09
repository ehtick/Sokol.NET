using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Faithful port of SoftBodySkinnedConstraintTest.cpp.
/// A cloth mesh is driven by a procedurally-animated 11-joint chain.
/// First and last vertex rows are kinematic (invMass=0).
/// All vertices are skinned with per-vertex blend weights to the two
/// nearest joints.  SkinVertices is called each frame before the
/// physics step (matching C++ PrePhysicsUpdate).
/// Green debug lines show the animated joint chain.
/// </summary>
public class Demo_SoftBody_SkinnedConstraint : DemoBase
{
    public override string Name     => "SoftBody: Skinned Constraint";
    public override string Category => "Soft Body";

    const int   cNumVerticesX  = 10;
    const int   cNumVerticesZ  = 50;
    const float cVertexSpacing = 0.5f;
    const int   cNumJoints     = 11;
    const float cBodyPosY      = 20.0f;

    JPH.Body?              _body;
    JPH.TempAllocatorImpl? _tempAlloc;
    float                  _time;

    // ─────────────────────────────────────────────────────────────
    //  World-space pose (matches C++ GetWorldSpacePose)
    // ─────────────────────────────────────────────────────────────
    JPH.Mat44[] GetWorldSpacePose(float t)
    {
        var pose = new JPH.Mat44[cNumJoints];

        // Joint 0: translation to cloth start
        using var v0 = new JPH.Vec3(0f, cBodyPosY, -0.5f * (cNumVerticesZ - 1) * cVertexSpacing);
        pose[0] = JPH.Mat44.STranslation(v0);

        float jointSpan = (cNumVerticesZ - 1) * cVertexSpacing / (cNumJoints - 1);
        using var vStep = new JPH.Vec3(0f, 0f, jointSpan);

        for (int i = 1; i < cNumJoints; i++)
        {
            float amplitude = 0.25f * MathF.Min(t, 2.0f);
            float angle     = amplitude * MathF.Sin(0.25f * MathF.PI * i + 2.0f * t);
            using var rot   = JPH.Mat44.SRotationX(angle);
            using var trans = JPH.Mat44.STranslation(vStep);
            pose[i] = rot * trans;  // local
        }

        // Convert to world space
        for (int i = 1; i < cNumJoints; i++)
        {
            var prev = pose[i - 1];
            var cur  = pose[i];
            var next = prev * cur;
            cur.Dispose();
            pose[i] = next;
        }

        return pose;
    }

    // ─────────────────────────────────────────────────────────────
    //  SkinVertices (matches C++ SkinVertices / PrePhysicsUpdate)
    // ─────────────────────────────────────────────────────────────
    unsafe void SkinVerticesInternal(bool hardSkinAll)
    {
        if (_body is null || _tempAlloc is null) return;

        using var com     = _body.GetCenterOfMassTransform()!;
        using var offset  = com.InversedRotationTranslation();

        var worldPose = GetWorldSpacePose(_time);

        // Apply offset: localPose[i] = offset * worldPose[i]
        var localPose = new JPH.Mat44[cNumJoints];
        for (int i = 0; i < cNumJoints; i++)
        {
            localPose[i] = offset * worldPose[i];
            worldPose[i].Dispose();
        }

        // Stack-allocate joint matrices (matches C++ alloca), aligned to 16 bytes for SIMD.
        // sizeof(JPH::Mat44) = 64 bytes, so 11 joints = 704 bytes on the stack.
        byte* rawArr = stackalloc byte[cNumJoints * 64 + 15];
        byte* arr    = (byte*)(((nint)rawArr + 15L) & ~15L);  // 16-byte align
        for (int i = 0; i < cNumJoints; i++)
        {
            System.Buffer.MemoryCopy(
                localPose[i]._UnderlyingPtr,
                arr + i * 64,
                64, 64);
            localPose[i].Dispose();
        }

        // Non-owning view of the first element
        var firstJoint = new JPH.Const_Mat44(
            (JPH.Const_Mat44._Underlying*)arr, is_owning: false);

        var mp = (JPH.SoftBodyMotionProperties)_body.GetMotionProperties()!;
        mp.SetEnableSkinConstraints(true);
        mp.SetSkinnedMaxDistanceMultiplier(1.0f);
        mp.SkinVertices(com, firstJoint, (uint)cNumJoints, hardSkinAll, _tempAlloc);

        firstJoint.Dispose();
        // arr is stack-allocated; no explicit free needed
    }

    // ─────────────────────────────────────────────────────────────
    //  Init
    // ─────────────────────────────────────────────────────────────
    public override void Init(
        JPH.BodyInterface bi, JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies, Random rng)
    {
        _time = 0f;

        AddFloor(bi, bodies);

        // ── Cloth settings ────────────────────────────────────────
        // First and last Z-rows are kinematic
        Func<uint, uint, float> invMassFunc =
            (x, z) => (z == 0 || z == (uint)(cNumVerticesZ - 1)) ? 0f : 1f;

        var faces    = new List<(uint, uint, uint)>();
        var settings = CreateClothSettings(
            cNumVerticesX, cNumVerticesZ, cVertexSpacing,
            invMassFunc, null,
            JPH.SoftBodySharedSettings.EBendType.None,
            faces);
        // ^ CreateClothSettings already called CreateConstraints + Optimize

        // ── Edge compliance ───────────────────────────────────────
        settings.SoftBodySettingsSetAllEdgeCompliance(1.0e-3f);

        // ── Bind pose ─────────────────────────────────────────────
        var bindPose = GetWorldSpacePose(0f);

        // Apply offset = translation(-body_translation) to put bind pose in body-space
        using var negTrans = new JPH.Vec3(0f, -cBodyPosY, 0f);
        using var offsetMat = JPH.Mat44.STranslation(negTrans);
        for (int i = 0; i < cNumJoints; i++)
        {
            var m = offsetMat * bindPose[i];
            bindPose[i].Dispose();
            bindPose[i] = m;
        }

        // ── Inverse bind matrices ─────────────────────────────────
        for (int i = 0; i < cNumJoints; i++)
        {
            using var inv = bindPose[i].Inversed();
            settings.mInvBindMatrices.PushBack(new JPH.SoftBodySharedSettings.InvBind((uint)i, inv));
        }

        // ── Skinned constraints ───────────────────────────────────
        // Precompute joint translation Z values from bind pose
        float[] jointZ = new float[cNumJoints];
        for (int i = 0; i < cNumJoints; i++)
        {
            using var t = bindPose[i].GetTranslation();
            jointZ[i] = t.GetZ();
        }

        // Vertex position Z in body-local space (from CreateClothSettings layout)
        float offsetZ = -0.5f * cVertexSpacing * (cNumVerticesZ - 1);

        for (int z = 0; z < cNumVerticesZ; z++)
        for (int x = 0; x < cNumVerticesX; x++)
        {
            uint vertexIdx = (uint)(z * cNumVerticesX + x);
            float invMass  = invMassFunc((uint)x, (uint)z);
            float maxDist  = invMass > 0f ? 2.0f : 0.0f;
            float vertPosZ = offsetZ + z * cVertexSpacing;

            // Find two closest joints by |vertPosZ - jointZ[i]|
            int   closest     = -1, secondClosest     = -1;
            float closestDist = float.MaxValue, secondDist = float.MaxValue;
            for (int i = 0; i < cNumJoints; i++)
            {
                float d = MathF.Abs(vertPosZ - jointZ[i]);
                if (d < closestDist)
                {
                    secondClosest = closest; secondDist = closestDist;
                    closest       = i;       closestDist = d;
                }
                else if (d < secondDist)
                {
                    secondClosest = i; secondDist = d;
                }
            }

            if (closestDist == 0f)
            {
                // Hard-skin to single joint
                settings.SoftBodySettingsAddSkinnedWithWeights(
                    vertexIdx,
                    maxDist, 0.1f, 40.0f,
                    (uint)closest,  1f,
                    (uint)closest,  0f);   // duplicate; helper normalizes → weight=1
            }
            else
            {
                // Blend two closest joints; helper normalizes internally
                settings.SoftBodySettingsAddSkinnedWithWeights(
                    vertexIdx,
                    maxDist, 0.1f, 40.0f,
                    (uint)closest,       1f / closestDist,
                    (uint)secondClosest, 1f / secondDist);
            }
        }

        // Dispose bind pose
        for (int i = 0; i < cNumJoints; i++)
            bindPose[i].Dispose();

        // ── Finalize settings ──────────────────────────────────────
        settings.CalculateSkinnedConstraintNormals();
        settings.Optimize();   // second pass – required after adding skinned constraints

        // ── Create & add body ──────────────────────────────────────
        uint vertCount = (uint)settings.mVertices.Size();

        using var bodyPos = new JPH.Vec3(0f, cBodyPosY, 0f);
        using var bodyRot = JPH.Quat.SIdentity();
        using var cs = new JPH.SoftBodyCreationSettings(
            settings, bodyPos, bodyRot, LayerMoving);

        _body = bi.CreateSoftBody(cs);
        var bodyId = _body!.GetID();
        bi.AddBody(bodyId, JPH.EActivation.Activate);
        BuildSoftBodyRenderEntry(bodyId, vertCount, faces, new Vector3(0.3f, 0.6f, 1.0f));

        // ── Temp allocator ────────────────────────────────────────
        _tempAlloc = new JPH.TempAllocatorImpl(4 * 1024 * 1024);

        // Hard-skin all vertices to the initial pose
        SkinVerticesInternal(hardSkinAll: true);
    }

    // ─────────────────────────────────────────────────────────────
    //  Update (= C++ PrePhysicsUpdate; called before physicsSystem.Update)
    // ─────────────────────────────────────────────────────────────
    public override void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        // Draw joint chain in green (before incrementing time, matching C++)
        var pose = GetWorldSpacePose(_time);
        for (int i = 1; i < cNumJoints; i++)
        {
            using var ta = pose[i - 1].GetTranslation();
            using var tb = pose[i].GetTranslation();
            var a = new Vector3(ta.GetX(), ta.GetY(), ta.GetZ());
            var b = new Vector3(tb.GetX(), tb.GetY(), tb.GetZ());
            AddDebugLine(a, b);
        }
        for (int i = 0; i < cNumJoints; i++)
            pose[i].Dispose();

        _time += dt;

        SkinVerticesInternal(hardSkinAll: false);
    }

    // ─────────────────────────────────────────────────────────────
    //  Cleanup
    // ─────────────────────────────────────────────────────────────
    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        _tempAlloc?.Dispose();
        _tempAlloc = null;
        base.Cleanup(sys);
    }
}
