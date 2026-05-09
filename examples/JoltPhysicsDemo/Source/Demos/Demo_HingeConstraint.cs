using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Direct port of HingeConstraintTest.cpp.
/// Two chains of 15 boxes connected by alternating Y/Z-axis hinges (limits -10°/+20°):
///   Chain 0 — straight line along +X at Y=50, Z=0.
///   Chain 1 — same origin shifted to Z=-20, with random displacements and rotations.
/// Two isolated hinge pairs at Y=5:
///   Hard hinge  at X=4..6  — limits -10°/+110°, no spring.
///   Soft hinge  at X=10..12 — limits -10°/+110°.
/// Note: mLimitsSpringSettings (frequency=1, damping=0.5 for the soft hinge) is not
/// exposed in the current generated bindings, so the soft hinge behaves like a hard hinge.
/// GroupFilterTable is also not yet exposed; adjacent chain bodies may collide.
/// </summary>
public class Demo_HingeConstraint : DemoBase
{
    public override string Name     => "Hinge Constraint";
    public override string Category => "Constraints";

    const int   ChainLength = 15;
    const float BoxSize     = 4.0f;
    const float MinAngle    = -10f * MathF.PI / 180f;
    const float MaxAngle    =  20f * MathF.PI / 180f;

    private readonly List<JPH.TwoBodyConstraint?> _constraints = new();

