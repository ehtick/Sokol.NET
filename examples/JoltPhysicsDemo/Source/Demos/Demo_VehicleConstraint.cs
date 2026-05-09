using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;
using static Sokol.SG;
using static Sokol.Utils;

/// <summary>
/// Port of Samples/Tests/Vehicle/VehicleConstraintTest.cpp.
///
/// A four-wheel vehicle using WheeledVehicleController.
/// Front wheels steer; front wheels drive (FWD by default).
/// Collision tester: CastCylinder (matches the C++ default mode 2).
///
/// Controls:
///   Up / Down   — accelerate / reverse
///   Left / Right — steer
///   Z           — hand brake
/// </summary>
public sealed class Demo_VehicleConstraint : DemoBase
{
    public override string Name     => "Vehicle Constraint";
    public override string Category => "Vehicle";

    // ── Physics constants ──────────────────────────────────────────────────
    const float WheelRadius        = 0.3f;
    const float WheelWidth         = 0.1f;
    const float HalfVehicleLength  = 2.0f;
    const float HalfVehicleWidth   = 0.9f;
    const float HalfVehicleHeight  = 0.2f;
    const float MaxSteeringAngle   = MathF.PI / 6f;  // 30°
    const float MaxRollAngle       = MathF.PI / 3f;  // 60°
    const float MaxEngineTorque    = 500f;
    const float ClutchStrength     = 10f;
    const float SuspMinLength      = 0.3f;
    const float SuspMaxLength      = 0.5f;
    const float SuspFrequency      = 1.5f;
    const float SuspDamping        = 0.5f;

    // ── State ──────────────────────────────────────────────────────────────
    JPH.VehicleConstraint?                  _vehicleConstraint;
    JPH.Body?                               _carBody;
    JPH.VehicleCollisionTesterRay?          _testerRay;
    JPH.VehicleCollisionTesterCastSphere?   _testerSphere;
    JPH.VehicleCollisionTesterCastCylinder? _testerCylinder;
    JPH.BodyID                              _carBodyId;

    float _forward         = 0f;
    float _previousForward = 1f;
    float _right           = 0f;
    float _brake           = 0f;
    float _handBrake       = 0f;

    // Index in 'bodies' where the 4 wheel cylinder entries start.
    int _wheelBodyStart = -1;

