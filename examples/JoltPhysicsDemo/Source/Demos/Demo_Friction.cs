using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of FrictionTest.cpp: tilted floor + 11 boxes and 11 spheres with
/// friction values from 0.0 to 1.0.  Blue = low friction, red = high friction.
/// </summary>
public sealed class Demo_Friction : DemoBase
{
    public override string Name     => "Friction";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 160,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(0, 20, -5),
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
        // Tilted floor (45° around X)  — friction 1.0 so objects slide based on their own friction
        var tiltRot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.25f * MathF.PI);
        AddBox(bi, bodies, 100f, 1f, 100f,
            0f, 0f, 0f,
            tiltRot,
            JPH.EMotionType.Static, LayerNonMoving,
            new Vector3(0.9f, 0.7f, 0.3f),
            friction: 1.0f);

        // 11 boxes with friction 0.0 → 1.0
        for (int i = 0; i <= 10; i++)
        {
            float f = 0.1f * i;
            var id = AddBox(bi, bodies, 2f, 2f, 2f,
                -50f + i * 10f, 55f, -50f,
                tiltRot,
                JPH.EMotionType.Dynamic, LayerMoving,
                FrictionColor(f),
                friction: f);
            SetBodyLabel(id, $"{f:F1}");
        }

        // 11 spheres with friction 0.0 → 1.0
        for (int i = 0; i <= 10; i++)
        {
            float f = 0.1f * i;
            var id = AddSphere(bi, bodies, 2f,
                -50f + i * 10f, 47f, -40f,
                JPH.EMotionType.Dynamic, LayerMoving,
                FrictionColor(f),
                friction: f);
            SetBodyLabel(id, $"{f:F1}");
        }
    }

    /// <summary>Blue (low) → red (high) gradient for friction visualisation.</summary>
    static Vector3 FrictionColor(float friction) =>
        Vector3.Lerp(new Vector3(0.2f, 0.4f, 0.9f), new Vector3(0.9f, 0.2f, 0.2f), friction);
}
