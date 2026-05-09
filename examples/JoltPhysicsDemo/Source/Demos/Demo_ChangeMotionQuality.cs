using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of Samples/Tests/General/ChangeMotionQualityTest.cpp.
///
/// A fast-moving sphere (v = 240 m/s laterally) bounces inside a kinematic
/// enclosure made of four thin walls. Every second the sphere's motion quality
/// toggles between EMotionQuality.LinearCast (tunnelling-safe) and
/// EMotionQuality.Discrete (cheaper but can miss thin surfaces at high speed).
///
/// LinearCast → sphere stays inside.
/// Discrete   → sphere may tunnel through the walls.
/// </summary>
public sealed class Demo_ChangeMotionQuality : DemoBase
{
    public override string Name     => "ChangeMotionQuality";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 20,
        Latitude  = 25,
        Longitude = 0,
        Center    = new Vector3(0, 1, 0),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 500f,
    };

    private JPH.BodyID _sphereId;
    private float _time;

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random random)
    {
        _time = 0f;

        AddFloor(bi, bodies);

        // Four individual kinematic walls forming a 10×2×10 enclosure centred at (0,1,0).
        // Using separate bodies so each renders at its correct world position.
        var wallColor = new Vector3(0.35f, 0.5f, 0.7f);
        AddWall(bi, bodies, 5f, 1f, 0.1f,  0f, 1f,  5f, wallColor); // +Z
        AddWall(bi, bodies, 5f, 1f, 0.1f,  0f, 1f, -5f, wallColor); // -Z
        AddWall(bi, bodies, 0.1f, 1f, 5f,  5f, 1f,  0f, wallColor); // +X
        AddWall(bi, bodies, 0.1f, 1f, 5f, -5f, 1f,  0f, wallColor); // -X

        // Fast sphere bouncing inside
        using var sphereSS = new JPH.SphereShapeSettings(1f);
        using var sphereCS = new JPH.BodyCreationSettings();
        sphereCS.SetShapeSettings(sphereSS);
        sphereCS.mPosition.Set(0f, 0.5f, 0f);
        sphereCS.mMotionType  = JPH.EMotionType.Dynamic;
        sphereCS.mObjectLayer = LayerMoving;
        sphereCS.mFriction    = 0f;
        sphereCS.mRestitution = 1f;
        sphereCS.mLinearVelocity.Set(-240f, 0f, -120f);

        var sphereBody = bi.CreateBody(sphereCS)!;
        _sphereId = sphereBody.GetID();
        bi.AddBody(_sphereId, JPH.EActivation.Activate);

        bodies.Add(new PhysicsBody
        {
            bodyId = _sphereId,
            color  = new Vector3(1f, 0.7f, 0.2f),
            shape  = RenderShape.Sphere,
            scale  = new Vector3(2f),
        });

        UpdateMotionQuality(bi);
    }

    static void AddWall(
        JPH.BodyInterface bi,
        List<PhysicsBody> bodies,
        float hx, float hy, float hz,
        float px, float py, float pz,
        Vector3 color)
    {
        using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(hx, hy, hz));
        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(px, py, pz);
        cs.mMotionType  = JPH.EMotionType.Kinematic;
        cs.mObjectLayer = LayerMoving;

        var body = bi.CreateBody(cs)!;
        var id   = body.GetID();
        bi.AddBody(id, JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId = id,
            shape  = RenderShape.Box,
            color  = color,
            scale  = new Vector3(hx * 2f, hy * 2f, hz * 2f),
        });
    }

    public override void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        _time += dt;
        UpdateMotionQuality(bi);
    }

    private void UpdateMotionQuality(JPH.BodyInterface bi)
    {
        var quality = ((int)_time & 1) == 0
            ? JPH.EMotionQuality.LinearCast
            : JPH.EMotionQuality.Discrete;
        bi.SetMotionQuality(_sphereId, quality);
    }
}
