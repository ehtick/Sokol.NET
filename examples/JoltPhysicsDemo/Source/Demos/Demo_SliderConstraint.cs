using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of Samples/Tests/Constraints/SliderConstraintTest.cpp.
///
/// Three setups demonstrating slider constraints:
///   Chain  (z=0):  10 boxes from a static anchor, each sliding along a 10° downward-tilted
///                  X axis, limits -5 to +10 m.
///   Hard   (x=5):  two dynamic boxes stacked vertically, slider axis Y, hard limits [0, 2 m].
///   Soft   (x=10): same geometry but limits use a spring (frequency=1 Hz, damping=0.5).
/// </summary>
public sealed class Demo_SliderConstraint : DemoBase
{
    public override string Name     => "Slider Constraint";
    public override string Category => "Constraints";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 80,
        Latitude  = 20,
        Longitude = 20,
        Center    = new Vector3(18f, 15f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000f,
    };

    private readonly List<JPH.TwoBodyConstraint?> _constraints = new();

    // Slider axis tilted 10° downward from +X: rotate X by -10° around Z.
    private const float cAxisX = 0.9848f;   // cos(10°)
    private const float cAxisY = -0.1736f;  // -sin(10°)
    // Perpendicular in XY plane: cross(sliderAxis, Z) = (axisY, -axisX, 0)
    private const float cNormX = -0.1736f;
    private const float cNormY = -0.9848f;

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

        // ── Chain: 10 boxes connected by tilted-axis sliders ──────────────────
        {
            using var topSS = new JPH.BoxShapeSettings(new JPH.Vec3(cHalf, cHalf, cHalf));
            using var topCS = new JPH.BodyCreationSettings();
            topCS.SetShapeSettings(topSS);
            topCS.mPosition.Set(0f, 25f, 0f);
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
            float prevX = 0f;

            for (int i = 1; i < cChainLength; ++i)
            {
                float curX = prevX + cBoxSize;

                using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(cHalf, cHalf, cHalf));
                using var cs = new JPH.BodyCreationSettings();
                cs.SetShapeSettings(ss);
                cs.mPosition.Set(curX, 25f, 0f);
                cs.mMotionType  = JPH.EMotionType.Dynamic;
                cs.mObjectLayer = LayerMoving;
                var seg = bi.CreateBody(cs)!;
                bi.AddBody(seg.GetID(), JPH.EActivation.Activate);
                bodies.Add(new PhysicsBody
                {
                    bodyId = seg.GetID(),
                    color  = new Vector3(0.85f, 0.45f, 0.15f),
                    shape  = RenderShape.Box,
                    scale  = new Vector3(cBoxSize),
                });

                using var sl = new JPH.SliderConstraintSettings();
                sl.mAutoDetectPoint = true;
                sl.mSliderAxis1.Set(cAxisX, cAxisY, 0f);
                sl.mSliderAxis2.Set(cAxisX, cAxisY, 0f);
                sl.mNormalAxis1.Set(cNormX, cNormY, 0f);
                sl.mNormalAxis2.Set(cNormX, cNormY, 0f);
                sl.mLimitsMin = -5f;
                sl.mLimitsMax = 10f;
                var c = sl.Create(prev, seg);
                _constraints.Add(c);
                sys.AddConstraint(c);

                prev  = seg;
                prevX = curX;
            }
        }

        // ── Hard-limit vertical slider (x=5) ─────────────────────────────────
        {
            var (vert1, vert2) = MakeVerticalPair(bi, bodies, 5f, new Vector3(0.3f, 0.75f, 0.35f));

            using var sl = new JPH.SliderConstraintSettings();
            sl.mAutoDetectPoint = true;
            sl.mSliderAxis1.Set(0f, 1f, 0f);
            sl.mSliderAxis2.Set(0f, 1f, 0f);
            sl.mNormalAxis1.Set(1f, 0f, 0f);
            sl.mNormalAxis2.Set(1f, 0f, 0f);
            sl.mLimitsMin = 0f;
            sl.mLimitsMax = 2f;
            var c = sl.Create(vert1, vert2);
            _constraints.Add(c);
            sys.AddConstraint(c);
        }

        // ── Soft-spring vertical slider (x=10) ───────────────────────────────
        {
            var (vert1, vert2) = MakeVerticalPair(bi, bodies, 10f, new Vector3(0.55f, 0.3f, 0.85f));

            using var sl = new JPH.SliderConstraintSettings();
            sl.mAutoDetectPoint = true;
            sl.mSliderAxis1.Set(0f, 1f, 0f);
            sl.mSliderAxis2.Set(0f, 1f, 0f);
            sl.mNormalAxis1.Set(1f, 0f, 0f);
            sl.mNormalAxis2.Set(1f, 0f, 0f);
            sl.mLimitsMin = 0f;
            sl.mLimitsMax = 2f;
            sl.mLimitsSpringSettings.mFrequency = 1f;
            sl.mLimitsSpringSettings.mDamping   = 0.5f;
            var c = sl.Create(vert1, vert2);
            _constraints.Add(c);
            sys.AddConstraint(c);
        }
    }

    private static unsafe (JPH.Body, JPH.Body) MakeVerticalPair(
        JPH.BodyInterface bi, List<PhysicsBody> bodies, float x, Vector3 color)
    {
        using var ss1 = new JPH.BoxShapeSettings(new JPH.Vec3(1f, 1f, 1f));
        using var cs1 = new JPH.BodyCreationSettings();
        cs1.SetShapeSettings(ss1);
        cs1.mPosition.Set(x, 9f, 0f);
        cs1.mMotionType  = JPH.EMotionType.Dynamic;
        cs1.mObjectLayer = LayerMoving;
        var b1 = bi.CreateBody(cs1)!;
        bi.AddBody(b1.GetID(), JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody { bodyId = b1.GetID(), color = color, shape = RenderShape.Box, scale = new Vector3(2f) });

        using var ss2 = new JPH.BoxShapeSettings(new JPH.Vec3(1f, 1f, 1f));
        using var cs2 = new JPH.BodyCreationSettings();
        cs2.SetShapeSettings(ss2);
        cs2.mPosition.Set(x, 3f, 0f);
        cs2.mMotionType  = JPH.EMotionType.Dynamic;
        cs2.mObjectLayer = LayerMoving;
        var b2 = bi.CreateBody(cs2)!;
        bi.AddBody(b2.GetID(), JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody { bodyId = b2.GetID(), color = color * 0.7f, shape = RenderShape.Box, scale = new Vector3(2f) });

        return (b1, b2);
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        foreach (var c in _constraints)
            if (c != null) sys.RemoveConstraint(c);
        _constraints.Clear();
    }
}
