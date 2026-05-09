using Sokol;
using System;
using System.Collections.Generic;
using System.Numerics;

public sealed class Demo_ShapeRotatedTranslated : DemoBase
{
    public override string Name     => "Rotated Translated Shape";
    public override string Category => "Shapes";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 20,
        Latitude  = 10,
        Longitude = 45,
        Center    = new Vector3(0f, 3f, 0f),
    };

    public override unsafe void Init(
        JPH.BodyInterface bi, JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies, Random random)
    {
        AddFloor(bi, bodies);

        // Build a cone centered on origin with point upwards:
        // apex at (0, 2.5, 0), 10 base points at y=-2.5 radius=1
        var pts = new JPH.Vec3f[11];
        pts[0] = new JPH.Vec3f(0f, 2.5f, 0f);
        for (int k = 0; k < 10; k++)
        {
            float a = k * (2f * MathF.PI / 10f);
            pts[1 + k] = new JPH.Vec3f(MathF.Sin(a), -2.5f, MathF.Cos(a));
        }
        using var convexHull = JPH.ConvexHullShapeSettingsFromPoints(pts);

        // Offset so the pivot is at the tip, then rotate 180° around X to flip upside down
        using var offsetVec = new JPH.Vec3(0f, 2.5f, 0f);
        using var axisX     = new JPH.Vec3(1f, 0f, 0f);
        using var rotFlip   = JPH.Quat.SRotation(axisX, MathF.PI);
        using var rotTrans  = new JPH.RotatedTranslatedShapeSettings(offsetVec, rotFlip, convexHull);

        // Place at origin so the point touches the floor
        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(rotTrans);
        cs.mPosition.Set(0f, 0f, 0f);
        cs.mMotionType  = JPH.EMotionType.Dynamic;
        cs.mObjectLayer = LayerMoving;
        bodies.Add(new PhysicsBody
        {
            bodyId      = bi.CreateAndAddBody(cs, JPH.EActivation.Activate),
            color       = HsvToRgb(0.1f, 0.7f, 1.0f),
            shape       = RenderShape.Box,
            scale       = new Vector3(2f, 5f, 2f),
            // The cone apex is at shape-local (0,-1.25,0) and base at (0,3.75,0);
            // midpoint at (0,1.25,0) — shift so the box covers the actual geometry.
            localOffset = new Vector3(0f, 1.25f, 0f),
        });
    }
}
