using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of WallTest.cpp: a 10-row brickwork wall of unit cubes (~455 bodies).
/// </summary>
public sealed class Demo_Wall : DemoBase
{
    public override string Name     => "Wall";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 120,
        Latitude  = 15,
        Longitude = 0,
        Center    = new Vector3(0, 15, 0),
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
        AddFloor(bi, bodies, hx: 60f, hy: 1f, hz: 10f, cy: -1f);

        for (int i = 0; i < 10; i++)
        {
            int jStart = i / 2;
            int jEnd   = 50 - (i + 1) / 2;
            for (int j = jStart; j < jEnd; j++)
            {
                float x = -50f + j * 2f + ((i & 1) != 0 ? 1f : 0f);
                float y = 1f + i * 3f;
                AddBox(bi, bodies, 1f, 1f, 1f, x, y, 0f,
                    Quaternion.Identity,
                    JPH.EMotionType.Dynamic, LayerMoving,
                    BrickColor(i));
            }
        }
    }

    static Vector3 BrickColor(int row)
    {
        // Warm brick gradient: bottom rows more red-brown, upper rows lighter
        float t = row / 9f;
        return new Vector3(0.75f + t * 0.15f, 0.35f + t * 0.15f, 0.15f + t * 0.1f);
    }
}
