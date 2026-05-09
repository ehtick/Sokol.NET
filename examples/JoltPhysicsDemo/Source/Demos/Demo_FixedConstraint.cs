using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of Samples/Tests/Constraints/FixedConstraintTest.cpp.
///
/// Two chains of 10 boxes rigidly glued together via FixedConstraints (mAutoDetectPoint=true).
///   Chain 0 (z=0):   perfectly aligned along the X axis.
///   Chain 1 (z=-20): random offsets and orientations — a wobbly rigid chain.
/// Bodies in each chain share collision layers (LayerMoving) but the rigid links keep them stable.
/// </summary>
public sealed class Demo_FixedConstraint : DemoBase
{
    public override string Name     => "Fixed Constraint";
    public override string Category => "Constraints";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 110,
        Latitude  = 25,
        Longitude = 30,
        Center    = new Vector3(18f, 18f, -10f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000f,
    };

    private readonly List<JPH.TwoBodyConstraint?> _constraints = new();

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random random)
    {
        AddFloor(bi, bodies);

        const float cBoxSize     = 4f;
        const float cHalf        = cBoxSize * 0.5f;
        const int   cChainLength = 10;

        var chainColors = new[]
        {
            new Vector3(0.8f, 0.3f, 0.2f),
            new Vector3(0.2f, 0.6f, 0.85f),
        };

        for (int variation = 0; variation < 2; ++variation)
        {
            float startZ = -variation * 20f;

            // Static anchor
            using var topSS = new JPH.BoxShapeSettings(new JPH.Vec3(cHalf, cHalf, cHalf));
            using var topCS = new JPH.BodyCreationSettings();
            topCS.SetShapeSettings(topSS);
            topCS.mPosition.Set(0f, 25f, startZ);
            topCS.mMotionType  = JPH.EMotionType.Static;
            topCS.mObjectLayer = LayerNonMoving;
            var top = bi.CreateBody(topCS)!;
            bi.AddBody(top.GetID(), JPH.EActivation.DontActivate);
            bodies.Add(new PhysicsBody
            {
                bodyId = top.GetID(),
                color  = new Vector3(0.5f, 0.5f, 0.5f),
                shape  = RenderShape.Box,
                scale  = new Vector3(cBoxSize),
            });

            JPH.Body? prev = top;
            var pos = new Vector3(0f, 25f, startZ);

            for (int i = 1; i < cChainLength; ++i)
            {
                Quaternion rot;
                Vector3 nextPos;

                if (variation == 0)
                {
                    nextPos = pos + new Vector3(cBoxSize, 0f, 0f);
                    rot     = Quaternion.Identity;
                }
                else
                {
                    float dx = cBoxSize + MathF.Abs((float)(random.NextDouble() * 2.0 - 1.0));
                    float dy = (float)(random.NextDouble() * 2.0 - 1.0);
                    float dz = (float)(random.NextDouble() * 2.0 - 1.0);
                    nextPos = pos + new Vector3(dx, dy, dz);
                    rot = RandomQuaternion(random);
                }

                using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(cHalf, cHalf, cHalf));
                using var cs = new JPH.BodyCreationSettings();
                cs.SetShapeSettings(ss);
                cs.mPosition.Set(nextPos.X, nextPos.Y, nextPos.Z);
                cs.mRotation.Set(rot.X, rot.Y, rot.Z, rot.W);
                cs.mMotionType  = JPH.EMotionType.Dynamic;
                cs.mObjectLayer = LayerMoving;
                var seg = bi.CreateBody(cs)!;
                bi.AddBody(seg.GetID(), JPH.EActivation.Activate);
                bodies.Add(new PhysicsBody
                {
                    bodyId = seg.GetID(),
                    color  = chainColors[variation],
                    shape  = RenderShape.Box,
                    scale  = new Vector3(cBoxSize),
                });

                using var fs = new JPH.FixedConstraintSettings();
                fs.mAutoDetectPoint = true;
                var c = fs.Create(prev, seg);
                _constraints.Add(c);
                sys.AddConstraint(c);

                prev = seg;
                pos  = nextPos;
            }
        }
    }

    // Uniform random quaternion via Shoemake's method.
    private static Quaternion RandomQuaternion(Random rng)
    {
        float u1 = (float)rng.NextDouble();
        float u2 = (float)rng.NextDouble();
        float u3 = (float)rng.NextDouble();
        float s1 = MathF.Sqrt(1f - u1);
        float s2 = MathF.Sqrt(u1);
        return new Quaternion(
            s1 * MathF.Sin(2f * MathF.PI * u2),
            s1 * MathF.Cos(2f * MathF.PI * u2),
            s2 * MathF.Sin(2f * MathF.PI * u3),
            s2 * MathF.Cos(2f * MathF.PI * u3));
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        foreach (var c in _constraints)
            if (c != null) sys.RemoveConstraint(c);
        _constraints.Clear();
    }
}
