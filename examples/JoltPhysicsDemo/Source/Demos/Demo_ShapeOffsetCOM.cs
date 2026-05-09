using Sokol;
using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of OffsetCenterOfMassShapeTest.cpp —
///   Three spheres with CoM at left, center, and right, plus two rotating spheres
///   using AddAngularImpulse and AddTorque to demonstrate spinning around the CoM.
/// </summary>
public sealed class Demo_ShapeOffsetCOM : DemoBase
{
    public override string Name     => "Offset Center Of Mass Shape";
    public override string Category => "Shapes";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 40,
        Latitude  = 10,
        Longitude = 0,
        Center    = new Vector3(0f, 5f, 5f),
    };

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        // Thick floor with high friction
        AddBox(bi, bodies, 100f, 1f, 100f, 0f, -1f, 0f, Quaternion.Identity,
               JPH.EMotionType.Static, LayerNonMoving, new Vector3(0.4f, 0.4f, 0.4f), 1.0f);

        using var sphereSS = new JPH.SphereShapeSettings(1.0f);

        // CoM shifted left
        using var leftOffset  = new JPH.Vec3(-1f, 0f, 0f);
        using var rightOffset = new JPH.Vec3( 1f, 0f, 0f);
        using var leftSS   = new JPH.OffsetCenterOfMassShapeSettings(leftOffset,  sphereSS);
        // CoM shifted right
        using var rightSS  = new JPH.OffsetCenterOfMassShapeSettings(rightOffset, sphereSS);

        // Left-biased CoM sphere
        {
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(leftSS);
            cs.mPosition.Set(-5f, 5f, 0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            cs.mFriction    = 1.0f;
            var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = id, color = new Vector3(0.3f, 0.6f, 1f), shape = RenderShape.Sphere, scale = new Vector3(2f) });
        }

        // Centered CoM sphere (plain sphere)
        {
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(sphereSS);
            cs.mPosition.Set(0f, 5f, 0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            cs.mFriction    = 1.0f;
            var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = id, color = new Vector3(0.9f, 0.9f, 0.3f), shape = RenderShape.Sphere, scale = new Vector3(2f) });
        }

        // Right-biased CoM sphere
        {
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(rightSS);
            cs.mPosition.Set(5f, 5f, 0f);
            cs.mMotionType  = JPH.EMotionType.Dynamic;
            cs.mObjectLayer = LayerMoving;
            cs.mFriction    = 1.0f;
            var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = id, color = new Vector3(0.2f, 0.9f, 0.3f), shape = RenderShape.Sphere, scale = new Vector3(2f) });
        }

        // ── Two rotating spheres at z=10 ───────────────────────────────────────
        using var rotSphSS = new JPH.SphereShapeSettings(1.0f);
        using var rotOff   = new JPH.Vec3(-3f, 0f, 0f);
        using var rotSS = new JPH.OffsetCenterOfMassShapeSettings(rotOff, rotSphSS);

        // Body 1: AddAngularImpulse
        {
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(rotSS);
            cs.mPosition.Set(-5f, 5f, 10f);
            cs.mMotionType      = JPH.EMotionType.Dynamic;
            cs.mObjectLayer     = LayerMoving;
            cs.mGravityFactor   = 0f;
            cs.mLinearDamping   = 0f;
            cs.mAngularDamping  = 0f;
            var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
        using var impulse   = new JPH.Vec3(0f, 1e6f, 0f);
            bi.AddAngularImpulse(id, impulse);
            bodies.Add(new PhysicsBody { bodyId = id, color = new Vector3(1f, 0.4f, 0.4f), shape = RenderShape.Sphere, scale = new Vector3(2f) });
        }

        // Body 2: AddTorque
        {
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(rotSS);
            cs.mPosition.Set( 5f, 5f, 10f);
            cs.mMotionType      = JPH.EMotionType.Dynamic;
            cs.mObjectLayer     = LayerMoving;
            cs.mGravityFactor   = 0f;
            cs.mLinearDamping   = 0f;
            cs.mAngularDamping  = 0f;
            var id = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
        using var torque    = new JPH.Vec3(0f, 1e6f * 60f, 0f);
            bi.AddTorque(id, torque);
            bodies.Add(new PhysicsBody { bodyId = id, color = new Vector3(0.4f, 0.4f, 1f), shape = RenderShape.Sphere, scale = new Vector3(2f) });
        }
    }
}
