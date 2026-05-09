using Sokol;
using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of TaperedCapsuleShapeTest.cpp — big TaperedCapsule bodies (rendered as Capsule approximations)
/// and a tower of long tapered capsules.
///
/// Note: RenderShape has no TaperedCapsule type. Bodies are rendered as Capsule using the average radius.
/// </summary>
public sealed class Demo_ShapeTaperedCapsule : DemoBase
{
    public override string Name     => "Tapered Capsule Shape";
    public override string Category => "Shapes";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 80,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(10f, 10f, -10f),
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

        // bigTaperedCapsule(halfHeight=2, topR=1, botR=3): render as Capsule, avg r=2
        AddTaperedCapsule(bi, bodies, halfHeight: 2f, topR: 1f, botR: 3f,
             0f, 10f, 0f, identity, JPH.EMotionType.Dynamic, LayerMoving, new Vector3(0.3f, 0.6f, 1.0f));

        // bigTaperedCapsule2(halfHeight=2, topR=3, botR=1)
        AddTaperedCapsule(bi, bodies, halfHeight: 2f, topR: 3f, botR: 1f,
            10f, 10f, 0f, identity, JPH.EMotionType.Dynamic, LayerMoving, new Vector3(1.0f, 0.6f, 0.2f));

        // Same shape on its side
        AddTaperedCapsule(bi, bodies, halfHeight: 2f, topR: 1f, botR: 3f,
            20f, 10f, 0f, rotX90, JPH.EMotionType.Dynamic, LayerMoving, new Vector3(0.2f, 0.9f, 0.3f));

        // Tower of long tapered capsules (halfHeight=5, topR=0.5, botR=1)
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
                AddTaperedCapsule(bi, bodies, 5f, 0.5f, 1f, px, 2f + 3f * i, pz, rot, JPH.EMotionType.Dynamic, LayerMoving, col);
            }
        }
    }

    /// <summary>
    /// Creates a TaperedCapsuleShape and renders it as three separate sub-shapes:
    /// a cylinder for the tapered cylinder part, and two spheres for the caps.
    /// </summary>
    private static unsafe void AddTaperedCapsule(
        JPH.BodyInterface bi, List<PhysicsBody> bodies,
        float halfHeight, float topR, float botR,
        float px, float py, float pz,
        Quaternion rotation,
        JPH.EMotionType motionType, ushort layer,
        Vector3 color)
    {
        using var ss = new JPH.TaperedCapsuleShapeSettings(halfHeight, topR, botR);
        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(px, py, pz);
        cs.mRotation.Set(rotation.X, rotation.Y, rotation.Z, rotation.W);
        cs.mMotionType  = motionType;
        cs.mObjectLayer = layer;
        var activation = motionType == JPH.EMotionType.Static ? JPH.EActivation.DontActivate : JPH.EActivation.Activate;
        var id = bi.CreateAndAddBody(cs, activation);

        // TaperedCapsule stores sphere centers in shape-local space as:
        //   mTopCenter    =  halfHeight + 0.5*(botR - topR)
        //   mBottomCenter = -halfHeight + 0.5*(botR - topR)
        // GetCenterOfMass() = (0, -0.5*(botR-topR), 0), so localOffset is in shape-local coords.
        float offset    = 0.5f * (botR - topR);
        float topCenter = halfHeight + offset;
        float botCenter = -halfHeight + offset;

        // Truncated cone for the lateral part — exact geometry, topR/botR correctly sized
        var coneMesh = CreateTaperedConeMesh(topR, botR, halfHeight, out int coneIdxCount);
        bodies.Add(new PhysicsBody
        {
            bodyId               = id, color = color,
            shape                = RenderShape.TaperedCylinder,
            scale                = Vector3.One,
            localOffset          = new Vector3(0f, offset, 0f),
            customMesh           = coneMesh,
            customMeshIndexCount = coneIdxCount,
        });
        // Top sphere cap
        bodies.Add(new PhysicsBody
        {
            bodyId      = id, color = color,
            shape       = RenderShape.Sphere,
            scale       = new Vector3(topR * 2f),
            localOffset = new Vector3(0f, topCenter, 0f),
        });
        // Bottom sphere cap
        bodies.Add(new PhysicsBody
        {
            bodyId      = id, color = color,
            shape       = RenderShape.Sphere,
            scale       = new Vector3(botR * 2f),
            localOffset = new Vector3(0f, botCenter, 0f),
        });
    }
}
