using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of ContactListenerTest.cpp:
/// 4 dynamic bodies fall onto a floor. A ContactListenerTrampolineManaged
/// is installed to demonstrate two effects:
///
///   OnContactValidate  — body1 (red box) and body2 (yellow tilted box) are
///                        configured to never collide with each other.
///   OnContactAdded     — whenever body1 is involved in a new contact, its
///                        combined restitution is forced to 1.0 so it
///                        bounces off the floor like a super-ball.
///
/// body3 is a sphere and body4 is a capsule — both behave normally.
/// </summary>
public sealed class Demo_ContactListener : DemoBase
{
    public override string Name     => "Contact Listener";
    public override string Category => "General";

    // Body IDs set during Init, read inside the contact callbacks.
    JPH.BodyID _body1;
    JPH.BodyID _body2;

    JPH.ContactListenerTrampolineManaged? _listener;

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 60,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(7.5f, 5f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000.0f
    };

    public override void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies);

        // body1: red box, half-extents (0.5, 1, 2) — will bounce (restitution set via listener)
        _body1 = AddBox(bi, bodies, 0.5f, 1f, 2f,
            0f, 10f, 0f,
            Quaternion.Identity,
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.9f, 0.2f, 0.2f),
            friction: 0.5f,
            restitution: 0f,
            allowSleeping: false);

        // body2: yellow box, same shape but tilted 45° around Z — never collides with body1
        _body2 = AddBox(bi, bodies, 0.5f, 1f, 2f,
            5f, 10f, 0f,
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 4f),
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.9f, 0.85f, 0.1f),
            friction: 0.5f,
            restitution: 0.1f,
            allowSleeping: false);

        // body3: sphere, radius 2 — normal behaviour
        AddSphere(bi, bodies, 2f,
            10f, 10f, 0f,
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.2f, 0.6f, 0.9f),
            friction: 0.5f,
            restitution: 0.3f,
            allowSleeping: false);

        // body4: capsule (radius 1, halfCylinderHeight 5) — normal behaviour
        AddCapsule(bi, bodies, 1f, 5f,
            15f, 10f, 0f,
            Quaternion.Identity,
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.4f, 0.85f, 0.4f),
            friction: 0.5f,
            restitution: 0.3f,
            allowSleeping: false);
    }

    public override void Activate(JPH.PhysicsSystem sys)
    {
        _listener = new JPH.ContactListenerTrampolineManaged();

        // Capture local copies so the lambdas don't need to close over 'this'
        var body1 = _body1;
        var body2 = _body2;

        uint body1Packed = body1.GetIndexAndSequenceNumber();
        uint body2Packed = body2.GetIndexAndSequenceNumber();

        _listener.SetOnContactValidate((b1, b2, baseOffset, result) =>
        {
            // body1 and body2 must never collide with each other
            uint id1 = b1.GetID().GetIndexAndSequenceNumber();
            uint id2 = b2.GetID().GetIndexAndSequenceNumber();
            if ((id1 == body1Packed && id2 == body2Packed) ||
                (id1 == body2Packed && id2 == body1Packed))
                return JPH.ValidateResult.RejectAllContactsForThisBodyPair;
            return JPH.ValidateResult.AcceptAllContactsForThisBodyPair;
        });

        _listener.SetOnContactAdded((b1, b2, manifold, settings) =>
        {
            // When body1 makes new contact: force combined restitution to 1 (super-bouncy)
            uint id1 = b1.GetID().GetIndexAndSequenceNumber();
            uint id2 = b2.GetID().GetIndexAndSequenceNumber();
            if (id1 == body1Packed || id2 == body1Packed)
                settings.mCombinedRestitution = 1.0f;
        });

        sys.SetContactListener(_listener.Inner);
    }

    public override void Deactivate(JPH.PhysicsSystem sys)
    {
        sys.SetContactListener(null);
        _listener?.Dispose();
        _listener = null;
    }
}
