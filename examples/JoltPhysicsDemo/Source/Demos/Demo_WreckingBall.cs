using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// A large sphere suspended from a static anchor by a PointConstraint.
/// Pulled to one side, it swings like a pendulum and demolishes a tower of boxes.
/// Demonstrates PointConstraint (ball-socket joint) and kinetic energy transfer.
/// </summary>
public class Demo_WreckingBall : DemoBase
{
    public override string Name     => "Wrecking Ball";
    public override string Category => "Constraints";

    private JPH.TwoBodyConstraint? _constraint;

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        // Floor
        AddFloor(bi, bodies, 50f, 0.5f, 50f);

        // ── Static anchor at the ceiling ──────────────────────────────────
        using var anchorHalf = new JPH.Vec3(1.5f, 1.5f, 1.5f);
        using var anchorSS   = new JPH.BoxShapeSettings(anchorHalf);
        using var anchorCS   = new JPH.BodyCreationSettings();
        anchorCS.SetShapeSettings(anchorSS);
        anchorCS.mPosition.Set(0f, 30f, 0f);
        anchorCS.mMotionType  = JPH.EMotionType.Static;
        anchorCS.mObjectLayer = LayerNonMoving;

        var anchor = bi.CreateBody(anchorCS);
        bi.AddBody(anchor!.GetID(), JPH.EActivation.DontActivate);
        bodies.Add(new PhysicsBody
        {
            bodyId = anchor.GetID(),
            color  = new Vector3(0.4f, 0.4f, 0.45f),
            shape  = RenderShape.Box,
            scale  = new Vector3(3f, 3f, 3f)
        });

        // ── Wrecking ball — pulled out horizontally to (-24, 30, 0) ──────
        // At rest it would hang at (0, 6, 0); pendulum length = 24.
        const float BallRadius    = 3.5f;
        const float PendulumLength = 24f;
        const float AnchorY       = 30f;
        // Start position: pulled horizontal in -X direction
        float startX = -PendulumLength;
        float startY = AnchorY;   // same height as anchor → max PE

        using var ballSS = new JPH.SphereShapeSettings(BallRadius);
        using var ballCS = new JPH.BodyCreationSettings();
        ballCS.SetShapeSettings(ballSS);
        ballCS.mPosition.Set(startX, startY, 0f);
        ballCS.mMotionType  = JPH.EMotionType.Dynamic;
        ballCS.mObjectLayer = LayerMoving;
        ballCS.mFriction    = 0.3f;
        ballCS.mRestitution = 0.3f;

        var ball = bi.CreateBody(ballCS);
        bi.AddBody(ball!.GetID(), JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId = ball.GetID(),
            color  = new Vector3(0.75f, 0.15f, 0.1f),
            shape  = RenderShape.Sphere,
            scale  = new Vector3(BallRadius * 2f)
        });

        // ── Point constraint: anchor-center to ball ───────────────────────
        // mPoint1 = mPoint2 = pivot (anchor center) in world space.
        // The ball's local attachment point is recorded as the vector from
        // ball-center to the pivot, so the sphere swings at radius PendulumLength.
        using var pcs = new JPH.PointConstraintSettings();
        pcs.mPoint1.Set(0f, AnchorY, 0f);
        pcs.mPoint2.Set(0f, AnchorY, 0f);
        _constraint = pcs.Create(anchor, ball);
        sys.AddConstraint(_constraint);

        // ── Tower of boxes at x = +16 ─────────────────────────────────────
        // 3 columns wide × 7 rows tall; box size 2×2×2
        const float TowerX      = 16f;
        const float BoxHE       = 1f;
        const int   TowerCols   = 3;
        const int   TowerRows   = 7;
        var towerColors = new Vector3[]
        {
            new Vector3(0.85f, 0.65f, 0.40f),  // warm terracotta
            new Vector3(0.70f, 0.55f, 0.35f),
        };

        for (int row = 0; row < TowerRows; row++)
        {
            float y = BoxHE + row * BoxHE * 2f;
            for (int col = 0; col < TowerCols; col++)
            {
                float x     = TowerX + (col - 1) * BoxHE * 2.2f;
                var   color = towerColors[(row + col) % towerColors.Length];
                AddBox(bi, bodies,
                    BoxHE, BoxHE, BoxHE,
                    x, y, 0f,
                    Quaternion.Identity,
                    JPH.EMotionType.Dynamic, LayerMoving, color,
                    friction: 0.6f, restitution: 0.1f);
            }
        }
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        if (_constraint != null)
        {
            sys.RemoveConstraint(_constraint);
            _constraint = null;
        }
    }

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 65,
        Latitude  = 18,
        Longitude = 0,
        Center    = new Vector3(0f, 12f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000f
    };
}