    readonly List<JPH.TwoBodyConstraint?> _bridgeConstraints = new();

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 15,
        Latitude  = 20,
        Longitude = 180,
        Center    = new Vector3(0f, 4f, 0f),
    };

    public override bool CameraFollowsPlayer => true;
    public override VirtualControlsType VirtualControls => VirtualControlsType.Arrows;

    public override Vector3 GetFollowPosition(JPH.BodyInterface bi)
    {
        if (_carBodyId.IsInvalid()) return Vector3.Zero;
        using var pos = bi.GetPosition(_carBodyId);
        return new Vector3(pos.GetX(), pos.GetY(), pos.GetZ());
    }

    public override unsafe void Init(
        JPH.BodyInterface   bi,
        JPH.PhysicsSystem   sys,
        List<PhysicsBody>   bodies,
        Random              random)
    {
        CreatePlaygroundTerrain(bi, bodies);
        // Walls at random Z positions and random X offsets across the terrain
        for (int w = 0; w < 6; w++)
        {
            float wallZ = 25f + w * 35f + (float)(random.NextDouble() * 15f - 7.5f);
            float wallX = (float)(random.NextDouble() * 60f - 30f);
            CreateWallObstacle(bi, bodies, wallZ, wallX);
        }
        CreateRubble(bi, bodies, random);
        CreateBridge(bi, sys, bodies, _bridgeConstraints);

        // ── Collision testers ───────────────────────────────────────────────
        _testerRay      = new JPH.VehicleCollisionTesterRay(LayerMoving);
        _testerSphere   = new JPH.VehicleCollisionTesterCastSphere(LayerMoving, 0.5f * WheelWidth);
        _testerCylinder = new JPH.VehicleCollisionTesterCastCylinder(LayerMoving);

        // ── Car body ────────────────────────────────────────────────────────
        using var boxHE  = new JPH.Vec3(HalfVehicleWidth, HalfVehicleHeight, HalfVehicleLength);
        using var boxSS  = new JPH.BoxShapeSettings(boxHE);
        using var comOff = new JPH.Vec3(0f, -HalfVehicleHeight, 0f);
        using var comSS  = new JPH.OffsetCenterOfMassShapeSettings(comOff, boxSS);

        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(comSS);
        cs.mPosition.Set(0f, 10f, 0f);  // y=10: well above max terrain height (3 m)
        cs.mMotionType  = JPH.EMotionType.Dynamic;
        cs.mObjectLayer = LayerMoving;
        cs.mFriction    = 0.5f;
        cs.SetOverrideMassProperties(1); // 1 = CalculateInertia
        cs.SetMassOverride(1500.0f);

        _carBody   = bi.CreateBody(cs);
        _carBodyId = _carBody!.GetID();
        bi.AddBody(_carBodyId, JPH.EActivation.Activate);

        bodies.Add(new PhysicsBody
        {
            bodyId = _carBodyId,
            color  = new Vector3(0.3f, 0.6f, 0.9f),
            shape  = RenderShape.Box,
            scale  = new Vector3(HalfVehicleWidth * 2f, HalfVehicleHeight * 2f, HalfVehicleLength * 2f),
        });

        // ── Wheel render placeholders ───────────────────────────────────────
        // Wheels are not physics bodies; they are rendered each frame via
        // GetWheelWorldTransform. We add Cylinder entries with the car's bodyId
        // and update their localOffset/localRotation in Update().
        _wheelBodyStart = bodies.Count;
        float[] wx = { HalfVehicleWidth, -HalfVehicleWidth, HalfVehicleWidth, -HalfVehicleWidth };
        float   wy = -0.9f * HalfVehicleHeight;
        float[] wz = { HalfVehicleLength - 2f * WheelRadius, HalfVehicleLength - 2f * WheelRadius,
                       -(HalfVehicleLength - 2f * WheelRadius), -(HalfVehicleLength - 2f * WheelRadius) };
        for (int i = 0; i < 4; i++)
        {
            bodies.Add(new PhysicsBody
            {
                bodyId        = _carBodyId,
                color         = new Vector3(0.1f, 0.8f, 0.1f),
                shape         = RenderShape.Cylinder,
                // Cylinder scale: (diameter, height, diameter)
                scale         = new Vector3(WheelRadius * 2f, WheelWidth, WheelRadius * 2f),
                // Initial local offset in car space (updated each frame in Update).
                localOffset   = new Vector3(wx[i], wy, wz[i]),
                // W != 0 triggers the local-rotation override path in the render loop.
                localRotation = Quaternion.Identity,
            });
        }

        // ── Vehicle constraint settings ─────────────────────────────────────
        using var vehicleSettings = new JPH.VehicleConstraintSettings();
        vehicleSettings.mDrawConstraintSize = 0.1f;
        vehicleSettings.mMaxPitchRollAngle  = MaxRollAngle;

        // All angles are 0 → directions reduce to simple axis values.
        // Suspension: (0, -1, 0); Steering: (0, 1, 0); WheelUp: (0, 1, 0); WheelFwd: (0, 0, 1)
        // flip_x (-1, 1, 1) applied to right-side wheels yields the same directions when angles = 0.

        using var w1 = new JPH.WheelSettingsWV();
        w1.mPosition.Set( HalfVehicleWidth, wy, HalfVehicleLength - 2f * WheelRadius);
        w1.mSuspensionDirection.Set(0f, -1f, 0f);
        w1.mSteeringAxis.Set(0f, 1f, 0f);
        w1.mWheelUp.Set(0f, 1f, 0f);
        w1.mWheelForward.Set(0f, 0f, 1f);
        w1.mSuspensionMinLength = SuspMinLength;
        w1.mSuspensionMaxLength = SuspMaxLength;
        w1.mSuspensionSpring.mFrequency = SuspFrequency;
        w1.mSuspensionSpring.mDamping   = SuspDamping;
        w1.mMaxSteerAngle      = MaxSteeringAngle;
        w1.mMaxHandBrakeTorque = 0f;   // front wheels don't hand-brake
        w1.mRadius = WheelRadius;
        w1.mWidth  = WheelWidth;

        using var w2 = new JPH.WheelSettingsWV();
        w2.mPosition.Set(-HalfVehicleWidth, wy, HalfVehicleLength - 2f * WheelRadius);
        w2.mSuspensionDirection.Set(0f, -1f, 0f);
        w2.mSteeringAxis.Set(0f, 1f, 0f);
        w2.mWheelUp.Set(0f, 1f, 0f);
        w2.mWheelForward.Set(0f, 0f, 1f);
        w2.mSuspensionMinLength = SuspMinLength;
        w2.mSuspensionMaxLength = SuspMaxLength;
        w2.mSuspensionSpring.mFrequency = SuspFrequency;
        w2.mSuspensionSpring.mDamping   = SuspDamping;
        w2.mMaxSteerAngle      = MaxSteeringAngle;
        w2.mMaxHandBrakeTorque = 0f;
        w2.mRadius = WheelRadius;
        w2.mWidth  = WheelWidth;

        using var w3 = new JPH.WheelSettingsWV();
        w3.mPosition.Set( HalfVehicleWidth, wy, -(HalfVehicleLength - 2f * WheelRadius));
        w3.mSuspensionDirection.Set(0f, -1f, 0f);
        w3.mSteeringAxis.Set(0f, 1f, 0f);
        w3.mWheelUp.Set(0f, 1f, 0f);
        w3.mWheelForward.Set(0f, 0f, 1f);
        w3.mSuspensionMinLength = SuspMinLength;
        w3.mSuspensionMaxLength = SuspMaxLength;
        w3.mSuspensionSpring.mFrequency = SuspFrequency;
        w3.mSuspensionSpring.mDamping   = SuspDamping;
        w3.mMaxSteerAngle = 0f;        // rear wheels don't steer
        w3.mRadius = WheelRadius;
        w3.mWidth  = WheelWidth;

        using var w4 = new JPH.WheelSettingsWV();
        w4.mPosition.Set(-HalfVehicleWidth, wy, -(HalfVehicleLength - 2f * WheelRadius));
        w4.mSuspensionDirection.Set(0f, -1f, 0f);
        w4.mSteeringAxis.Set(0f, 1f, 0f);
        w4.mWheelUp.Set(0f, 1f, 0f);
        w4.mWheelForward.Set(0f, 0f, 1f);
        w4.mSuspensionMinLength = SuspMinLength;
        w4.mSuspensionMaxLength = SuspMaxLength;
        w4.mSuspensionSpring.mFrequency = SuspFrequency;
        w4.mSuspensionSpring.mDamping   = SuspDamping;
        w4.mMaxSteerAngle = 0f;
        w4.mRadius = WheelRadius;
        w4.mWidth  = WheelWidth;

        vehicleSettings.VehicleSettingsAddWheel(w1);
        vehicleSettings.VehicleSettingsAddWheel(w2);
        vehicleSettings.VehicleSettingsAddWheel(w3);
        vehicleSettings.VehicleSettingsAddWheel(w4);

        // ── Controller ──────────────────────────────────────────────────────
        using var ctrlSettings = new JPH.WheeledVehicleControllerSettings();
        vehicleSettings.VehicleSettingsSetController(ctrlSettings);

        // ── Differential (front-wheel drive: wheels 0=LF, 1=RF) ────────────
        using var diff = new JPH.VehicleDifferentialSettings();
        diff.mLeftWheel  = 0;
        diff.mRightWheel = 1;
        ctrlSettings.mDifferentials.PushBack(diff);

        // ── Anti-roll bars ──────────────────────────────────────────────────
        using var arb1 = new JPH.VehicleAntiRollBar(0, 1, 1000f);
        using var arb2 = new JPH.VehicleAntiRollBar(2, 3, 1000f);
        vehicleSettings.mAntiRollBars.PushBack(arb1);
        vehicleSettings.mAntiRollBars.PushBack(arb2);

        // ── Create constraint ───────────────────────────────────────────────
        _vehicleConstraint = new JPH.VehicleConstraint(_carBody!, vehicleSettings);
        _vehicleConstraint.SetVehicleCollisionTester(_testerCylinder!);

        sys.AddConstraint(_vehicleConstraint);
        sys.AddStepListener(_vehicleConstraint);
    }

    public override unsafe void Update(
        float              dt,
        JPH.BodyInterface  bi,
        List<PhysicsBody>  bodies)
    {
        if (_vehicleConstraint == null) return;

        // ── Driver input ────────────────────────────────────────────────────
        bool up    = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_UP);
        bool down  = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_DOWN);
        bool left  = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_LEFT);
        bool right = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_RIGHT);
        bool z     = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_Z);

        float newForward = up ? 1f : (down ? -1f : 0f);
        _brake = 0f;

        // When reversing direction, apply brake until the car stops.
        if (_previousForward * newForward < 0f)
        {
            using var carRot = _carBody!.GetRotation();
            using var carVel = _carBody!.GetLinearVelocity();
            using var conjRot   = carRot.Conjugated();
            using var localVel  = conjRot * carVel;
            float velocityZ = localVel.GetZ();

            if ((newForward > 0f && velocityZ < -0.1f) || (newForward < 0f && velocityZ > 0.1f))
            {
                // Not stopped yet — brake instead of switching direction.
                newForward = 0f;
                _brake     = 1f;
            }
            else
            {
                _previousForward = newForward;
            }
        }

        _forward   = newForward;
        _right     = left ? -1f : (right ? 1f : 0f);
        _handBrake = 0f;

        if (z)
        {
            _forward   = 0f;
            _handBrake = 1f;
        }

        // ── Activate car on any input ───────────────────────────────────────
        if (_right != 0f || _forward != 0f || _brake != 0f || _handBrake != 0f)
            bi.ActivateBody(_carBodyId);

        // ── Send input to controller ────────────────────────────────────────
        var ctrl = _vehicleConstraint.GetWheeledController();
        if (ctrl != null)
        {
            ctrl.GetEngine().mMaxTorque            = MaxEngineTorque;
            ctrl.GetTransmission().mClutchStrength  = ClutchStrength;
            ctrl.SetDifferentialLimitedSlipRatio(1.4f);
            ctrl.SetDriverInput(_forward, _right, _brake, _handBrake);
        }

        // ── Update wheel render transforms ──────────────────────────────────
        // The render loop uses localOffset/localRotation relative to the car body.
        // Decompose GetWheelWorldTransform into car-relative position and rotation.
        using var axisY = JPH.Vec3.SAxisY();
        using var axisX = JPH.Vec3.SAxisX();
        using var carWt = bi.GetWorldTransform(_carBodyId);
        using var carTr = carWt.GetTranslation();
        using var carRQ = carWt.GetQuaternion();

        var carQ    = new Quaternion(carRQ.GetX(), carRQ.GetY(), carRQ.GetZ(), carRQ.GetW());
        var carQInv = Quaternion.Inverse(carQ);

        for (int i = 0; i < 4; i++)
        {
            using var wheelMat  = _vehicleConstraint.GetWheelWorldTransform((uint)i, axisY, axisX);
            using var wheelPos  = wheelMat.GetTranslation();
            using var wheelQuat = wheelMat.GetQuaternion();

            // Wheel world position relative to car shape-origin, rotated into car local space.
            var delta  = new Vector3(
                wheelPos.GetX() - carTr.GetX(),
                wheelPos.GetY() - carTr.GetY(),
                wheelPos.GetZ() - carTr.GetZ());
            var relPos = Vector3.Transform(delta, carQInv);
            var relRot = carQInv * new Quaternion(wheelQuat.GetX(), wheelQuat.GetY(), wheelQuat.GetZ(), wheelQuat.GetW());

            // Ensure W != 0 so the render loop applies the local rotation.
            // (edge case: if W happens to be 0, nudge slightly — extremely unlikely)
            if (relRot.W == 0f) relRot.W = 1e-6f;

            var wb = bodies[_wheelBodyStart + i];
            wb.localOffset   = relPos;
            wb.localRotation = relRot;
            bodies[_wheelBodyStart + i] = wb;
        }
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        foreach (var c in _bridgeConstraints)
            if (c != null) sys.RemoveConstraint(c);
        _bridgeConstraints.Clear();

        if (_vehicleConstraint != null)
        {
            sys.RemoveStepListener(_vehicleConstraint);
            sys.RemoveConstraint(_vehicleConstraint);
            _vehicleConstraint.Dispose();
            _vehicleConstraint = null;
        }
        _testerRay?.Dispose();      _testerRay      = null;
        _testerSphere?.Dispose();   _testerSphere   = null;
        _testerCylinder?.Dispose(); _testerCylinder = null;
        _carBody = null;
    }

    public override float GetFollowYaw(JPH.BodyInterface bi)
    {
        if (_carBodyId.IsInvalid() || _carBody == null) return float.NaN;
        using var rot = bi.GetRotation(_carBodyId);
        var q = new Quaternion(rot.GetX(), rot.GetY(), rot.GetZ(), rot.GetW());
        var fwd = Vector3.Transform(Vector3.UnitZ, q);
        return MathF.Atan2(fwd.X, fwd.Z) * 180f / MathF.PI;
    }
}

