using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// A "morning star" compound rigid body: a central sphere with six box spikes
/// attached via FixedConstraints (one in each principal axis direction).
/// The compound is launched horizontally into a wall of stacked boxes.
/// Demonstrates FixedConstraint (rigid weld between dynamic bodies) and shows
/// how compound bodies behave differently from simple shapes.
/// </summary>
public class Demo_Mace : DemoBase
{
    public override string Name     => "Mace";
    public override string Category => "Constraints";

    const int MaxConstraints = 6;
    private readonly JPH.TwoBodyConstraint?[] _constraints = new JPH.TwoBodyConstraint?[MaxConstraints];
    private readonly JPH.BodyID[]             _spikeIds    = new JPH.BodyID[MaxConstraints];

    const float SphereR     = 1.5f;
    const float SpikeLen    = 1.5f;
    const float SpikeRadius = 0.30f;

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies, 60f, 0.5f, 60f);

        // ── Central sphere ─────────────────────────────────────────────────
        const float StartX = 0f;
        const float StartY = 22f;

        using var sphereSS = new JPH.SphereShapeSettings(SphereR);
        using var sphereCS = new JPH.BodyCreationSettings();
        sphereCS.SetShapeSettings(sphereSS);
        sphereCS.mPosition.Set(StartX, StartY, 0f);
        sphereCS.mMotionType  = JPH.EMotionType.Dynamic;
        sphereCS.mObjectLayer = LayerMoving;
        sphereCS.mRestitution = 0.2f;
        sphereCS.mFriction    = 0.4f;

        var sphereBody = bi.CreateBody(sphereCS);
        bi.AddBody(sphereBody!.GetID(), JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId = sphereBody.GetID(),
            color  = new Vector3(0.60f, 0.60f, 0.65f),
            shape  = RenderShape.Sphere,
            scale  = new Vector3(SphereR * 2f)
        });

        // ── Six spikes: one per principal axis ─────────────────────────────
        // FixedConstraint with mAutoDetectPoint=true locks each spike rigidly
        // to the sphere at its current relative position/orientation.
        var spikeColor = new Vector3(0.85f, 0.35f, 0.15f);

        float d = SphereR + SpikeLen;
        int ci = 0;
        ci = AddSpike(bi, sys, bodies, sphereBody, StartX+d, StartY,   0f,   SpikeLen, SpikeRadius, SpikeRadius, spikeColor, ci);
        ci = AddSpike(bi, sys, bodies, sphereBody, StartX-d, StartY,   0f,   SpikeLen, SpikeRadius, SpikeRadius, spikeColor, ci);
        ci = AddSpike(bi, sys, bodies, sphereBody, StartX,   StartY+d, 0f,   SpikeRadius, SpikeLen, SpikeRadius, spikeColor, ci);
        ci = AddSpike(bi, sys, bodies, sphereBody, StartX,   StartY-d, 0f,   SpikeRadius, SpikeLen, SpikeRadius, spikeColor, ci);
        ci = AddSpike(bi, sys, bodies, sphereBody, StartX,   StartY,   d,    SpikeRadius, SpikeRadius, SpikeLen, spikeColor, ci);
        ci = AddSpike(bi, sys, bodies, sphereBody, StartX,   StartY,   -d,   SpikeRadius, SpikeRadius, SpikeLen, spikeColor, ci);

        // ── Launch: set matching velocity on sphere + all spikes ──────────
        // v_x = 6, v_y = 1 (slight arc). Wall at x=12 → impact t≈2s, y≈4.4m.
        using var launchVel = new JPH.Vec3(6f, 1f, 0f);
        bi.SetLinearVelocity(sphereBody.GetID(), launchVel);
        for (int k = 0; k < MaxConstraints; k++)
            bi.SetLinearVelocity(_spikeIds[k], launchVel);

        // ── Target wall: 5 rows × 4 columns at x=12 ──────────────────────
        const float WallX = 12f;
        const float BoxHE = 1.0f;
        var rowColors = new Vector3[]
        {
            new Vector3(0.85f, 0.65f, 0.40f),
            new Vector3(0.70f, 0.55f, 0.35f),
            new Vector3(0.80f, 0.60f, 0.38f),
            new Vector3(0.65f, 0.50f, 0.30f),
            new Vector3(0.75f, 0.58f, 0.36f),
        };
        float[] colsZ = { -4.5f, -1.5f, 1.5f, 4.5f };

        for (int row = 0; row < 5; row++)
        {
            float y = BoxHE + row * BoxHE * 2f;
            foreach (float z in colsZ)
            {
                AddBox(bi, bodies,
                    BoxHE, BoxHE, BoxHE,
                    WallX, y, z,
                    Quaternion.Identity,
                    JPH.EMotionType.Dynamic, LayerMoving,
                    rowColors[row],
                    friction: 0.5f, restitution: 0.15f);
            }
        }
    }

    private int AddSpike(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        JPH.Body          sphereBody,
        float px, float py, float pz,
        float hx, float hy, float hz,
        Vector3 color,
        int ci)
    {
        using var spHalf = new JPH.Vec3(hx, hy, hz);
        using var spSS   = new JPH.BoxShapeSettings(spHalf);
        using var spCS   = new JPH.BodyCreationSettings();
        spCS.SetShapeSettings(spSS);
        spCS.mPosition.Set(px, py, pz);
        spCS.mMotionType  = JPH.EMotionType.Dynamic;
        spCS.mObjectLayer = LayerMoving;
        spCS.mRestitution = 0.2f;
        spCS.mFriction    = 0.4f;

        var spike = bi.CreateBody(spCS);
        bi.AddBody(spike!.GetID(), JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId = spike.GetID(),
            color  = color,
            shape  = RenderShape.Box,
            scale  = new Vector3(hx * 2f, hy * 2f, hz * 2f)
        });
        _spikeIds[ci] = spike.GetID();

        using var fcs = new JPH.FixedConstraintSettings();
        fcs.mAutoDetectPoint = true;
        _constraints[ci] = fcs.Create(sphereBody, spike);
        sys.AddConstraint(_constraints[ci]);

        return ci + 1;
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
        Distance  = 50,
        Latitude  = 12,
        Longitude = -20,
        Center    = new Vector3(6f, 10f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000f
    };
}
