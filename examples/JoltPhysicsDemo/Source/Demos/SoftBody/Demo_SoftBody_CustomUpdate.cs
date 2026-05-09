using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Demonstrates manually driving the soft body update loop via
/// SoftBodyMotionProperties.CustomUpdate, instead of letting the physics
/// system update the body automatically.
/// Ported from SoftBodyCustomUpdateTest.cpp.
/// </summary>
public class Demo_SoftBody_CustomUpdate : DemoBase
{
    public override string Name     => "SoftBody: Custom Update";
    public override string Category => "Soft Body";

    JPH.Body?          _body;
    JPH.PhysicsSystem? _sys;

    public override void Init(JPH.BodyInterface bi, JPH.PhysicsSystem sys, List<PhysicsBody> bodies, Random rng)
    {
        _sys = sys;

        AddFloor(bi, bodies);

        var faces    = new List<(uint, uint, uint)>();
        var settings = CreateSphereSettings(1f, 10, 20, faces);

        using var pos = new JPH.Vec3(0f, 5f, 0f);
        using var rot = new JPH.Quat(0f, 0f, 0f, 1f);
        using var cs  = new JPH.SoftBodyCreationSettings(
            settings, pos, rot, LayerMoving);
        cs.mPressure = 2000f;

        // Create but do NOT add to the physics system — we step it manually via CustomUpdate.
        // (Matches C++ SoftBodyCustomUpdateTest which calls CreateSoftBody without AddBody.)
        _body = bi.CreateSoftBody(cs);

        uint vertCount = (settings).SoftBodySettingsGetVertexCount();
        RegisterStandaloneSoftBody(_body, vertCount, faces, new Vector3(1.0f, 0.4f, 0.1f));
    }

    public override void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        if (_body is null || _sys is null) return;

        var mp = (JPH.SoftBodyMotionProperties)_body.GetMotionProperties()!;
        mp.CustomUpdate(dt, _body, _sys);
    }
}
