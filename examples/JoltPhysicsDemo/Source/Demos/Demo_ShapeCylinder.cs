using Sokol;
using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of CylinderShapeTest.cpp — big cylinder on flat/round side, tower of long cylinders, tower of thin cylinders.
/// </summary>
public sealed class Demo_ShapeCylinder : DemoBase
{
    public override string Name     => "Cylinder Shape";
    public override string Category => "Shapes";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 80,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(10f, 10f, -10f),
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

        var identity = Quaternion.Identity;
        var rotX90   = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.5f * MathF.PI);

        // Big cylinder: halfHeight=2.5, radius=2
        AddCylinder(bi, bodies, 2.5f, 2f,  0f, 10f,  0f, identity, JPH.EMotionType.Dynamic, LayerMoving, blue);
        AddCylinder(bi, bodies, 2.5f, 2f, 10f, 10f,  0f, rotX90,   JPH.EMotionType.Dynamic, LayerMoving, orange);

        // Tower of long cylinders (halfHeight=5, radius=1), alternating X/Z orientation
        for (int i = 0; i < 10; ++i)
        {
            for (int j = 0; j < 2; ++j)
            {
                float px, pz;
                Quaternion rot;
                if ((i & 1) != 0)
                {
                    px  = -4f + 8f * j;
                    pz  = -20f;
                    rot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.5f * MathF.PI);
                }
                else
                {
                    px  = 0f;
                    pz  = -20f - 4f + 8f * j;
                    rot = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.5f * MathF.PI);
                }
                var col = HsvToRgb((i * 2 + j) / 20f, 0.8f, 1.0f);
                AddCylinder(bi, bodies, 5f, 1f, px, 2f + 3f * i, pz, rot, JPH.EMotionType.Dynamic, LayerMoving, col);
            }
        }

        // Tower of thin cylinders (halfHeight=0.1, radius=5) stacked vertically
        for (int i = 0; i < 10; ++i)
        {
            var col = HsvToRgb(i / 10f, 0.6f, 1.0f);
            AddCylinder(bi, bodies, 0.1f, 5f, 20f, 10f - 1f * i, 0f, identity, JPH.EMotionType.Dynamic, LayerMoving, col);
        }
    }
}
