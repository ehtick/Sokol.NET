using Sokol;
using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of BoatTest.cpp — simulates a boat floating on animated waves.
///
/// A convex-hull boat shape with an OffsetCenterOfMassShape sits on a sinusoidal
/// water surface. A sensor detects which bodies are in the water; buoyancy impulses
/// are applied each frame using the local surface normal from the wave equation.
///
/// Arrow keys: Up/Down = throttle, Left/Right = steering.
/// </summary>
public sealed class Demo_Boat : DemoBase
{
    public override string Name              => "Boat";
    public override string Category          => "Water";
    public override bool   CameraFollowsPlayer => true;
    public override VirtualControlsType VirtualControls => VirtualControlsType.Arrows;

    // ── Boat geometry constants (from BoatTest.h) ─────────────────────────────
    const float CMaxWaterHeight      = 5.0f;
    const float CMinWaterHeight      = 3.0f;
    const float CWaterWidth          = 100.0f;

    const float CHalfBoatLength      = 4.0f;
    const float CHalfBoatTopWidth    = 1.5f;
    const float CHalfBoatBottomWidth = 1.2f;
    const float CBoatBowLength       = 2.0f;
    const float CHalfBoatHeight      = 0.75f;

    const float CBoatMass            = 1000.0f;
    const float CBoatBuoyancy        = 3.0f;
    const float CBoatLinearDrag      = 0.5f;
    const float CBoatAngularDrag     = 0.7f;

    const float CBarrelMass          = 50.0f;
    const float CBarrelBuoyancy      = 1.5f;
    const float CBarrelLinearDrag    = 0.5f;
    const float CBarrelAngularDrag   = 0.1f;

    const float CForwardAcceleration = 15.0f;
    const float CSteerAcceleration   = 1.5f;

    // ── State ──────────────────────────────────────────────────────────────────
    JPH.BodyID _boatBodyId;
    uint       _waterSensorPacked;
    float      _time;

    // Bodies currently in the water (packed BodyID), with ref-count for overlapping sub-shapes
    readonly Dictionary<uint, int> _bodiesInWater = new();
    readonly object                _lock          = new();

