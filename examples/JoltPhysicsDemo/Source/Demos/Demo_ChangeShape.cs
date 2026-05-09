using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of ChangeShapeTest.cpp:
/// A single dynamic body whose shape is swapped every 3 seconds —
/// cycling through Box → Sphere → Capsule — demonstrating
/// BodyInterface::SetShape at runtime.
/// (TaperedCapsule omitted: no direct shape constructor in C# bindings.)
/// </summary>
public sealed class Demo_ChangeShape : DemoBase
{
    public override string Name     => "Change Shape";
    public override string Category => "General";

    const float SwitchInterval = 3f;

    // Pre-built shapes — created in Init, disposed in Deactivate
    JPH.Const_BoxShape?     _boxShape;
    JPH.Const_SphereShape?  _sphereShape;
    JPH.Const_CapsuleShape? _capsuleShape;

    // Render metadata matching each shape index
    static readonly (RenderShape render, Vector3 scale)[] ShapeMeta = new[]
    {
        (RenderShape.Box,     new Vector3(1f, 3f, 1f)),   // box  he=(0.5,1.5,0.5) → diameter×height
        (RenderShape.Sphere,  new Vector3(1f)),            // sphere r=0.5
        (RenderShape.Capsule, new Vector3(1f, 1f, 1f)),   // capsule r=0.5 halfH=1
    };

    JPH.BodyID _bodyId;
    int        _bodyIndex;   // index into bodies list
    float      _timer;
    int        _shapeIndex;

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 30,
        Latitude  = 20,
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
        _timer      = 0f;
        _shapeIndex = 0;

        AddFloor(bi, bodies);

        // Pre-build shapes that will be cycled through
        using var he = new JPH.Vec3(0.5f, 1.5f, 0.5f);
        _boxShape     = new JPH.Const_BoxShape(he);
        _sphereShape  = new JPH.Const_SphereShape(0.5f);
        _capsuleShape = new JPH.Const_CapsuleShape(1.0f, 0.5f);

        // Spawn dynamic body starting as box
        using var ss = new JPH.BoxShapeSettings(he);
        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(0f, 10f, 0f);
        cs.mMotionType  = JPH.EMotionType.Dynamic;
        cs.mObjectLayer = LayerMoving;

        _bodyId    = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
        _bodyIndex = bodies.Count;
        bodies.Add(new PhysicsBody
        {
            bodyId = _bodyId,
            color  = new Vector3(0.4f, 0.7f, 1.0f),
            shape  = ShapeMeta[0].render,
            scale  = ShapeMeta[0].scale,
        });
    }

    public override unsafe void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        _timer += dt;
        if (_timer < SwitchInterval) return;
        _timer -= SwitchInterval;

        _shapeIndex = (_shapeIndex + 1) % ShapeMeta.Length;
        var (render, scale) = ShapeMeta[_shapeIndex];

        JPH.Const_Shape shape = _shapeIndex switch
        {
            0 => _boxShape!,
            1 => _sphereShape!,
            _ => _capsuleShape!,
        };

        bi.SetShape(_bodyId, shape, inUpdateMassProperties: true, JPH.EActivation.Activate);

        // Update render metadata in bodies list (struct — must replace in place)
        var pb = bodies[_bodyIndex];
        pb.shape = render;
        pb.scale = scale;
        bodies[_bodyIndex] = pb;
    }

    public override void Deactivate(JPH.PhysicsSystem sys)
    {
        _boxShape?.Dispose();     _boxShape     = null;
        _sphereShape?.Dispose();  _sphereShape  = null;
        _capsuleShape?.Dispose(); _capsuleShape = null;
    }
}
