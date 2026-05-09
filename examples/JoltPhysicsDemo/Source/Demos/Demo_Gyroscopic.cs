using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Demonstrates the gyroscopic (Dzhanibekov / Tennis Racket) effect.
///
/// Two flat, elongated boxes float freely in space (zero gravity):
///   Left  — mApplyGyroscopicForce = false: spins uniformly without precession
///   Right — mApplyGyroscopicForce = true:  shows realistic gyroscopic precession;
///            when spun around the intermediate (Y) axis the body spontaneously
///            flips end-over-end — the Dzhanibekov / Tennis Racket Theorem.
///
/// Both boxes receive the same initial angular velocity (mostly Y-axis, small X
/// perturbation). Without gyroscopic correction the box glides smoothly; with it
/// the asymmetric inertia drives the characteristic tumbling flip.
///
/// Inspired by JoltPhysics Samples/Tests/General/GyroscopicForceTest.cpp.
/// </summary>
public sealed class Demo_Gyroscopic : DemoBase
{
    public override string Name     => "Gyroscopic Force";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 35,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(0, 12, 0),
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
        // Flat "book" shape: wide X, medium Y, very thin Z.
        // Moments of inertia:
        //   I_z = (hy²+hx²)·m/3 ≈ 4.33  ← largest  (stable spin axis)
        //   I_y = (hx²+hz²)·m/3 ≈ 3.01  ← INTERMEDIATE ← spin here for Dzhanibekov
        //   I_x = (hy²+hz²)·m/3 ≈ 1.35  ← smallest (also stable)
        // Spinning around the intermediate axis is UNSTABLE — the gyroscopic torque
        // amplifies the perturbation and causes the body to spontaneously flip.
        const float hx = 3.0f;
        const float hy = 2.0f;
        const float hz = 0.2f;

        // Spin primarily around Y (intermediate axis), small X perturbation.
        const float wx = 0.3f;
        const float wy = 8.0f;
        const float wz = 0.0f;

        var blueish = new Vector3(0.3f, 0.6f, 1.0f);
        var goldish = new Vector3(1.0f, 0.75f, 0.2f);

        // Left box — no gyroscopic force: spins cleanly, no flip
        AddSpinningBox(bi, bodies, -10f, 12f, 0f, hx, hy, hz,
            wx, wy, wz, applyGyroscopic: false, color: blueish);

        // Right box — gyroscopic force on: Dzhanibekov tumbling
        AddSpinningBox(bi, bodies, +10f, 12f, 0f, hx, hy, hz,
            wx, wy, wz, applyGyroscopic: true,  color: goldish);

        // Static floor so bodies have somewhere to land if they drift
        AddFloor(bi, bodies, hx: 80f, hy: 1f, hz: 80f);
    }

    static void AddSpinningBox(
        JPH.BodyInterface  bi,
        List<PhysicsBody>  bodies,
        float x, float y, float z,
        float hx, float hy, float hz,
        float wx, float wy, float wz,
        bool applyGyroscopic,
        Vector3 color)
    {
        using var half = new JPH.Vec3(hx, hy, hz);
        using var ss   = new JPH.BoxShapeSettings(half);
        using var cs   = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(x, y, z);
        cs.mRotation.Set(0f, 0f, 0f, 1f);
        cs.mMotionType  = JPH.EMotionType.Dynamic;
        cs.mObjectLayer = DemoBase.LayerMoving;

        cs.mGravityFactor        = 0.0f;
        cs.mLinearDamping        = 0.0f;
        cs.mAngularDamping       = 0.0f;
        cs.mAllowSleeping        = false;
        cs.mApplyGyroscopicForce = applyGyroscopic;
        cs.mAngularVelocity.Set(wx, wy, wz);

        var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody { bodyId = id, color = color, shape = RenderShape.Box,
            scale = new Vector3(hx * 2f, hy * 2f, hz * 2f) });
    }
}
