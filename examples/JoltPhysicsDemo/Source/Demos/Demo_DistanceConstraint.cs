using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of Samples/Tests/Constraints/DistanceConstraintTest.cpp.
///
/// Two horizontal chains of 15 boxes hanging from a static anchor via DistanceConstraints.
///   Chain 0 (z=0):  fixed distance — bodies hang exactly 5 m apart.
///   Chain 1 (z=10): min/max range (4–8 m) — slack allows bodies to oscillate.
/// </summary>
public sealed class Demo_DistanceConstraint : DemoBase
{
    public override string Name     => "Distance Constraint";
    public override string Category => "Constraints";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 200,
        Latitude  = 15,
        Longitude = 0,
        Center    = new Vector3(70f, 65f, 5f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 2000f,
    };

    private readonly List<JPH.TwoBodyConstraint?> _constraints = new();

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random random)
    {
        AddFloor(bi, bodies);

        const float cHalfLen     = 2.5f;  // half-extent X of each body
        const float cGap         = 5.0f;  // surface-to-surface gap (= constraint distance)
        const float cTopY        = 75f;
        const int   cChainLength = 15;

        for (int variation = 0; variation < 2; ++variation)
        {
            float z = variation * 10f;

            // Static anchor
            using var topSS = new JPH.BoxShapeSettings(new JPH.Vec3(cHalfLen, 1f, 1f));
            using var topCS = new JPH.BodyCreationSettings();
            topCS.SetShapeSettings(topSS);
            topCS.mPosition.Set(0f, cTopY, z);
            topCS.mMotionType  = JPH.EMotionType.Static;
            topCS.mObjectLayer = LayerNonMoving;
            var top = bi.CreateBody(topCS)!;
            bi.AddBody(top.GetID(), JPH.EActivation.DontActivate);
            bodies.Add(new PhysicsBody
            {
                bodyId = top.GetID(),
                color  = new Vector3(0.5f, 0.5f, 0.5f),
                shape  = RenderShape.Box,
                scale  = new Vector3(cHalfLen * 2f, 2f, 2f),
            });

            JPH.Body? prev = top;
            var position = new Vector3(0f, cTopY, z);

            for (int i = 1; i < cChainLength; ++i)
            {
                position.X += cGap + 2f * cHalfLen;  // step = 10 m

                using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(cHalfLen, 1f, 1f));
                using var cs = new JPH.BodyCreationSettings();
                cs.SetShapeSettings(ss);
                cs.mPosition.Set(position.X, position.Y, position.Z);
                cs.mMotionType  = JPH.EMotionType.Dynamic;
                cs.mObjectLayer = LayerMoving;
                var seg = bi.CreateBody(cs)!;
                bi.AddBody(seg.GetID(), JPH.EActivation.Activate);
                bodies.Add(new PhysicsBody
                {
                    bodyId = seg.GetID(),
                    color  = variation == 0
                        ? new Vector3(0.3f, 0.6f, 0.9f)
                        : new Vector3(0.9f, 0.6f, 0.2f),
                    shape  = RenderShape.Box,
                    scale  = new Vector3(cHalfLen * 2f, 2f, 2f),
                });

                using var ds = new JPH.DistanceConstraintSettings();
                // Point1: right edge of previous body; Point2: left edge of current body.
                // Distance between them = cGap = 5 m.
                ds.mPoint1.Set(position.X - (cGap + cHalfLen), position.Y, position.Z);
                ds.mPoint2.Set(position.X - cHalfLen,          position.Y, position.Z);

                if (variation == 1)
                {
                    ds.mMinDistance = 4f;
                    ds.mMaxDistance = 8f;
                }

                var c = ds.Create(prev, seg);
                _constraints.Add(c);
                sys.AddConstraint(c);

                prev = seg;
            }
        }
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        foreach (var c in _constraints)
            if (c != null) sys.RemoveConstraint(c);
        _constraints.Clear();
    }
}
