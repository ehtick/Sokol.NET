using Sokol;
using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of WaterShapeTest.cpp — demonstrates buoyancy with various shape types.
/// A flat water surface sits at y=10. All dynamic bodies are dropped from y=20
/// and float on the simulated water.
///
/// Bodies in the water volume (y &lt; 10) have ApplyBuoyancyImpulse called each frame.
/// Uses BodyInterface.ApplyBuoyancyImpulse on the demo's own body list as a
/// substitute for BroadPhaseQuery.CollideAABox (not bound in C# bindings).
/// </summary>
public sealed class Demo_WaterShape : DemoBase
{
    public override string Name     => "Water Shapes";
    public override string Category => "Water";

    const float WaterSurfaceY = 10f;

    // Bodies that are dynamic (non-floor) — populated during Init
    readonly List<uint> _dynamicBodyPackedIDs = new();

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 80,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(10f, 10f, 0f),
    };

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random random)
    {
        _dynamicBodyPackedIDs.Clear();
        AddFloor(bi, bodies);

        // ── Scaled box: BoxShape(1,2,2.5) × scale(0.5, 0.6, -0.7) at (-10,20,0) ──
        {
            using var boxSS    = new JPH.BoxShapeSettings(new JPH.Vec3(1.0f, 2.0f, 2.5f));
            using var scaleVec = new JPH.Vec3(0.5f, 0.6f, -0.7f);
            using var scaleSS  = new JPH.ScaledShapeSettings(boxSS, scaleVec);
            using var cs       = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(scaleSS);
            cs.mPosition.Set(-10f, 20f, 0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = id, color = HsvToRgb(0.0f, 0.7f, 1.0f), shape = RenderShape.Box, scale = new Vector3(1.0f, 2.4f, 3.5f) });
            _dynamicBodyPackedIDs.Add(id.GetIndexAndSequenceNumber());
        }

        // ── Box: BoxShape(1,2,2.5) at (-7,20,0) ──────────────────────────────────
        {
            using var boxSS = new JPH.BoxShapeSettings(new JPH.Vec3(1.0f, 2.0f, 2.5f));
            using var cs    = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(boxSS);
            cs.mPosition.Set(-7f, 20f, 0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = id, color = HsvToRgb(0.1f, 0.7f, 1.0f), shape = RenderShape.Box, scale = new Vector3(2.0f, 4.0f, 5.0f) });
            _dynamicBodyPackedIDs.Add(id.GetIndexAndSequenceNumber());
        }

        // ── Sphere: SphereShape(2) at (-3,20,0) ──────────────────────────────────
        {
            using var ss = new JPH.SphereShapeSettings(2.0f);
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(ss);
            cs.mPosition.Set(-3f, 20f, 0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = id, color = HsvToRgb(0.2f, 0.7f, 1.0f), shape = RenderShape.Sphere, scale = new Vector3(4.0f) });
            _dynamicBodyPackedIDs.Add(id.GetIndexAndSequenceNumber());
        }

        // ── Static compound: sphere(r=2) at (2,0,0) + sphere(r=1) at (-1,0,0), body at (3,20,0) ─
        {
            using var compSS = new JPH.StaticCompoundShapeSettings();
            using var s2     = new JPH.SphereShapeSettings(2.0f);
            using var s1     = new JPH.SphereShapeSettings(1.0f);
            using var pos2   = new JPH.Vec3(2.0f, 0f, 0f);
            using var pos1   = new JPH.Vec3(-1.0f, 0f, 0f);
            using var rot    = new JPH.Quat();
            rot.Set(0f, 0f, 0f, 1f);
            ((JPH.CompoundShapeSettings)compSS).AddShape(pos2, rot, s2);
            ((JPH.CompoundShapeSettings)compSS).AddShape(pos1, rot, s1);
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(compSS);
            cs.mPosition.Set(3f, 20f, 0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            // Compound with two spheres — render as sphere approximation
            bodies.Add(new PhysicsBody { bodyId = id, color = HsvToRgb(0.3f, 0.7f, 1.0f), shape = RenderShape.Sphere, scale = new Vector3(4.0f) });
            _dynamicBodyPackedIDs.Add(id.GetIndexAndSequenceNumber());
        }

        // ── Tetrahedron convex hull at (10,20,0) ──────────────────────────────────
        {
            var pts = new[]
            {
                new JPH.Vec3f(-2, 0, -2), new JPH.Vec3f(0, 0, 2),
                new JPH.Vec3f( 2, 0, -2), new JPH.Vec3f(0, -2, 0),
            };
            using var hullSS = JPH.ConvexHullShapeSettingsFromPoints(pts, 0.05f);
            using var cs     = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(hullSS);
            cs.mPosition.Set(10f, 20f, 0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            var shape    = cs.GetShape()!;
            var hullShape = (JPH.Const_ConvexHullShape)shape;
            var mesh      = CreateConvexHullMesh(hullShape, out int idxCount);
            var id        = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = id, color = HsvToRgb(0.4f, 0.7f, 1.0f), shape = RenderShape.TaperedCylinder, scale = Vector3.One, customMesh = mesh, customMeshIndexCount = idxCount });
            _dynamicBodyPackedIDs.Add(id.GetIndexAndSequenceNumber());
        }

        // ── Non-uniform scaled tetrahedron: scale(1,-1.5,2) at (15,20,0) ─────────
        {
            var pts = new[]
            {
                new JPH.Vec3f(-2, 0, -2), new JPH.Vec3f(0, 0, 2),
                new JPH.Vec3f( 2, 0, -2), new JPH.Vec3f(0, -2, 0),
            };
            using var hullSS   = JPH.ConvexHullShapeSettingsFromPoints(pts, 0.05f);
            using var scaleVec = new JPH.Vec3(1.0f, -1.5f, 2.0f);
            using var scaledSS = new JPH.ScaledShapeSettings(hullSS, scaleVec);
            using var cs       = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(scaledSS);
            cs.mPosition.Set(15f, 20f, 0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = id, color = HsvToRgb(0.5f, 0.7f, 1.0f), shape = RenderShape.Box, scale = new Vector3(4.0f, 3.0f, 4.0f) });
            _dynamicBodyPackedIDs.Add(id.GetIndexAndSequenceNumber());
        }

        // ── Convex hull box at (18,20,0) ──────────────────────────────────────────
        {
            var pts = new[]
            {
                new JPH.Vec3f( 1.5f,  1.0f,  0.5f), new JPH.Vec3f(-1.5f,  1.0f,  0.5f),
                new JPH.Vec3f( 1.5f, -1.0f,  0.5f), new JPH.Vec3f(-1.5f, -1.0f,  0.5f),
                new JPH.Vec3f( 1.5f,  1.0f, -0.5f), new JPH.Vec3f(-1.5f,  1.0f, -0.5f),
                new JPH.Vec3f( 1.5f, -1.0f, -0.5f), new JPH.Vec3f(-1.5f, -1.0f, -0.5f),
            };
            using var hullSS = JPH.ConvexHullShapeSettingsFromPoints(pts, 0.05f);
            using var cs     = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(hullSS);
            cs.mPosition.Set(18f, 20f, 0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            var shape     = cs.GetShape()!;
            var hullShape = (JPH.Const_ConvexHullShape)shape;
            var mesh      = CreateConvexHullMesh(hullShape, out int idxCount);
            var id        = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = id, color = HsvToRgb(0.6f, 0.7f, 1.0f), shape = RenderShape.TaperedCylinder, scale = Vector3.One, customMesh = mesh, customMeshIndexCount = idxCount });
            _dynamicBodyPackedIDs.Add(id.GetIndexAndSequenceNumber());
        }

        // ── Random convex hull at (21,20,0) ───────────────────────────────────────
        {
            var rng        = new Random(12345); // fixed seed for determinism
            var hullSizeDist_min = 0.1f;
            var hullSizeDist_range = 1.8f; // 0.1..1.9
            var pts = new JPH.Vec3f[20];
            for (int j = 0; j < 20; j++)
            {
                float hullSize = hullSizeDist_min + (float)rng.NextDouble() * hullSizeDist_range;
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                float y = (float)(rng.NextDouble() * 2.0 - 1.0);
                float z = (float)(rng.NextDouble() * 2.0 - 1.0);
                float len = MathF.Sqrt(x * x + y * y + z * z);
                if (len < 0.0001f) len = 1f;
                pts[j] = new JPH.Vec3f(hullSize * x / len, hullSize * y / len, hullSize * z / len);
            }
            using var hullSS = JPH.ConvexHullShapeSettingsFromPoints(pts, 0.05f);
            using var cs     = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(hullSS);
            cs.mPosition.Set(21f, 20f, 0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            var shape     = cs.GetShape()!;
            var hullShape = (JPH.Const_ConvexHullShape)shape;
            var mesh      = CreateConvexHullMesh(hullShape, out int idxCount);
            var id        = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = id, color = HsvToRgb(0.7f, 0.7f, 1.0f), shape = RenderShape.TaperedCylinder, scale = Vector3.One, customMesh = mesh, customMeshIndexCount = idxCount });
            _dynamicBodyPackedIDs.Add(id.GetIndexAndSequenceNumber());
        }

        // ── Mutable compound: box(0.5,0.75,1) at (1,0,0) + sphere(r=1) at (-1,0,0), body at (25,20,0) ─
        {
            using var mc    = new JPH.MutableCompoundShapeSettings();
            using var boxSS = new JPH.BoxShapeSettings(new JPH.Vec3(0.5f, 0.75f, 1.0f));
            using var sphSS = new JPH.SphereShapeSettings(1.0f);
            using var pos1  = new JPH.Vec3(1.0f, 0f, 0f);
            using var posN1 = new JPH.Vec3(-1.0f, 0f, 0f);
            using var rot   = new JPH.Quat();
            rot.Set(0f, 0f, 0f, 1f);
            ((JPH.CompoundShapeSettings)mc).AddShape(pos1,  rot, boxSS);
            ((JPH.CompoundShapeSettings)mc).AddShape(posN1, rot, sphSS);
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(mc);
            cs.mPosition.Set(25f, 20f, 0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = id, color = HsvToRgb(0.8f, 0.7f, 1.0f), shape = RenderShape.Box, scale = new Vector3(4.0f, 2.0f, 2.0f) });
            _dynamicBodyPackedIDs.Add(id.GetIndexAndSequenceNumber());
        }

        // ── COM-offset box: BoxShape(2,0.25,0.25) with COM offset (-1,0,0) at (30,20,0) ─
        {
            using var boxSS  = new JPH.BoxShapeSettings(new JPH.Vec3(2.0f, 0.25f, 0.25f));
            using var offset = new JPH.Vec3(-1.0f, 0.0f, 0.0f);
            using var comSS  = new JPH.OffsetCenterOfMassShapeSettings(offset, boxSS);
            using var cs     = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(comSS);
            cs.mPosition.Set(30f, 20f, 0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = id, color = HsvToRgb(0.9f, 0.7f, 1.0f), shape = RenderShape.Box, scale = new Vector3(4.0f, 0.5f, 0.5f) });
            _dynamicBodyPackedIDs.Add(id.GetIndexAndSequenceNumber());
        }
    }

    public override unsafe void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        using var surfacePos    = new JPH.Vec3(0f, WaterSurfaceY, 0f);
        using var surfaceNormal = new JPH.Vec3(0f, 1f, 0f);
        using var fluidVel      = new JPH.Vec3(0f, 0f, 0f);
        using var gravity       = new JPH.Vec3(0f, -9.81f, 0f);

        foreach (uint packed in _dynamicBodyPackedIDs)
        {
            var bodyId = new JPH.BodyID(packed);
            bi.ApplyBuoyancyImpulse(bodyId, surfacePos, surfaceNormal, 1.1f, 0.3f, 0.05f, fluidVel, gravity, dt);
        }
    }
}
