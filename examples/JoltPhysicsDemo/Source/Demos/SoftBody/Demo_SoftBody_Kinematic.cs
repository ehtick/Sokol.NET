using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Demonstrates a soft-body sphere whose vertex 0 is kinematically driven:
/// its inverse mass is set to zero so it acts as a static pin, and in Update
/// its velocity is toggled to make the body bounce back and forth along Z.
/// Ported from SoftBodyKinematicTest.cpp.
/// </summary>
public class Demo_SoftBody_Kinematic : DemoBase
{
    public override string Name     => "SoftBody: Kinematic";
    public override string Category => "Soft Body";

    JPH.Body? _body;
    float     _dir = 1f;

    public override void Init(JPH.BodyInterface bi, JPH.PhysicsSystem sys, List<PhysicsBody> bodies, Random rng)
    {
        AddFloor(bi, bodies);

        var faces    = new List<(uint, uint, uint)>();
        var settings = CreateSphereSettings(1.0f, 20, 10, faces);

        using var pos = new JPH.Vec3(0f, 5f, 0f);
        using var rot = new JPH.Quat(0f, 0f, 0f, 1f);
        using var cs  = new JPH.SoftBodyCreationSettings(
            settings, pos, rot, LayerMoving);
        cs.mPressure = 2000f;

        // CreateSoftBody assigns a BodyID but does NOT add to the simulation yet.
        _body = bi.CreateSoftBody(cs);
        if (_body is null) return;

        // Pin vertex 0 (invMass = 0 → kinematically driven)
        var mp = (JPH.SoftBodyMotionProperties)_body.GetMotionProperties()!;
        mp.GetVertex(0).mInvMass = 0f;

        var id = _body.GetID();
        bi.AddBody(in id, JPH.EActivation.Activate);

        // Build render entry using helper
        uint vertCount = (settings).SoftBodySettingsGetVertexCount();
        BuildSoftBodyRenderEntry(id, vertCount, faces, new Vector3(0.2f, 0.8f, 0.3f));
    }

    public override void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        if (_body is null) return;

        var mp  = (JPH.SoftBodyMotionProperties)_body.GetMotionProperties()!;
        var v0  = mp.GetVertex(0);

        using var currentPos = _body.GetCenterOfMassPosition();
        float z = currentPos.GetZ();

        if (_dir > 0f && z > 10f) _dir = -1f;
        else if (_dir < 0f && z < -10f) _dir = 1f;

        // Drive the pinned vertex at 5 m/s along Z
        v0.mVelocity.Set(0f, 0f, 5f * _dir);
    }
}
