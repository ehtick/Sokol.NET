using Sokol;
using System;
using System.Collections.Generic;
using System.Numerics;

public sealed class Demo_ShapeMutableCompound : DemoBase
{
    public override string Name     => "Mutable Compound Shape";
    public override string Category => "Shapes";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 80,
        Latitude  = 25,
        Longitude = 0,
        Center    = new Vector3(0f, 30f, 0f),
    };

    public override unsafe void Init(
        JPH.BodyInterface bi, JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies, Random random)
    {
        // Thick floor
        using var floorHalf = new JPH.Vec3(100f, 10f, 100f);
        using var floorSS   = new JPH.BoxShapeSettings(floorHalf, 0f);
        using var floorCS   = new JPH.BodyCreationSettings();
        floorCS.SetShapeSettings(floorSS);
        floorCS.mPosition.Set(0f, -10f, 0f);
        floorCS.mMotionType  = JPH.EMotionType.Static;
        floorCS.mObjectLayer = LayerNonMoving;
        bodies.Add(new PhysicsBody
        {
            bodyId = bi.CreateAndAddBody(floorCS, JPH.EActivation.DontActivate),
            color  = new Vector3(0.5f, 0.5f, 0.5f),
            shape  = RenderShape.Box,
            scale  = new Vector3(200f, 20f, 200f),
        });

        // sub_compound_settings: box + cylinder + tapered capsule
        using var subCompoundSettings = new JPH.StaticCompoundShapeSettings();
        JPH.CompoundShapeSettings cmSub = subCompoundSettings;

        using var axisX   = new JPH.Vec3(1f, 0f, 0f);
        using var axisZ   = new JPH.Vec3(0f, 0f, 1f);

        using var halfBox  = new JPH.Vec3(1.5f, 0.25f, 0.2f);
        using var boxShape = new JPH.BoxShape(halfBox);
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

        // Two combined rotations for sub-compound placement
        using var rx1  = JPH.Quat.SRotation(axisX, -0.25f * MathF.PI);
        using var rz1  = JPH.Quat.SRotation(axisZ,  0.25f * MathF.PI);
        using var rot1 = rx1 * rz1;
        using var rx2  = JPH.Quat.SRotation(axisX,  0.25f * MathF.PI);
        using var rz2  = JPH.Quat.SRotation(axisZ, -0.75f * MathF.PI);
        using var rot2 = rx2 * rz2;
        using var zero = new JPH.Vec3(0f, 0f, 0f);

        // Precompute sub-shape render data.
        // Both sub-compounds sit at Vec3::sZero() in the MutableCompound's local space.
        var rot1q   = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.25f * MathF.PI)
                    * Quaternion.CreateFromAxisAngle(Vector3.UnitZ,  0.25f * MathF.PI);
        var rot2q   = Quaternion.CreateFromAxisAngle(Vector3.UnitX,  0.25f * MathF.PI)
                    * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -0.75f * MathF.PI);
        var rotZ90q = Quaternion.CreateFromAxisAngle(Vector3.UnitZ,  0.5f  * MathF.PI);
        var rotX90q = Quaternion.CreateFromAxisAngle(Vector3.UnitX,  0.5f  * MathF.PI);

        var box1Off  = Vector3.Transform(new Vector3(0,    1.5f, 0),    rot1q);
        var cyl1Off  = Vector3.Transform(new Vector3(1.5f, 0,    0),    rot1q);
        var tap1Off  = Vector3.Transform(new Vector3(0,    0,    1.5f), rot1q);
        var box2Off  = Vector3.Transform(new Vector3(0,    1.5f, 0),    rot2q);
        var cyl2Off  = Vector3.Transform(new Vector3(1.5f, 0,    0),    rot2q);
        var tap2Off  = Vector3.Transform(new Vector3(0,    0,    1.5f), rot2q);
        var box1Rot  = rot1q * rotZ90q;
        var tap1Rot  = rot1q * rotX90q;
        var box2Rot  = rot2q * rotZ90q;
        var tap2Rot  = rot2q * rotX90q;

        var boxSc = new Vector3(3f,     0.5f, 0.4f);
        var cylSc = new Vector3(0.4f,   3f,   0.4f);
        var tapSc = new Vector3(0.225f, 1.5f, 0.225f);

        for (int i = 0; i < 10; i++)
        {
            using var mc = new JPH.MutableCompoundShapeSettings();
            JPH.CompoundShapeSettings cmMC = mc;
            cmMC.AddShape(zero, rot1, subCompoundSettings);
            cmMC.AddShape(zero, rot2, subCompoundSettings);

            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(mc);
            cs.mPosition.Set(0f, 10f + 5f * i, 0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            var id  = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            var col = HsvToRgb(i / 10f, 0.7f, 1.0f);

            // sub_compound 1 (rot1): box + cylinder + tapered capsule
            bodies.Add(new PhysicsBody { bodyId=id, shape=RenderShape.Box,      scale=boxSc, color=col, localOffset=box1Off, localRotation=box1Rot });
            bodies.Add(new PhysicsBody { bodyId=id, shape=RenderShape.Cylinder, scale=cylSc, color=col, localOffset=cyl1Off, localRotation=box1Rot });
            bodies.Add(new PhysicsBody { bodyId=id, shape=RenderShape.Capsule,  scale=tapSc, color=col, localOffset=tap1Off, localRotation=tap1Rot });
            // sub_compound 2 (rot2): box + cylinder + tapered capsule
            bodies.Add(new PhysicsBody { bodyId=id, shape=RenderShape.Box,      scale=boxSc, color=col, localOffset=box2Off, localRotation=box2Rot });
            bodies.Add(new PhysicsBody { bodyId=id, shape=RenderShape.Cylinder, scale=cylSc, color=col, localOffset=cyl2Off, localRotation=box2Rot });
            bodies.Add(new PhysicsBody { bodyId=id, shape=RenderShape.Capsule,  scale=tapSc, color=col, localOffset=tap2Off, localRotation=tap2Rot });
        }
    }
}
