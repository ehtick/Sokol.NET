using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of ConveyorBeltTest.cpp.
/// Static belt platforms use a ContactListener to set surface velocities on every
/// contact so cargo boxes slide as if the belt is moving.
///
/// Scene:
///   4 linear static belts in a cross pattern (each rotated 90°, 1° tilt).
///   1 dynamic belt resting on 2 cylinders.
///   1 angular belt (flat square that spins cargo).
/// </summary>
public sealed class Demo_ConveyorBelt : DemoBase
{
    public override string Name     => "Conveyor Belt";
    public override string Category => "General";

    HashSet<uint> _linearBeltIds = new();
    uint          _angularBeltId;
    JPH.ContactListenerTrampolineManaged? _listener;

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 130,
        Latitude  = 35,
        Longitude = 225,
        Center    = new Vector3(0, 0, 0),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 2000.0f
    };

    public override void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies);

        const float cBeltWidth  = 10f;
        const float cBeltLength = 50f;

        _linearBeltIds.Clear();

        // ── 4 linear static belts in a cross (0°/90°/180°/270°, 1° X tilt) ─
        for (int i = 0; i < 4; i++)
        {
            var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f * MathF.PI * i)
                    * Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 180f);
            // Position = rot * (cBeltLength, 6, cBeltWidth)  — matches C++ exactly
            var pos = Vector3.Transform(new Vector3(cBeltLength, 6f, cBeltWidth), rot);

            var id = AddBox(bi, bodies, cBeltWidth, 0.1f, cBeltLength,
                pos.X, pos.Y, pos.Z, rot,
                JPH.EMotionType.Static, LayerNonMoving,
                new Vector3(0.4f, 0.6f, 0.4f),
                friction: 0.25f * (i + 1));
            _linearBeltIds.Add(id.GetIndexAndSequenceNumber());
        }

        // ── 11 cargo boxes with decreasing friction ──────────────────────
        for (int i = 0; i <= 10; i++)
        {
            AddBox(bi, bodies, 2f, 2f, 2f,
                -cBeltLength + i * 10f, 10f, -cBeltLength,
                Quaternion.Identity,
                JPH.EMotionType.Dynamic, LayerMoving,
                new Vector3(0.8f, 0.35f, 0.2f),
                friction: MathF.Max(0f, 1f - 0.1f * i));
        }

        // ── 2 cylinders (laid on their side) ────────────────────────────
        var cylRot = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.5f * MathF.PI);
        AddCylinder(bi, bodies, 6f, 1f, -25f, 1f, -20f, cylRot,
            JPH.EMotionType.Dynamic, LayerMoving, new Vector3(0.5f, 0.5f, 0.8f));
        AddCylinder(bi, bodies, 6f, 1f, -25f, 1f,  20f, cylRot,
            JPH.EMotionType.Dynamic, LayerMoving, new Vector3(0.5f, 0.5f, 0.8f));

        // ── Dynamic belt resting on the cylinders (also a linear belt) ──
        var dynId = AddBox(bi, bodies, 5f, 0.1f, 25f,
            -25f, 3f, 0f, Quaternion.Identity,
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.4f, 0.7f, 0.4f), friction: 0.5f);
        _linearBeltIds.Add(dynId.GetIndexAndSequenceNumber());

        // Cargo on the dynamic belt
        AddBox(bi, bodies, 2f, 2f, 2f,
            -25f, 6f, 15f, Quaternion.Identity,
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.8f, 0.5f, 0.3f), friction: 1f);

        // ── Angular belt ─────────────────────────────────────────────────
        var angId = AddBox(bi, bodies, 20f, 0.1f, 20f,
            10f, 3f, 0f, Quaternion.Identity,
            JPH.EMotionType.Static, LayerNonMoving,
            new Vector3(0.7f, 0.5f, 0.2f), friction: 0.5f);
        _angularBeltId = angId.GetIndexAndSequenceNumber();

        // Cargo on the angular belt (7 boxes, decreasing friction)
        for (int i = 0; i <= 6; i++)
        {
            AddBox(bi, bodies, 2f, 2f, 2f,
                10f, 10f, -15f + 5f * i,
                Quaternion.Identity,
                JPH.EMotionType.Dynamic, LayerMoving,
                new Vector3(0.4f, 0.4f + 0.05f * i, 0.9f),
                friction: MathF.Max(0f, 1f - 0.1f * i));
        }
    }

    public override void Activate(JPH.PhysicsSystem sys)
    {
        var linearIds  = _linearBeltIds;
        uint angularId = _angularBeltId;
        const float angRad = 10f * MathF.PI / 180f;  // 10 deg/s, matches C++ DegreesToRadians(10.0f)

        void ApplyBeltContact(JPH.Const_Body b1, JPH.Const_Body b2,
                               JPH.ContactSettings settings)
        {
            uint id1 = b1.GetID().GetIndexAndSequenceNumber();
            uint id2 = b2.GetID().GetIndexAndSequenceNumber();

            // ── Linear belts ──────────────────────────────────────────────
            bool b1Lin = linearIds.Contains(id1);
            bool b2Lin = linearIds.Contains(id2);
            if (b1Lin || b2Lin)
            {
                using var localVel = new JPH.Vec3(0f, 0f, -10f);  // 10 m/s, matches C++ cLocalSpaceVelocity
                float v1x = 0f, v1y = 0f, v1z = 0f;
                float v2x = 0f, v2y = 0f, v2z = 0f;
                if (b1Lin)
                {
                    using var q1 = b1.GetRotation();
                    using var w1 = q1 * localVel;
                    v1x = w1.GetX(); v1y = w1.GetY(); v1z = w1.GetZ();
                }
                if (b2Lin)
                {
                    using var q2 = b2.GetRotation();
                    using var w2 = q2 * localVel;
                    v2x = w2.GetX(); v2y = w2.GetY(); v2z = w2.GetZ();
                }
                // mRelativeLinearSurfaceVelocity = body2_vel - body1_vel
                settings.mRelativeLinearSurfaceVelocity.Set(v2x - v1x, v2y - v1y, v2z - v1z);
            }

            // ── Angular belt ──────────────────────────────────────────────
            bool b1Ang = id1 == angularId;
            bool b2Ang = id2 == angularId;
            if (b1Ang || b2Ang)
            {
                // World-space angular velocity of whichever body is the belt.
                // (Static belt has identity rotation so world = local.)
                float a1x = 0f, a1y = 0f, a1z = 0f;
                float a2x = 0f, a2y = 0f, a2z = 0f;
                if (b1Ang)
                {
                    using var q1  = b1.GetRotation();
                    using var la  = new JPH.Vec3(0f, angRad, 0f);
                    using var wa  = q1 * la;
                    a1x = wa.GetX(); a1y = wa.GetY(); a1z = wa.GetZ();
                }
                if (b2Ang)
                {
                    using var q2  = b2.GetRotation();
                    using var la  = new JPH.Vec3(0f, angRad, 0f);
                    using var wa  = q2 * la;
                    a2x = wa.GetX(); a2y = wa.GetY(); a2z = wa.GetZ();
                }

                // mRelativeAngularSurfaceVelocity = body2_ang - body1_ang
                settings.mRelativeAngularSurfaceVelocity.Set(a2x - a1x, a2y - a1y, a2z - a1z);

                // Linear correction: body2_ang × (com1 − com2)
                // Only body2's angular velocity contributes here
                // (body1's angular motion is fully accounted for by mRelativeAngularSurfaceVelocity).
                using var com1  = b1.GetCenterOfMassPosition();
                using var com2  = b2.GetCenterOfMassPosition();
                using var delta = com1 - com2;
                using var ang2  = new JPH.Vec3(a2x, a2y, a2z);
                using var addLin = ang2.Cross(delta);

                // Set linear surface velocity (angular belt overrides any prior linear assignment)
                settings.mRelativeLinearSurfaceVelocity.Set(addLin.GetX(), addLin.GetY(), addLin.GetZ());
            }
        }

        _listener = new JPH.ContactListenerTrampolineManaged();
        _listener.SetOnContactAdded((b1, b2, manifold, settings) =>
            ApplyBeltContact(b1, b2, settings));
        _listener.SetOnContactPersisted((b1, b2, manifold, settings) =>
            ApplyBeltContact(b1, b2, settings));

        sys.SetContactListener(_listener.Inner);
    }

    public override void Deactivate(JPH.PhysicsSystem sys)
    {
        sys.SetContactListener(null);
        _listener?.Dispose();
        _listener = null;
    }
}