    JPH.ContactListenerTrampolineManaged? _listener;

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 70,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(0f, CMaxWaterHeight, 0f),
    };

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random random)
    {
        _time = 0f;

        // ── Boat convex hull ──────────────────────────────────────────────────────
        {
            var pts = new[]
            {
                // top 4 (wider)
                new JPH.Vec3f(-CHalfBoatTopWidth,     CHalfBoatHeight,  -CHalfBoatLength),
                new JPH.Vec3f( CHalfBoatTopWidth,     CHalfBoatHeight,  -CHalfBoatLength),
                new JPH.Vec3f(-CHalfBoatTopWidth,     CHalfBoatHeight,   CHalfBoatLength),
                new JPH.Vec3f( CHalfBoatTopWidth,     CHalfBoatHeight,   CHalfBoatLength),
                // bottom 4 (narrower)
                new JPH.Vec3f(-CHalfBoatBottomWidth, -CHalfBoatHeight,  -CHalfBoatLength),
                new JPH.Vec3f( CHalfBoatBottomWidth, -CHalfBoatHeight,  -CHalfBoatLength),
                new JPH.Vec3f(-CHalfBoatBottomWidth, -CHalfBoatHeight,   CHalfBoatLength),
                new JPH.Vec3f( CHalfBoatBottomWidth, -CHalfBoatHeight,   CHalfBoatLength),
                // bow point
                new JPH.Vec3f(0f,                    CHalfBoatHeight,   CHalfBoatLength + CBoatBowLength),
            };

            using var hullSS   = JPH.ConvexHullShapeSettingsFromPoints(pts, 0.05f);
            using var comOffset = new JPH.Vec3(0f, -CHalfBoatHeight, 0f);
            using var comSS    = new JPH.OffsetCenterOfMassShapeSettings(comOffset, hullSS);
            using var cs       = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(comSS);
            cs.mPosition.Set(0f, CMaxWaterHeight + 2f, 0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            cs.SetOverrideMassProperties(1); // CalculateInertia
            cs.SetMassOverride(CBoatMass);

            // Resolve shape for hull mesh rendering
            var baseShape  = cs.GetShape()!;
            // OffsetCenterOfMassShape wraps the hull — get inner shape for mesh extraction
            // Fall back to rendering as a box approximation (the COM-offset wrapper hides the inner hull)
            _boatBodyId = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody
            {
                bodyId = _boatBodyId,
                color  = new Vector3(0.4f, 0.6f, 1.0f),
                shape  = RenderShape.Box,
                scale  = new Vector3(CHalfBoatTopWidth * 2f, CHalfBoatHeight * 2f, (CHalfBoatLength + CBoatBowLength) * 2f),
            });
        }

        // ── Water sensor ──────────────────────────────────────────────────────────
        {
            using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(CWaterWidth, CMaxWaterHeight, CWaterWidth));
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(ss);
            cs.mPosition.Set(0f, 0f, 0f);
            cs.mMotionType  = JPH.EMotionType.Static;
            cs.mObjectLayer = LayerMoving;
            cs.mIsSensor    = true;
            var sensorId = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            _waterSensorPacked = sensorId.GetIndexAndSequenceNumber();
            // Sensor is physics-only — not added to the render bodies list.
        }

        // ── Barrels ───────────────────────────────────────────────────────────────
        {
            using var cylSS = new JPH.CylinderShapeSettings(1.0f, 0.7f);
            for (int i = 0; i < 10; i++)
            {
                float angle = (float)(random.NextDouble() * MathF.PI * 2f);
                using var cs = new JPH.BodyCreationSettings();
                cs.SetShapeSettings(cylSS);
                cs.mPosition.Set(-10f + i * 2f, CMaxWaterHeight + 2f, 10f);
                // Random rotation around Y
                using var rotQ = new JPH.Quat();
                rotQ.Set(0f, MathF.Sin(angle * 0.5f), 0f, MathF.Cos(angle * 0.5f));
                cs.mRotation.Set(rotQ.GetX(), rotQ.GetY(), rotQ.GetZ(), rotQ.GetW());
                cs.mMotionType  = JPH.EMotionType.Dynamic;
                cs.mObjectLayer = LayerMoving;
                cs.SetOverrideMassProperties(1); // CalculateInertia
                cs.SetMassOverride(CBarrelMass);
                var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
                bodies.Add(new PhysicsBody
                {
                    bodyId = id,
                    color  = new Vector3(0.7f, 0.5f, 0.3f),
                    shape  = RenderShape.Cylinder,
                    scale  = new Vector3(0.7f * 2f, 2.0f, 0.7f * 2f),
                });
            }
        }
    }

    // ── Water surface formula ─────────────────────────────────────────────────
    (float x, float y, float z) GetWaterSurfacePosition(float inX, float inZ)
    {
        float y = CMinWaterHeight + MathF.Sin(0.1f * inZ + _time) * (CMaxWaterHeight - CMinWaterHeight);
        return (inX, y, inZ);
    }

    public override void Activate(JPH.PhysicsSystem sys)
    {
        lock (_lock) _bodiesInWater.Clear();

        var sensorPacked  = _waterSensorPacked;
        var bodiesInWater = _bodiesInWater;
        var lk            = _lock;

        _listener = new JPH.ContactListenerTrampolineManaged();

        _listener.SetOnContactAdded((b1, b2, manifold, settings) =>
        {
            uint id1 = b1.GetID().GetIndexAndSequenceNumber();
            uint id2 = b2.GetID().GetIndexAndSequenceNumber();
            lock (lk)
            {
                uint bodyId;
                if      (id1 == sensorPacked) bodyId = id2;
                else if (id2 == sensorPacked) bodyId = id1;
                else return;

                bodiesInWater.TryGetValue(bodyId, out int count);
                bodiesInWater[bodyId] = count + 1;
            }
        });

        _listener.SetOnContactRemoved(pair =>
        {
            uint id1 = pair.GetBody1ID().GetIndexAndSequenceNumber();
            uint id2 = pair.GetBody2ID().GetIndexAndSequenceNumber();
            lock (lk)
            {
                uint bodyId;
                if      (id1 == sensorPacked) bodyId = id2;
                else if (id2 == sensorPacked) bodyId = id1;
                else return;

                if (bodiesInWater.TryGetValue(bodyId, out int count))
                {
                    if (count <= 1) bodiesInWater.Remove(bodyId);
                    else           bodiesInWater[bodyId] = count - 1;
                }
            }
        });

        sys.SetContactListener(_listener.Inner);
    }

    public override void Deactivate(JPH.PhysicsSystem sys)
    {
        sys.SetContactListener(null);
        _listener?.Dispose();
        _listener = null;
    }

    public override unsafe void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        _time += dt;

        // ── Draw animated water surface ───────────────────────────────────────────
        const float step = 2f; // 1 strip per 2 world units; gives a 100×100 grid of quads
        for (float z = -CWaterWidth; z < CWaterWidth; z += step)
        {
            var (x1, y1, z1) = GetWaterSurfacePosition(-CWaterWidth, z);
            var (x2, y2, z2) = GetWaterSurfacePosition(-CWaterWidth, z + step);
            var (x3, y3, z3) = GetWaterSurfacePosition( CWaterWidth, z);
            var (x4, y4, z4) = GetWaterSurfacePosition( CWaterWidth, z + step);
            var p1 = new Vector3(x1, y1, z1);
            var p2 = new Vector3(x2, y2, z2);
            var p3 = new Vector3(x3, y3, z3);
            var p4 = new Vector3(x4, y4, z4);
            AddDebugTri(p1, p2, p3);
            AddDebugTri(p2, p4, p3);
        }

        // ── Collect current bodies-in-water snapshot ──────────────────────────────
        List<uint> inWater;
        lock (_lock)
        {
            inWater = new List<uint>(_bodiesInWater.Keys);
        }

        // ── Apply buoyancy ────────────────────────────────────────────────────────
        using var fluidVel = new JPH.Vec3(0f, 0f, 0f);

        // Get gravity from system (cached per-frame approach: use constant 9.81)
        using var gravity = new JPH.Vec3(0f, -9.81f, 0f);

        uint boatPacked = _boatBodyId.GetIndexAndSequenceNumber();

        foreach (uint packed in inWater)
        {
            var bodyId = new JPH.BodyID(packed);

            // Get center of mass position to determine local wave height
            using var com = bi.GetCenterOfMassPosition(bodyId);
            float comX = com.GetX(), comZ = com.GetZ();

            // Surface position at CoM XZ
            var (_, surfY,  _)   = GetWaterSurfacePosition(comX,       comZ);
            // Two nearby surface points for crude normal computation
            var (_, surfY2, _)   = GetWaterSurfacePosition(comX,       comZ + 1f);
            var (_, surfY3, _)   = GetWaterSurfacePosition(comX + 1f,  comZ);

            // Surface vectors from surface_position
            float d2x = 0f,  d2y = surfY2 - surfY, d2z = 1f;
            float d3x = 1f,  d3y = surfY3 - surfY, d3z = 0f;

            // Normal = d2 × d3
            float nx = d2y * d3z - d2z * d3y;
            float ny = d2z * d3x - d2x * d3z;
            float nz = d2x * d3y - d2y * d3x;
            float nLen = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
            if (nLen < 0.0001f) { nx = 0f; ny = 1f; nz = 0f; }
            else { nx /= nLen; ny /= nLen; nz /= nLen; }

            using var surfPos   = new JPH.Vec3(comX, surfY, comZ);
            using var surfNorm  = new JPH.Vec3(nx, ny, nz);

            float buoyancy, linDrag, angDrag;
            if (packed == boatPacked)
            {
                buoyancy = CBoatBuoyancy;
                linDrag  = CBoatLinearDrag;
                angDrag  = CBoatAngularDrag;
            }
            else
            {
                buoyancy = CBarrelBuoyancy;
                linDrag  = CBarrelLinearDrag;
                angDrag  = CBarrelAngularDrag;
            }

            bi.ApplyBuoyancyImpulse(bodyId, surfPos, surfNorm, buoyancy, linDrag, angDrag, fluidVel, gravity, dt);
        }

        // ── Steering input ────────────────────────────────────────────────────────
        float forward = 0f, right = 0f;
        if (IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_UP))    forward =  -1f;
        if (IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_DOWN))  forward = 1f;
        if (IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_LEFT))  right   = -1f;
        if (IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_RIGHT)) right   =  1f;

        if (right != 0f || forward != 0f)
            bi.ActivateBody(_boatBodyId);

        // ── Apply propeller thrust ─────────────────────────────────────────────────
        // Propeller world position: boat_transform * (0, -cHalfBoatHeight, -cHalfBoatLength)
        using var boatTransform = bi.GetWorldTransform(_boatBodyId);
        using var localPropVec  = new JPH.Vec3(0f, -CHalfBoatHeight, -CHalfBoatLength);
        using var propWorldPos  = boatTransform * localPropVec;

        float propX = propWorldPos.GetX(), propY = propWorldPos.GetY(), propZ = propWorldPos.GetZ();
        var (_, propSurfY, _) = GetWaterSurfacePosition(propX, propZ);

        if (propSurfY > propY)
        {
            // forward direction (boat's +Z axis in world space)
            using var boatRot = bi.GetRotation(_boatBodyId);
            using var fwdVec  = boatRot.RotateAxisZ();
            using var rightVec = boatRot.RotateAxisX();

            float fwdX = fwdVec.GetX(), fwdY = fwdVec.GetY(), fwdZ = fwdVec.GetZ();
            float rgtX = rightVec.GetX(), rgtY = rightVec.GetY(), rgtZ = rightVec.GetZ();

            // Flip steering when reversing so left/right feel consistent regardless of travel direction
            float steerInput = forward > 0f ? -right : right;

            float impX = (fwdX * forward * CForwardAcceleration + rgtX * steerInput * CSteerAcceleration) * CBoatMass * dt;
            float impY = (fwdY * forward * CForwardAcceleration + rgtY * steerInput * CSteerAcceleration) * CBoatMass * dt;
            float impZ = (fwdZ * forward * CForwardAcceleration + rgtZ * steerInput * CSteerAcceleration) * CBoatMass * dt;

            using var impulse   = new JPH.Vec3(impX, impY, impZ);
            bi.AddImpulse(_boatBodyId, impulse, propWorldPos);
        }
    }
}
