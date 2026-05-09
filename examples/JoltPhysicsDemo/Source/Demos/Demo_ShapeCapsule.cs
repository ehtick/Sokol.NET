using Sokol;
using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of CapsuleShapeTest.cpp — big upright capsule, big sideways capsule, tower of alternating capsules.
/// </summary>
public sealed class Demo_ShapeCapsule : DemoBase
{
    public override string Name     => "Capsule Shape";
    public override string Category => "Shapes";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 60,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(3f, 10f, -10f),
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

        // Big capsule: halfCylH=2.5, r=2
        var identity = Quaternion.Identity;
        var rotX90   = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.5f * MathF.PI);
        AddCapsule(bi, bodies, 2f, 2.5f,  0f, 10f,  0f, identity, JPH.EMotionType.Dynamic, LayerMoving, blue);
        AddCapsule(bi, bodies, 2f, 2.5f, 10f, 10f,  0f, rotX90,   JPH.EMotionType.Dynamic, LayerMoving, orange);

        // Tower of long capsules (halfCylH=5, r=1), alternating X/Z orientation
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
                AddCapsule(bi, bodies, 1f, 5f, px, 2f + 3f * i, pz, rot, JPH.EMotionType.Dynamic, LayerMoving, col);
            }
        }
    }
}
