using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of SoftBodyVsFastMovingTest — fast-moving LinearCast sphere punches through a cloth.
/// </summary>
public class Demo_SoftBody_VsFastMoving : DemoBase
{
    public override string Name     => "SoftBody: Vs Fast Moving";
    public override string Category  => "Soft Body";

    public override unsafe void Init(JPH.BodyInterface bi, JPH.PhysicsSystem physicsSystem,
                               List<PhysicsBody> bodies, Random rng)
    {

        AddFloor(bi, bodies);

        // Fast LinearCast sphere
        var bcs1 = new JPH.BodyCreationSettings();
        var sphere1 = new JPH.SphereShapeSettings(1f);
        bcs1.SetShapeSettings(sphere1);
        bcs1.mPosition.Set(-2f, 20f, 0f);
        bcs1.mRotation.Set(0f, 0f, 0f, 1f);
        bcs1.mMotionType   = JPH.EMotionType.Dynamic;
        bcs1.mObjectLayer  = LayerMoving;
        bcs1.mMotionQuality = JPH.EMotionQuality.LinearCast;
        bcs1.mLinearVelocity.Set(0f, -250f, 0f);
        bcs1.SetOverrideMassProperties(1);
        bcs1.SetMassOverride(25f);
        var id1 = bi.CreateAndAddBody(bcs1, JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody { bodyId = id1, shape = RenderShape.Sphere,
            color = new Vector3(0.8f, 0.2f, 0.2f), scale = new Vector3(2f) });
        bcs1.Dispose(); sphere1.Dispose();

        // Cloth fixated at corners, slightly rotated so it isn't perfectly flat
        float sinX = MathF.Sin(0.05f * MathF.PI);
        float cosX = MathF.Cos(0.05f * MathF.PI);
        float sinY = MathF.Sin(0.125f * MathF.PI);
        float cosY = MathF.Cos(0.125f * MathF.PI);
        // quat = rotY * rotX
        float qx = sinX * cosY;
        float qy = cosX * sinY;
        float qz = sinX * sinY;
        float qw = cosX * cosY;
        var clothFaces = new List<(uint, uint, uint)>();
        var clothSettings = CreateClothWithFixatedCornersSettings(30, 30, 0.75f, clothFaces);
        RegisterSoftBody(bi, clothSettings, clothFaces,
            0f, 15f, 0f,
            qx, qy, qz, qw,
            new Vector3(0.8f, 0.8f, 0.2f),
            cs => { cs.mUpdatePosition = false; cs.mMakeRotationIdentity = false; cs.mVertexRadius = 0.1f; });

        // Second fast sphere (higher body ID than cloth to test ordering)
        var bcs2 = new JPH.BodyCreationSettings();
        var sphere2 = new JPH.SphereShapeSettings(1f);
        bcs2.SetShapeSettings(sphere2);
        bcs2.mPosition.Set(2f, 20f, 0f);
        bcs2.mRotation.Set(0f, 0f, 0f, 1f);
        bcs2.mMotionType   = JPH.EMotionType.Dynamic;
        bcs2.mObjectLayer  = LayerMoving;
        bcs2.mMotionQuality = JPH.EMotionQuality.LinearCast;
        bcs2.mLinearVelocity.Set(0f, -250f, 0f);
        bcs2.SetOverrideMassProperties(1);
        bcs2.SetMassOverride(25f);
        var id2 = bi.CreateAndAddBody(bcs2, JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody { bodyId = id2, shape = RenderShape.Sphere,
            color = new Vector3(0.8f, 0.2f, 0.2f), scale = new Vector3(2f) });
        bcs2.Dispose(); sphere2.Dispose();
    }
}
