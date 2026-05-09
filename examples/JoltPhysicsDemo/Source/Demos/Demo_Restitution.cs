using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of RestitutionTest.cpp: flat floor + 11 spheres and 11 boxes dropped
/// from height 20 with restitution from 0.0 to 1.0 (zero linear damping).
/// Grey = low restitution, bright = high restitution.
/// </summary>
public sealed class Demo_Restitution : DemoBase
{
    public override string Name     => "Restitution";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 130,
        Latitude  = 15,
        Longitude = 0,
        Center    = new Vector3(0, 10, 0),
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
        AddFloor(bi, bodies, hx: 60f, hy: 1f, hz: 30f, cy: -1f);

        // 11 spheres, restitution 0.0 → 1.0, row at z = -20
        for (int i = 0; i <= 10; i++)
        {
            float r = 0.1f * i;
            var id = AddSphere(bi, bodies, 2f,
                -50f + i * 10f, 20f, -20f,
                JPH.EMotionType.Dynamic, LayerMoving,
                RestitutionColor(r),
                friction: 0.5f,
                restitution: r,
                linearDamping: 0f);
            SetBodyLabel(id, $"{r:F1}");
        }

        // 11 boxes, restitution 0.0 → 1.0, row at z = +20
        for (int i = 0; i <= 10; i++)
        {
            float r = 0.1f * i;
            var id = AddBox(bi, bodies, 2f, 2f, 2f,
                -50f + i * 10f, 20f, 20f,
                Quaternion.Identity,
                JPH.EMotionType.Dynamic, LayerMoving,
                RestitutionColor(r),
                friction: 0.5f,
                restitution: r);
            SetBodyLabel(id, $"{r:F1}");
        }
    }

    /// <summary>Dark grey (no bounce) → bright yellow (max bounce).</summary>
    static Vector3 RestitutionColor(float restitution) =>
        Vector3.Lerp(new Vector3(0.35f, 0.35f, 0.35f), new Vector3(0.95f, 0.85f, 0.1f), restitution);
}
