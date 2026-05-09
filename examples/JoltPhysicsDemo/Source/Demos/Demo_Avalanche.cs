using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Avalanche: a wide tilted ramp with hundreds of spheres packed at the top.
/// They all roll and tumble down the slope in a wave.
/// Blue → red gradient shows depth in the mass of spheres.
/// </summary>
public sealed class Demo_Avalanche : DemoBase
{
    public override string Name     => "Avalanche";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 130,
        Latitude  = 20,
        Longitude = 90,                    // side view from -X, looking along the ramp length
        Center    = new Vector3(0, 12, 20),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 2000.0f
    };

    public override void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        // Flat run-out floor at the bottom
        AddFloor(bi, bodies, hx: 60f, hy: 1f, hz: 60f, cx: 0f, cy: -1f, cz: -20f);

        // Tilted ramp (~20° pitch along Z axis)
        float angle = 20f * MathF.PI / 180f;
        var rampRot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -angle);
        AddBox(bi, bodies,
            40f, 1f, 50f,
            0f, 10f, 20f,
            rampRot,
            JPH.EMotionType.Static, LayerNonMoving,
            new Vector3(0.85f, 0.65f, 0.3f),
            friction: 0.3f);

        // Side walls to keep spheres on the ramp
        var wallRot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -angle);
        AddBox(bi, bodies,  1f, 5f, 50f, -40f, 14f, 20f, wallRot, JPH.EMotionType.Static, LayerNonMoving, new Vector3(0.7f, 0.5f, 0.2f));
        AddBox(bi, bodies,  1f, 5f, 50f,  40f, 14f, 20f, wallRot, JPH.EMotionType.Static, LayerNonMoving, new Vector3(0.7f, 0.5f, 0.2f));

        // Dense grid of spheres at the top of the ramp
        const float R       = 1.5f;
        const int   Cols    = 20;
        const int   Rows    = 10;

        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                // Center the grid within the ramp's x-extent (±40)
                float x     = -(Cols - 1) * R * 1.1f + col * (R * 2.2f);
                float baseZ =  45f - row * (R * 2.2f);
                // Offset every other column slightly for tighter packing
                float offsetZ = (col % 2 == 0) ? 0f : R * 1.1f;
                float actualZ = baseZ + offsetZ;
                // Correct ramp surface height: 10 + sec(angle) + (wz - 20) * tan(angle)
                float rampY = 10f + 1f / MathF.Cos(angle) + (actualZ - 20f) * MathF.Tan(angle) + R + row * 0.5f;

                float t     = (float)row / Rows;
                var   color = Vector3.Lerp(new Vector3(0.2f, 0.5f, 0.95f), new Vector3(0.95f, 0.25f, 0.2f), t);

                AddSphere(bi, bodies, R,
                    x, rampY, actualZ,
                    JPH.EMotionType.Dynamic, LayerMoving,
                    color,
                    friction: 0.3f,
                    restitution: 0.2f,
                    linearDamping: 0.05f);
            }
        }
    }
}
