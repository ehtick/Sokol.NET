using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// A long board balanced on a central HingeConstraint (attached to the world via
/// Body::SFixedToWorld).  A heavy sphere falls onto the left end, launching a
/// stack of lighter boxes off the right end.
/// Demonstrates HingeConstraint and torque / lever mechanics.
/// </summary>
public class Demo_Seesaw : DemoBase
{
    public override string Name     => "Seesaw";
    public override string Category => "Constraints";

    private JPH.TwoBodyConstraint? _hinge;

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        // Floor
        AddFloor(bi, bodies, 55f, 0.5f, 20f);

        // ── Fulcrum stand (visual only — static) ──────────────────────────
        // Top sits just below the board (board bottom = 8.5 − 0.5 = 8.0;
        // stand top = 3.0 + 3.0 = 6.0 → comfortable gap so they don't clash).
        AddBox(bi, bodies,
            1.5f, 3.0f, 1.5f,
            0f, 3.0f, 0f,
            Quaternion.Identity,
            JPH.EMotionType.Static, LayerNonMoving,
            new Vector3(0.30f, 0.25f, 0.22f));   // dark wood

        // ── Seesaw board — held at its centre by the hinge ────────────────
        const float BoardHX  = 16f;
        const float BoardHY  = 0.45f;
        const float BoardHZ  = 1.8f;
        const float PivotY   = 8.5f;   // board centre height

        using var boardHalf = new JPH.Vec3(BoardHX, BoardHY, BoardHZ);
        using var boardSS   = new JPH.BoxShapeSettings(boardHalf);
        using var boardCS   = new JPH.BodyCreationSettings();
        boardCS.SetShapeSettings(boardSS);
        boardCS.mPosition.Set(0f, PivotY, 0f);
        boardCS.mMotionType  = JPH.EMotionType.Dynamic;
        boardCS.mObjectLayer = LayerMoving;
        boardCS.mFriction    = 0.7f;
        boardCS.mRestitution = 0.05f;
        boardCS.mAngularDamping = 0.1f;

        var board = bi.CreateBody(boardCS);
        bi.AddBody(board!.GetID(), JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId = board.GetID(),
            color  = new Vector3(0.70f, 0.50f, 0.25f),   // warm wood
            shape  = RenderShape.Box,
            scale  = new Vector3(BoardHX * 2f, BoardHY * 2f, BoardHZ * 2f)
        });

        // ── HingeConstraint: world anchor → board centre ──────────────────
        // Hinge axis = Z (board rotates in the XY plane).
        // No angular limits → full spin allowed.
        var worldAnchor = JPH.Body.SFixedToWorld;

        using var hs = new JPH.HingeConstraintSettings();
        hs.mPoint1.Set(0f, PivotY, 0f);
        hs.mPoint2.Set(0f, PivotY, 0f);
        hs.mHingeAxis1.Set(0f, 0f, 1f);
        hs.mHingeAxis2.Set(0f, 0f, 1f);
        hs.mNormalAxis1.Set(1f, 0f, 0f);
        hs.mNormalAxis2.Set(1f, 0f, 0f);

        _hinge = hs.Create(worldAnchor, board);
        sys.AddConstraint(_hinge);

        // ── Payload: small boxes stacked on the right half of the board ───
        // Board surface at y = PivotY + BoardHY = 8.95 ≈ 9.0
        const float BoxHE    = 0.9f;
        float       boardTop = PivotY + BoardHY;
        float       stackX   = 11f;    // right-side placement
        var payloadColors = new Vector3[]
        {
            new Vector3(0.20f, 0.50f, 0.85f),  // blue
            new Vector3(0.85f, 0.30f, 0.20f),  // red
            new Vector3(0.25f, 0.75f, 0.35f),  // green
            new Vector3(0.90f, 0.75f, 0.15f),  // yellow
        };
        for (int k = 0; k < 4; k++)
        {
            float y = boardTop + BoxHE + k * BoxHE * 2f;
            AddBox(bi, bodies,
                BoxHE, BoxHE, BoxHE,
                stackX, y, 0f,
                Quaternion.Identity,
                JPH.EMotionType.Dynamic, LayerMoving,
                payloadColors[k],
                friction: 0.5f, restitution: 0.2f);
        }

        // ── Heavy sphere — falls onto the left end of the board ───────────
        const float SphereR = 2.0f;
        float dropX = -11f;                 // left side of board
        float dropY = PivotY + 16f;         // well above the board

        using var sphereSS = new JPH.SphereShapeSettings(SphereR);
        using var sphereCS = new JPH.BodyCreationSettings();
        sphereCS.SetShapeSettings(sphereSS);
        sphereCS.mPosition.Set(dropX, dropY, 0f);
        sphereCS.mMotionType  = JPH.EMotionType.Dynamic;
        sphereCS.mObjectLayer = LayerMoving;
        sphereCS.mFriction    = 0.4f;
        sphereCS.mRestitution = 0.1f;

        var sphereId = bi.CreateAndAddBody(sphereCS, JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId = sphereId,
            color  = new Vector3(0.80f, 0.20f, 0.15f),  // cannon-ball red
            shape  = RenderShape.Sphere,
            scale  = new Vector3(SphereR * 2f)
        });
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        if (_hinge != null)
        {
            sys.RemoveConstraint(_hinge);
            _hinge = null;
        }
    }

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 60,
        Latitude  = 15,
        Longitude = 0,
        Center    = new Vector3(0f, 10f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000f
    };
}
