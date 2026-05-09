using Sokol;
using System;
using System.Collections.Generic;
using System.Numerics;

public sealed class Demo_ShapeEmpty : DemoBase
{
    public override string Name     => "Empty Shape";
    public override string Category => "Shapes";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 30,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(0f, 8f, 0f),
    };

    public override unsafe void Init(
        JPH.BodyInterface bi, JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies, Random random)
    {
        AddFloor(bi, bodies);

        // One dynamic empty shape at (0, 10, 0) — no collision
        using var com     = new JPH.Vec3(0f, 0f, 0f);
        using var emptySS = new JPH.EmptyShapeSettings(com);
        using var cs      = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(emptySS);
        cs.mPosition.Set(0f, 10f, 0f);
        cs.mMotionType  = JPH.EMotionType.Dynamic;
        cs.mObjectLayer = LayerMoving;
        bodies.Add(new PhysicsBody
        {
            bodyId = bi.CreateAndAddBody(cs, JPH.EActivation.Activate),
            color  = new Vector3(0.8f, 0.8f, 0.2f),
            shape  = RenderShape.Box,
            scale  = new Vector3(0.5f, 0.5f, 0.5f),
        });
    }
}
