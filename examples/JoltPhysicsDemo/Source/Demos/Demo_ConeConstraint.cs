using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of ConeConstraintTest.cpp.
/// Two horizontal chains of 5 capsules connected by ConeConstraints.
/// Chain 0 (Z=0):  HalfConeAngle = 0   — rigid, acts like a fixed hinge.
/// Chain 1 (Z=10): HalfConeAngle = 20° — links can swing within the cone.
/// Each capsule's long axis points along X; successive links are twisted 45°
/// around X relative to the previous one, matching the C++ original.
/// </summary>
public class Demo_ConeConstraint : DemoBase
{
    public override string Name     => "Cone Constraint";
    public override string Category => "Constraints";

    private readonly List<JPH.TwoBodyConstraint?> _constraints = new();

    const int   ChainLen          = 5;
    const float HalfCylinderHeight = 2.5f;  // half-length of each capsule
    const float CapsuleRadius      = 1.0f;

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies);

        // Disable collision between adjacent chain links (matches C++ GroupFilterTable setup).
        using var groupFilter = new JPH.GroupFilterTable(ChainLen);
        for (uint k = 0; k < ChainLen - 1; k++)
            groupFilter.DisableCollision(k, k + 1);

        // Base rotation: lay capsule on its side so its long (Y) axis points along +X.
        var baseRot = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.5f * MathF.PI);

        for (int j = 0; j < 2; j++)
        {
            float halfConeAngle = j == 0 ? 0.0f : 20f * MathF.PI / 180f;
            float posZ          = 10f * j;

            JPH.Body? prev = null;
            float posX = 0f;

            for (int i = 0; i < ChainLen; i++)
            {
                posX += 2f * HalfCylinderHeight;  // advance one full capsule length

                // Each link twisted 45° around the chain axis relative to the last
                var bodyRot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.25f * MathF.PI * i) * baseRot;

                using var ss = new JPH.CapsuleShapeSettings(HalfCylinderHeight, CapsuleRadius);
                using var cs = new JPH.BodyCreationSettings();
                cs.SetShapeSettings(ss);
                cs.mPosition.Set(posX, 20f, posZ);
                cs.mRotation.Set(bodyRot.X, bodyRot.Y, bodyRot.Z, bodyRot.W);
                cs.mMotionType  = i == 0 ? JPH.EMotionType.Static  : JPH.EMotionType.Dynamic;
                cs.mObjectLayer = i == 0 ? LayerNonMoving           : LayerMoving;

                var seg = bi.CreateBody(cs)!;
                using var cg = new JPH.CollisionGroup(groupFilter, (uint)j, (uint)i);
                seg.SetCollisionGroup(cg);
                bi.AddBody(seg.GetID(), JPH.EActivation.Activate);
                bodies.Add(new PhysicsBody
                {
                    bodyId = seg.GetID(),
                    color  = i == 0
                        ? new Vector3(0.45f, 0.42f, 0.38f)
                        : (j == 0
                            ? new Vector3(0.85f, 0.30f, 0.20f)
                            : new Vector3(0.25f, 0.65f, 0.90f)),
                    shape  = RenderShape.Capsule,
                    scale  = new Vector3(CapsuleRadius, HalfCylinderHeight, CapsuleRadius),
                });

                if (prev != null)
                {
                    // Pivot = left face of the current body (shared contact point)
                    float pivotX = posX - HalfCylinderHeight;

                    using var coneCS = new JPH.ConeConstraintSettings();
                    coneCS.mPoint1.Set(pivotX, 20f, posZ);
                    coneCS.mPoint2.Set(pivotX, 20f, posZ);
                    coneCS.mTwistAxis1.Set(1f, 0f, 0f);
                    coneCS.mTwistAxis2.Set(1f, 0f, 0f);
                    coneCS.mHalfConeAngle = halfConeAngle;

                    var c = coneCS.Create(prev, seg);
                    _constraints.Add(c);
                    sys.AddConstraint(c);
                }

                prev = seg;
            }
        }
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        foreach (var c in _constraints)
            if (c != null) sys.RemoveConstraint(c);
        _constraints.Clear();
    }

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 45,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(13f, 20f, 5f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000f
    };
}
