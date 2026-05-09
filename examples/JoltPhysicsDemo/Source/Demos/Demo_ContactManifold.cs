using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of ContactManifoldTest.cpp:
/// A grid of large static boxes topped with dynamic capsules and long boxes,
/// showing contact manifold behaviour.
/// </summary>
public sealed class Demo_ContactManifold : DemoBase
{
    public override string Name     => "Contact Manifold";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 120,
        Latitude  = 25,
        Longitude = 0,
        Center    = new Vector3(0f, 8f, 0f),
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

        // 3×2 grid of large static boxes (half-extents 4,4,4)
        // Dynamic bodies placed on top: row j=0 → capsule, row j=1 → long box
        for (int i = 0; i < 3; ++i)
        {
            for (int j = 0; j < 2; ++j)
            {
                float bx = -20f + i * 10f;
                float bz = -20f + j * 40f;

                // Static pedestal
                AddBox(bi, bodies, 4f, 4f, 4f,
                    bx, 4f, bz,
                    Quaternion.Identity,
                    JPH.EMotionType.Static, LayerNonMoving,
                    new Vector3(0.6f, 0.6f, 0.6f));

                // Dynamic body on top
                float dynX = bx;
                float dynZ = -5f + i * 5f + bz;

                var tilt = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.1f * MathF.PI)
                         * Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.5f * MathF.PI);

                if (j == 0)
                {
                    // Capsule: halfCylH=5, radius=2
                    AddCapsule(bi, bodies, 2f, 5f,
                        dynX, 12f, dynZ,
                        tilt,
                        JPH.EMotionType.Dynamic, LayerMoving,
                        new Vector3(0.3f, 0.6f, 0.9f));
                }
                else
                {
                    // Long box: half-extents (2,7,2)
                    AddBox(bi, bodies, 2f, 7f, 2f,
                        dynX, 12f, dynZ,
                        tilt,
                        JPH.EMotionType.Dynamic, LayerMoving,
                        new Vector3(0.9f, 0.5f, 0.2f));
                }
            }
        }
    }
}
