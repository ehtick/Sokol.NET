using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Simplified port of Samples/Tests/Constraints/RackAndPinionConstraintTest.cpp.
///
/// A cylinder (pinion, radius=0.5) is pinned to the world by a HingeConstraint and
/// coupled to a long box (rack) that slides on a SliderConstraint via a
/// RackAndPinionConstraint.  Spinning the pinion translates the rack and vice versa.
///
/// The pinion is given an initial angular velocity; watch the rack slide back and
/// forth between its limits.
///
/// Note: The original demo uses StaticCompoundShape with ConvexHull teeth; here both
/// bodies are simplified shapes.  Collision between pinion and rack is avoided by
/// positioning them with a small gap (no GroupFilterTable available yet).
/// </summary>
public sealed class Demo_RackAndPinion : DemoBase
{
    public override string Name     => "Rack And Pinion";
    public override string Category => "Constraints";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 18,
        Latitude  = 25,
        Longitude = 30,
        Center    = new Vector3(0f, 1.8f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 200f,
    };

    private JPH.Constraint?              _hinge;
    private JPH.Constraint?              _slider;
    private JPH.RackAndPinionConstraint? _randp;

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random random)
    {
        AddFloor(bi, bodies);

        const float cGearRadius  = 0.5f;
        const float cHalfWidth   = 0.05f;
        const int   cGearTeeth   = 100;
        const float cGearY       = 2f;
        const float cGearX       = 0f;
        const float cRackLength  = 10f;
        const float cRackHalfH   = 0.1f;
        const float cRackHalfW   = 0.05f;
        const float cToothHeight = 0.02f;
        const int   cRackTeeth   = (int)(cRackLength * cGearTeeth / (2f * MathF.PI * cGearRadius)); // ≈ 318
        // Rack center: gear bottom minus rack half-height minus tooth height (matching C++)
        const float cRackY       = cGearY - cGearRadius - cRackHalfH - cToothHeight;

        // Quaternion for 90° around X (places cylinder axis along Z, disc in XY)
        float sinH = MathF.Sin(MathF.PI * 0.25f);
        float cosH = MathF.Cos(MathF.PI * 0.25f);

        // Shared static world anchor for both hinge and slider
        using var anchorSS = new JPH.SphereShapeSettings(0.01f);
        using var anchorCS = new JPH.BodyCreationSettings();
        anchorCS.SetShapeSettings(anchorSS);
        anchorCS.mPosition.Set(0f, 0f, 0f);
        anchorCS.mMotionType  = JPH.EMotionType.Static;
        anchorCS.mObjectLayer = LayerNonMoving;
        var worldAnchor = bi.CreateBody(anchorCS)!;
        bi.AddBody(worldAnchor.GetID(), JPH.EActivation.DontActivate);

        // Pinion (cylinder, rendered as flat disc box)
        using var pinionSS = new JPH.CylinderShapeSettings(cHalfWidth, cGearRadius);
        using var pinionCS = new JPH.BodyCreationSettings();
        pinionCS.SetShapeSettings(pinionSS);
        pinionCS.mPosition.Set(cGearX, cGearY, 0f);
        pinionCS.mRotation.Set(sinH, 0f, 0f, cosH);
        pinionCS.mMotionType  = JPH.EMotionType.Dynamic;
        pinionCS.mObjectLayer = LayerMoving;
        var pinion = bi.CreateBody(pinionCS)!;
        bi.AddBody(pinion.GetID(), JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId      = pinion.GetID(),
            color       = new Vector3(0.85f, 0.45f, 0.15f),
            shape       = RenderShape.Cylinder,
            scale       = new Vector3(cGearRadius * 2f, cHalfWidth * 2f, cGearRadius * 2f),
            numTeeth    = cGearTeeth,
            toothHeight = cToothHeight,
        });

        // Rack (long box sliding along X, teeth on top face)
        using var rackSS = new JPH.BoxShapeSettings(new JPH.Vec3(cRackLength * 0.5f, cRackHalfH, cRackHalfW));
        using var rackCS = new JPH.BodyCreationSettings();
        rackCS.SetShapeSettings(rackSS);
        rackCS.mPosition.Set(cGearX, cRackY, 0f);
        rackCS.mMotionType  = JPH.EMotionType.Dynamic;
        rackCS.mObjectLayer = LayerMoving;
        var rack = bi.CreateBody(rackCS)!;
        bi.AddBody(rack.GetID(), JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId      = rack.GetID(),
            color       = new Vector3(0.3f, 0.7f, 0.35f),
            shape       = RenderShape.Box,
            scale       = new Vector3(cRackLength, cRackHalfH * 2f, cRackHalfW * 2f),
            numTeeth    = cRackTeeth,
            toothHeight = cToothHeight,
        });

        // Hinge: world → pinion, pivot at pinion position, axis = Z
        using var hs = new JPH.HingeConstraintSettings();
        hs.mPoint1.Set(cGearX, cGearY, 0f);
        hs.mPoint2.Set(cGearX, cGearY, 0f);
        hs.mHingeAxis1.Set(0f, 0f, 1f);
        hs.mHingeAxis2.Set(0f, 0f, 1f);
        hs.mNormalAxis1.Set(1f, 0f, 0f);
        hs.mNormalAxis2.Set(1f, 0f, 0f);
        _hinge = (JPH.Constraint?)hs.Create(worldAnchor, pinion);
        sys.AddConstraint(_hinge);

        // Slider: world → rack, axis = X (perpendicular to pinion spin axis Z), limits ±5 m
        using var ss = new JPH.SliderConstraintSettings();
        ss.mPoint1.Set(cGearX, cGearY, 0f);
        ss.mPoint2.Set(cGearX, cGearY, 0f);
        ss.mSliderAxis1.Set(1f, 0f, 0f);
        ss.mSliderAxis2.Set(1f, 0f, 0f);
        ss.mNormalAxis1.Set(0f, 0f, 1f);
        ss.mNormalAxis2.Set(0f, 0f, 1f);
        ss.mLimitsMin = -cRackLength * 0.5f;
        ss.mLimitsMax =  cRackLength * 0.5f;
        _slider = (JPH.Constraint?)ss.Create(worldAnchor, rack);
        sys.AddConstraint(_slider);

        // Rack-and-pinion constraint: pinion spins around Z, rack slides along X
        using var rps = new JPH.RackAndPinionConstraintSettings();
        rps.mHingeAxis.Set(0f, 0f, 1f);
        rps.mSliderAxis.Set(1f, 0f, 0f);
        rps.SetRatio(cRackTeeth, cRackLength, cGearTeeth);
        _randp = new JPH.RackAndPinionConstraint(pinion, rack, rps);
        _randp.SetConstraints(_hinge, _slider);
        sys.AddConstraint(_randp);

        // Give pinion an initial spin (6 rad/s around Z)
        using var angVel = new JPH.Vec3(0f, 0f, 6f);
        var pid = pinion.GetID();
        bi.SetAngularVelocity(pid, angVel);
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        if (_hinge  != null) { sys.RemoveConstraint(_hinge);  _hinge  = null; }
        if (_slider != null) { sys.RemoveConstraint(_slider); _slider = null; }
        if (_randp  != null) { sys.RemoveConstraint(_randp);  _randp  = null; }
    }
}
