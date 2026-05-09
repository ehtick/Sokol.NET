using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Stress-test: 5 000 cubes + 5 000 spheres.
/// Uses the batch body API for efficient bulk insertion.
/// </summary>
public sealed class Demo_MassSpawn : DemoBase
{
    public override string Name     => "Mass Spawn (10k bodies)";
    public override string Category => "General";

    const int Count = 5000;

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 60,
        Latitude  = 25,
        Longitude = 45,
        Center    = new Vector3(0, 10, 0),
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
        // ── Floor ─────────────────────────────────────────────────────────
        AddFloor(bi, bodies, hx: 25f, hy: 2.5f, hz: 25f, cy: -2.5f);

        // ── Create cube + sphere bodies (not yet added) ───────────────────
        var batchIds  = new JPH.BodyID[Count * 2];
        int batchCount = 0;

        using var cubeHalf = new JPH.Vec3(1f, 1f, 1f);
        using var cubeSS   = new JPH.BoxShapeSettings(cubeHalf);
        using var cubeCS   = new JPH.BodyCreationSettings();
        cubeCS.SetShapeSettings(cubeSS);
        cubeCS.mMotionType  = JPH.EMotionType.Dynamic;
        cubeCS.mObjectLayer = LayerMoving;
        cubeCS.mFriction    = 0.2f;
        cubeCS.mRestitution = 0.3f;

        for (int i = 0; i < Count; i++)
        {
            cubeCS.mPosition.Set(
                random.NextSingle() * 4f - 2f,
                10f + i * 2f,
                random.NextSingle() * 4f - 2f);
            var body = bi.CreateBody(cubeCS)!;
            var id   = body.GetID();
            batchIds[batchCount++] = id;
            bodies.Add(new PhysicsBody
            {
                bodyId = id,
                color  = RandomColor(random),
                shape  = RenderShape.Box,
                scale  = new Vector3(2f)
            });
        }

        using var sphereSS = new JPH.SphereShapeSettings(0.5f);
        using var sphereCS = new JPH.BodyCreationSettings();
        sphereCS.SetShapeSettings(sphereSS);
        sphereCS.mMotionType  = JPH.EMotionType.Dynamic;
        sphereCS.mObjectLayer = LayerMoving;
        sphereCS.mFriction    = 0.2f;
        sphereCS.mRestitution = 0.3f;

        for (int i = 0; i < Count; i++)
        {
            sphereCS.mPosition.Set(
                random.NextSingle() * 4f - 2f,
                15f + i * 2f,
                random.NextSingle() * 4f - 2f);
            sphereCS.mAngularVelocity.Set(
                (random.NextSingle() - 0.5f) * 4f,
                (random.NextSingle() - 0.5f) * 4f,
                (random.NextSingle() - 0.5f) * 4f);
            var body = bi.CreateBody(sphereCS)!;
            var id   = body.GetID();
            batchIds[batchCount++] = id;
            bodies.Add(new PhysicsBody
            {
                bodyId = id,
                color  = RandomColor(random),
                shape  = RenderShape.Sphere,
                scale  = new Vector3(1f)
            });
        }

        // Batch-add all dynamic bodies at once
        void* addState = bi.AddBodiesPrepare(batchIds);
        bi.AddBodiesFinalize(batchIds, addState, JPH.EActivation.Activate);
    }

    static Vector3 RandomColor(Random r) =>
        new Vector3(
            r.NextSingle() * 0.5f + 0.5f,
            r.NextSingle() * 0.5f + 0.5f,
            r.NextSingle() * 0.5f + 0.5f);
}
