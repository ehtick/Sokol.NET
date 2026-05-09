using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of ModifyMassTest.cpp:
/// Two spheres are launched toward each other. A contact listener overrides
/// the inverse-mass scale of sphere A on each collision, cycling through
/// four mass-ratio presets every 2 seconds:
///   0 — equal mass (normal elastic collision)
///   1 — sphere A has infinite mass (B bounces back, A keeps going)
///   2 — sphere A has half the effective mass
///   3 — sphere A has double the effective mass
/// After each preset cycle the bodies are reset to their start positions
/// and velocities.
/// </summary>
public sealed class Demo_ModifyMass : DemoBase
{
    public override string Name     => "Modify Mass";
    public override string Category => "General";

    // Mass-scale presets for sphere A (index cycles 0..3)
    static readonly float[] InvMassScalePresets = { 1f, 0f, 2f, 0.5f };

    const float ResetInterval = 2.0f;
    const float Speed         = 10f;

    JPH.BodyID _sphereA;
    JPH.BodyID _sphereB;

    float _timer;
    int   _preset;

    // Thread-safe preset snapshot read by contact callbacks
    volatile int _currentPreset;

    JPH.ContactListenerTrampolineManaged? _listener;

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 30,
        Latitude  = 15,
        Longitude = 0,
        Center    = new Vector3(0f, 5f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 500.0f
    };

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        _timer  = 0f;
        _preset = 0;
        _currentPreset = 0;

        AddFloor(bi, bodies);

        // Sphere A — heading right (+X)
        _sphereA = AddSphere(bi, bodies, 1f,
            -5f, 5f, 0f,
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.9f, 0.3f, 0.3f),
            restitution: 1f,
            allowSleeping: false);
        bi.SetUserData(_sphereA, 0UL);

        // Sphere B — heading left (-X)
        _sphereB = AddSphere(bi, bodies, 1f,
            5f, 5f, 0f,
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.3f, 0.5f, 0.9f),
            restitution: 1f,
            allowSleeping: false);

        // Launch
        using var velA = new JPH.Vec3(Speed, 0f, 0f);
        using var velB = new JPH.Vec3(-Speed, 0f, 0f);
        bi.SetLinearVelocity(_sphereA, velA);
        bi.SetLinearVelocity(_sphereB, velB);
    }

    public override void Activate(JPH.PhysicsSystem sys)
    {
        var sphereAPacked = _sphereA.GetIndexAndSequenceNumber();

        _listener = new JPH.ContactListenerTrampolineManaged();

        // Apply mass-scale override whenever sphere A is involved
        void ApplyScale(
            JPH.Const_Body b1,
            JPH.Const_Body b2,
            JPH.ContactSettings settings)
        {
            float scale = InvMassScalePresets[_currentPreset];
            uint id1 = b1.GetID().GetIndexAndSequenceNumber();
            if (id1 == sphereAPacked)
            {
                settings.mInvMassScale1    = scale;
                settings.mInvInertiaScale1 = scale;
            }
            else
            {
                settings.mInvMassScale2    = scale;
                settings.mInvInertiaScale2 = scale;
            }
        }

        _listener.SetOnContactAdded((b1, b2, manifold, settings) =>
        {
            // Floor (static) gets normal collision response — match C++ behaviour
            if (!b1.IsDynamic() || !b2.IsDynamic()) return;
            uint id1 = b1.GetID().GetIndexAndSequenceNumber();
            uint id2 = b2.GetID().GetIndexAndSequenceNumber();
            if (id1 == sphereAPacked || id2 == sphereAPacked)
                ApplyScale(b1, b2, settings);
        });

        _listener.SetOnContactPersisted((b1, b2, manifold, settings) =>
        {
            if (!b1.IsDynamic() || !b2.IsDynamic()) return;
            uint id1 = b1.GetID().GetIndexAndSequenceNumber();
            uint id2 = b2.GetID().GetIndexAndSequenceNumber();
            if (id1 == sphereAPacked || id2 == sphereAPacked)
                ApplyScale(b1, b2, settings);
        });

        sys.SetContactListener(_listener.Inner);
    }

    public override unsafe void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        _timer += dt;
        if (_timer < ResetInterval) return;
        _timer -= ResetInterval;

        _preset = (_preset + 1) % InvMassScalePresets.Length;
        _currentPreset = _preset;

        // Reset sphere A
        using var posA = new JPH.Vec3(-5f, 5f, 0f);
        using var posB = new JPH.Vec3( 5f, 5f, 0f);
        using var velA = new JPH.Vec3(Speed, 0f, 0f);
        using var velB = new JPH.Vec3(-Speed, 0f, 0f);

        bi.SetPosition(_sphereA, posA, JPH.EActivation.Activate);
        bi.SetLinearVelocity(_sphereA, velA);

        bi.SetPosition(_sphereB, posB, JPH.EActivation.Activate);
        bi.SetLinearVelocity(_sphereB, velB);
    }

    public override void Deactivate(JPH.PhysicsSystem sys)
    {
        sys.SetContactListener(null);
        _listener?.Dispose();
        _listener = null;
    }
}
