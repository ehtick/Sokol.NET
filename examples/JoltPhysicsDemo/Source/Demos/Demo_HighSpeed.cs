using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Inspired by HighSpeedTest.cpp: demonstrates Continuous Collision Detection
/// (CCD) via EMotionQuality.LinearCast vs the default Discrete mode.
///
/// Three rows, each with a thin wall and two fast-moving spheres:
///   Row 1 — both Discrete:   left sphere tunnels through the wall
///   Row 2 — left LinearCast, right Discrete: CCD sphere stops at wall
///   Row 3 — both LinearCast: both spheres stop cleanly at the wall
///
/// Camera looks along -Z so the rows are visible side by side.
/// </summary>
public sealed class Demo_HighSpeed : DemoBase
{
    public override string Name     => "High Speed (CCD)";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 70,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(0, 8, 15),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 2000.0f
    };

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies);

        // Thin wall half-extents: wide but very thin in Z.
        // Wall at z=0; spheres start at z=+50 flying toward -Z.
        // Tunneling condition: step_dist > 2*(R + wallHZ)
        //   speed=500, dt≈1/128 → step≈3.9m; 2*(1.5+0.08)=3.16 < 3.9 ✓
        const float wallHX    = 18.0f;
        const float wallHY    = 8.0f;
        const float wallHZ    = 0.08f;
        const float speed     = 500.0f;
        const float sphereR   = 1.5f;

        // Three rows spaced along X
        for (int row = 0; row < 3; row++)
        {
            float cx = -30.0f + row * 30.0f;

            // Thin static wall in the middle of each row
            AddBox(bi, bodies, wallHX, wallHY, wallHZ,
                cx, wallHY, 0.0f,
                Quaternion.Identity,
                JPH.EMotionType.Static, LayerNonMoving,
                new Vector3(0.6f, 0.6f, 0.8f),
                friction: 0.0f);

            // Left sphere (coming from -Z)
            var leftQuality  = (row == 0) ? JPH.EMotionQuality.Discrete : JPH.EMotionQuality.LinearCast;
            var rightQuality = (row == 2) ? JPH.EMotionQuality.LinearCast : JPH.EMotionQuality.Discrete;

            var leftColor  = leftQuality  == JPH.EMotionQuality.LinearCast
                ? new Vector3(0.2f, 0.9f, 0.2f)   // green = CCD on
                : new Vector3(0.9f, 0.3f, 0.2f);   // red   = CCD off

            var rightColor = rightQuality == JPH.EMotionQuality.LinearCast
                ? new Vector3(0.2f, 0.9f, 0.2f)
                : new Vector3(0.9f, 0.3f, 0.2f);

            // Spheres start on the camera side (+Z) and fly toward the wall at z=0
            AddFastSphere(bi, bodies, sphereR,
                cx - 12.0f, wallHY, 50.0f,
                new Vector3(0, 0, -speed),
                leftQuality, leftColor);

            AddFastSphere(bi, bodies, sphereR,
                cx + 12.0f, wallHY, 50.0f,
                new Vector3(0, 0, -speed),
                rightQuality, rightColor);
        }
    }

    static unsafe void AddFastSphere(
        JPH.BodyInterface bi,
        List<PhysicsBody> bodies,
        float radius,
        float px, float py, float pz,
        Vector3 velocity,
        JPH.EMotionQuality quality,
        Vector3 color)
    {
        using var ss = new JPH.SphereShapeSettings(radius);
        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(px, py, pz);
        cs.mMotionType    = JPH.EMotionType.Dynamic;
        cs.mObjectLayer   = 1; // LayerMoving
        cs.mRestitution   = 1.0f;  // bounce back visibly after hitting wall
        cs.mFriction      = 0.0f;
        cs.mLinearDamping = 0.0f;
        cs.mGravityFactor = 0.0f;  // zero gravity so they travel in a straight line
        cs.mMotionQuality = quality;

        var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);

        using var vel = new JPH.Vec3(velocity.X, velocity.Y, velocity.Z);
        bi.SetLinearVelocity(id, vel);

        bodies.Add(new PhysicsBody
        {
            bodyId = id,
            color  = color,
            shape  = RenderShape.Sphere,
            scale  = new Vector3(radius * 2.0f)
        });
    }
}
