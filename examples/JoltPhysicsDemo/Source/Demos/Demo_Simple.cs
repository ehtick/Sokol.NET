using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of SimpleTest.cpp: floor + two boxes (one tilted) + one large sphere.
/// </summary>
public sealed class Demo_Simple : DemoBase
{
    public override string Name     => "Simple";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 35,
        Latitude  = 20,
        Longitude = 30,
        Center    = new Vector3(5, 8, 0),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000.0f
    };

    public override void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies);

        // Box 1 — upright
        AddBox(bi, bodies, 0.5f, 1f, 2f, 0f, 10f, 0f,
            Quaternion.Identity,
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.8f, 0.4f, 0.2f));

        // Box 2 — rotated 45° around X
        var rotX45 = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.25f * MathF.PI);
        AddBox(bi, bodies, 0.5f, 1f, 2f, 5f, 10f, 0f,
            rotX45,
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.2f, 0.6f, 0.9f));

        // Large sphere
        AddSphere(bi, bodies, 2f, 10f, 10f, 0f,
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.3f, 0.9f, 0.4f));
    }
}
