using Sokol;
using System;
using System.Collections.Generic;
using System.Numerics;

public sealed class Demo_ShapeTriangle : DemoBase
{
    public override string Name     => "Triangle Shape";
    public override string Category => "Shapes";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 30,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(0f, 2f, 0f),
    };

    public override unsafe void Init(
        JPH.BodyInterface bi, JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies, Random random)
    {
        // Static triangle (matches C++ TriangleTest.cpp)
        using var v1    = new JPH.Vec3(-10f, -1f,   0f);
        using var v2    = new JPH.Vec3(  0f,  1f,  10f);
        using var v3    = new JPH.Vec3( 10f, -2f, -10f);
        using var triSS = new JPH.TriangleShapeSettings(v1, v2, v3, 0.01f);
        using var cs0   = new JPH.BodyCreationSettings();
        cs0.SetShapeSettings(triSS);
        cs0.mPosition.Set(0f, 0f, 0f);
        cs0.mMotionType  = JPH.EMotionType.Static;
        cs0.mObjectLayer = LayerNonMoving;
        bodies.Add(new PhysicsBody
        {
            bodyId = bi.CreateAndAddBody(cs0, JPH.EActivation.DontActivate),
            color  = new Vector3(0.5f, 0.5f, 0.5f),
            shape  = RenderShape.Box,
            scale  = new Vector3(10f, 1f, 10f),
        });

        // Dynamic box dropped onto triangle
        using var halfBox = new JPH.Vec3(0.2f, 0.2f, 0.4f);
        using var boxSS   = new JPH.BoxShapeSettings(halfBox, 0.01f);
        using var cs1     = new JPH.BodyCreationSettings();
        cs1.SetShapeSettings(boxSS);
        cs1.mPosition.Set(0f, 5f, 0f);
        cs1.mMotionType  = JPH.EMotionType.Dynamic;
        cs1.mObjectLayer = LayerMoving;
        bodies.Add(new PhysicsBody
        {
            bodyId = bi.CreateAndAddBody(cs1, JPH.EActivation.Activate),
            color  = new Vector3(0.8f, 0.4f, 0.2f),
            shape  = RenderShape.Box,
            scale  = new Vector3(0.4f, 0.4f, 0.8f),
        });
    }
}
