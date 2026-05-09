using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Kinematic bodies: two spheres that barrel back and forth through a
/// brickwork wall of dynamic boxes, demonstrating that kinematic bodies
/// push dynamic bodies without being stopped themselves.
/// Mirrors JoltPhysics Samples/Tests/General/KinematicTest.cpp.
/// </summary>
public sealed class Demo_Kinematic : DemoBase
{
    public override string Name     => "Kinematic";
    public override string Category => "General";

    JPH.BodyID _sphere0;
    JPH.BodyID _sphere1;

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 50,
        Latitude  = 25,
        Longitude = 0,
        Center    = new Vector3(0, 3, 0),
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

        // Brickwork wall at z=0 (3 rows, staggered)
        var wallColor = new Vector3(0.8f, 0.75f, 0.55f);
        for (int row = 0; row < 3; row++)
        {
            int start = row / 2;
            int end   = 10 - (row + 1) / 2;
            for (int col = start; col < end; col++)
            {
                float x = -10f + col * 2f + ((row & 1) == 1 ? 1f : 0f);
                float y = 1f + row * 2f;
                AddBox(bi, bodies, 1f, 1f, 1f, x, y, 0f,
                    Quaternion.Identity,
                    JPH.EMotionType.Dynamic, LayerMoving,
                    wallColor);
            }
        }

        // Two kinematic spheres that bounce between z=+5 and z=-5
        var sphereColor = new Vector3(0.3f, 0.6f, 1.0f);

        _sphere0 = AddSphere(bi, bodies, 1f, -10f, 2f, 5f,
            JPH.EMotionType.Kinematic, LayerMoving, sphereColor,
            friction: 0f, linearDamping: 0f);
        using (var vel = new JPH.Vec3(2f, 0f, -10f))
            bi.SetLinearVelocity(_sphere0, vel);

        _sphere1 = AddSphere(bi, bodies, 1f, -10f, 2f, -5f,
            JPH.EMotionType.Kinematic, LayerMoving, sphereColor,
            friction: 0f, linearDamping: 0f);
        using (var vel = new JPH.Vec3(2f, 0f, 10f))
            bi.SetLinearVelocity(_sphere1, vel);
    }

    public override void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        BounceAtZ(bi, _sphere0);
        BounceAtZ(bi, _sphere1);
    }

    // Reverse Z-velocity when sphere reaches z=±5; keep X-advance
    static void BounceAtZ(JPH.BodyInterface bi, JPH.BodyID id)
    {
        using var pos = bi.GetCenterOfMassPosition(id);
        float z = pos.GetZ();
        if (z >= 5f)
        {
            using var vel = new JPH.Vec3(2f, 0f, -10f);
            bi.SetLinearVelocity(id, vel);
        }
        else if (z <= -5f)
        {
            using var vel = new JPH.Vec3(2f, 0f, 10f);
            bi.SetLinearVelocity(id, vel);
        }
    }
}
