using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Shows instability caused by stacking heavy objects on light ones.
/// 10 columns: each has a light base box (density 1000) topped by a
/// progressively heavier box (density 1000→10000).  The heavy boxes
/// destabilize and push through/over the light ones.
/// Mirrors JoltPhysics Samples/Tests/General/HeavyOnLightTest.cpp.
/// </summary>
public sealed class Demo_HeavyOnLight : DemoBase
{
    public override string Name     => "Heavy On Light";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 200,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(0, 15, 0),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 2000.0f
    };

    public override void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies, hx: 200f, hy: 1f, hz: 50f);

        // Exact match to C++ HeavyOnLightTest.cpp:
        //   light box (density=1000) at y=10, heavy box (density=5000*i) at y=30
        //   Both dropped simultaneously — light settles first, then heavy impacts.
        //   High mass ratios (up to 50:1) stress the solver, causing the light
        //   box to be pushed through the floor or ejected sideways.
        const float HE = 5f;   // half-extent (10 m cubes, same as Vec3::sReplicate(5))

        for (int i = 1; i <= 10; i++)
        {
            float x          = -75f + i * 15f;
            float topDensity = 5000f * i;
            float t          = (float)(i - 1) / 9f;

            var lightColor = new Vector3(0.4f, 0.7f, 0.9f);
            var heavyColor = new Vector3(0.2f + t * 0.8f, 0.8f - t * 0.6f, 0.2f);

            AddBoxWithDensity(bi, bodies, HE, HE, HE, x, 10f, 0f, 1000f,     lightColor);
            AddBoxWithDensity(bi, bodies, HE, HE, HE, x, 30f, 0f, topDensity, heavyColor);
        }
    }

    static JPH.BodyID AddBoxWithDensity(
        JPH.BodyInterface bi, List<PhysicsBody> bodies,
        float hx, float hy, float hz,
        float px, float py, float pz,
        float density, Vector3 color)
    {
        using var half = new JPH.Vec3(hx, hy, hz);
        using var ss   = new JPH.BoxShapeSettings(half);
        ss.SetDensity(density);

        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(px, py, pz);
        cs.mRotation.Set(0f, 0f, 0f, 1f);
        cs.mMotionType  = JPH.EMotionType.Dynamic;
        cs.mObjectLayer = LayerMoving;
        cs.mFriction    = 0.5f;

        var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId = id,
            color  = color,
            shape  = RenderShape.Box,
            scale  = new Vector3(hx * 2f, hy * 2f, hz * 2f)
        });
        return id;
    }
}
