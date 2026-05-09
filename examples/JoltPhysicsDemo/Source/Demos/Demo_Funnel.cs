using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Inspired by FunnelTest.cpp: four large tilted static boxes form a funnel
/// bowl, then 300 spheres of random sizes are dropped from above and collect
/// at the bottom.
/// </summary>
public sealed class Demo_Funnel : DemoBase
{
    public override string Name     => "Funnel";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 180,
        Latitude  = 40,
        Longitude = 20,
        Center    = new Vector3(0, 30, 0),
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
        // ── Funnel walls ─────────────────────────────────────────────────────
        // Four large thin boxes rotated 45° around Z and spaced 90° around Y
        // to form a square funnel.  Each panel is 100×1×100 half-extents.
        for (int i = 0; i < 4; i++)
        {
            float yAngle = MathF.PI * 0.5f * i;
            var   yRot   = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yAngle);

            // Tilt 45° around local Z, then orient around Y
            var zTilt = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.25f * MathF.PI);
            var rot   = Quaternion.Multiply(yRot, zTilt);

            // Panel centre: 25 m out from origin, at height 25, rotated around Y
            var localCentre = new Vector3(25.0f, 25.0f, 0.0f);
            // Rotate the centre around Y
            var centre = Vector3.Transform(localCentre, yRot);

            AddBox(bi, bodies, 50.0f, 1.0f, 50.0f,
                centre.X, centre.Y, centre.Z,
                rot,
                JPH.EMotionType.Static, LayerNonMoving,
                new Vector3(0.6f, 0.5f, 0.4f),
                friction: 0.3f);
        }

        // ── Falling spheres ───────────────────────────────────────────────────
        var hues = new Vector3[]
        {
            new Vector3(0.9f, 0.3f, 0.2f),
            new Vector3(0.2f, 0.7f, 0.9f),
            new Vector3(0.3f, 0.9f, 0.3f),
            new Vector3(0.9f, 0.8f, 0.2f),
            new Vector3(0.8f, 0.3f, 0.9f),
        };

        for (int i = 0; i < 300; i++)
        {
            float radius = 0.4f + (float)random.NextDouble() * 1.2f;
            float px     = (float)(random.NextDouble() * 60.0 - 30.0);
            float pz     = (float)(random.NextDouble() * 60.0 - 30.0);
            float py     = 80.0f + (float)(random.NextDouble() * 40.0);
            var   color  = hues[i % hues.Length];

            AddSphere(bi, bodies, radius, px, py, pz,
                JPH.EMotionType.Dynamic, LayerMoving,
                color,
                friction: 0.4f, restitution: 0.3f);
        }
    }
}
