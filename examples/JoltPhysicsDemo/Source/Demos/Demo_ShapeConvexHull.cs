using Sokol;
using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of ConvexHullShapeTest.cpp — convex hulls from various point sets.
/// </summary>
public sealed class Demo_ShapeConvexHull : DemoBase
{
    public override string Name     => "Convex Hull Shape";
    public override string Category => "Shapes";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 120,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(40f, 10f, 10f),
    };

    public override void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies);

        // Tetrahedron (4 points) @ (0,10,0)
        SpawnHull(bi, bodies, new[]
        {
            new JPH.Vec3f(-5, 0,-5), new JPH.Vec3f(0, 0, 5),
            new JPH.Vec3f( 5, 0,-5), new JPH.Vec3f(0,-5, 0)
        }, 0f, 10f, 0f, HsvToRgb(0.0f, 0.7f, 1.0f));

        // Box (8 points) @ (20,10,0)
        SpawnHull(bi, bodies, new[]
        {
            new JPH.Vec3f( 5, 5, 5), new JPH.Vec3f(-5, 5, 5),
            new JPH.Vec3f( 5,-5, 5), new JPH.Vec3f(-5,-5, 5),
            new JPH.Vec3f( 5, 5,-5), new JPH.Vec3f(-5, 5,-5),
            new JPH.Vec3f( 5,-5,-5), new JPH.Vec3f(-5,-5,-5),
        }, 20f, 10f, 0f, HsvToRgb(0.1f, 0.7f, 1.0f));

        // Sphere approximation: theta 0..PI step PI/20 (21 vals), phi 0..2PI step 2PI/20 (21 vals) @ (40,10,0)
        {
            var pts = new List<JPH.Vec3f>(441);
            for (float theta = 0f; theta <= MathF.PI; theta += MathF.PI / 20f)
            {
                float st = MathF.Sin(theta), ct = MathF.Cos(theta);
                for (float phi = 0f; phi <= 2f * MathF.PI; phi += 2f * MathF.PI / 20f)
                    pts.Add(new JPH.Vec3f(5f * st * MathF.Cos(phi), 5f * ct, 5f * st * MathF.Sin(phi)));
            }
            SpawnHull(bi, bodies, pts.ToArray(), 40f, 10f, 0f, HsvToRgb(0.2f, 0.7f, 1.0f));
        }

        // Tapered cylinder: theta 0..2PI step PI/128 (~257 iters x 2 pts) @ (60,10,0)
        {
            var pts = new List<JPH.Vec3f>(514);
            for (float theta = 0f; theta <= 2f * MathF.PI; theta += MathF.PI / 128f)
            {
                float s = MathF.Sin(theta), c = MathF.Cos(theta);
                pts.Add(new JPH.Vec3f(4f * (-0.1f), 4f * s, 4f * c));
                pts.Add(new JPH.Vec3f(4.5f * 0.1f,  4.5f * s, 4.5f * c));
            }
            SpawnHull(bi, bodies, pts.ToArray(), 60f, 10f, 0f, HsvToRgb(0.3f, 0.7f, 1.0f));
        }

        // Coplanar — exact 20 hardcoded points from C++ @ (80,10,0)
        SpawnHull(bi, bodies, new[]
        {
            new JPH.Vec3f( 1.04298747f,  4.68531752f,  0.858853102f),
            new JPH.Vec3f(-1.00753999f,  4.63935566f, -0.959064901f),
            new JPH.Vec3f(-1.01861656f,  4.72096348f,  0.846121550f),
            new JPH.Vec3f(-2.37996006f,  1.26311386f, -1.10994697f),
            new JPH.Vec3f( 0.213164970f, 0.0198628306f,-1.70677519f),
            new JPH.Vec3f(-2.27295995f, -0.899001241f,-0.472913086f),
            new JPH.Vec3f(-1.85078228f, -1.25204790f,  2.42339849f),
            new JPH.Vec3f( 1.91183412f, -1.25204790f,  2.42339849f),
            new JPH.Vec3f(-2.75279832f,  3.25019693f,  1.67055058f),
            new JPH.Vec3f(-0.0697868019f,-2.78841114f,-0.422013819f),
            new JPH.Vec3f( 2.26410985f, -0.918261647f,-0.493922710f),
            new JPH.Vec3f( 0.765828013f, -2.82050991f, 1.91100550f),
            new JPH.Vec3f( 2.33326006f,  1.26643038f, -1.18808103f),
            new JPH.Vec3f(-0.591650009f, 2.27845216f, -1.87628603f),
            new JPH.Vec3f(-2.22145009f,  3.04359150f,  0.234738767f),
            new JPH.Vec3f(-1.00753999f,  4.39097166f, -1.27783847f),
            new JPH.Vec3f( 0.995577991f, 4.39734173f, -1.27900386f),
            new JPH.Vec3f( 0.995577991f, 4.64572525f, -0.960230291f),
            new JPH.Vec3f( 2.74527335f,  3.06491613f,  1.77647924f),
            new JPH.Vec3f(-1.53122997f, -2.18120861f,  2.31516361f),
        }, 80f, 10f, 0f, HsvToRgb(0.4f, 0.7f, 1.0f));

        // 10 random 3D hulls: -90 + i*18, z=20
        var rng = new Random(0);
        for (int i = 0; i < 10; ++i)
        {
            var pts = new JPH.Vec3f[20];
            for (int j = 0; j < 20; ++j)
            {
                float hullSize = 0.1f + (float)rng.NextDouble() * 9.9f;
                var dir = RandomOnUnitSphere(rng);
                pts[j] = new JPH.Vec3f(hullSize * dir.X, hullSize * dir.Y, hullSize * dir.Z);
            }
            SpawnHull(bi, bodies, pts, -90f + i * 18f, 10f, 20f, HsvToRgb(i / 10f, 0.8f, 1.0f));
        }
    }

    private static unsafe void SpawnHull(
        JPH.BodyInterface bi, List<PhysicsBody> bodies,
        JPH.Vec3f[] pts,
        float px, float py, float pz,
        Vector3 color,
        float maxConvexRadius = 0.05f)
    {
        using var hull = JPH.ConvexHullShapeSettingsFromPoints(pts, maxConvexRadius);
        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(hull);
        cs.mPosition.Set(px, py, pz);
        cs.mMotionType  = JPH.EMotionType.Dynamic;
        cs.mObjectLayer = LayerMoving;

        // Resolve shape now so we can extract its geometry for rendering
        var baseShape = cs.GetShape()!;
        var hullShape = (JPH.Const_ConvexHullShape)baseShape;
        var mesh = CreateConvexHullMesh(hullShape, out int idxCount);

        var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId               = id,
            color                = color,
            shape                = RenderShape.TaperedCylinder,
            scale                = Vector3.One,
            localOffset          = Vector3.Zero,
            customMesh           = mesh,
            customMeshIndexCount = idxCount,
        });
    }

    private static Vector3 RandomOnUnitSphere(Random rng)
    {
        while (true)
        {
            float x = (float)(rng.NextDouble() * 2.0 - 1.0);
            float y = (float)(rng.NextDouble() * 2.0 - 1.0);
            float z = (float)(rng.NextDouble() * 2.0 - 1.0);
            float len2 = x * x + y * y + z * z;
            if (len2 > 0f && len2 <= 1f)
            {
                float inv = 1f / MathF.Sqrt(len2);
                return new Vector3(x * inv, y * inv, z * inv);
            }
        }
    }
}
