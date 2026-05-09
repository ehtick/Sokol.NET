using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of SensorTest.cpp.
/// Four sensor volumes demonstrate different sensor behaviors:
///   [0] StaticAttractor    — static sphere at (0,10,0): attracts dynamic bodies toward its center,
///                            cancelling gravity so bodies float and orbit.
///   [1] StaticSensor       — static box at (-10,5.1,0): only detects active dynamic bodies.
///   [2] KinematicSensor    — kinematic box at (10,5.1,0): also detects sleeping dynamic bodies.
///   [3] SensorDetectStatic — kinematic box at (25,5.1,0): also detects static bodies
///                            (mCollideKinematicVsNonDynamic = true).
///
/// A kinematic box oscillates along the X axis through the sensor field.
/// Three static boxes near the rightmost sensor demonstrate static-body detection.
/// (Ragdoll omitted — requires external asset files.)
/// </summary>
public sealed class Demo_Sensor : DemoBase
{
    public override string Name     => "Sensor";
    public override string Category => "General";

    const int SensorStaticAttractor  = 0;
    const int SensorStatic           = 1;
    const int SensorKinematic        = 2;
    const int SensorDetectStatic     = 3;
    const int NumSensors             = 4;

    // Sensor packed body IDs
    readonly uint[] _sensorPackedIDs = new uint[NumSensors];

    // Per-sensor: packed body ID → contact-manifold count (ref-counted like C++)
    readonly Dictionary<uint, int>[] _bodiesInSensor;

    readonly object _lock = new();
    JPH.ContactListenerTrampolineManaged? _listener;

    JPH.BodyID _kinematicBodyID;
    float      _time;

    // Gravity cached in Activate for use in Update (sys not available in Update)
    float _gravX, _gravY, _gravZ;

    // Box (0.1,0.5,0.2) half-extents, default density 1000 kg/m³:
    //   mass = 1000 * (0.2 * 1.0 * 0.4) = 80 kg
    const float BoxMass              = 80f;
    const float CentripetalAccel     = 10f;   // matches C++ centrifugal_force

    // Per-frame snapshot list (avoids per-frame allocation)
    readonly List<uint> _pendingAttract = new();

