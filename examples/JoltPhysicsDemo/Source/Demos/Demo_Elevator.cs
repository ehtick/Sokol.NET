using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// A dynamic platform constrained to slide vertically by a SliderConstraint anchored
/// to the world.  The platform ping-pongs between two height limits, carrying stacked
/// boxes up and launching them off the top when the platform reverses direction.
/// Demonstrates SliderConstraint (single-axis prismatic joint with limits).
/// </summary>
public class Demo_Elevator : DemoBase
{
    public override string Name     => "Elevator";
    public override string Category => "Constraints";

    const float StartY    = 2.5f;   // platform center height at rest
    const float TravelUp  = 14f;    // distance above start
    const float SpeedUp   = 10f;    // ascent speed m/s
    const float SpeedDown = 8f;     // descent speed m/s

    JPH.BodyID             _platformId;
    JPH.TwoBodyConstraint? _slider;

    // Track direction so we can reverse at limits
    bool _goingUp = true;

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        _goingUp = true;

        AddFloor(bi, bodies, 40f, 0.5f, 40f);

        // ── Elevator platform ──────────────────────────────────────────────
        const float PlatHX = 4.5f, PlatHY = 0.5f, PlatHZ = 4.5f;

        using var platHalf = new JPH.Vec3(PlatHX, PlatHY, PlatHZ);
        using var platSS   = new JPH.BoxShapeSettings(platHalf);
        using var platCS   = new JPH.BodyCreationSettings();
        platCS.SetShapeSettings(platSS);
        platCS.mPosition.Set(0f, StartY, 0f);
        platCS.mMotionType    = JPH.EMotionType.Dynamic;
        platCS.mObjectLayer   = LayerMoving;
        platCS.mGravityFactor = 0f;    // driven manually via velocity
        platCS.mLinearDamping = 0f;
        platCS.mFriction      = 0.8f;

        var platform = bi.CreateBody(platCS);
        bi.AddBody(platform!.GetID(), JPH.EActivation.Activate);
        bodies.Add(new PhysicsBody
        {
            bodyId = platform.GetID(),
            color  = new Vector3(0.50f, 0.45f, 0.40f),
            shape  = RenderShape.Box,
            scale  = new Vector3(PlatHX * 2f, PlatHY * 2f, PlatHZ * 2f)
        });
        _platformId = platform.GetID();

        // ── SliderConstraint: world → platform, vertical (Y) axis ──────────
        // mAutoDetectPoint captures the attachment point at current positions.
        // mLimitsMin/Max are relative to that initial position.
        using var scs = new JPH.SliderConstraintSettings();
        scs.mAutoDetectPoint = true;
        scs.mSliderAxis1.Set(0f, 1f, 0f);   // Y axis is the slide direction
        scs.mNormalAxis1.Set(1f, 0f, 0f);
        scs.mSliderAxis2.Set(0f, 1f, 0f);
        scs.mNormalAxis2.Set(1f, 0f, 0f);
        scs.mLimitsMin = -(StartY - 0.6f);  // can descend to just above floor
        scs.mLimitsMax = TravelUp;

        _slider = scs.Create(JPH.Body.SFixedToWorld, platform);
        sys.AddConstraint(_slider);

        // ── Boxes stacked on the platform ─────────────────────────────────
        float platTop   = StartY + PlatHY;
        const float BoxHE = 0.85f;
        var boxColors = new Vector3[]
        {
            new Vector3(0.85f, 0.25f, 0.20f),  // red
            new Vector3(0.25f, 0.65f, 0.85f),  // blue
            new Vector3(0.25f, 0.75f, 0.30f),  // green
            new Vector3(0.90f, 0.75f, 0.15f),  // yellow
            new Vector3(0.80f, 0.45f, 0.80f),  // purple
        };
        for (int k = 0; k < 5; k++)
        {
            float y = platTop + BoxHE + k * BoxHE * 2f;
            AddBox(bi, bodies,
                BoxHE, BoxHE, BoxHE,
                0f, y, 0f,
                Quaternion.Identity,
                JPH.EMotionType.Dynamic, LayerMoving,
                boxColors[k],
                friction: 0.6f, restitution: 0.2f);
        }

    }

    public override void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        using var pos = bi.GetCenterOfMassPosition(_platformId);
        float y = pos.GetY();

        if (_goingUp && y >= StartY + TravelUp - 0.4f)
            _goingUp = false;
        else if (!_goingUp && y <= 1.0f)
            _goingUp = true;

        // Re-apply every frame: box weight would otherwise bleed off velocity
        // and the platform would stall before reaching either trigger height.
        bi.ActivateBody(_platformId);
        float targetVel = _goingUp ? SpeedUp : -SpeedDown;
        using var vel = new JPH.Vec3(0f, targetVel, 0f);
        bi.SetLinearVelocity(_platformId, vel);
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        if (_slider != null)
        {
            sys.RemoveConstraint(_slider);
            _slider = null;
        }
    }

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 50,
        Latitude  = 15,
        Longitude = 10,
        Center    = new Vector3(0f, 8f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000f
    };
}
