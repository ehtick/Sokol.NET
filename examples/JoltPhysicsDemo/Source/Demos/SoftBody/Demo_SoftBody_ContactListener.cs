using System.Collections.Generic;
using System.Numerics;
using static Sokol.SG;

/// <summary>
/// Port of SoftBodyContactListenerTest.cpp.
/// Cycles through 10 contact-listener modes every 2.5 seconds, recreating the cloth and sphere.
/// </summary>
public class Demo_SoftBody_ContactListener : DemoBase
{
    public override string Name     => "SoftBody: Contact Listener";
    public override string Category => "Soft Body";

    static readonly string[] CycleNames =
    {
        "Accept contact",
        "Sphere 10x mass",
        "Cloth 10x mass",
        "Sphere infinite mass",
        "Cloth infinite mass",
        "Sensor contact",
        "Reject contact",
        "Kinematic Sphere",
        "Kinematic Sphere, cloth infinite mass",
        "Kinematic Sphere, sensor contact",
    };

    int           _cycle = 0;
    float         _time  = 0f;
    JPH.BodyID    _clothBodyId;
    JPH.BodyID    _otherId;

    JPH.SoftBodyContactListenerTrampolineManaged? _listener;

    public override void Init(JPH.BodyInterface bi, JPH.PhysicsSystem sys, List<PhysicsBody> bodies, Random rng)
    {
        _cycle = 0;
        _time  = 0f;

        _listener = new JPH.SoftBodyContactListenerTrampolineManaged();
        _listener.SetOnValidate(OnValidate);
        sys.SetSoftBodyContactListener(_listener.Inner);

        AddFloor(bi, bodies);
        StartCycle(bi, bodies);
    }

    public override void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        _time += dt;
        if (_time > 2.5f)
        {
            _time  = 0f;
            _cycle = (_cycle + 1) % 10;
            RemoveCyclingBodies(bi, bodies);
            StartCycle(bi, bodies);
        }
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        sys.SetSoftBodyContactListener(null);
        _listener?.Dispose();
        _listener = null;
    }

    // ── contact listener ─────────────────────────────────────────────────────

    JPH.SoftBodyValidateResult OnValidate(
        JPH.Const_Body softBody, JPH.Const_Body otherBody, JPH.SoftBodyContactSettings settings)
    {
        switch (_cycle)
        {
            case 1: settings.mInvMassScale2 = 0.1f;   break; // Sphere 10x mass
            case 2: settings.mInvMassScale1 = 0.1f;   break; // Cloth 10x mass
            case 3: settings.mInvMassScale2 = 0f;
                    settings.mInvInertiaScale2 = 0f;   break; // Sphere infinite mass
            case 4: settings.mInvMassScale1 = 0f;     break; // Cloth infinite mass
            case 5: settings.mIsSensor = true;         break; // Sensor contact
            case 6: return JPH.SoftBodyValidateResult.RejectContact;
            case 8: settings.mInvMassScale1 = 0f;     break; // Kinematic, cloth infinite mass
            case 9: settings.mIsSensor = true;         break; // Kinematic, sensor contact
        }
        return JPH.SoftBodyValidateResult.AcceptContact;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    void StartCycle(JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        // ── cloth ────────────────────────────────────────────────────────────
        var clothFaces = new List<(uint, uint, uint)>();
        var clothSettings = CreateClothWithFixatedCornersSettings(15, 15, 0.75f, clothFaces);

        float sinHalfPi8 = MathF.Sin(MathF.PI / 8f);
        float cosHalfPi8 = MathF.Cos(MathF.PI / 8f);
        _clothBodyId = RegisterSoftBody(bi, clothSettings, clothFaces,
            0f, 5f, 0f,
            0f, sinHalfPi8, 0f, cosHalfPi8,
            new Vector3(0.2f, 0.7f, 0.4f),
            cs => { cs.mUpdatePosition = false; cs.mMakeRotationIdentity = false; });

        // ── sphere ───────────────────────────────────────────────────────────
        bool kinematic = _cycle > 6;
        var motionType = kinematic ? JPH.EMotionType.Kinematic : JPH.EMotionType.Dynamic;

        _otherId = AddSphere(bi, bodies, 1f, 0f, 7f, 0f,
            motionType, LayerMoving,
            new Vector3(0.8f, 0.3f, 0.3f), mass: 100f);

        using var vel = new JPH.Vec3(0f, -2.5f, 0f);
        bi.SetLinearVelocity(_otherId, vel);

        SetBodyLabel(_otherId, CycleNames[_cycle]);
    }

    void RemoveCyclingBodies(JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        // Remove cloth
        uint clothRaw = _clothBodyId.GetIndexAndSequenceNumber();
        int sbIdx = _softBodies!.FindIndex(e => e.bodyId.GetIndexAndSequenceNumber() == clothRaw);
        if (sbIdx >= 0)
        {
            var entry = _softBodies[sbIdx];
            sg_destroy_buffer(entry.vertexBuf);
            sg_destroy_buffer(entry.indexBuf);
            _softBodies.RemoveAt(sbIdx);
        }
        bi.RemoveBody(_clothBodyId);
        bi.DestroyBody(_clothBodyId);

        // Remove sphere
        uint otherRaw = _otherId.GetIndexAndSequenceNumber();
        int bIdx = bodies.FindIndex(b => b.bodyId.GetIndexAndSequenceNumber() == otherRaw);
        if (bIdx >= 0) bodies.RemoveAt(bIdx);
        bi.RemoveBody(_otherId);
        bi.DestroyBody(_otherId);

        _bodyLabels?.RemoveAll(e => e.id.GetIndexAndSequenceNumber() == otherRaw);
    }
}
