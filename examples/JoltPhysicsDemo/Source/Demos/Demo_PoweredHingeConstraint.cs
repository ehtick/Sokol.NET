using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of Samples/Tests/Constraints/PoweredHingeConstraintTest.cpp.
///
/// A static box connected to a dynamic box via a Y-axis hinge at their shared face.
/// A velocity motor spins the dynamic box at 25 deg/s.  Motor torque limit is derived
/// analytically from the dynamic box inertia about the constraint point (same
/// calculation as the C++ PrePhysicsUpdate, using fixed defaults).
/// </summary>
public sealed class Demo_PoweredHingeConstraint : DemoBase
{
    public override string Name     => "Powered Hinge";
    public override string Category => "Constraints";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 25,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(4f, 10f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 500f,
    };

    private JPH.HingeConstraint? _constraint;

    // C++ defaults from PoweredHingeConstraintTest.h
    private const float MaxAngularAcceleration         = 3600f * MathF.PI / 180f; // rad/s²
    private const float MaxFrictionAngularAcceleration = 0f;
    private const float MotorFrequency                 = 2f;
    private const float MotorDamping                   = 1f;

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random random)
    {
        AddFloor(bi, bodies);

        const float cBoxSize = 4f;
        const float cHalf    = cBoxSize * 0.5f;

        // Suppress collision between body1 and body2 (same group filter, same group/subgroup ID).
        // This matches the C++ original which uses: Ref<GroupFilterTable> group_filter = new GroupFilterTable;
        using var groupFilter = new JPH.GroupFilterTable();

        // body1 — static anchor
        using var ss1 = new JPH.BoxShapeSettings(new JPH.Vec3(cHalf, cHalf, cHalf));
        using var cs1 = new JPH.BodyCreationSettings();
        cs1.SetShapeSettings(ss1);
        cs1.mPosition.Set(0f, 10f, 0f);
        cs1.mMotionType  = JPH.EMotionType.Static;
        cs1.mObjectLayer = LayerNonMoving;
        // Cast to Const_GroupFilterTable to disambiguate: GroupFilterTable has two implicit
        // conversions (→ GroupFilter and → Const_GroupFilter). Upcasting to the base class
        // first leaves only one applicable conversion (Const_GroupFilterTable → Const_GroupFilter).
        cs1.mCollisionGroup.SetGroupFilter(groupFilter);
        cs1.mCollisionGroup.SetGroupID(0);
        cs1.mCollisionGroup.SetSubGroupID(0);
        var body1 = bi.CreateBody(cs1)!;
        bi.AddBody(body1.GetID(), JPH.EActivation.DontActivate);
        bodies.Add(new PhysicsBody
        {
            bodyId = body1.GetID(),
            color  = new Vector3(0.5f, 0.5f, 0.5f),
            shape  = RenderShape.Box,
            scale  = new Vector3(cBoxSize),
        });

        // body2 — dynamic, no damping, no sleep.
        using var ss2 = new JPH.BoxShapeSettings(new JPH.Vec3(cHalf, cHalf, cHalf));
        using var cs2 = new JPH.BodyCreationSettings();
        cs2.SetShapeSettings(ss2);
        cs2.mPosition.Set(cBoxSize, 10f, 0f);
        cs2.mMotionType     = JPH.EMotionType.Dynamic;
        cs2.mObjectLayer    = LayerMoving;
        cs2.mLinearDamping  = 0f;
        cs2.mAngularDamping = 0f;
        cs2.mAllowSleeping  = false;
        cs2.mCollisionGroup.SetGroupFilter(groupFilter); // same disambiguation as above
        cs2.mCollisionGroup.SetGroupID(0);
        cs2.mCollisionGroup.SetSubGroupID(0);
        var body2 = bi.CreateBody(cs2)!;
        bi.AddBody(body2.GetID(), JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId = body2.GetID(),
            color  = new Vector3(0.85f, 0.45f, 0.15f),
            shape  = RenderShape.Box,
            scale  = new Vector3(cBoxSize),
        });

        // Constraint at corner between the two boxes
        using var hs = new JPH.HingeConstraintSettings();
        hs.mPoint1.Set(cHalf, 10f, cHalf);
        hs.mPoint2.Set(cHalf, 10f, cHalf);
        hs.mHingeAxis1.Set(0f, 1f, 0f);
        hs.mHingeAxis2.Set(0f, 1f, 0f);
        hs.mNormalAxis1.Set(1f, 0f, 0f);
        hs.mNormalAxis2.Set(1f, 0f, 0f);

        var raw = hs.Create(body1, body2);
        _constraint = (JPH.HingeConstraint)raw!;

        _constraint!.SetMotorState(JPH.EMotorState.Velocity);
        _constraint.SetTargetAngularVelocity(25f * MathF.PI / 180f);

        // Compute body2 inertia about the Y-axis as seen from the constraint point.
        // C++ uses GetLocalSpaceInverseInertia().Inversed3x3() + Translate(); we
        // replicate analytically: uniform-density box, parallel-axis theorem.
        const float cDensity = 1000f;
        float mass   = cBoxSize * cBoxSize * cBoxSize * cDensity;
        float Iy     = mass * (cBoxSize * cBoxSize + cBoxSize * cBoxSize) / 12f;
        // constraint is at (cHalf, 10, cHalf); body2 center is at (cBoxSize, 10, 0)
        float dx = cHalf, dz = -cHalf;
        float inertia = Iy + mass * (dx * dx + dz * dz);

        var ms = _constraint.GetMotorSettings();
        ms.SetTorqueLimit(inertia * MaxAngularAcceleration);
        ms.mSpringSettings.mFrequency = MotorFrequency;
        ms.mSpringSettings.mDamping   = MotorDamping;
        _constraint.SetMaxFrictionTorque(inertia * MaxFrictionAngularAcceleration);

        sys.AddConstraint(_constraint);
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        if (_constraint != null) { sys.RemoveConstraint(_constraint); _constraint = null; }
    }

}

