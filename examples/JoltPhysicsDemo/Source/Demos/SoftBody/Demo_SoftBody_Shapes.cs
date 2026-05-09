using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of SoftBodyShapesTest — cloth, cube and pressurised sphere soft bodies,
/// plus various hard shapes falling onto the cloth and a mesh terrain.
/// </summary>
public class Demo_SoftBody_Shapes : DemoBase
{
    public override string Name     => "SoftBody: Shapes";
    public override string Category  => "Soft Body";
    // public override int    CollisionSteps => 3;

    public override void Init(JPH.BodyInterface bi, JPH.PhysicsSystem physicsSystem,
                               List<PhysicsBody> bodies, Random rng)
    {

        CreatePlaygroundTerrain(bi, bodies);

        // Cloth fixated at corners, rotated 45° around Y
        float qy = MathF.Sin(0.125f * MathF.PI);
        float qw = MathF.Cos(0.125f * MathF.PI);
        var clothFaces = new List<(uint, uint, uint)>();
        var clothSettings = CreateClothWithFixatedCornersSettings(75, 75, 0.25f, clothFaces);
        RegisterSoftBody(bi, clothSettings, clothFaces,
            0f, 10f, 0f,
            0f, qy, 0f, qw,
            new Vector3(0.1f, 0.8f, 0.9f),
            cs => { cs.mUpdatePosition = false; cs.mMakeRotationIdentity = false; cs.mVertexRadius = 0.1f; });

        // Cube soft body with a tilted orientation (rotate 45° around axis (1,1,1)/√3)
        float sqrtThird = MathF.Sqrt(1f / 3f);
        float angle = 45f * MathF.PI / 180f;
        float cubeQw = MathF.Cos(angle * 0.5f);
        float cubeQs = MathF.Sin(angle * 0.5f);
        RegisterCubeSoftBody(bi, 5, 0.5f,
            20f, 10f, 0f,
            cubeQs * sqrtThird, cubeQs * sqrtThird, cubeQs * sqrtThird, cubeQw,
            new Vector3(0.2f, 0.8f, 0.2f),
            cs => { cs.mRestitution = 0f; });

        // Pressurised sphere
        var sphereFaces = new List<(uint, uint, uint)>();
        var sphereSettings = CreateSphereSettings(1f, 20, 10, sphereFaces);
        RegisterSoftBody(bi, sphereSettings, sphereFaces,
            15f, 10f, 15f,
            0f, 0f, 0f, 1f,
            new Vector3(0.2f, 0.2f, 0.8f),
            cs => { cs.mPressure = 2000f; });

        // Hard sphere below the pressurised soft sphere
        AddSphere(bi, bodies, 1f, 15.5f, 7f, 15f,
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.7f, 0.7f, 0.7f),
            friction: 0.5f, mass: 100f);

        // Various shapes above the cloth — mirrors SoftBodyShapesTest.cpp
        // C++: bcs.mMassPropertiesOverride.mMass = 100 for all rigid bodies above cloth.
        // Missing C# equivalents (TaperedCapsule, TaperedCylinder, ConvexHull, Compound)
        // are substituted with the nearest available shape.
        var rotX90 = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI * 0.5f);
        var gray   = new Vector3(0.7f, 0.7f, 0.7f);

        // i=0 → Sphere(1)
        AddSphere(bi, bodies, 1f, -8f, 20f, 0f, JPH.EMotionType.Dynamic, LayerMoving, gray, mass: 100f);
        // i=1 → Box(0.75, 1, 1.25)
        AddBox(bi, bodies, 0.75f, 1f, 1.25f, -6f, 20f, 0f, Quaternion.Identity, JPH.EMotionType.Dynamic, LayerMoving, gray, mass: 100f);
        // i=2 → CapsuleShape(halfH=1, r=0.5) rotated 90° around X (lying on side)
        AddCapsule(bi, bodies, 0.5f, 1f, -4f, 20f, 0f, rotX90, JPH.EMotionType.Dynamic, LayerMoving, gray, mass: 100f);
        // i=3 → TaperedCapsule — substitute: Capsule(0.5, 0.75) rotated 90° X
        AddCapsule(bi, bodies, 0.5f, 0.75f, -2f, 20f, 0f, rotX90, JPH.EMotionType.Dynamic, LayerMoving, gray, mass: 100f);
        // i=4 → CylinderShape(halfH=1, r=0.5) rotated 90° around X
        AddCylinder(bi, bodies, 1f, 0.5f, 0f, 20f, 0f, rotX90, JPH.EMotionType.Dynamic, LayerMoving, gray, mass: 100f);
        // i=5 → TaperedCylinder — substitute: Cylinder(0.75, 0.5) rotated 90° X
        AddCylinder(bi, bodies, 0.75f, 0.5f, 2f, 20f, 0f, rotX90, JPH.EMotionType.Dynamic, LayerMoving, gray, mass: 100f);
        // i=6 → Tetrahedron convex hull — substitute: small Box
        AddBox(bi, bodies, 0.5f, 0.5f, 0.5f, 4f, 20f, 0f, Quaternion.Identity, JPH.EMotionType.Dynamic, LayerMoving, gray, mass: 100f);
        // i=7 → Compound (capsule + 2 spheres) — substitute: Sphere(1)
        AddSphere(bi, bodies, 1f, 6f, 20f, 0f, JPH.EMotionType.Dynamic, LayerMoving, gray, mass: 100f);
    }
}