    public override unsafe void Init(
        JPH.BodyInterface   bi,
        JPH.PhysicsSystem   sys,
        List<PhysicsBody>   bodies,
        Random              random)
    {
        AddFloor(bi, bodies);

        float boxHE = 0.5f * BoxSize; // 2.0f

        using var chainHalf = new JPH.Vec3(boxHE, boxHE, boxHE);
        using var chainSS   = new JPH.BoxShapeSettings(chainHalf);

        // ── Two chains (randomness = 0 and 1) ─────────────────────────────
        for (int randomness = 0; randomness < 2; randomness++)
        {
            var rng = new Random(0); // fresh deterministic engine per chain, matching C++ default_random_engine
            var pos = new Vector3(0f, 50f, -randomness * 20f);

            // Static top anchor
            using var topCS = new JPH.BodyCreationSettings();
            topCS.SetShapeSettings(chainSS);
            topCS.mPosition.Set(pos.X, pos.Y, pos.Z);
            topCS.mMotionType  = JPH.EMotionType.Static;
            topCS.mObjectLayer = LayerNonMoving;
            topCS.mCollisionGroup.SetGroupID((uint)randomness);
            topCS.mCollisionGroup.SetSubGroupID(0u);
            var topBody = bi.CreateBody(topCS)!;
            bi.AddBody(topBody.GetID(), JPH.EActivation.DontActivate);
            bodies.Add(new PhysicsBody
            {
                bodyId = topBody.GetID(),
                color  = new Vector3(0.55f, 0.50f, 0.45f),
                shape  = RenderShape.Box,
                scale  = new Vector3(BoxSize)
            });

            var prevBody = topBody;

            for (int i = 1; i < ChainLength; i++)
            {
                Quaternion rot;
                if (randomness == 0)
                {
                    pos += new Vector3(BoxSize, 0f, 0f);
                    rot  = Quaternion.Identity;
                }
                else
                {
                    float dx = (float)(rng.NextDouble() * 2.0 - 1.0);
                    float dy = (float)(rng.NextDouble() * 2.0 - 1.0);
                    float dz = (float)(rng.NextDouble() * 2.0 - 1.0);
                    pos += new Vector3(BoxSize + MathF.Abs(dx), dy, dz);
                    rot  = RandomUnitQuat(rng);
                }

                using var segCS = new JPH.BodyCreationSettings();
                segCS.SetShapeSettings(chainSS);
                segCS.mPosition.Set(pos.X, pos.Y, pos.Z);
                segCS.mRotation.Set(rot.X, rot.Y, rot.Z, rot.W);
                segCS.mMotionType  = JPH.EMotionType.Dynamic;
                segCS.mObjectLayer = LayerMoving;
                segCS.mCollisionGroup.SetGroupID((uint)randomness);
                segCS.mCollisionGroup.SetSubGroupID((uint)i);
                var segBody = bi.CreateBody(segCS)!;
                bi.AddBody(segBody.GetID(), JPH.EActivation.Activate);
                float t = (float)i / (ChainLength - 1);
                bodies.Add(new PhysicsBody
                {
                    bodyId = segBody.GetID(),
                    color  = randomness == 0
                        ? new Vector3(0.20f + t * 0.60f, 0.30f, 0.70f - t * 0.40f)
                        : new Vector3(0.70f, 0.30f + t * 0.40f, 0.20f),
                    shape  = RenderShape.Box,
                    scale  = new Vector3(BoxSize)
                });

                using var hs = new JPH.HingeConstraintSettings();
                if ((i & 1) == 0)
                {
                    // Even i — Y-axis hinge at back-top corner of this segment
                    hs.mPoint1.Set(pos.X - boxHE, pos.Y, pos.Z + boxHE);
                    hs.mPoint2.Set(pos.X - boxHE, pos.Y, pos.Z + boxHE);
                    hs.mHingeAxis1.Set(0f, 1f, 0f);
                    hs.mHingeAxis2.Set(0f, 1f, 0f);
                    hs.mNormalAxis1.Set(1f, 0f, 0f);
                    hs.mNormalAxis2.Set(1f, 0f, 0f);
                }
                else
                {
                    // Odd i — Z-axis hinge at left-bottom corner of this segment
                    hs.mPoint1.Set(pos.X - boxHE, pos.Y - boxHE, pos.Z);
                    hs.mPoint2.Set(pos.X - boxHE, pos.Y - boxHE, pos.Z);
                    hs.mHingeAxis1.Set(0f, 0f, 1f);
                    hs.mHingeAxis2.Set(0f, 0f, 1f);
                    hs.mNormalAxis1.Set(1f, 0f, 0f);
                    hs.mNormalAxis2.Set(1f, 0f, 0f);
                }
                hs.mLimitsMin = MinAngle;
                hs.mLimitsMax = MaxAngle;

                var c = hs.Create(prevBody, segBody);
                _constraints.Add(c);
                sys.AddConstraint(c);

                prevBody = segBody;
            }
        }

        // ── Hard hinge pair ────────────────────────────────────────────────
        // body1: static box at (4, 5, 0)  body2: dynamic box at (6, 5, 0)
        // hinge pivot at (5, 4, 0), Z-axis, Y-normal, limits -10°..110°
        using var unitHalf = new JPH.Vec3(1f, 1f, 1f);
        using var unitSS   = new JPH.BoxShapeSettings(unitHalf);
        {
            using var b1CS = new JPH.BodyCreationSettings();
            b1CS.SetShapeSettings(unitSS);
            b1CS.mPosition.Set(4f, 5f, 0f);
            b1CS.mMotionType  = JPH.EMotionType.Static;
            b1CS.mObjectLayer = LayerNonMoving;
            var body1 = bi.CreateBody(b1CS)!;
            bi.AddBody(body1.GetID(), JPH.EActivation.DontActivate);
            bodies.Add(new PhysicsBody { bodyId = body1.GetID(), color = new Vector3(0.40f, 0.40f, 0.80f), shape = RenderShape.Box, scale = new Vector3(2f) });

            using var b2CS = new JPH.BodyCreationSettings();
            b2CS.SetShapeSettings(unitSS);
            b2CS.mPosition.Set(6f, 5f, 0f);
            b2CS.mMotionType  = JPH.EMotionType.Dynamic;
            b2CS.mObjectLayer = LayerMoving;
            var body2 = bi.CreateBody(b2CS)!;
            bi.AddBody(body2.GetID(), JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = body2.GetID(), color = new Vector3(0.80f, 0.40f, 0.40f), shape = RenderShape.Box, scale = new Vector3(2f) });

            using var hs = new JPH.HingeConstraintSettings();
            hs.mPoint1.Set(5f, 4f, 0f);
            hs.mPoint2.Set(5f, 4f, 0f);
            hs.mHingeAxis1.Set(0f, 0f, 1f);
            hs.mHingeAxis2.Set(0f, 0f, 1f);
            hs.mNormalAxis1.Set(0f, 1f, 0f);
            hs.mNormalAxis2.Set(0f, 1f, 0f);
            hs.mLimitsMin = -10f * MathF.PI / 180f;
            hs.mLimitsMax = 110f * MathF.PI / 180f;
            var c = hs.Create(body1, body2);
            _constraints.Add(c);
            sys.AddConstraint(c);
        }

