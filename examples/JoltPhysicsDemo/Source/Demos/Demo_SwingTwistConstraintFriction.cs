using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of Samples/Tests/Constraints/SwingTwistConstraintFrictionTest.cpp.
///
/// Two capsules connected by a SwingTwistConstraint with 90° cone limits and
/// full ±180° twist range.  A velocity motor drives swing+twist at 180 deg/s
/// along the body's X-axis for 2.5 s, then turns off for 2.5 s (repeating).
/// Friction torque resists free spinning at up to 90 deg/s² of deceleration.
/// </summary>
public sealed class Demo_SwingTwistConstraintFriction : DemoBase
{
    public override string Name     => "SwingTwist Friction";
    public override string Category => "Constraints";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 15,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(0f, 8f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 500f,
    };

    private JPH.SwingTwistConstraint? _constraint;
    private float                     _time;

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random random)
    {
        AddFloor(bi, bodies);

        const float cHalfHeight = 1.5f;
        const float cRadius     = 0.5f;

        // Create group filter — same as C++ original (suppresses collision between the two capsules)
        using var groupFilter = new JPH.GroupFilterTable();

        // body1 — static capsule
        using var caps1 = new JPH.CapsuleShapeSettings(cHalfHeight, cRadius);
        using var cs1   = new JPH.BodyCreationSettings();
        cs1.SetShapeSettings(caps1);
        cs1.mPosition.Set(0f, 10f, 0f);
        cs1.mMotionType  = JPH.EMotionType.Static;
        cs1.mObjectLayer = LayerNonMoving;
        var body1 = bi.CreateBody(cs1)!;
        // Match C++: body.SetCollisionGroup(CollisionGroup(group_filter, 0, 0))
        using var cg1 = new JPH.CollisionGroup(groupFilter, 0, 0);
        body1.SetCollisionGroup(cg1);
        bi.AddBody(body1.GetID(), JPH.EActivation.DontActivate);
        bodies.Add(new PhysicsBody
        {
            bodyId = body1.GetID(),
            color  = new Vector3(0.5f, 0.5f, 0.5f),
            shape  = RenderShape.Capsule,
            scale  = new Vector3(cRadius, cHalfHeight, cRadius),
        });

        // body2 — dynamic capsule, no damping, no sleep
        using var caps2 = new JPH.CapsuleShapeSettings(cHalfHeight, cRadius);
        using var cs2   = new JPH.BodyCreationSettings();
        cs2.SetShapeSettings(caps2);
        cs2.mPosition.Set(0f, 10f - 2f * cHalfHeight, 0f);
        cs2.mMotionType     = JPH.EMotionType.Dynamic;
        cs2.mObjectLayer    = LayerMoving;
        cs2.mLinearDamping  = 0f;
        cs2.mAngularDamping = 0f;
        cs2.mAllowSleeping  = false;
        var body2 = bi.CreateBody(cs2)!;
        using var cg2 = new JPH.CollisionGroup(groupFilter, 0, 0);
        body2.SetCollisionGroup(cg2);
        bi.AddBody(body2.GetID(), JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId = body2.GetID(),
            color  = new Vector3(0.85f, 0.45f, 0.15f),
            shape  = RenderShape.Capsule,
            scale  = new Vector3(cRadius, cHalfHeight, cRadius),
        });

        // Compute I_y (spin about long/twist Y-axis) of a capsule analytically,
        // identical to C++ GetLocalSpaceInverseInertia().Inversed3x3() * Vec3::sAxisY().
        const float cDensity = 1000f;
        float mCyl  = cDensity * MathF.PI * cRadius * cRadius * 2f * cHalfHeight;
        float mHemi = cDensity * (2f / 3f) * MathF.PI * cRadius * cRadius * cRadius;
        float Iy = 0.5f * mCyl * cRadius * cRadius
                 + 2f * (2f / 5f) * mHemi * cRadius * cRadius;
        const float maxAngularAccel = 90f * MathF.PI / 180f;
        float frictionTorque = Iy * maxAngularAccel;

        using var st = new JPH.SwingTwistConstraintSettings();
        st.mPosition1.Set(0f, 10f - cHalfHeight, 0f);
        st.mPosition2.Set(0f, 10f - cHalfHeight, 0f);
        st.mTwistAxis1.Set(0f, -1f, 0f);
        st.mTwistAxis2.Set(0f, -1f, 0f);
        st.mPlaneAxis1.Set(1f, 0f, 0f);
        st.mPlaneAxis2.Set(1f, 0f, 0f);
        st.mNormalHalfConeAngle = MathF.PI / 2f;
        st.mPlaneHalfConeAngle  = MathF.PI / 2f;
        st.mTwistMinAngle       = -MathF.PI;
        st.mTwistMaxAngle       =  MathF.PI;
        st.mMaxFrictionTorque   = frictionTorque;
        st.mSwingMotorSettings.mSpringSettings.mFrequency = 10f;
        st.mSwingMotorSettings.mSpringSettings.mDamping   = 2f;
        st.mTwistMotorSettings.mSpringSettings.mFrequency = 10f;
        st.mTwistMotorSettings.mSpringSettings.mDamping   = 2f;

        var raw = st.Create(body1, body2);
        _constraint = (JPH.SwingTwistConstraint)raw!;

        sys.AddConstraint(_constraint!);
        _time = 0f;
    }

    public override unsafe void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        _time += dt;
        bool pause = (_time % 5f) > 2.5f;

        if (pause)
        {
            _constraint!.SetSwingMotorState(JPH.EMotorState.Off);
            _constraint.SetTwistMotorState(JPH.EMotorState.Off);
        }
        else
        {
            _constraint!.SetSwingMotorState(JPH.EMotorState.Velocity);
            _constraint.SetTwistMotorState(JPH.EMotorState.Velocity);
            using var vel = new JPH.Vec3(180f * MathF.PI / 180f, 0f, 0f);
            _constraint.SetTargetAngularVelocityCS(vel);
        }
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        if (_constraint != null) { sys.RemoveConstraint(_constraint); _constraint = null; }
        _time = 0f;
    }

}

