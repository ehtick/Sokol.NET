using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of Samples/Tests/Constraints/SpringTest.cpp.
///
/// Three groups of 10 boxes hanging from a fixed overhead bar via DistanceConstraints
/// with spring settings:
///   Group 1 (x=-100..-55) same frequency 0.33 Hz, varying natural lengths.
///   Group 2 (x=-25..+20)  same rest length 25 m, varying frequency 0.1..1.0 Hz.
///   Group 3 (x=+50..+95)  same rest length and frequency, varying damping 0..1.
/// All boxes start 5 m above their rest position and oscillate.
/// </summary>
public sealed class Demo_Spring : DemoBase
{
    public override string Name     => "Spring";
    public override string Category => "Constraints";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 250,
        Latitude  = 15,
        Longitude = 0,
        Center    = new Vector3(0f, 55f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 2000f,
    };

    private readonly List<JPH.TwoBodyConstraint?> _constraints = new();

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random random)
    {
        AddFloor(bi, bodies);

        const float topY = 75f;

        using var topSS = new JPH.BoxShapeSettings(new JPH.Vec3(100f, 1f, 1f));
        using var topCS = new JPH.BodyCreationSettings();
        topCS.SetShapeSettings(topSS);
        topCS.mPosition.Set(0f, topY, 0f);
        topCS.mMotionType  = JPH.EMotionType.Static;
        topCS.mObjectLayer = LayerNonMoving;
        var topBody = bi.CreateBody(topCS)!;
        bi.AddBody(topBody.GetID(), JPH.EActivation.DontActivate);
        bodies.Add(new PhysicsBody
        {
            bodyId = topBody.GetID(),
            color  = new Vector3(0.45f, 0.45f, 0.45f),
            shape  = RenderShape.Box,
            scale  = new Vector3(200f, 2f, 2f)
        });

        // Group 1: same frequency (0.33 Hz), varying natural lengths
        for (int i = 0; i < 10; i++)
        {
            float ax    = -100f + i * 5f;
            float restY = topY - (10f + i * 2.5f);
            Attach(bi, sys, bodies, topBody, ax, topY, restY,
                new Vector3(0.3f, 0.6f, 0.9f), frequency: 0.33f, damping: 0f);
        }

        // Group 2: same rest length (25 m), varying frequency 0.1..1.0 Hz
        for (int i = 0; i < 10; i++)
        {
            float ax    = -25f + i * 5f;
            float restY = topY - 25f;
            Attach(bi, sys, bodies, topBody, ax, topY, restY,
                new Vector3(0.9f, 0.55f, 0.2f), frequency: 0.1f + 0.1f * i, damping: 0f);
        }

        // Group 3: same rest length and frequency, varying damping 0..1
        for (int i = 0; i < 10; i++)
        {
            float ax    = 50f + i * 5f;
            float restY = topY - 25f;
            Attach(bi, sys, bodies, topBody, ax, topY, restY,
                new Vector3(0.3f, 0.8f, 0.45f), frequency: 0.33f, damping: (1f / 9f) * i);
        }
    }

    private void Attach(
        JPH.BodyInterface bi, JPH.PhysicsSystem sys, List<PhysicsBody> bodies,
        JPH.Body topBody, float x, float attachY, float restY,
        Vector3 color, float frequency, float damping)
    {
        using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(0.75f, 0.75f, 0.75f));
        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(x, restY, 0f);
        cs.mMotionType    = JPH.EMotionType.Dynamic;
        cs.mObjectLayer   = LayerMoving;
        cs.mLinearDamping  = 0f;
        cs.mAngularDamping = 0f;
        var body = bi.CreateBody(cs)!;
        bi.AddBody(body.GetID(), JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId = body.GetID(),
            color  = color,
            shape  = RenderShape.Box,
            scale  = new Vector3(1.5f)
        });

        using var ds = new JPH.DistanceConstraintSettings();
        ds.mPoint1.Set(x, attachY, 0f);
        ds.mPoint2.Set(x, restY, 0f);
        ds.mLimitsSpringSettings.mFrequency = frequency;
        ds.mLimitsSpringSettings.mDamping   = damping;
        var c = ds.Create(topBody, body);
        _constraints.Add(c);
        sys.AddConstraint(c);

        // Move body 5 m above rest so it starts oscillating
        using var newPos = new JPH.Vec3(x, attachY - 5f, 0f);
        using var idRot  = new JPH.Quat(); idRot.Set(0f, 0f, 0f, 1f);
        var bid = body.GetID();
        bi.SetPositionAndRotation(bid, newPos, idRot, JPH.EActivation.DontActivate);
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        foreach (var c in _constraints)
            if (c != null) sys.RemoveConstraint(c);
        _constraints.Clear();
    }
}
