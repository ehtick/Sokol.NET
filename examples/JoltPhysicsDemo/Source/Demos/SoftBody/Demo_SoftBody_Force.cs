using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of SoftBodyForceTest — a cloth held at two corners with a sinusoidal force applied.
/// </summary>
public class Demo_SoftBody_Force : DemoBase
{
    public override string Name     => "SoftBody: Force";
    public override string Category  => "Soft Body";

    private JPH.BodyID _clothId;
    private float _time;

    public override void Init(JPH.BodyInterface bi, JPH.PhysicsSystem physicsSystem,
                               List<PhysicsBody> bodies, Random rng)
    {

        AddFloor(bi, bodies);

        // 30×30 cloth, 0.75f spacing, pinned along top row (z==0 → invMass=0)
        var clothFaces = new List<(uint, uint, uint)>();
        var clothSettings = CreateClothSettings(
            30, 30, 0.75f,
            // invMassFunc: pin only the two top corners (matches C++ SoftBodyForceTest)
            (x, z) => (x == 0 && z == 0) || (x == 29 && z == 0) ? 0f : 1f,
            // perturbFunc: no perturbation
            null,
            JPH.SoftBodySharedSettings.EBendType.None,
            clothFaces,
            null);

        // Rotate 90° around X so cloth hangs vertically (Quat::sRotation(AxisX, 0.5*PI))
        float qx = MathF.Sin(0.25f * MathF.PI);  // sin(PI/4) = sin(90°/2)
        float qw = MathF.Cos(0.25f * MathF.PI);  // cos(PI/4)

        _clothId = RegisterSoftBody(bi, clothSettings, clothFaces,
            0f, 15f, 0f,
            qx, 0f, 0f, qw,
            new Vector3(0.2f, 0.7f, 0.9f));
        _time = 0f;
    }

    public override void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        _time += dt;
        float forceZ = 5000f * (1f + MathF.Sin(_time * 2f));
        using var force = new JPH.Vec3(0f, 0f, forceZ);
        bi.AddForce(_clothId, force);
    }
}
