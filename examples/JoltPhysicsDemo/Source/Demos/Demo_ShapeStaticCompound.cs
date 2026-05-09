using Sokol;
using System;
using System.Collections.Generic;
using System.Numerics;

public sealed class Demo_ShapeStaticCompound : DemoBase
{
    public override string Name     => "Static Compound Shape";
    public override string Category => "Shapes";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 120,
        Latitude  = 25,
        Longitude = 0,
        Center    = new Vector3(0f, 20f, 30f),
    };

    public override unsafe void Init(
        JPH.BodyInterface bi, JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies, Random random)
    {
        AddFloor(bi, bodies);

        using var zero     = new JPH.Vec3(0f, 0f, 0f);
        using var identity = new JPH.Quat(0f, 0f, 0f, 1f);
        using var axisX    = new JPH.Vec3(1f, 0f, 0f);
        using var axisZ    = new JPH.Vec3(0f, 0f, 1f);

        // compound_shape1: capsule + 2 spheres
        using var compound1 = new JPH.StaticCompoundShapeSettings();
        JPH.CompoundShapeSettings cm1 = compound1;
        using var capsule = new JPH.CapsuleShape(5f, 1f);
        using var sph1    = new JPH.SphereShape(2f);
        using var sph2    = new JPH.SphereShape(2f);
        using var pos1Neg = new JPH.Vec3(0f, -5f, 0f);
        using var pos1Pos = new JPH.Vec3(0f,  5f, 0f);
        cm1.AddShape(zero, identity, capsule);
        cm1.AddShape(pos1Neg, identity, sph1);
        cm1.AddShape(pos1Pos, identity, sph2);

        // sub_compound: box + cylinder + tapered capsule
        using var subCompound = new JPH.StaticCompoundShapeSettings();
        JPH.CompoundShapeSettings cmSub = subCompound;
        using var halfBox3 = new JPH.Vec3(1.5f, 0.25f, 0.2f);
        using var boxShape = new JPH.BoxShape(halfBox3);
        using var cylShape = new JPH.CylinderShape(1.5f, 0.2f);
        using var tapCS    = new JPH.TaperedCapsuleShapeSettings(1.5f, 0.25f, 0.2f);
        using var posBox   = new JPH.Vec3(0f,   1.5f, 0f);
        using var posCyl   = new JPH.Vec3(1.5f, 0f,   0f);
        using var posTap   = new JPH.Vec3(0f,   0f,   1.5f);
        using var rotBoxZ  = JPH.Quat.SRotation(axisZ, 0.5f * MathF.PI);
        using var rotCylZ  = JPH.Quat.SRotation(axisZ, 0.5f * MathF.PI);
        using var rotTapX  = JPH.Quat.SRotation(axisX, 0.5f * MathF.PI);
        cmSub.AddShape(posBox, rotBoxZ, boxShape);
        cmSub.AddShape(posCyl, rotCylZ, cylShape);
        cmSub.AddShape(posTap, rotTapX, tapCS);

        // compound_shape2: 2 sub_compounds with combined rotations
        using var compound2 = new JPH.StaticCompoundShapeSettings();
        JPH.CompoundShapeSettings cm2 = compound2;
        using var rx1  = JPH.Quat.SRotation(axisX, -0.25f * MathF.PI);
        using var rz1  = JPH.Quat.SRotation(axisZ,  0.25f * MathF.PI);
        using var rot1 = rx1 * rz1;
        using var rx2  = JPH.Quat.SRotation(axisX,  0.25f * MathF.PI);
        using var rz2  = JPH.Quat.SRotation(axisZ, -0.75f * MathF.PI);
        using var rot2 = rx2 * rz2;
        using var pos2sub = new JPH.Vec3(0f, -0.1f, 0f);
        cm2.AddShape(zero, rot1, subCompound);
        cm2.AddShape(pos2sub, rot2, subCompound);

        // compound_shape3: 5×5×5 grid of boxes
        using var compound3 = new JPH.StaticCompoundShapeSettings();
        JPH.CompoundShapeSettings cm3 = compound3;
        using var halfBox35 = new JPH.Vec3(0.5f, 0.5f, 0.5f);
        using var rxG = JPH.Quat.SRotation(axisX, -0.25f * MathF.PI);
        using var rzG = JPH.Quat.SRotation(axisZ,  0.25f * MathF.PI);
        using var rotG = rxG * rzG;
        for (int y = -2; y <= 2; y++)
        for (int x = -2; x <= 2; x++)
        for (int z = -2; z <= 2; z++)
        {
            using var box = new JPH.BoxShape(halfBox35);
            using var pos = new JPH.Vec3(0.5f * x, 0.5f * y, 0.5f * z);
            cm3.AddShape(pos, rotG, box);
        }

        JPH.StaticCompoundShapeSettings[] shapes = { compound1, compound2, compound3 };

        // Precompute sub-shape render data for compound_shape2 and compound_shape3.
        // localOffset = sub-shape position in compound SHAPE-LOCAL space (arg to AddShape).
        // localRotation = sub-shape orientation in compound shape-local space.
        var rot1q   = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.25f * MathF.PI)
                    * Quaternion.CreateFromAxisAngle(Vector3.UnitZ,  0.25f * MathF.PI);
        var rot2q   = Quaternion.CreateFromAxisAngle(Vector3.UnitX,  0.25f * MathF.PI)
                    * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -0.75f * MathF.PI);
        var rotZ90q = Quaternion.CreateFromAxisAngle(Vector3.UnitZ,  0.5f  * MathF.PI);
        var rotX90q = Quaternion.CreateFromAxisAngle(Vector3.UnitX,  0.5f  * MathF.PI);
        var rotGq   = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.25f * MathF.PI)
                    * Quaternion.CreateFromAxisAngle(Vector3.UnitZ,  0.25f * MathF.PI);

        // compound_shape2: sub_compound 1 at (0,0,0) rot1, sub_compound 2 at (0,-0.1,0) rot2
        var sub2Base = new Vector3(0f, -0.1f, 0f);
        var box1Off  = Vector3.Transform(new Vector3(0,    1.5f, 0),    rot1q);
        var cyl1Off  = Vector3.Transform(new Vector3(1.5f, 0,    0),    rot1q);
        var tap1Off  = Vector3.Transform(new Vector3(0,    0,    1.5f), rot1q);
        var box2Off  = sub2Base + Vector3.Transform(new Vector3(0,    1.5f, 0),    rot2q);
        var cyl2Off  = sub2Base + Vector3.Transform(new Vector3(1.5f, 0,    0),    rot2q);
        var tap2Off  = sub2Base + Vector3.Transform(new Vector3(0,    0,    1.5f), rot2q);
        var box1Rot  = rot1q * rotZ90q;
        var tap1Rot  = rot1q * rotX90q;
        var box2Rot  = rot2q * rotZ90q;
        var tap2Rot  = rot2q * rotX90q;

        // Render scales: full extents (half-extents × 2)
        var boxSc = new Vector3(3f,     0.5f, 0.4f);   // BoxShape(1.5,0.25,0.2)
        var cylSc = new Vector3(0.4f,   3f,   0.4f);   // CylinderShape(halfH=1.5, r=0.2)
        var tapSc = new Vector3(0.225f, 1.5f, 0.225f); // TaperedCapsule avg r=0.225, halfH=1.5

        for (int i = 0; i < 10; i++)
        for (int j = 0; j < 3; j++)
        {
            JPH.Quat rotation;
            if ((i & 1) == 0)
                rotation = JPH.Quat.SRotation(axisX, 0.5f * MathF.PI);
            else
                rotation = JPH.Quat.SRotation(axisZ, 0.5f * MathF.PI);

            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(shapes[j]);
            cs.mPosition.Set(0f, 10f + 4f * i, j * 20f);
            cs.mRotation.Set(rotation.GetX(), rotation.GetY(), rotation.GetZ(), rotation.GetW());
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            var id  = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            var col = HsvToRgb(j / 3f, 0.7f, 1.0f);
            rotation.Dispose();

            if (j == 0)
            {
                // compound_shape1: CapsuleShape(halfH=5,r=1) at origin + 2×SphereShape(r=2) at ±5Y
                bodies.Add(new PhysicsBody { bodyId=id, shape=RenderShape.Capsule, scale=new Vector3(1f,5f,1f), color=col });
                bodies.Add(new PhysicsBody { bodyId=id, shape=RenderShape.Sphere,  scale=new Vector3(4f),       color=col, localOffset=new Vector3(0,-5f,0) });
                bodies.Add(new PhysicsBody { bodyId=id, shape=RenderShape.Sphere,  scale=new Vector3(4f),       color=col, localOffset=new Vector3(0, 5f,0) });
            }
            else if (j == 1)
            {
                // compound_shape2: 2×sub_compound, each with BoxShape+CylinderShape+TaperedCapsule
                bodies.Add(new PhysicsBody { bodyId=id, shape=RenderShape.Box,      scale=boxSc, color=col, localOffset=box1Off, localRotation=box1Rot });
                bodies.Add(new PhysicsBody { bodyId=id, shape=RenderShape.Cylinder, scale=cylSc, color=col, localOffset=cyl1Off, localRotation=box1Rot });
                bodies.Add(new PhysicsBody { bodyId=id, shape=RenderShape.Capsule,  scale=tapSc, color=col, localOffset=tap1Off, localRotation=tap1Rot });
                bodies.Add(new PhysicsBody { bodyId=id, shape=RenderShape.Box,      scale=boxSc, color=col, localOffset=box2Off, localRotation=box2Rot });
                bodies.Add(new PhysicsBody { bodyId=id, shape=RenderShape.Cylinder, scale=cylSc, color=col, localOffset=cyl2Off, localRotation=box2Rot });
                bodies.Add(new PhysicsBody { bodyId=id, shape=RenderShape.Capsule,  scale=tapSc, color=col, localOffset=tap2Off, localRotation=tap2Rot });
            }
            else
            {
                // compound_shape3: 5×5×5 grid of BoxShape(0.5) all rotated by rotGq
                for (int gy = -2; gy <= 2; gy++)
                for (int gx = -2; gx <= 2; gx++)
                for (int gz = -2; gz <= 2; gz++)
                {
                    var lOff = new Vector3(0.5f * gx, 0.5f * gy, 0.5f * gz);
                    // Ensure non-zero so GetWorldTransform path applies localRotation
                    if (lOff == Vector3.Zero) lOff = new Vector3(0, 0, 1e-5f);
                    bodies.Add(new PhysicsBody
                    {
                        bodyId        = id,
                        shape         = RenderShape.Box,
                        scale         = Vector3.One,
                        color         = col,
                        localOffset   = lOff,
                        localRotation = rotGq,
                    });
                }
            }
        }
    }
}

