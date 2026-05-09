using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Crater impact: a dense grid of small boxes covers the floor.
/// A very heavy sphere falls from high above and blasts them all outward.
/// </summary>
public sealed class Demo_Crater : DemoBase
{
    public override string Name     => "Crater";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 90,
        Latitude  = 30,
        Longitude = 30,
        Center    = new Vector3(0, 5, 0),
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
        AddFloor(bi, bodies, hx: 60f, hy: 1f, hz: 60f, cy: -1f);

        // Dense grid of small boxes on the floor — 30×30 grid
        const int   N    = 30;
        const float Half = 0.7f;
        const float Gap  = Half * 2.2f;

        for (int row = 0; row < N; row++)
        for (int col = 0; col < N; col++)
        {
            float x    = -N * Gap * 0.5f + col * Gap + Half;
            float z    = -N * Gap * 0.5f + row * Gap + Half;
            float dist = MathF.Sqrt(x * x + z * z);

            // Colour: warm near centre, cool at edges
            float t     = MathF.Min(1f, dist / (N * Gap * 0.4f));
            var   color = Vector3.Lerp(new Vector3(0.95f, 0.7f, 0.1f), new Vector3(0.2f, 0.55f, 0.95f), t);

            AddBox(bi, bodies,
                Half, Half, Half,
                x, Half, z,
                Quaternion.Identity,
                JPH.EMotionType.Dynamic, LayerMoving,
                color,
                friction: 0.05f,
                restitution: 0.5f);
        }

        // The wrecking ball — large, no air resistance, fired downward at high speed
        var ballId = AddSphere(bi, bodies, 4f,
            0f, 30f, 0f,
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.9f, 0.15f, 0.15f),
            friction: 0.2f,
            restitution: 0.4f,
            linearDamping: 0.0f);

        // Fire it down hard so it blasts through the box layer
        using var vel = new JPH.Vec3(0f, -60f, 0f);
        bi.SetLinearVelocity(ballId, vel);
    }
}
