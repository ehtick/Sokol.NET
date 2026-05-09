using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of ChangeObjectLayerTest.cpp:
/// A large flat box (the "mover") sits in the MOVING layer. 50 small debris
/// cubes toggle every 2 seconds between:
///   DEBRIS  — collides only with the floor (NON_MOVING), not with the mover or each other.
///   MOVING  — collides with everything (floor, mover, other MOVING bodies).
/// This demonstrates how changing a body's object layer at runtime changes what it collides with.
/// </summary>
public sealed class Demo_ChangeObjectLayer : DemoBase
{
    public override string Name     => "Change Object Layer";
    public override string Category => "General";

    const int   DebrisCount  = 50;
    const float SwitchTime   = 2.0f;

    JPH.BodyID   _moving;
    JPH.BodyID[] _debris = Array.Empty<JPH.BodyID>();
    // Indices into the bodies list (used to update colour when layer changes)
    int[] _debrisBodyIdx = Array.Empty<int>();

    float  _time;
    bool   _isDebris;  // current layer of debris: true = DEBRIS, false = MOVING
    Random _random = new Random(42);

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 40,
        Latitude  = 30,
        Longitude = 0,
        Center    = new Vector3(0f, 5f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 500.0f
    };

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        _time     = 0f;
        _isDebris = true;
        _random   = new Random(42);

        AddFloor(bi, bodies);

        // Large flat mover box — starts in MOVING layer
        _moving = AddBox(bi, bodies, 5f, 0.1f, 5f,
            0f, 1.5f, 0f,
            Quaternion.Identity,
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.3f, 0.7f, 0.9f));

        // 50 small debris cubes — start in DEBRIS layer
        _debris       = new JPH.BodyID[DebrisCount];
        _debrisBodyIdx = new int[DebrisCount];

        for (int i = 0; i < DebrisCount; i++)
        {
            float px = (float)(random.NextDouble() * 20.0 - 10.0);
            float pz = (float)(random.NextDouble() * 20.0 - 10.0);

            int idx = bodies.Count;
            _debrisBodyIdx[i] = idx;
            _debris[i] = AddBox(bi, bodies, 0.1f, 0.11f, 0.11f,
                px, 2.0f, pz,
                Quaternion.Identity,
                JPH.EMotionType.Dynamic, LayerDebris,
                HsvToRgb(i / (float)DebrisCount, 0.8f, 1.0f));
        }
    }

    public override unsafe void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        _time += dt;
        if (_time < SwitchTime) return;
        _time = 0f;

        _isDebris = !_isDebris;

        // Reset the mover
        using var moverPos = new JPH.Vec3(0f, 1.5f, 0f);
        using var identRot = JPH.Quat.SIdentity();
        bi.SetPositionAndRotation(_moving, moverPos, identRot, JPH.EActivation.Activate);

        ushort newLayer = _isDebris ? LayerDebris : LayerMoving;

        using var zero = new JPH.Vec3(0f, 0f, 0f);
        for (int i = 0; i < DebrisCount; i++)
        {
            // Scatter to new random position
            float px = (float)(_random.NextDouble() * 15.0 - 7.5);
            float pz = (float)(_random.NextDouble() * 15.0 - 7.5);

            using var pos = new JPH.Vec3(px, 2.0f, pz);
            bi.SetPositionAndRotation(_debris[i], pos, identRot, JPH.EActivation.Activate);
            // Clear accumulated velocity so debris don't tunnel through the floor
            // when switching to LayerMoving (in LayerDebris they fall indefinitely).
            bi.SetLinearAndAngularVelocity(_debris[i], zero, zero);

            // Switch object layer
            bi.SetObjectLayer(_debris[i], newLayer);
        }
    }
}
