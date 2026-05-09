using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Classic Newton's Cradle: five equal spheres hanging from PointConstraints
/// attached to a static top bar.  The leftmost ball is pulled back and released.
/// Demonstrates conservation of momentum through elastic collisions.
/// </summary>
public class Demo_NewtonsCradle : DemoBase
{
    public override string Name           => "Newton's Cradle";
    public override string Category       => "Constraints";
    public override int    CollisionSteps => 8;

    // Newton's Cradle requires mNumVelocitySteps = 1 so the collision impulse
    // propagates one ball at a time per substep (one-hop per substep).
    // With CollisionSteps = 8 substeps, the impulse travels through all 4
    // resting balls sequentially, giving the correct 1-in/1-out behaviour.
    public override void Activate(JPH.PhysicsSystem sys)
    {
        using var ps = new JPH.PhysicsSettings();
        ps.mNumVelocitySteps  = 1u;  // impulse propagates one ball per substep
        ps.mNumPositionSteps  = 0u;  // disable Baumgarte correction — prevents tiny
                                     // outward velocities being injected into resting balls
        sys.SetPhysicsSettings(ps);
    }

    public override void Deactivate(JPH.PhysicsSystem sys)
    {
        using var ps = new JPH.PhysicsSettings(); // restores all defaults
        sys.SetPhysicsSettings(ps);
    }

    // Store all five constraints so we can remove them on cleanup.
    private readonly JPH.TwoBodyConstraint?[] _constraints = new JPH.TwoBodyConstraint?[5];

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        const float BallRadius     = 1.2f;
        const float Spacing        = BallRadius * 2f + 0.001f; // essentially touching at rest
        const float PivotY         = 22f;
        const float PendulumLength = 16f;  // pivot-to-ball-centre distance
        const float RestY          = PivotY - PendulumLength;
        const int   Count          = 5;

        // ── Static support bar (visual only — constraints attach to SFixedToWorld) ──
        float barHalfX = (Count - 1) * Spacing * 0.5f + BallRadius * 1.5f;
        using var barHalf = new JPH.Vec3(barHalfX, 0.6f, 0.6f);
        using var barSS   = new JPH.BoxShapeSettings(barHalf);
        using var barCS   = new JPH.BodyCreationSettings();
        barCS.SetShapeSettings(barSS);
        barCS.mPosition.Set(0f, PivotY, 0f);
        barCS.mMotionType  = JPH.EMotionType.Static;
        barCS.mObjectLayer = LayerNonMoving;

        var barId = bi.CreateAndAddBody(barCS, JPH.EActivation.DontActivate);
        bodies.Add(new PhysicsBody
        {
            bodyId = barId,
            color  = new Vector3(0.5f, 0.45f, 0.4f),
            shape  = RenderShape.Box,
            scale  = new Vector3(barHalfX * 2f, 1.2f, 1.2f)
        });

        // Centre x-positions of the five balls
        float totalWidth = (Count - 1) * Spacing;
        float x0         = -totalWidth * 0.5f;

        var ballColor = new Vector3(0.78f, 0.78f, 0.82f);

        for (int i = 0; i < Count; i++)
        {
            float bx = x0 + i * Spacing;

            // Ball 0: pulled back to ~50° so it has a clear swing before impact.
            // Balls 1-4: hang at rest directly below their pivot.
            float ballX, ballY;
            if (i == 0)
            {
                float angle = MathF.PI * 0.278f;  // ~50°
                ballX = bx - PendulumLength * MathF.Sin(angle);
                ballY = PivotY - PendulumLength * MathF.Cos(angle);
            }
            else
            {
                ballX = bx;
                ballY = RestY;
            }

            using var ballSS = new JPH.SphereShapeSettings(BallRadius);
            using var ballCS = new JPH.BodyCreationSettings();
            ballCS.SetShapeSettings(ballSS);
            ballCS.mPosition.Set(ballX, ballY, 0f);
            ballCS.mMotionType      = JPH.EMotionType.Dynamic;
            ballCS.mObjectLayer     = LayerMoving;
            ballCS.mRestitution     = 1.0f;   // perfectly elastic — no energy leak per cycle
            ballCS.mFriction        = 0.0f;
            ballCS.mLinearDamping   = 0.0f;
            ballCS.mAngularDamping  = 0.0f;
            ballCS.mAllowSleeping   = false;

            var ball = bi.CreateBody(ballCS);

            // All balls start active — the resting balls have zero velocity and sit
            // at equilibrium so they won't move.  Having all bodies active means the
            // chain collision (0→1→2→3→4) is resolved in a single solver pass instead
            // of staggered wake-up across frames, which keeps the middle balls together.
            bi.AddBody(ball!.GetID(), JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody
            {
                bodyId = ball.GetID(),
                color  = ballColor,
                shape  = RenderShape.Sphere,
                scale  = new Vector3(BallRadius * 2f)
            });

            // HingeConstraint with Z-axis: each ball can only swing in the XY plane.
            // This prevents the ball spinning around the string (which PointConstraint
            // allows) and eliminates Z-drift and arc-length changes during collisions.
            using var hcs = new JPH.HingeConstraintSettings();
            hcs.mPoint1.Set(bx, PivotY, 0f);
            hcs.mPoint2.Set(bx, PivotY, 0f);
            hcs.mHingeAxis1.Set(0f, 0f, 1f);
            hcs.mHingeAxis2.Set(0f, 0f, 1f);
            hcs.mNormalAxis1.Set(1f, 0f, 0f);
            hcs.mNormalAxis2.Set(1f, 0f, 0f);
            _constraints[i] = hcs.Create(JPH.Body.SFixedToWorld, ball);
            sys.AddConstraint(_constraints[i]);
        }
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        for (int i = 0; i < _constraints.Length; i++)
        {
            if (_constraints[i] != null)
            {
                sys.RemoveConstraint(_constraints[i]);
                _constraints[i] = null;
            }
        }
    }

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 45,
        Latitude  = 10,
        Longitude = 0,
        Center    = new Vector3(0f, 14f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000f
    };
}
