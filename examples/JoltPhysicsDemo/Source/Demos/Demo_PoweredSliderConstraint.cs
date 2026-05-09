using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of Samples/Tests/Constraints/PoweredSliderConstraintTest.cpp.
///
/// A static box anchored at the origin connected to a dynamic box 14 m away
/// via a X-axis slider (limits -5 to +100 m).  A velocity motor pushes the
/// dynamic box at 1 m/s.  Force limit = mass × 250 m/s² (C++ default).
/// </summary>
public sealed class Demo_PoweredSliderConstraint : DemoBase
{
    public override string Name     => "Powered Slider";
    public override string Category => "Constraints";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 40,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(10f, 10f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000f,
    };

    private JPH.SliderConstraint? _constraint;
    private JPH.Body?             _body2;

    // C++ defaults from PoweredSliderConstraintTest.h
    private const float MaxMotorAcceleration   = 250f;   // m/s²
    private const float MaxFrictionAcceleration = 0f;
    private const float MotorFrequency          = 2f;
    private const float MotorDamping            = 1f;

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random random)
    {
        AddFloor(bi, bodies);

        const float cBoxSize = 4f;
        const float cHalf    = cBoxSize * 0.5f;

        // body1 — static anchor
        using var ss1 = new JPH.BoxShapeSettings(new JPH.Vec3(cHalf, cHalf, cHalf));
        using var cs1 = new JPH.BodyCreationSettings();
        cs1.SetShapeSettings(ss1);
        cs1.mPosition.Set(0f, 10f, 0f);
        cs1.mMotionType  = JPH.EMotionType.Static;
        cs1.mObjectLayer = LayerNonMoving;
        var body1 = bi.CreateBody(cs1)!;
        bi.AddBody(body1.GetID(), JPH.EActivation.DontActivate);
        bodies.Add(new PhysicsBody
        {
            bodyId = body1.GetID(),
            color  = new Vector3(0.5f, 0.5f, 0.5f),
            shape  = RenderShape.Box,
            scale  = new Vector3(cBoxSize),
        });

        // body2 — dynamic, no linear damping, no sleep (mirrors C++ exactly)
        using var ss2 = new JPH.BoxShapeSettings(new JPH.Vec3(cHalf, cHalf, cHalf));
        using var cs2 = new JPH.BodyCreationSettings();
        cs2.SetShapeSettings(ss2);
        cs2.mPosition.Set(cBoxSize + 10f, 10f, 0f);  // 14 m from origin
        cs2.mMotionType    = JPH.EMotionType.Dynamic;
        cs2.mObjectLayer   = LayerMoving;
        cs2.mLinearDamping = 0f;
        cs2.mAllowSleeping = false;
        _body2 = bi.CreateBody(cs2)!;
        bi.AddBody(_body2.GetID(), JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId = _body2.GetID(),
            color  = new Vector3(0.85f, 0.45f, 0.15f),
            shape  = RenderShape.Box,
            scale  = new Vector3(cBoxSize),
        });

        // Slider along X
        using var sl = new JPH.SliderConstraintSettings();
        sl.mAutoDetectPoint = true;
        using var axisX = new JPH.Vec3(1f, 0f, 0f);
        sl.SetSliderAxis(axisX);
        sl.mLimitsMin = -5f;
        sl.mLimitsMax = 100f;

        var raw = sl.Create(body1, _body2);
        _constraint = (JPH.SliderConstraint)raw!;

        _constraint!.SetMotorState(JPH.EMotorState.Velocity);
        _constraint.SetTargetVelocity(1f);

        // Force limit = mass * acceleration  (C++: motor_settings.SetForceLimit(a / inverseMass))
        float invMass = _body2.GetInverseMass();
        float mass    = invMass > 0f ? 1f / invMass : float.MaxValue;
        var ms = _constraint.GetMotorSettings();
        ms.SetForceLimit(MaxMotorAcceleration * mass);
        ms.mSpringSettings.mFrequency = MotorFrequency;
        ms.mSpringSettings.mDamping   = MotorDamping;
        _constraint.SetMaxFrictionForce(MaxFrictionAcceleration * mass);

        sys.AddConstraint(_constraint);
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        if (_constraint != null) { sys.RemoveConstraint(_constraint); _constraint = null; }
        _body2 = null;
    }

}

