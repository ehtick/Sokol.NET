using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of CenterOfMassTest.cpp:
/// Three dynamic bodies whose shape has a large center-of-mass offset,
/// demonstrating how Jolt corrects body origin vs CoM.
///
///   Body 1 — StaticCompoundShape: a single SphereShape (r=2) offset 10 m along X.
///   Body 2 — ConvexHullShape: a box whose vertices all sit in the +X+Y+Z octant,
///             so the CoM is at roughly (7.5, 7.5, 7.5). Spawned at z=20.
///   Body 3 — StaticCompoundShape: CapsuleShape (half=5, r=1) + two spheres
///             (r=4 and r=2) offset from center, spawned at z=40.
/// </summary>
public sealed class Demo_CenterOfMass : DemoBase
{
    public override string Name     => "Center Of Mass";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 100,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(5f, 10f, 20f),
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
        AddFloor(bi, bodies);

        // ── Body 1: StaticCompoundShape — sphere (r=2) offset +10 m on X ────
        {
            using var compound = new JPH.StaticCompoundShapeSettings();
            JPH.CompoundShapeSettings compoundBase = compound; // access AddShape
            using var pos      = new JPH.Vec3(10f, 0f, 0f);
            using var rot      = new JPH.Quat(0f, 0f, 0f, 1f);
            using var sphere   = new JPH.SphereShapeSettings(2f);
            compoundBase.AddShape(pos, rot, sphere);

            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(compound);
            cs.mPosition.Set(0f, 10f, 0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody
            {
                bodyId = id,
                color  = new Vector3(0.3f, 0.7f, 0.9f),
                shape  = RenderShape.Sphere,
                scale  = new Vector3(4f)   // diameter = 2*r
            });
        }

        // ── Body 2: StaticCompoundShape — BoxShape (2.5 half-extents) at (7.5,7.5,7.5)
        //    Equivalent CoM offset to ConvexHull with vertices at {5..10}^3,
        //    but uses the same compound-shape path that works for bodies 1 and 3.
        {
            using var compound   = new JPH.StaticCompoundShapeSettings();
            JPH.CompoundShapeSettings compoundBase = compound;
            using var boxPos     = new JPH.Vec3(7.5f, 7.5f, 7.5f);
            using var boxRot     = new JPH.Quat(0f, 0f, 0f, 1f);
            using var boxSS      = new JPH.BoxShapeSettings(new JPH.Vec3(2.5f, 2.5f, 2.5f));
            compoundBase.AddShape(boxPos, boxRot, boxSS);

            var tilt = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.25f * MathF.PI);
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(compound);
            cs.mPosition.Set(0f, 10f, 20f);
            cs.mRotation.Set(tilt.X, tilt.Y, tilt.Z, tilt.W);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody
            {
                bodyId      = id,
                color       = new Vector3(0.9f, 0.6f, 0.2f),
                shape       = RenderShape.Box,
                scale       = new Vector3(5f),    // 5×5×5 box visual
                localOffset = new Vector3(7.5f, 7.5f, 7.5f),
            });
        }

        // ── Body 3: StaticCompoundShape — capsule + 2 spheres at offset ─────
        {
            using var compound = new JPH.StaticCompoundShapeSettings();
            JPH.CompoundShapeSettings compoundBase = compound; // access AddShape
            var tilt = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.25f * MathF.PI);

            using var capsPos    = new JPH.Vec3(10f, 0f, 0f);
            using var capsRot    = new JPH.Quat(tilt.X, tilt.Y, tilt.Z, tilt.W);
            using var capsule    = new JPH.CapsuleShapeSettings(5f, 1f);
            compoundBase.AddShape(capsPos, capsRot, capsule);

            // sphere r=4 at rotated offset (10, -5, 0)
            var off4  = Vector3.Transform(new Vector3(10f, -5f, 0f), tilt);
            using var sph4Pos = new JPH.Vec3(off4.X, off4.Y, off4.Z);
            using var identRot = new JPH.Quat(0f, 0f, 0f, 1f);
            using var sphere4  = new JPH.SphereShapeSettings(4f);
            compoundBase.AddShape(sph4Pos, identRot, sphere4);

            // sphere r=2 at rotated offset (10, +5, 0)
            var off2  = Vector3.Transform(new Vector3(10f, 5f, 0f), tilt);
            using var sph2Pos = new JPH.Vec3(off2.X, off2.Y, off2.Z);
            using var sphere2  = new JPH.SphereShapeSettings(2f);
            compoundBase.AddShape(sph2Pos, identRot, sphere2);

            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(compound);
            cs.mPosition.Set(0f, 10f, 40f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);

            // Three sub-shape entries — each rendered at its local position within the compound.
            // Sub-shape positions are in SHAPE LOCAL space (matching the AddShape calls above).
            var color3 = new Vector3(0.5f, 0.9f, 0.4f);

            // Capsule (halfH=5, r=1) at shape-local (10, 0, 0) with tilt rotation
            bodies.Add(new PhysicsBody
            {
                bodyId        = id,
                color         = color3,
                shape         = RenderShape.Capsule,
                scale         = new Vector3(1f, 5f, 1f),      // radius=1, halfCylH=5
                localOffset   = new Vector3(10f, 0f, 0f),
                localRotation = tilt,
            });

            // Sphere r=4 at shape-local rotation*(10, -5, 0)
            bodies.Add(new PhysicsBody
            {
                bodyId      = id,
                color       = color3,
                shape       = RenderShape.Sphere,
                scale       = new Vector3(8f),                // diameter = 2*4
                localOffset = off4,
            });

            // Sphere r=2 at shape-local rotation*(10, +5, 0)
            bodies.Add(new PhysicsBody
            {
                bodyId      = id,
                color       = color3,
                shape       = RenderShape.Sphere,
                scale       = new Vector3(4f),                // diameter = 2*2
                localOffset = off2,
            });
        }
    }
}