    public Demo_Sensor()
    {
        _bodiesInSensor = new Dictionary<uint, int>[NumSensors];
        for (int i = 0; i < NumSensors; i++)
            _bodiesInSensor[i] = new Dictionary<uint, int>();
    }

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 100,
        Latitude  = 30,
        Longitude = 15,
        Center    = new Vector3(5, 10, 0),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000.0f
    };

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies, 400f, 1f, 400f);

        // ── [0] Static sphere sensor — attracts dynamic bodies ────────────
        _sensorPackedIDs[SensorStaticAttractor] = CreateSphereSensor(
            bi, bodies, 0f, 10f, 0f, radius: 10f,
            JPH.EMotionType.Static,
            new Vector3(1f, 0.3f, 0.3f));

        // ── [1] Static box sensor — detects active bodies only ────────────
        _sensorPackedIDs[SensorStatic] = CreateBoxSensor(
            bi, bodies, -10f, 5.1f, 0f, halfExtent: 5f,
            JPH.EMotionType.Static,
            new Vector3(0.3f, 1f, 0.3f));

        // ── [2] Kinematic box sensor — detects sleeping bodies too ────────
        _sensorPackedIDs[SensorKinematic] = CreateBoxSensor(
            bi, bodies, 10f, 5.1f, 0f, halfExtent: 5f,
            JPH.EMotionType.Kinematic,
            new Vector3(0.3f, 0.3f, 1f));

        // ── [3] Kinematic box sensor — also detects static bodies ─────────
        _sensorPackedIDs[SensorDetectStatic] = CreateBoxSensorDetectStatic(
            bi, bodies, 25f, 5.1f, 0f, halfExtent: 5f,
            new Vector3(0.8f, 0.3f, 0.8f));

        // ── 15 dynamic boxes in a line ─────────────────────────────────────
        for (int i = 0; i < 15; i++)
        {
            AddBox(bi, bodies, 0.1f, 0.5f, 0.2f,
                -15f + i * 3f, 25f, 0f,
                Quaternion.Identity,
                JPH.EMotionType.Dynamic, LayerMoving,
                new Vector3(0.8f, 0.6f, 0.3f));
        }

        // ── 3 static boxes near SensorDetectStatic ────────────────────────
        float[] sx = { -14f, 6f, 21f };
        for (int i = 0; i < 3; i++)
        {
            AddBox(bi, bodies, 0.5f, 0.5f, 0.5f,
                sx[i], 1f, 4f,
                Quaternion.Identity,
                JPH.EMotionType.Static, LayerNonMoving,
                new Vector3(0.55f, 0.55f, 0.55f));
        }

        // ── Kinematic box: oscillates along X through the sensors ─────────
        {
            using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(0.25f, 0.5f, 1.0f));
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(ss);
            cs.mPosition.Set(-20f, 10f, 0f);
            cs.mMotionType  = JPH.EMotionType.Kinematic;
            cs.mObjectLayer = LayerMoving;
            _kinematicBodyID = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody
            {
                bodyId = _kinematicBodyID,
                color  = new Vector3(1f, 0.8f, 0.2f),
                shape  = RenderShape.Box,
                scale  = new Vector3(0.5f, 1f, 2f)
            });
        }

        _time = 0f;
    }

    unsafe uint CreateSphereSensor(
        JPH.BodyInterface bi, List<PhysicsBody> bodies,
        float sx, float sy, float sz, float radius,
        JPH.EMotionType motionType, Vector3 color)
    {
        using var ss = new JPH.SphereShapeSettings(radius);
        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(sx, sy, sz);
        cs.mMotionType  = motionType;
        cs.mObjectLayer = LayerMoving;
        cs.mIsSensor    = true;
        var activation = motionType == JPH.EMotionType.Kinematic
            ? JPH.EActivation.Activate : JPH.EActivation.DontActivate;
        var id = bi.CreateAndAddBody(cs, activation);
        bodies.Add(new PhysicsBody
        {
            bodyId    = id,
            color     = color,
            shape     = RenderShape.Sphere,
            scale     = new Vector3(radius * 2f),
            wireframe = true
        });
        return id.GetIndexAndSequenceNumber();
    }

    unsafe uint CreateBoxSensor(
        JPH.BodyInterface bi, List<PhysicsBody> bodies,
        float sx, float sy, float sz, float halfExtent,
        JPH.EMotionType motionType, Vector3 color)
    {
        using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(halfExtent, halfExtent, halfExtent));
        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(sx, sy, sz);
        cs.mMotionType  = motionType;
        cs.mObjectLayer = LayerMoving;
        cs.mIsSensor    = true;
        var activation = motionType == JPH.EMotionType.Kinematic
            ? JPH.EActivation.Activate : JPH.EActivation.DontActivate;
        var id = bi.CreateAndAddBody(cs, activation);
        bodies.Add(new PhysicsBody
        {
            bodyId    = id,
            color     = color,
            shape     = RenderShape.Box,
            scale     = new Vector3(halfExtent * 2f),
            wireframe = true
        });
        return id.GetIndexAndSequenceNumber();
    }

    unsafe uint CreateBoxSensorDetectStatic(
        JPH.BodyInterface bi, List<PhysicsBody> bodies,
        float sx, float sy, float sz, float halfExtent,
        Vector3 color)
    {
        using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(halfExtent, halfExtent, halfExtent));
        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(sx, sy, sz);
        cs.mMotionType                   = JPH.EMotionType.Kinematic;
        cs.mObjectLayer                  = LayerMoving;
        cs.mIsSensor                     = true;
        cs.mCollideKinematicVsNonDynamic = true;
        var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId    = id,
            color     = color,
            shape     = RenderShape.Box,
            scale     = new Vector3(halfExtent * 2f),
            wireframe = true
        });
        return id.GetIndexAndSequenceNumber();
    }

    public override void Activate(JPH.PhysicsSystem sys)
    {
        for (int i = 0; i < NumSensors; i++)
            _bodiesInSensor[i].Clear();

        // Cache gravity so Update can cancel it without sys reference
        using var g = sys.GetGravity();
        _gravX = g.GetX(); _gravY = g.GetY(); _gravZ = g.GetZ();

        var sensorPackedIDs = _sensorPackedIDs;
        var bodiesInSensor  = _bodiesInSensor;
        var lk              = _lock;

        _listener = new JPH.ContactListenerTrampolineManaged();

        _listener.SetOnContactAdded((b1, b2, manifold, settings) =>
        {
            uint id1 = b1.GetID().GetIndexAndSequenceNumber();
            uint id2 = b2.GetID().GetIndexAndSequenceNumber();
            lock (lk)
            {
                for (int s = 0; s < NumSensors; s++)
                {
                    uint sid = sensorPackedIDs[s];
                    uint bodyId;
                    if      (id1 == sid) bodyId = id2;
                    else if (id2 == sid) bodyId = id1;
                    else continue;

                    bodiesInSensor[s].TryGetValue(bodyId, out int count);
                    bodiesInSensor[s][bodyId] = count + 1;
                }
            }
        });

        _listener.SetOnContactRemoved(pair =>
        {
            uint id1 = pair.GetBody1ID().GetIndexAndSequenceNumber();
            uint id2 = pair.GetBody2ID().GetIndexAndSequenceNumber();
            lock (lk)
            {
                for (int s = 0; s < NumSensors; s++)
                {
                    uint sid = sensorPackedIDs[s];
                    uint bodyId;
                    if      (id1 == sid) bodyId = id2;
                    else if (id2 == sid) bodyId = id1;
                    else continue;

                    var dict = bodiesInSensor[s];
                    if (dict.TryGetValue(bodyId, out int count))
                    {
                        if (count <= 1) dict.Remove(bodyId);
                        else           dict[bodyId] = count - 1;
                    }
                }
            }
        });

        sys.SetContactListener(_listener.Inner);
    }

    public override void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        _time += dt;

        // ── Move kinematic body: x = -20 * cos(time) ──────────────────────
        {
            using var targetPos = new JPH.Vec3(-20f * MathF.Cos(_time), 10f, 0f);
            using var targetRot = new JPH.Quat();
            targetRot.Set(0f, 0f, 0f, 1f);
            bi.MoveKinematic(_kinematicBodyID, targetPos, targetRot, dt);
        }

        // ── Attract bodies inside StaticAttractor toward its centre ────────
        // force = (centripetal_accel * normalize(center - pos) - gravity) * mass
        _pendingAttract.Clear();
        lock (_lock)
        {
            foreach (var kvp in _bodiesInSensor[SensorStaticAttractor])
                _pendingAttract.Add(kvp.Key);
        }

        const float cx = 0f, cy = 10f, cz = 0f;  // sensor centre matches C++

        foreach (uint packedId in _pendingAttract)
        {
            var bodyId = new JPH.BodyID(packedId);
            using var pos = bi.GetCenterOfMassPosition(bodyId);
            float dx = cx - pos.GetX();
            float dy = cy - pos.GetY();
            float dz = cz - pos.GetZ();
            float len = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 0.001f) continue;

            // centripetal acceleration (magnitude CentripetalAccel toward centre)
            float ax = dx / len * CentripetalAccel;
            float ay = dy / len * CentripetalAccel;
            float az = dz / len * CentripetalAccel;

            // cancel gravity
            ax -= _gravX;
            ay -= _gravY;
            az -= _gravZ;

            using var force = new JPH.Vec3(ax * BoxMass, ay * BoxMass, az * BoxMass);
            bi.AddForce(bodyId, force);
        }
    }

    public override void Deactivate(JPH.PhysicsSystem sys)
    {
        sys.SetContactListener(null);
        _listener?.Dispose();
        _listener = null;
        lock (_lock)
            for (int i = 0; i < NumSensors; i++)
                _bodiesInSensor[i].Clear();
    }
}

