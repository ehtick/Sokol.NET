using Sokol;
using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of BoxShapeTest.cpp — three differently-sized dynamic boxes dropped onto a floor.
/// </summary>
public sealed class Demo_ShapeBox : DemoBase
{
    public override string Name     => "Box Shape";
    public override string Category => "Shapes";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 60,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(0f, 10f, 10f),
    };

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies);

        var identity = Quaternion.Identity;
        var blue     = new Vector3(0.3f, 0.6f, 1.0f);
        var orange   = new Vector3(1.0f, 0.6f, 0.2f);
        var green    = new Vector3(0.2f, 0.9f, 0.3f);

        // (20, 1, 1) box
        AddBox(bi, bodies, 20f, 1f, 1f, 0f, 10f, 0f, identity, JPH.EMotionType.Dynamic, LayerMoving, blue);

        // (2, 3, 4) box rotated 45° around Z
        var rotZ45 = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.25f * MathF.PI);
        AddBox(bi, bodies, 2f, 3f, 4f, 0f, 10f, 10f, rotZ45, JPH.EMotionType.Dynamic, LayerMoving, orange);

        // (0.5, 0.75, 1) box rotated 45° around both X and Z
        var rotXZ45 = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.25f * MathF.PI)
                    * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.25f * MathF.PI);
        AddBox(bi, bodies, 0.5f, 0.75f, 1f, 0f, 10f, 20f, rotXZ45, JPH.EMotionType.Dynamic, LayerMoving, green);
    }
}
