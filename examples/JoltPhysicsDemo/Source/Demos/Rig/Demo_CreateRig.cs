using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of Samples/Tests/Rig/CreateRigTest.cpp.
/// Creates a single humanoid ragdoll from scratch using RagdollSettings
/// (12 capsule bodies connected by SwingTwistConstraints).
/// </summary>
class Demo_CreateRig : DemoBase
{
    public override string Name     => "Create Rig";
    public override string Category => "Rig";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 5,
        Latitude  = 20,
        Longitude = 45,
        Center    = new Vector3(0, 1, 0),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 500.0f,
    };

    private JPH.Ragdoll?     _ragdoll;
    private int               _ragdollBodyStart = -1;
    private int               _ragdollBodyCount = 0;
    private List<PhysicsBody>? _bodies;

    // ── Part data ─────────────────────────────────────────────────────────

    // (halfCylH, radius)
    private static readonly (float h, float r)[] s_capsules =
    {
        (0.15f,  0.10f),  // 0  LowerBody
        (0.15f,  0.10f),  // 1  MidBody
        (0.15f,  0.10f),  // 2  UpperBody
        (0.075f, 0.10f),  // 3  Head
        (0.15f,  0.06f),  // 4  UpperArmL
        (0.15f,  0.06f),  // 5  UpperArmR
        (0.15f,  0.05f),  // 6  LowerArmL
        (0.15f,  0.05f),  // 7  LowerArmR
        (0.20f,  0.075f), // 8  UpperLegL
        (0.20f,  0.075f), // 9  UpperLegR
        (0.20f,  0.06f),  // 10 LowerLegL
        (0.20f,  0.06f),  // 11 LowerLegR
    };

    private static readonly (float x, float y, float z)[] s_positions =
    {
        ( 0f,      1.150f, 0f),  // 0  LowerBody
        ( 0f,      1.350f, 0f),  // 1  MidBody
        ( 0f,      1.550f, 0f),  // 2  UpperBody
        ( 0f,      1.825f, 0f),  // 3  Head
        (-0.425f,  1.550f, 0f),  // 4  UpperArmL
        ( 0.425f,  1.550f, 0f),  // 5  UpperArmR
        (-0.8f,    1.550f, 0f),  // 6  LowerArmL
        ( 0.8f,    1.550f, 0f),  // 7  LowerArmR
        (-0.15f,   0.800f, 0f),  // 8  UpperLegL
        ( 0.15f,   0.800f, 0f),  // 9  UpperLegR
        (-0.15f,   0.300f, 0f),  // 10 LowerLegL
        ( 0.15f,   0.300f, 0f),  // 11 LowerLegR
    };

    // Constraint positions (parts 1-11; part 0 is the root, no constraint)
    private static readonly (float x, float y, float z)[] s_constraintPositions =
    {
        ( 0f,      1.250f, 0f),  // 1  MidBody
        ( 0f,      1.450f, 0f),  // 2  UpperBody
        ( 0f,      1.650f, 0f),  // 3  Head
        (-0.225f,  1.550f, 0f),  // 4  UpperArmL
        ( 0.225f,  1.550f, 0f),  // 5  UpperArmR
        (-0.65f,   1.550f, 0f),  // 6  LowerArmL
        ( 0.65f,   1.550f, 0f),  // 7  LowerArmR
        (-0.15f,   1.050f, 0f),  // 8  UpperLegL
        ( 0.15f,   1.050f, 0f),  // 9  UpperLegR
        (-0.15f,   0.550f, 0f),  // 10 LowerLegL
        ( 0.15f,   0.550f, 0f),  // 11 LowerLegR
    };

    // Swing/twist limits in degrees [twistDeg, normalDeg, planeDeg] for parts 1-11
    private static readonly (float twist, float normal, float plane)[] s_limits =
    {
        (5f,  10f,  10f),  // 1  MidBody
        (5f,  10f,  10f),  // 2  UpperBody
        (90f, 45f,  45f),  // 3  Head
        (45f, 90f,  45f),  // 4  UpperArmL
        (45f, 90f,  45f),  // 5  UpperArmR
        (45f,  0f,  90f),  // 6  LowerArmL
        (45f,  0f,  90f),  // 7  LowerArmR
        (45f, 45f,  45f),  // 8  UpperLegL
        (45f, 45f,  45f),  // 9  UpperLegR
        (45f,  0f,  60f),  // 10 LowerLegL
        (45f,  0f,  60f),  // 11 LowerLegR
    };

    // Joint parent indices (-1 = root)
    private static readonly int[] s_parents =
    {
        -1,  // 0  LowerBody  (root)
         0,  // 1  MidBody
         1,  // 2  UpperBody
         2,  // 3  Head
         2,  // 4  UpperArmL
         2,  // 5  UpperArmR
         4,  // 6  LowerArmL
         5,  // 7  LowerArmR
         0,  // 8  UpperLegL
         0,  // 9  UpperLegR
         8,  // 10 LowerLegL
         9,  // 11 LowerLegR
    };

    private static readonly string[] s_names =
    {
        "LowerBody", "MidBody",   "UpperBody", "Head",
        "UpperArmL", "UpperArmR", "LowerArmL", "LowerArmR",
        "UpperLegL", "UpperLegR", "LowerLegL", "LowerLegR",
    };

    // ── Init ─────────────────────────────────────────────────────────────

    public override void Init(JPH.BodyInterface bi, JPH.PhysicsSystem sys, List<PhysicsBody> bodies, Random random)
    {
        _bodies = bodies;
        AddFloor(bi, bodies);
        _ = random;

        int partCount = s_capsules.Length;

        // ── Skeleton ──────────────────────────────────────────────────────
        var skeleton = new JPH.Skeleton();
        skeleton.AddJoint(s_names[0]);  // root joint (no parent)
        for (int i = 1; i < partCount; i++)
            skeleton.AddJoint(s_names[i], s_parents[i]);

        // ── RagdollSettings ───────────────────────────────────────────────
        var settings = new JPH.RagdollSettings();
        settings.SetSkeleton(skeleton);

        // Upright rotation (identity)
        using var rotIdentity = JPH.Quat.SIdentity();
        // Horizontal rotation (capsule aligned along X axis)
        using var axisZ = JPH.Vec3.SAxisZ();
        using var rotHoriz = JPH.Quat.SRotation(axisZ, 0.5f * MathF.PI);

        for (int i = 0; i < partCount; i++)
        {
            var part = new JPH.RagdollSettings.Part();

            // Position
            var (px, py, pz) = s_positions[i];
            part.mPosition.Set(px, py, pz);

            // Rotation: torso/arms = horizontal; head/legs = upright
            bool isHoriz = i is 0 or 1 or 2 or 4 or 5 or 6 or 7;
            if (isHoriz)
                part.mRotation.Assign(rotHoriz);
            else
                part.mRotation.Assign(rotIdentity);

            // Layer and motion type
            part.mObjectLayer = LayerMoving;
            part.mMotionType  = JPH.EMotionType.Dynamic;

            // Capsule shape
            var (h, r) = s_capsules[i];
            using var capsule = new JPH.CapsuleShapeSettings(h, r);
            part.SetShapeSettings(capsule);

            // Constraint (all parts except the root)
            if (i > 0)
            {
                var cs = new JPH.SwingTwistConstraintSettings();

                var (cx, cy, cz) = s_constraintPositions[i - 1];
                cs.mPosition1.Set(cx, cy, cz);
                cs.mPosition2.Set(cx, cy, cz);

                // Twist axis
                SetTwistAxis(cs, i);

                // Plane axis (same for all)
                using var axisZPlane = JPH.Vec3.SAxisZ();
                cs.mPlaneAxis1.Assign(axisZPlane);
                cs.mPlaneAxis2.Assign(axisZPlane);

                // Limits
                var (twistDeg, normalDeg, planeDeg) = s_limits[i - 1];
                cs.mTwistMinAngle        = -twistDeg  * MathF.PI / 180f;
                cs.mTwistMaxAngle        =  twistDeg  * MathF.PI / 180f;
                cs.mNormalHalfConeAngle  =  normalDeg * MathF.PI / 180f;
                cs.mPlaneHalfConeAngle   =  planeDeg  * MathF.PI / 180f;
                cs.mDrawConstraintSize   = 0.1f;

                part.RagdollSettingsPartSetToParent(cs);
            }

            settings.AddPart(part);
        }

        settings.Stabilize();
        settings.CalculateConstraintPriorities();
        settings.DisableParentChildCollisions();
        settings.CalculateBodyIndexToConstraintIndex();

        // ── Create & activate ragdoll ─────────────────────────────────────
        _ragdoll = settings.CreateRagdoll(1, 0, sys);
        _ragdoll!.AddToPhysicsSystem(JPH.EActivation.Activate);

        // ── Add render entries ─────────────────────────────────────────────
        _ragdollBodyStart = bodies.Count;
        _ragdollBodyCount = (int)_ragdoll.GetBodyCount();

        for (int i = 0; i < _ragdollBodyCount; i++)
        {
            var id      = _ragdoll.GetBodyID(i);
            var (h, r)  = s_capsules[i];
            bodies.Add(new PhysicsBody
            {
                bodyId = id,
                shape  = RenderShape.Capsule,
                scale  = new Vector3(r, h, r),
                color  = GetDistinctColor(i),
            });
        }

        // Keep alive (settings and skeleton referenced by ragdoll internally)
        skeleton.Dispose();
        settings.Dispose();
    }

    // ── Cleanup ───────────────────────────────────────────────────────────

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
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void SetTwistAxis(JPH.SwingTwistConstraintSettings cs, int partIndex)
    {
        // Torso / head: twist along +Y
        if (partIndex is 1 or 2 or 3)
        {
            using var axisY = JPH.Vec3.SAxisY();
            cs.mTwistAxis1.Assign(axisY);
            cs.mTwistAxis2.Assign(axisY);
            return;
        }
        // Left arms: twist along -X
        if (partIndex is 4 or 6)
        {
            using var negAxisX = -JPH.Vec3.SAxisX();
            cs.mTwistAxis1.Assign(negAxisX);
            cs.mTwistAxis2.Assign(negAxisX);
            return;
        }
        // Right arms: twist along +X
        if (partIndex is 5 or 7)
        {
            using var axisX = JPH.Vec3.SAxisX();
            cs.mTwistAxis1.Assign(axisX);
            cs.mTwistAxis2.Assign(axisX);
            return;
        }
        // Legs: twist along -Y
        using var negAxisY = -JPH.Vec3.SAxisY();
        cs.mTwistAxis1.Assign(negAxisY);
        cs.mTwistAxis2.Assign(negAxisY);
    }
}
