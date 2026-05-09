using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of SoftBodySensorTest.cpp.
/// Shows a 30×30 cloth with fixated corners, a TaperedCylinder sensor lying along world Z,
/// a sphere sensor, and a heavy sphere (500 kg) falling from above.
/// When the cloth contacts a sensor, a green wireframe AABB is drawn around it using
/// SG_PRIMITIVETYPE_LINES (via the DemoBase AddDebugBox helper).
/// </summary>
public class Demo_SoftBody_Sensor : DemoBase
{
    public override string Name     => "SoftBody: Sensor";
    public override string Category => "Soft Body";

    JPH.SoftBodyContactListenerTrampolineManaged? _listener;

    JPH.BodyID _cylinderSensorId;
    JPH.BodyID _sphereSensorId;

    // Approximate world-space half-extents for each sensor's AABB (used for debug box drawing)
    Vector3 _cylinderHalfExt;
    Vector3 _sphereHalfExt;

    // Set of sensor body IDs (raw) that had cloth contact during the last physics step.
    // Written by OnAdded (physics thread); read + cleared by Update (main thread).
    readonly object        _lock             = new();
    readonly HashSet<uint> _sensorsInContact = new();

    public override unsafe void Init(JPH.BodyInterface bi, JPH.PhysicsSystem sys, List<PhysicsBody> bodies, Random rng)
    {
        _listener = new JPH.SoftBodyContactListenerTrampolineManaged();
        _listener.SetOnAdded(OnAdded);
        sys.SetSoftBodyContactListener(_listener.Inner);

        AddFloor(bi, bodies);

        // Cloth: 30×30 with fixated corners, at (0, 10, 0), identity rotation
        var clothFaces    = new List<(uint, uint, uint)>();
        var clothSettings = CreateClothWithFixatedCornersSettings(30, 30, 0.75f, clothFaces);
        RegisterSoftBody(bi, clothSettings, clothFaces,
            0f, 10f, 0f,
            0f, 0f, 0f, 1f,
            new Vector3(0.2f, 0.7f, 0.4f));

        // TaperedCylinder sensor: halfH=4, topR=1, botR=2
        // Rotated 90° around X → cylinder axis aligns with world Z
        {
            using var ss = new JPH.TaperedCylinderShapeSettings(4f, 1f, 2f);
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(ss);
            cs.mPosition.Set(0f, 6f, 0f);
            float sinHalf = MathF.Sin(MathF.PI / 4f);
            float cosHalf = MathF.Cos(MathF.PI / 4f);
            cs.mRotation.Set(sinHalf, 0f, 0f, cosHalf); // 90° around X
            cs.mMotionType  = JPH.EMotionType.Static;
            cs.mObjectLayer = LayerNonMoving;
            cs.mIsSensor    = true;
            _cylinderSensorId = bi.CreateAndAddBody(cs, JPH.EActivation.DontActivate);

            float tr2 = 1f * 1f, br2 = 2f * 2f;
            float denom        = tr2 + 1f * 2f + br2;
            float localOffsetY = 4f * (br2 - tr2) / (2f * denom); // COM offset along shape's local Y
            var   coneMesh     = CreateTaperedConeMesh(1f, 2f, 4f, out int coneIdxCount);
            bodies.Add(new PhysicsBody
            {
                bodyId               = _cylinderSensorId,
                color                = new Vector3(1f, 1f, 0f),
                shape                = RenderShape.TaperedCylinder,
                scale                = Vector3.One,
                localOffset          = new Vector3(0f, localOffsetY, 0f),
                customMesh           = coneMesh,
                customMeshIndexCount = coneIdxCount,
                wireframe            = true,
            });
            // After 90° around X: half-height (4) along world Z, max cross-section radius (2) in X/Y.
            // Use symmetric ±5 along Z to cover the asymmetry from the COM offset (~0.86).
            _cylinderHalfExt = new Vector3(2f, 2f, 5f);
        }

        // Sphere sensor: radius=4, at (4, 5, 0)
        {
            using var ss = new JPH.SphereShapeSettings(4f);
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(ss);
            cs.mPosition.Set(4f, 5f, 0f);
            cs.mMotionType  = JPH.EMotionType.Static;
            cs.mObjectLayer = LayerNonMoving;
            cs.mIsSensor    = true;
            _sphereSensorId = bi.CreateAndAddBody(cs, JPH.EActivation.DontActivate);
            bodies.Add(new PhysicsBody
            {
                bodyId    = _sphereSensorId,
                color     = new Vector3(1f, 0.8f, 0f),
                shape     = RenderShape.Sphere,
                scale     = new Vector3(8f),
                wireframe = true,
            });
            _sphereHalfExt = new Vector3(4f); // sphere radius = 4
        }

        // Heavy sphere (mass=500) falling from above onto the cloth
        AddSphere(bi, bodies, 1f, 0f, 15f, 0f,
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.9f, 0.3f, 0.3f), mass: 500f);
    }

    public override void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        // Draw green wireframe AABB (SG_PRIMITIVETYPE_LINES) around sensors in contact this step
        HashSet<uint>? active;
        lock (_lock)
        {
            if (_sensorsInContact.Count == 0) return;
            active = new HashSet<uint>(_sensorsInContact);
            _sensorsInContact.Clear();
        }

        uint cylRaw = _cylinderSensorId.GetIndexAndSequenceNumber();
        uint sphRaw = _sphereSensorId.GetIndexAndSequenceNumber();
        foreach (uint raw in active)
        {
            JPH.BodyID id;
            Vector3    halfExt;
            if      (raw == cylRaw) { id = _cylinderSensorId; halfExt = _cylinderHalfExt; }
            else if (raw == sphRaw) { id = _sphereSensorId;   halfExt = _sphereHalfExt;   }
            else continue;

            using var pos    = bi.GetPosition(id);
            var       center = new Vector3(pos.GetX(), pos.GetY(), pos.GetZ());
            AddDebugBox(center - halfExt, center + halfExt);
        }
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        sys.SetSoftBodyContactListener(null);
        _listener?.Dispose();
        _listener = null;
    }

    // ── Contact listener ──────────────────────────────────────────────────────

    void OnAdded(JPH.Const_Body softBody, JPH.Const_SoftBodyManifold manifold)
    {
        uint n = manifold.GetNumSensorContacts();
        if (n == 0) return;
        lock (_lock)
        {
            for (uint i = 0; i < n; i++)
            {
                var id = manifold.GetSensorContactBodyID(i);
                _sensorsInContact.Add(id.GetIndexAndSequenceNumber());
            }
        }
    }
}

