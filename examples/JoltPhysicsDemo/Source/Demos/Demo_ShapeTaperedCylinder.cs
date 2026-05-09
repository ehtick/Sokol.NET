using Sokol;
using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of TaperedCylinderShapeTest.cpp — big TaperedCylinder bodies (rendered as Cylinder approximations),
/// cones, and a tower of long tapered cylinders.
///
/// Bodies are rendered as exact truncated cone meshes using RenderShape.TaperedCylinder.
/// </summary>
public sealed class Demo_ShapeTaperedCylinder : DemoBase
{
    public override string Name     => "Tapered Cylinder Shape";
    public override string Category => "Shapes";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 80,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(10f, 10f, 5f),
    };

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies);

        var identity = Quaternion.Identity;
        var rotX90   = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.5f * MathF.PI);

        // bigTaperedCylinder(halfHeight=2, topR=1, botR=3)
        AddTaperedCylinder(bi, bodies, 2f, 1f, 3f,  0f, 10f,  0f, identity, JPH.EMotionType.Dynamic, LayerMoving, new Vector3(0.3f, 0.6f, 1.0f));
        // bigTaperedCylinder2(halfHeight=2, topR=3, botR=1)
        AddTaperedCylinder(bi, bodies, 2f, 3f, 1f, 10f, 10f,  0f, identity, JPH.EMotionType.Dynamic, LayerMoving, new Vector3(1.0f, 0.6f, 0.2f));
        // On its side
        AddTaperedCylinder(bi, bodies, 2f, 1f, 3f, 20f, 10f,  0f, rotX90,   JPH.EMotionType.Dynamic, LayerMoving, new Vector3(0.2f, 0.9f, 0.3f));

        // Cones: topR=0 (cone), convexRadius=0 to match C++
        AddTaperedCylinder(bi, bodies, 2f, 0f, 3f,  0f, 10f, 10f, identity, JPH.EMotionType.Dynamic, LayerMoving, new Vector3(0.9f, 0.3f, 0.3f), 0f);
        AddTaperedCylinder(bi, bodies, 2f, 3f, 0f, 10f, 10f, 10f, identity, JPH.EMotionType.Dynamic, LayerMoving, new Vector3(0.9f, 0.7f, 0.2f), 0f);
        AddTaperedCylinder(bi, bodies, 2f, 0f, 3f, 20f, 10f, 10f, rotX90,   JPH.EMotionType.Dynamic, LayerMoving, new Vector3(0.6f, 0.2f, 0.9f), 0f);

        // Tower of long tapered cylinders (halfHeight=5, topR=0.5, botR=1)
        for (int i = 0; i < 10; ++i)
        {
            for (int j = 0; j < 2; ++j)
            {
                float px, pz;
                Quaternion rot;
                float extra = (j & 1) != 0 ? MathF.PI : 0f;
                if ((i & 1) != 0)
                {
                    px  = -4f + 8f * j;
                    pz  = -20f;
                    rot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.5f * MathF.PI + extra);
                }
                else
                {
                    px  = 0f;
                    pz  = -20f - 4f + 8f * j;
                    rot = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.5f * MathF.PI + extra);
                }
                var col = HsvToRgb((i * 2 + j) / 20f, 0.8f, 1.0f);
                AddTaperedCylinder(bi, bodies, 5f, 0.5f, 1f, px, 2f + 3f * i, pz, rot, JPH.EMotionType.Dynamic, LayerMoving, col);
            }
        }
    }

    /// <summary>
    /// Creates a TaperedCylinderShape and renders it as an exact truncated cone mesh with
    /// localOffset to account for the COM not being at the geometric midpoint.
    /// </summary>
    private static unsafe void AddTaperedCylinder(
        JPH.BodyInterface bi, List<PhysicsBody> bodies,
        float halfHeight, float topR, float botR,
        float px, float py, float pz,
        Quaternion rotation,
        JPH.EMotionType motionType, ushort layer,
        Vector3 color, float? convexRadius = null)
    {
        using var ss = new JPH.TaperedCylinderShapeSettings(halfHeight, topR, botR, convexRadius);
        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(px, py, pz);
        cs.mRotation.Set(rotation.X, rotation.Y, rotation.Z, rotation.W);
        cs.mMotionType  = motionType;
        cs.mObjectLayer = layer;
        var activation = motionType == JPH.EMotionType.Static ? JPH.EActivation.DontActivate : JPH.EActivation.Activate;
        var id = bi.CreateAndAddBody(cs, activation);

        // TaperedCylinder stores the shape in COM-centered space.
        // The COM is at y = com (measured from the bottom of the natural cylinder).
        // The cylinder midpoint in shape-local = halfH - com = halfH*(botR²-topR²) / (2*(topR²+botR*topR+botR²))
        float tr2 = topR * topR, br2 = botR * botR;
        float denom = tr2 + botR * topR + br2;
        float localOffsetY = (denom > 0f) ? halfHeight * (br2 - tr2) / (2f * denom) : 0f;

        var coneMesh = CreateTaperedConeMesh(topR, botR, halfHeight, out int coneIdxCount);
        bodies.Add(new PhysicsBody
        {
            bodyId               = id,
            color                = color,
            shape                = RenderShape.TaperedCylinder,
            scale                = Vector3.One,
            localOffset          = new Vector3(0f, localOffsetY, 0f),
            customMesh           = coneMesh,
            customMeshIndexCount = coneIdxCount,
        });
    }
}
