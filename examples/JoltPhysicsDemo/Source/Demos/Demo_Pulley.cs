using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of PulleyConstraintTest.cpp.
/// Three pulley pairs demonstrate different rope ratios:
///   Pair A (ratio 1:1)  — left:  heavy box pulls light box up at equal speed.
///   Pair B (ratio 2:1)  — centre: heavy box falls half as fast as light rises.
///   Pair C (ratio 1:2)  — right:  heavy box falls twice as fast as light rises.
/// Fixed pulleys are at y=20.  Both boxes start at y=3.
/// </summary>
public class Demo_Pulley : DemoBase
{
    public override string Name     => "Pulley";
    public override string Category => "Constraints";

    private readonly List<JPH.TwoBodyConstraint?> _constraints = new();

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies, 80f, 0.5f, 20f);

        const float PulleyY  = 20f;
        const float StartY   = 3f;
        const float BoxHE    = 1.5f;

        // (offsetX, ratio, heavyColor, lightColor)
        var pairs = new (float ox, float ratio, Vector3 hc, Vector3 lc)[]
        {
            (-24f, 1.0f,
                new Vector3(0.85f, 0.25f, 0.20f), new Vector3(0.25f, 0.65f, 0.90f)),
            (0f, 2.0f,
                new Vector3(0.90f, 0.55f, 0.10f), new Vector3(0.25f, 0.80f, 0.30f)),
            (24f, 0.5f,
                new Vector3(0.70f, 0.25f, 0.85f), new Vector3(0.90f, 0.80f, 0.20f)),
        };

        foreach (var (ox, ratio, heavyColor, lightColor) in pairs)
        {
            float leftX  = ox - 5f;
            float rightX = ox + 5f;

            // Heavy box (left)
            using var heavySS = new JPH.BoxShapeSettings(new JPH.Vec3(BoxHE, BoxHE, BoxHE));
            heavySS.SetDensity(3000f);
            using var heavyCS = new JPH.BodyCreationSettings();
            heavyCS.SetShapeSettings(heavySS);
            heavyCS.mPosition.Set(leftX, StartY, 0f);
            heavyCS.mMotionType  = JPH.EMotionType.Dynamic;
            heavyCS.mObjectLayer = LayerMoving;
            var heavy = bi.CreateBody(heavyCS)!;
            bi.AddBody(heavy.GetID(), JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody
            {
                bodyId = heavy.GetID(),
                color  = heavyColor,
                shape  = RenderShape.Box,
                scale  = new Vector3(BoxHE * 2f)
            });

            // Light box (right)
            using var lightSS = new JPH.BoxShapeSettings(new JPH.Vec3(BoxHE, BoxHE, BoxHE));
            lightSS.SetDensity(500f);
            using var lightCS = new JPH.BodyCreationSettings();
            lightCS.SetShapeSettings(lightSS);
            lightCS.mPosition.Set(rightX, StartY, 0f);
            lightCS.mMotionType  = JPH.EMotionType.Dynamic;
            lightCS.mObjectLayer = LayerMoving;
            var light = bi.CreateBody(lightCS)!;
            bi.AddBody(light.GetID(), JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody
            {
                bodyId = light.GetID(),
                color  = lightColor,
                shape  = RenderShape.Box,
                scale  = new Vector3(BoxHE * 2f)
            });

            // Pulley constraint
            using var ps = new JPH.PulleyConstraintSettings();
            ps.mBodyPoint1.Set(leftX,  StartY + BoxHE, 0f);  // top of heavy box
            ps.mBodyPoint2.Set(rightX, StartY + BoxHE, 0f);  // top of light box
            ps.mFixedPoint1.Set(leftX,  PulleyY, 0f);
            ps.mFixedPoint2.Set(rightX, PulleyY, 0f);
            ps.mRatio     = ratio;
            ps.mMinLength = 0f;
            ps.mMaxLength = -1f;  // auto-calculate from initial positions

            var c = ps.Create(heavy, light);
            _constraints.Add(c);
            sys.AddConstraint(c);
        }
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        foreach (var c in _constraints)
            if (c != null) sys.RemoveConstraint(c);
        _constraints.Clear();
    }

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 80,
        Latitude  = 15,
        Longitude = 0,
        Center    = new Vector3(0f, 10f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000f
    };
}
