using Sokol;
using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of SphereShapeTest.cpp — three spheres of different sizes and a tower of 10 spheres.
/// </summary>
public sealed class Demo_ShapeSphere : DemoBase
{
    public override string Name     => "Sphere Shape";
    public override string Category => "Shapes";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 60,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(5f, 10f, 10f),
    };

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies);

        var blue   = new Vector3(0.3f, 0.6f, 1.0f);
        var orange = new Vector3(1.0f, 0.6f, 0.2f);
        var green  = new Vector3(0.2f, 0.9f, 0.3f);
        var yellow = new Vector3(1.0f, 0.9f, 0.2f);

        AddSphere(bi, bodies, 1.0f,  0f, 10f,  0f, JPH.EMotionType.Dynamic, LayerMoving, blue);
        AddSphere(bi, bodies, 2.0f,  0f, 10f, 10f, JPH.EMotionType.Dynamic, LayerMoving, orange);
        AddSphere(bi, bodies, 0.5f,  0f, 10f, 20f, JPH.EMotionType.Dynamic, LayerMoving, green);

        // Tower of 10 spheres (r=0.5)
        for (int i = 0; i < 10; ++i)
            AddSphere(bi, bodies, 0.5f, 10f, 10f + 1.5f * i, 0f, JPH.EMotionType.Dynamic, LayerMoving, yellow);
    }
}
