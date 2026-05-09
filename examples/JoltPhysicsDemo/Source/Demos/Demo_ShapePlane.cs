using Sokol;
using System;
using System.Collections.Generic;
using System.Numerics;

public sealed class Demo_ShapePlane : DemoBase
{
    public override string Name     => "Plane Shape";
    public override string Category => "Shapes";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 40,
        Latitude  = 25,
        Longitude = 0,
        Center    = new Vector3(0f, 5f, 0f),
    };

    public override unsafe void Init(
        JPH.BodyInterface bi, JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies, Random random)
    {
        // Infinite plane: normal = Y-up, constant = 0
        using var normal   = new JPH.Vec3(0, 1, 0);
        using var plane    = new JPH.Plane(normal, 0.0f);
        using var planeSS  = new JPH.PlaneShapeSettings(plane);
        using var cs0      = new JPH.BodyCreationSettings();
        cs0.SetShapeSettings(planeSS);
        cs0.mPosition.Set(0, 0, 0);
        cs0.mMotionType  = JPH.EMotionType.Static;
        cs0.mObjectLayer = LayerNonMoving;
        bodies.Add(new PhysicsBody
        {
            bodyId = bi.CreateAndAddBody(cs0, JPH.EActivation.DontActivate),
            color  = new Vector3(0.5f, 0.5f, 0.5f),
            shape  = RenderShape.Floor,
            scale  = new Vector3(50, 1, 50),
        });

        // Spheres
        for (int i = 0; i < 4; i++)
        {
            float x = (i - 1.5f) * 5f;
            AddSphere(bi, bodies, 0.5f, x, 8 + i, 0,
                      JPH.EMotionType.Dynamic, LayerMoving,
                      new Vector3(0.3f, 0.7f, 0.9f));
        }

        // Boxes
        for (int i = 0; i < 4; i++)
        {
            float x = (i - 1.5f) * 5f;
            AddBox(bi, bodies, 0.7f, 0.7f, 0.7f, x, 12 + i, 3,
                   Quaternion.Identity,
                   JPH.EMotionType.Dynamic, LayerMoving,
                   new Vector3(0.9f, 0.6f, 0.2f));
        }
    }
}
