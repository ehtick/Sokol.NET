using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of GravityFactorTest.cpp: 11 boxes dropped from height with gravity
/// factors ranging from 0.0 (floats in place) to 1.0 (normal gravity).
/// Shows how per-body gravity scaling works.
/// Blue = low gravity factor, red = full gravity.
/// </summary>
public sealed class Demo_GravityFactor : DemoBase
{
    public override string Name     => "Gravity Factor";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 140,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(0, 15, 0),
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

        for (int i = 0; i <= 10; i++)
        {
            float g = 0.1f * i;
            var color = Vector3.Lerp(new Vector3(0.2f, 0.5f, 1.0f), new Vector3(1.0f, 0.3f, 0.1f), g);

            using var half = new JPH.Vec3(2.0f, 2.0f, 2.0f);
            using var ss   = new JPH.BoxShapeSettings(half);
            using var cs   = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(ss);
            cs.mPosition.Set(-50.0f + i * 10.0f, 25.0f, 0.0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            cs.mGravityFactor = g;

            var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody
            {
                bodyId    = id,
                color     = color,
                shape     = RenderShape.Box,
                scale     = new Vector3(4.0f)
            });
            SetBodyLabel(id, $"{g:F1}");
        }
    }
}
