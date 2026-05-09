using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of StackTest.cpp: 10 boxes stacked vertically with alternating 90° Y-rotation.
/// </summary>
public sealed class Demo_Stack : DemoBase
{
    public override string Name     => "Stack";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 30,
        Latitude  = 15,
        Longitude = 20,
        Center    = new Vector3(10, 12, 0),
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

        var rotY90 = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f * MathF.PI);

        for (int i = 0; i < 10; i++)
        {
            var rot = (i & 1) != 0 ? rotY90 : Quaternion.Identity;
            AddBox(bi, bodies, 0.5f, 1f, 2f,
                10f, 1.0f + i * 2.1f, 0f,
                rot,
                JPH.EMotionType.Dynamic, LayerMoving,
                new Vector3(0.3f + i * 0.07f, 0.5f, 0.9f - i * 0.07f));
        }
    }
}
