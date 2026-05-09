using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of Samples/Tests/Constraints/SwingTwistConstraintTest.cpp.
///
/// A horizontal chain of 10 spheres hanging from a static anchor.
/// Adjacent spheres are connected by SwingTwistConstraints that limit:
///   Normal cone half-angle:  25°
///   Plane cone half-angle:   10°
///   Twist range:            -20°..+20°
///
/// The chain hangs horizontally at first then swings under gravity,
/// demonstrating the cone and twist limits.
///
/// Note: GroupFilterTable is not yet exposed in the bindings.
/// Adjacent spheres may briefly clip during large swings, but the constraint
/// limits prevent sustained penetration.
/// </summary>
public sealed class Demo_SwingTwist : DemoBase
{
    public override string Name     => "Swing Twist";
    public override string Category => "Constraints";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 45,
        Latitude  = 10,
        Longitude = 0,
        Center    = new Vector3(16f, 18f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 500f,
    };

    private readonly List<JPH.TwoBodyConstraint?> _constraints = new();

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random random)
    {
        AddFloor(bi, bodies);

        const int   cChainLength = 10;
        const float cRadius      = 0.75f;
        const float cSpacing     = 3.0f;
        const float cNormalHalf  = 25f * MathF.PI / 180f;
        const float cPlaneHalf   = 10f * MathF.PI / 180f;
        const float cTwistMin    = -20f * MathF.PI / 180f;
        const float cTwistMax    =  20f * MathF.PI / 180f;

        const float posY = 20f;
        float posX = 0f;
        JPH.Body? prevBody = null;

        for (int i = 0; i < cChainLength; i++)
        {
            posX += cSpacing;
            bool isStatic = (i == 0);

            using var ss = new JPH.SphereShapeSettings(cRadius);
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(ss);
            cs.mPosition.Set(posX, posY, 0f);
            cs.mMotionType  = isStatic ? JPH.EMotionType.Static  : JPH.EMotionType.Dynamic;
            cs.mObjectLayer = isStatic ? LayerNonMoving           : LayerMoving;
            var body = bi.CreateBody(cs)!;
            bi.AddBody(body.GetID(), isStatic ? JPH.EActivation.DontActivate : JPH.EActivation.Activate);
            float t = (float)i / (cChainLength - 1);
            bodies.Add(new PhysicsBody
            {
                bodyId = body.GetID(),
                color  = isStatic
                    ? new Vector3(0.5f, 0.5f, 0.5f)
                    : new Vector3(0.8f - t * 0.4f, 0.3f + t * 0.5f, 0.2f + t * 0.4f),
                shape  = RenderShape.Sphere,
                scale  = new Vector3(cRadius * 2f)
            });

            if (prevBody != null)
            {
                float pivotX = posX - cSpacing * 0.5f;

                using var st = new JPH.SwingTwistConstraintSettings();
                st.mPosition1.Set(pivotX, posY, 0f);
                st.mPosition2.Set(pivotX, posY, 0f);
                st.mTwistAxis1.Set(1f, 0f, 0f);
                st.mTwistAxis2.Set(1f, 0f, 0f);
                st.mPlaneAxis1.Set(0f, 1f, 0f);
                st.mPlaneAxis2.Set(0f, 1f, 0f);
                st.mNormalHalfConeAngle = cNormalHalf;
                st.mPlaneHalfConeAngle  = cPlaneHalf;
                st.mTwistMinAngle       = cTwistMin;
                st.mTwistMaxAngle       = cTwistMax;

                var c = st.Create(prevBody, body);
                _constraints.Add(c);
                sys.AddConstraint(c);
            }

            prevBody = body;
        }
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        foreach (var c in _constraints)
            if (c != null) sys.RemoveConstraint(c);
        _constraints.Clear();
    }
}
