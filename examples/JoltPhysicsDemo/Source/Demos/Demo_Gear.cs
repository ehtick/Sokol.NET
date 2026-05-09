using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Simplified port of Samples/Tests/Constraints/GearConstraintTest.cpp.
///
/// Two cylinders (gear1 radius=0.5, gear2 radius=2.0) are each pinned to the
/// world via a HingeConstraint and then coupled by a GearConstraint with a
/// 1:4 tooth ratio, so gear2 spins at ¼ the speed of gear1.
///
/// Gear2 is given an initial angular velocity; the gear constraint propagates
/// motion to gear1. Both gears are rendered as flat discs (Box shape used as a
/// visual approximation since there is no cylinder RenderShape).
///
/// Note: The original demo uses StaticCompoundShape with ConvexHull teeth.
/// Those shapes are not yet usable from managed code, so the gears are smooth
/// cylinders here.  Collision between the two cylinder bodies is naturally
/// avoided because each is pinned to a fixed world position by its hinge.
/// </summary>
public sealed class Demo_Gear : DemoBase
{
    public override string Name     => "Gear Constraint";
    public override string Category => "Constraints";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 15,
        Latitude  = 45,
        Longitude = 0,
        Center    = new Vector3(1.5f, 3f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 200f,
    };

    private JPH.Constraint?      _hinge1;
    private JPH.Constraint?      _hinge2;
    private JPH.GearConstraint?  _gear;

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random random)
    {
        AddFloor(bi, bodies);

        const float cGear1Radius = 0.5f;
        const float cGear2Radius = 2.0f;
        const float cHalfWidth   = 0.05f;
        const int   cGear1Teeth  = 100;
        const int   cGear2Teeth  = 400;  // cGear1Teeth * cGear2Radius / cGear1Radius
        const float cToothHeight = 0.02f;
        const float cGearY       = 3f;
        const float cGear1X      = 0f;
        const float cGear2X      = cGear1Radius + cGear2Radius + cToothHeight;

        // Quaternion for 90° rotation around X: places cylinder axis along Z (flat disc in XY)
        float sinH = MathF.Sin(MathF.PI * 0.25f); // 0.7071
        float cosH = MathF.Cos(MathF.PI * 0.25f); // 0.7071

        // Static world anchor used by both hinges (position is irrelevant, constraint is world-space)
        using var anchorSS = new JPH.SphereShapeSettings(0.01f);
        using var anchorCS = new JPH.BodyCreationSettings();
        anchorCS.SetShapeSettings(anchorSS);
        anchorCS.mPosition.Set(0f, 0f, 0f);
        anchorCS.mMotionType  = JPH.EMotionType.Static;
        anchorCS.mObjectLayer = LayerNonMoving;
        var worldAnchor = bi.CreateBody(anchorCS)!;
        bi.AddBody(worldAnchor.GetID(), JPH.EActivation.DontActivate);

        // Gear 1 (small, orange)
        using var g1SS = new JPH.CylinderShapeSettings(cHalfWidth, cGear1Radius);
        using var g1CS = new JPH.BodyCreationSettings();
        g1CS.SetShapeSettings(g1SS);
        g1CS.mPosition.Set(cGear1X, cGearY, 0f);
        g1CS.mRotation.Set(sinH, 0f, 0f, cosH);
        g1CS.mMotionType  = JPH.EMotionType.Dynamic;
        g1CS.mObjectLayer = LayerMoving;
        var gear1 = bi.CreateBody(g1CS)!;
        bi.AddBody(gear1.GetID(), JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId     = gear1.GetID(),
            color      = new Vector3(0.85f, 0.45f, 0.15f),
            shape      = RenderShape.Cylinder,
            scale      = new Vector3(cGear1Radius * 2f, cHalfWidth * 2f, cGear1Radius * 2f),
            numTeeth   = cGear1Teeth,
            toothHeight = cToothHeight,
        });

        // Gear 2 (large, blue)
        using var g2SS = new JPH.CylinderShapeSettings(cHalfWidth, cGear2Radius);
        using var g2CS = new JPH.BodyCreationSettings();
        g2CS.SetShapeSettings(g2SS);
        g2CS.mPosition.Set(cGear2X, cGearY, 0f);
        g2CS.mRotation.Set(sinH, 0f, 0f, cosH);
        g2CS.mMotionType  = JPH.EMotionType.Dynamic;
        g2CS.mObjectLayer = LayerMoving;
        var gear2 = bi.CreateBody(g2CS)!;
        bi.AddBody(gear2.GetID(), JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId      = gear2.GetID(),
            color       = new Vector3(0.2f, 0.5f, 0.85f),
            shape       = RenderShape.Cylinder,
            scale       = new Vector3(cGear2Radius * 2f, cHalfWidth * 2f, cGear2Radius * 2f),
            numTeeth    = cGear2Teeth,
            toothHeight = cToothHeight,
        });

        // Hinge for gear1: world anchor → gear1, pivot at gear1 position, axis = Z
        using var hs1 = new JPH.HingeConstraintSettings();
        hs1.mPoint1.Set(cGear1X, cGearY, 0f);
        hs1.mPoint2.Set(cGear1X, cGearY, 0f);
        hs1.mHingeAxis1.Set(0f, 0f, 1f);
        hs1.mHingeAxis2.Set(0f, 0f, 1f);
        hs1.mNormalAxis1.Set(1f, 0f, 0f);
        hs1.mNormalAxis2.Set(1f, 0f, 0f);
        _hinge1 = (JPH.Constraint?)hs1.Create(worldAnchor, gear1);
        sys.AddConstraint(_hinge1);

        // Hinge for gear2: world anchor → gear2, pivot at gear2 position, axis = Z
        using var hs2 = new JPH.HingeConstraintSettings();
        hs2.mPoint1.Set(cGear2X, cGearY, 0f);
        hs2.mPoint2.Set(cGear2X, cGearY, 0f);
        hs2.mHingeAxis1.Set(0f, 0f, 1f);
        hs2.mHingeAxis2.Set(0f, 0f, 1f);
        hs2.mNormalAxis1.Set(1f, 0f, 0f);
        hs2.mNormalAxis2.Set(1f, 0f, 0f);
        _hinge2 = (JPH.Constraint?)hs2.Create(worldAnchor, gear2);
        sys.AddConstraint(_hinge2);

        // Gear constraint: ratio matches tooth counts (100:400 = 1:4)
        using var gs = new JPH.GearConstraintSettings();
        gs.mHingeAxis1.Set(0f, 0f, 1f);
        gs.mHingeAxis2.Set(0f, 0f, 1f);
        gs.SetRatio(cGear1Teeth, cGear2Teeth);
        _gear = new JPH.GearConstraint(gear1, gear2, gs);
        _gear.SetConstraints(_hinge1, _hinge2);
        sys.AddConstraint(_gear);

        // Give gear2 initial spin: 3 rad/s around Z
        using var angVel = new JPH.Vec3(0f, 0f, 3f);
        var g2id = gear2.GetID();
        bi.SetAngularVelocity(g2id, angVel);
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        if (_hinge1 != null) { sys.RemoveConstraint(_hinge1); _hinge1 = null; }
        if (_hinge2 != null) { sys.RemoveConstraint(_hinge2); _hinge2 = null; }
        if (_gear   != null) { sys.RemoveConstraint(_gear);   _gear   = null; }
    }
}
