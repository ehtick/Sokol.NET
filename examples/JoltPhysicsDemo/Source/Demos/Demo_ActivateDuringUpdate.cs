using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of Samples/Tests/General/ActivateDuringUpdateTest.cpp.
///
/// Two rows of 3 tightly-packed sleeping boxes. In each row one box
/// is given an extreme lateral velocity so that when it contacts its
/// dormant neighbours during the physics update step, the engine must
/// activate those bodies mid-step. Tests that activation-during-update
/// is handled correctly.
///
/// Row 1 (z = 0):  box 0 fired at +X 500 m/s.
/// Row 2 (z = 2):  box 2 fired at -X 500 m/s.
/// </summary>
public sealed class Demo_ActivateDuringUpdate : DemoBase
{
    public override string Name     => "ActivateDuringUpdate";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 30,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(0, 2, 1),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 500f,
    };

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random random)
    {
        AddFloor(bi, bodies, hx: 40f, hy: 0.5f, hz: 40f);

        const int   cNumBodies = 3;
        float penetrationSlop  = sys.GetPhysicsSettings().mPenetrationSlop;
        float spacing          = 1.0f - penetrationSlop;

        using var boxSS = new JPH.BoxShapeSettings(new JPH.Vec3(0.5f, 0.5f, 0.5f));

        // Row 1 — box[0] fired right at +X
        for (int i = 0; i < cNumBodies; i++)
        {
            var id = CreateAndAddBox(bi, bodies, boxSS, i * spacing, 2.0f, 0.0f, JPH.EActivation.DontActivate);
            if (i == 0)
            {
                using var vel = new JPH.Vec3(500f, 0f, 0f);
                bi.SetLinearVelocity(id, vel);
            }
        }

        // Row 2 — box[2] fired left at -X
        for (int i = 0; i < cNumBodies; i++)
        {
            var id = CreateAndAddBox(bi, bodies, boxSS, i * spacing, 2.0f, 2.0f, JPH.EActivation.DontActivate);
            if (i == cNumBodies - 1)
            {
                using var vel = new JPH.Vec3(-500f, 0f, 0f);
                bi.SetLinearVelocity(id, vel);
            }
        }
    }

    static JPH.BodyID CreateAndAddBox(
        JPH.BodyInterface bi,
        List<PhysicsBody> bodies,
        JPH.BoxShapeSettings ss,
        float px, float py, float pz,
        JPH.EActivation activation)
    {
        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(px, py, pz);
        cs.mMotionType  = JPH.EMotionType.Dynamic;
        cs.mObjectLayer = LayerMoving;

        var body = bi.CreateBody(cs)!;
        var id   = body.GetID();
        bi.AddBody(id, activation);

        bodies.Add(new PhysicsBody
        {
            bodyId = id,
            color  = new Vector3(0.65f, 0.45f, 0.25f),
            shape  = RenderShape.Box,
            scale  = new Vector3(1f, 1f, 1f),
        });
        return id;
    }
}
