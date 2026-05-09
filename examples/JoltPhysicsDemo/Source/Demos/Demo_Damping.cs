using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Demonstrates linear and angular damping:
///   Row 1 (z=0):  11 spheres with linear  damping 0.0→1.0, kicked forward (+Z)
///   Row 2 (z=-20): 11 spheres with angular damping 0.0→1.0, given a spin
/// Blue = no damping (travels/spins longest), red = full damping (stops fastest).
/// Mirrors JoltPhysics Samples/Tests/General/DampingTest.cpp.
/// </summary>
public sealed class Demo_Damping : DemoBase
{
    public override string Name     => "Damping";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 90,
        Latitude  = 25,
        Longitude = 90,
        Center    = new Vector3(0, 2, 15),
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
        AddFloor(bi, bodies, hx: 110f, hy: 1f, hz: 120f);

        const float R       = 2f;
        const float Spacing = 10f;

        for (int i = 0; i <= 10; i++)
        {
            float t     = i / 10f;
            float x     = -50f + i * Spacing;
            var   color = LerpColor(new Vector3(0.2f, 0.4f, 1.0f),
                                    new Vector3(1.0f, 0.2f, 0.2f), t);

            // Row 1 — linear damping: sphere is kicked in +Z, stops at varying distances
            var id1 = AddSphere(bi, bodies, R, x, R, 0f,
                JPH.EMotionType.Dynamic, LayerMoving, color,
                friction: 0.3f, restitution: 0.0f, linearDamping: t);
            using (var vel = new JPH.Vec3(0f, 0f, 10f))
                bi.SetLinearVelocity(id1, vel);
            SetBodyLabel(id1, $"Linear\n{t:F1}");

            // Row 2 — angular damping: sphere spins in place, rate decays at varying rates
            var id2 = AddSphereWithAngularDamping(bi, bodies, R, x, R, -20f, color,
                angularDamping: t);
            SetBodyLabel(id2, $"Angular\n{t:F1}");
        }
    }

    // Inline helper: creates a sphere with specific angular damping + initial spin
    static JPH.BodyID AddSphereWithAngularDamping(
        JPH.BodyInterface bi, List<PhysicsBody> bodies,
        float radius, float px, float py, float pz,
        Vector3 color, float angularDamping)
    {
        using var ss = new JPH.SphereShapeSettings(radius);
        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(px, py, pz);
        cs.mMotionType  = JPH.EMotionType.Dynamic;
        cs.mObjectLayer = LayerMoving;
        cs.mFriction    = 0.3f;
        cs.mLinearDamping  = 0f;
        cs.mAngularDamping = angularDamping;
        cs.mAngularVelocity.Set(0f, 10f, 0f);

        var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId = id,
            color  = color,
            shape  = RenderShape.Sphere,
            scale  = new Vector3(radius * 2f)
        });
        return id;
    }

    static Vector3 LerpColor(Vector3 a, Vector3 b, float t) =>
        new Vector3(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t);
}
