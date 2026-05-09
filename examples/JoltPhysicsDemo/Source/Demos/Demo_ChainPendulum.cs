using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Ten spheres linked in a chain by DistanceConstraints hanging from a static anchor.
/// All spheres are positioned along an arc (30° from vertical) and released from rest,
/// swinging as a flexible pendulum chain with a whip-like "crack-the-whip" effect.
/// Demonstrates DistanceConstraint (rigid rope links).
/// </summary>
public class Demo_ChainPendulum : DemoBase
{
    public override string Name     => "Chain Pendulum";
    public override string Category => "Constraints";

    const int   N        = 10;
    const float R        = 0.5f;
    const float LinkDist = 2.2f;
    const float AnchorY  = 34f;
    const float SwingAngle = MathF.PI / 5f;  // 36° from vertical

    private readonly JPH.TwoBodyConstraint?[] _constraints = new JPH.TwoBodyConstraint?[N];

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies, 80f, 0.5f, 80f);

        // ── Static anchor ──────────────────────────────────────────────────
        using var anchorHalf = new JPH.Vec3(0.8f, 0.8f, 0.8f);
        using var anchorSS   = new JPH.BoxShapeSettings(anchorHalf);
        using var anchorCS   = new JPH.BodyCreationSettings();
        anchorCS.SetShapeSettings(anchorSS);
        anchorCS.mPosition.Set(0f, AnchorY, 0f);
        anchorCS.mMotionType  = JPH.EMotionType.Static;
        anchorCS.mObjectLayer = LayerNonMoving;

        var anchorBody = bi.CreateBody(anchorCS);
        bi.AddBody(anchorBody!.GetID(), JPH.EActivation.DontActivate);
        bodies.Add(new PhysicsBody
        {
            bodyId = anchorBody.GetID(),
            color  = new Vector3(0.35f, 0.35f, 0.4f),
            shape  = RenderShape.Box,
            scale  = new Vector3(1.6f, 1.6f, 1.6f)
        });

        // ── Chain links: placed along an arc at SwingAngle from vertical ──
        var oddColor  = new Vector3(0.90f, 0.72f, 0.18f);  // gold
        var evenColor = new Vector3(0.65f, 0.52f, 0.12f);  // dark gold

        var chainBodies = new JPH.Body?[N];

        float sinA = MathF.Sin(SwingAngle);
        float cosA = MathF.Cos(SwingAngle);

        for (int i = 0; i < N; i++)
        {
            float dist = (i + 1) * LinkDist;
            float bx   = sinA * dist;
            float by   = AnchorY - cosA * dist;

            using var ss = new JPH.SphereShapeSettings(R);
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(ss);
            cs.mPosition.Set(bx, by, 0f);
            cs.mMotionType    = JPH.EMotionType.Dynamic;
            cs.mObjectLayer   = LayerMoving;
            cs.mRestitution   = 0.15f;
            cs.mLinearDamping = 0.01f;

            var b = bi.CreateBody(cs);
            bi.AddBody(b!.GetID(), JPH.EActivation.Activate);
            chainBodies[i] = b;

            bodies.Add(new PhysicsBody
            {
                bodyId = b.GetID(),
                color  = (i % 2 == 0) ? oddColor : evenColor,
                shape  = RenderShape.Sphere,
                scale  = new Vector3(R * 2f)
            });
        }

        // ── DistanceConstraints: anchor→[0], [i]→[i+1] ────────────────────
        // mMinDistance = mMaxDistance = -1 → auto-detect rigid link distance.
        // mPoint1/mPoint2 set to world-space body centers so local offset = (0,0,0).
        float prevX   = 0f;
        float prevY   = AnchorY;
        var   prevBod = anchorBody;

        for (int i = 0; i < N; i++)
        {
            float curDist = (i + 1) * LinkDist;
            float curX    = sinA * curDist;
            float curY    = AnchorY - cosA * curDist;

            using var dcs = new JPH.DistanceConstraintSettings();
            dcs.mPoint1.Set(prevX, prevY, 0f);
            dcs.mPoint2.Set(curX,  curY,  0f);
            // Leave mMinDistance / mMaxDistance at -1 → auto-detects distance = LinkDist

            _constraints[i] = dcs.Create(prevBod, chainBodies[i]);
            sys.AddConstraint(_constraints[i]);

            prevX   = curX;
            prevY   = curY;
            prevBod = chainBodies[i];
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
        Distance  = 55,
        Latitude  = 10,
        Longitude = 0,
        Center    = new Vector3(0f, 22f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000f
    };
}