        // ── Soft hinge pair ────────────────────────────────────────────────
        // body1: static box at (10, 5, 0)  body2: dynamic box at (12, 5, 0)
        // hinge pivot at (11, 4, 0), Z-axis, Y-normal, limits -10°..110°
        // C++ also sets mLimitsSpringSettings.mFrequency=1, mDamping=0.5
        // but those fields are not yet exposed in the generated bindings.
        {
            using var b1CS = new JPH.BodyCreationSettings();
            b1CS.SetShapeSettings(unitSS);
            b1CS.mPosition.Set(10f, 5f, 0f);
            b1CS.mMotionType  = JPH.EMotionType.Static;
            b1CS.mObjectLayer = LayerNonMoving;
            var body1 = bi.CreateBody(b1CS)!;
            bi.AddBody(body1.GetID(), JPH.EActivation.DontActivate);
            bodies.Add(new PhysicsBody { bodyId = body1.GetID(), color = new Vector3(0.40f, 0.75f, 0.40f), shape = RenderShape.Box, scale = new Vector3(2f) });

            using var b2CS = new JPH.BodyCreationSettings();
            b2CS.SetShapeSettings(unitSS);
            b2CS.mPosition.Set(12f, 5f, 0f);
            b2CS.mMotionType  = JPH.EMotionType.Dynamic;
            b2CS.mObjectLayer = LayerMoving;
            var body2 = bi.CreateBody(b2CS)!;
            bi.AddBody(body2.GetID(), JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = body2.GetID(), color = new Vector3(0.85f, 0.70f, 0.20f), shape = RenderShape.Box, scale = new Vector3(2f) });

            using var hs = new JPH.HingeConstraintSettings();
            hs.mPoint1.Set(11f, 4f, 0f);
            hs.mPoint2.Set(11f, 4f, 0f);
            hs.mHingeAxis1.Set(0f, 0f, 1f);
            hs.mHingeAxis2.Set(0f, 0f, 1f);
            hs.mNormalAxis1.Set(0f, 1f, 0f);
            hs.mNormalAxis2.Set(0f, 1f, 0f);
            hs.mLimitsMin = -10f * MathF.PI / 180f;
            hs.mLimitsMax = 110f * MathF.PI / 180f;
            hs.mLimitsSpringSettings.mFrequency = 1f;
            hs.mLimitsSpringSettings.mDamping   = 0.5f; 
            var c = hs.Create(body1, body2);
            _constraints.Add(c);
            sys.AddConstraint(c);
        }
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        foreach (var c in _constraints)
        {
            if (c != null)
                sys.RemoveConstraint(c);
        }
        _constraints.Clear();
    }

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 120,
        Latitude  = 25,
        Longitude = 10,
        Center    = new Vector3(28f, 20f, -10f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 2000f
    };

    // Uniform random unit quaternion — Shoemake's method, matching Quat::sRandom in Jolt.
    private static Quaternion RandomUnitQuat(Random rng)
    {
        float u1 = (float)rng.NextDouble();
        float u2 = (float)rng.NextDouble();
        float u3 = (float)rng.NextDouble();
        float a1 = 2f * MathF.PI * u2;
        float a2 = 2f * MathF.PI * u3;
        float s  = MathF.Sqrt(1f - u1);
        float c  = MathF.Sqrt(u1);
        return new Quaternion(s * MathF.Sin(a1), s * MathF.Cos(a1), c * MathF.Sin(a2), c * MathF.Cos(a2));
    }
}
