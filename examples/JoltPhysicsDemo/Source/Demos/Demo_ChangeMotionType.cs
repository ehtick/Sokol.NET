using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Four towers of boxes start as Static (frozen in place).  One tower is
/// released per second, toppling each one onto the floor in turn.
/// Demonstrates BodyInterface::SetMotionType — switching Static → Dynamic
/// at runtime to "unfreeze" bodies.
/// Corresponds to ChangeMotionTypeTest.cpp.
/// </summary>
public class Demo_ChangeMotionType : DemoBase
{
    public override string Name     => "Change Motion Type";
    public override string Category => "General";

    const int   TowerCount  = 4;
    const int   BoxesPerTower = 6;
    const float ReleaseInterval = 1.2f;

    // Body IDs grouped by tower — set at Init time
    private readonly JPH.BodyID[][] _towerBodies = new JPH.BodyID[TowerCount][];

    private float _timer;
    private int   _released;

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        _timer    = 0f;
        _released = 0;

        AddFloor(bi, bodies, 80f, 0.5f, 20f);

        const float BoxHE    = 1.0f;
        const float Spacing  = 18f;

        var towerColors = new Vector3[]
        {
            new Vector3(0.85f, 0.25f, 0.20f),
            new Vector3(0.25f, 0.65f, 0.85f),
            new Vector3(0.25f, 0.75f, 0.30f),
            new Vector3(0.90f, 0.70f, 0.15f),
        };

        float totalW = (TowerCount - 1) * Spacing;
        float x0     = -totalW * 0.5f;

        for (int t = 0; t < TowerCount; t++)
        {
            _towerBodies[t] = new JPH.BodyID[BoxesPerTower];
            float tx = x0 + t * Spacing;

            for (int k = 0; k < BoxesPerTower; k++)
            {
                float y = BoxHE + k * BoxHE * 2f;

                // Give each box a slight tilt on top of each other to help them
                // topple in interesting directions once released.
                float tiltZ = (k % 2 == 0) ? 0.015f : -0.015f;
                var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, tiltZ);

                using var half = new JPH.Vec3(BoxHE, BoxHE, BoxHE);
                using var ss   = new JPH.BoxShapeSettings(half);
                using var cs   = new JPH.BodyCreationSettings();
                cs.SetShapeSettings(ss);
                cs.mPosition.Set(tx, y, 0f);
                cs.mRotation.Set(rot.X, rot.Y, rot.Z, rot.W);
                cs.mMotionType              = JPH.EMotionType.Static;
                cs.mObjectLayer             = LayerNonMoving;
                cs.mAllowDynamicOrKinematic = true;  // pre-allocates MotionProperties so SetMotionType→Dynamic works later
                cs.mFriction    = 0.01f;
                cs.mRestitution = 0.2f;

                var id = bi.CreateAndAddBody(cs, JPH.EActivation.DontActivate);
                _towerBodies[t][k] = id;
                bodies.Add(new PhysicsBody
                {
                    bodyId = id,
                    color  = towerColors[t],
                    shape  = RenderShape.Box,
                    scale  = new Vector3(BoxHE * 2f, BoxHE * 2f, BoxHE * 2f)
                });
            }
        }
    }

    public override void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        if (_released >= TowerCount) return;

        _timer += dt;
        while (_released < TowerCount && _timer >= (_released + 1) * ReleaseInterval)
        {
            float tipDir = (_released % 2 == 0) ? 1f : -1f;
            using var angVel = new JPH.Vec3(0f, 0f, tipDir * 5f);

            foreach (var id in _towerBodies[_released])
            {
                bi.SetObjectLayer(id, LayerMoving);
                bi.SetMotionType(id, JPH.EMotionType.Dynamic, JPH.EActivation.Activate);
                bi.SetAngularVelocity(id, angVel);
            }
            _released++;
        }
    }

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 90,
        Latitude  = 18,
        Longitude = 0,
        Center    = new Vector3(0f, 8f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000f
    };
}
