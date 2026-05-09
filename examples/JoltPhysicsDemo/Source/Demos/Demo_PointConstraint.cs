using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Five boxes hang from a static bar via PointConstraints (ball-socket joints).
/// Each box can swing freely in any direction.  A heavy sphere is launched from
/// the side and sweeps through all five boxes, showing how each constraint allows
/// full rotational freedom while keeping the pivot fixed.
/// Corresponds to PointConstraintTest.cpp.
/// </summary>
public class Demo_PointConstraint : DemoBase
{
    public override string Name     => "Point Constraint";
    public override string Category => "Constraints";

    const int N = 5;
    private readonly JPH.TwoBodyConstraint?[] _constraints = new JPH.TwoBodyConstraint?[N];

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies, 60f, 0.5f, 20f);

        const float BarY      = 24f;
        const float Spacing   = 4.5f;
        const float RopeLen   = 10f;
        const float BoxHE     = 1.0f;

        float totalW  = (N - 1) * Spacing;
        float startX  = -totalW * 0.5f;

        // ── Static support bar ─────────────────────────────────────────────
        using var barHalf = new JPH.Vec3(totalW * 0.5f + BoxHE + 1f, 0.5f, 0.5f);
        using var barSS   = new JPH.BoxShapeSettings(barHalf);
        using var barCS   = new JPH.BodyCreationSettings();
        barCS.SetShapeSettings(barSS);
        barCS.mPosition.Set(0f, BarY, 0f);
        barCS.mMotionType  = JPH.EMotionType.Static;
        barCS.mObjectLayer = LayerNonMoving;

        var barBody = bi.CreateBody(barCS);
        bi.AddBody(barBody!.GetID(), JPH.EActivation.DontActivate);
        bodies.Add(new PhysicsBody
        {
            bodyId = barBody.GetID(),
            color  = new Vector3(0.45f, 0.38f, 0.30f),
            shape  = RenderShape.Box,
            scale  = new Vector3((totalW * 0.5f + BoxHE + 1f) * 2f, 1f, 1f)
        });

        var boxColors = new Vector3[]
        {
            new Vector3(0.85f, 0.25f, 0.20f),
            new Vector3(0.25f, 0.65f, 0.85f),
            new Vector3(0.25f, 0.75f, 0.30f),
            new Vector3(0.90f, 0.70f, 0.15f),
            new Vector3(0.75f, 0.35f, 0.80f),
        };

        for (int i = 0; i < N; i++)
        {
            float px = startX + i * Spacing;
            float py = BarY - RopeLen;

            using var boxHalf = new JPH.Vec3(BoxHE, BoxHE, BoxHE);
            using var boxSS   = new JPH.BoxShapeSettings(boxHalf);
            using var boxCS   = new JPH.BodyCreationSettings();
            boxCS.SetShapeSettings(boxSS);
            boxCS.mPosition.Set(px, py, 0f);
            boxCS.mMotionType     = JPH.EMotionType.Dynamic;
            boxCS.mObjectLayer    = LayerMoving;
            boxCS.mAngularDamping = 0.1f;
            boxCS.mFriction       = 0.3f;
            boxCS.mRestitution    = 0.2f;

            var box = bi.CreateBody(boxCS);
            bi.AddBody(box!.GetID(), JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody
            {
                bodyId = box.GetID(),
                color  = boxColors[i],
                shape  = RenderShape.Box,
                scale  = new Vector3(BoxHE * 2f, BoxHE * 2f, BoxHE * 2f)
            });

            using var pcs = new JPH.PointConstraintSettings();
            pcs.mPoint1.Set(px, BarY, 0f);
            pcs.mPoint2.Set(px, BarY, 0f);

            _constraints[i] = pcs.Create(barBody, box);
            sys.AddConstraint(_constraints[i]);
        }

        // ── Sphere launched horizontally to sweep through all boxes ────────
        const float SphereR = 1.2f;
        float launchX = startX - 18f;
        float launchY = BarY - RopeLen;   // same height as boxes

        using var sphereSS = new JPH.SphereShapeSettings(SphereR);
        using var sphereCS = new JPH.BodyCreationSettings();
        sphereCS.SetShapeSettings(sphereSS);
        sphereCS.mPosition.Set(launchX, launchY, 0f);
        sphereCS.mMotionType  = JPH.EMotionType.Dynamic;
        sphereCS.mObjectLayer = LayerMoving;
        sphereCS.mFriction    = 0.2f;
        sphereCS.mRestitution = 0.4f;

        var sphereId = bi.CreateAndAddBody(sphereCS, JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId = sphereId,
            color  = new Vector3(0.80f, 0.80f, 0.20f),
            shape  = RenderShape.Sphere,
            scale  = new Vector3(SphereR * 2f)
        });

        using var launchVel = new JPH.Vec3(40f, 0f, 0f);
        bi.SetLinearVelocity(sphereId, launchVel);
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
        Distance  = 55,
        Latitude  = 10,
        Longitude = 0,
        Center    = new Vector3(0f, 16f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000f
    };
}
