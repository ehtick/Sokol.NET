using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of Samples/Tests/Vehicle/VehicleSixDOFTest.cpp.
///
/// A four-wheel car simulated with SixDOFConstraints instead of a VehicleConstraint.
/// Each wheel is a separate rigid body connected to the car body via a 6DOF constraint
/// that allows suspension (Y-translation), rotation (drive), and optional steering (Y-rotation).
///
/// Controls:
///   Up / Down   — accelerate / reverse
///   Left / Right — steer
/// </summary>
public sealed class Demo_VehicleSixDOF : DemoBase
{
    public override string Name     => "Car (SixDOFConstraint)";
    public override string Category => "Vehicle";

    // ── Constants (match VehicleSixDOFTest.cpp) ─────────────────────────────
    const float HalfVehicleLength = 2.0f;
    const float HalfVehicleWidth  = 0.9f;
    const float HalfVehicleHeight = 0.2f;
    const float HalfWheelHeight   = 0.3f;    // radius
    const float HalfWheelWidth    = 0.05f;
    const float HalfWheelTravel   = 0.5f;
    const float MaxSteeringAngle  = 30f * MathF.PI / 180f;   // 30 degrees
    const float MaxRotationSpeed  = 10f * MathF.PI;           // 10π rad/s
    const float WheelDensity      = 1.0e4f;

    // ── State ────────────────────────────────────────────────────────────────
    JPH.Body?                _carBody;
    JPH.BodyID               _carBodyId;
    JPH.SixDOFConstraint?[]  _wheelConstraints = new JPH.SixDOFConstraint?[4];
    JPH.BodyID[]             _wheelIds          = new JPH.BodyID[4];
    bool[]                   _isLeftWheel       = { true, false, true, false };   // i==0||i==2
    bool[]                   _isFrontWheel      = { true, true, false, false };   // i<2

    float _steeringAngle = 0f;
    float _speed         = 0f;

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

    public override float GetFollowYaw(JPH.BodyInterface bi)
    {
        if (_carBodyId.IsInvalid() || _carBody == null) return float.NaN;
        using var rot = bi.GetRotation(_carBodyId);
        var q = new Quaternion(rot.GetX(), rot.GetY(), rot.GetZ(), rot.GetW());
        var fwd = Vector3.Transform(Vector3.UnitZ, q);
        return MathF.Atan2(fwd.X, fwd.Z) * 180f / MathF.PI;
    }

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random random)
    {
        CreatePlaygroundTerrain(bi, bodies);
        CreateWallObstacle(bi, bodies, 10f);
        CreateRubble(bi, bodies, random);
        CreateBridge(bi, sys, bodies, _bridgeConstraints);

        // ── Collision group so car and wheel bodies don't collide each other ─
        using var groupFilter = new JPH.GroupFilterTable();

        // ── Car body ─────────────────────────────────────────────────────────
        using var boxHE = new JPH.Vec3(HalfVehicleWidth, HalfVehicleHeight, HalfVehicleLength);
        using var boxSS = new JPH.BoxShapeSettings(boxHE);

        using var carCS = new JPH.BodyCreationSettings();
        carCS.SetShapeSettings(boxSS);
        carCS.mPosition.Set(0f, 2f, 0f);
        carCS.mMotionType  = JPH.EMotionType.Dynamic;
        carCS.mObjectLayer = LayerMoving;
        carCS.mCollisionGroup.SetGroupFilter(groupFilter);
        carCS.mCollisionGroup.SetGroupID(0);
        carCS.mCollisionGroup.SetSubGroupID(0);

        _carBody   = bi.CreateBody(carCS);
        _carBodyId = _carBody!.GetID();
        bi.AddBody(_carBodyId, JPH.EActivation.Activate);

        bodies.Add(new PhysicsBody
        {
            bodyId = _carBodyId,
            color  = new Vector3(0.3f, 0.6f, 0.9f),
            shape  = RenderShape.Box,
            scale  = new Vector3(HalfVehicleWidth * 2f, HalfVehicleHeight * 2f, HalfVehicleLength * 2f),
        });

        // ── Wheel positions in car space ─────────────────────────────────────
        // C++: position1 = (±0.9, -0.2, ±(HalfVehicleLength - HalfWheelHeight*2)) = (±0.9, -0.2, ±1.4)
        // but the code actually does:
        //   x = is_left ? -HalfVehicleWidth : HalfVehicleWidth
        //   y = -HalfVehicleHeight
        //   z = is_front ? (HalfVehicleLength - 2*HalfWheelHeight) : -(HalfVehicleLength - 2*HalfWheelHeight)
        float[] wx = { -HalfVehicleWidth, HalfVehicleWidth, -HalfVehicleWidth, HalfVehicleWidth };
        float   wy = -HalfVehicleHeight;
        float   wz_front = HalfVehicleLength - 2f * HalfWheelHeight;    // 2.0 - 0.6 = 1.4
        float[] wz = { wz_front, wz_front, -wz_front, -wz_front };

        for (int i = 0; i < 4; i++)
        {
            bool isLeft  = _isLeftWheel[i];
            bool isFront = _isFrontWheel[i];

            // World-space attachment point on car (car starts at y=2, axis-aligned):
            // position1 = (±HalfVehicleWidth, 2 - HalfVehicleHeight, ±(HalfVehicleLength - 2*HalfWheelHeight))
            float worldWheelX  = wx[i];
            float worldWheelY1 = 2f + wy;                   // = 2 - 0.2 = 1.8
            float worldWheelZ  = wz[i];

            // Wheel body placed at bottom of suspension travel
            float worldWheelY2 = worldWheelY1 - HalfWheelTravel;   // = 1.8 - 0.5 = 1.3

            // Wheel body: Cylinder(halfHeight=HalfWheelWidth, radius=HalfWheelHeight), rotated 90° around Z
            using var wheelSS = new JPH.CylinderShapeSettings(HalfWheelWidth, HalfWheelHeight);
            wheelSS.SetDensity(WheelDensity);

            using var wheelRot = JPH.Quat.SRotation(JPH.Vec3.SAxisZ(), 0.5f * MathF.PI);

            using var wheelCS = new JPH.BodyCreationSettings();
            wheelCS.SetShapeSettings(wheelSS);
            wheelCS.mPosition.Set(worldWheelX, worldWheelY2, worldWheelZ);
            wheelCS.mRotation.Set(wheelRot.GetX(), wheelRot.GetY(), wheelRot.GetZ(), wheelRot.GetW());
            wheelCS.mMotionType  = JPH.EMotionType.Dynamic;
            wheelCS.mObjectLayer = LayerMoving;
            wheelCS.mFriction    = 1.0f;
            wheelCS.mCollisionGroup.SetGroupFilter(groupFilter);
            wheelCS.mCollisionGroup.SetGroupID(0);
            wheelCS.mCollisionGroup.SetSubGroupID(0);

            var wheelBody = bi.CreateBody(wheelCS);
            _wheelIds[i] = wheelBody!.GetID();
            bi.AddBody(_wheelIds[i], JPH.EActivation.Activate);

            bodies.Add(new PhysicsBody
            {
                bodyId = _wheelIds[i],
                color  = new Vector3(0.1f, 0.8f, 0.1f),
                shape  = RenderShape.Cylinder,
                scale  = new Vector3(HalfWheelHeight * 2f, HalfWheelWidth * 2f, HalfWheelHeight * 2f),
            });

            // ── SixDOF constraint (WorldSpace — use world positions at creation time) ───
            using var constraintSettings = new JPH.SixDOFConstraintSettings();

            // Attachment points in world space:
            constraintSettings.mPosition1.Set(worldWheelX, worldWheelY1, worldWheelZ);
            constraintSettings.mPosition2.Set(worldWheelX, worldWheelY2, worldWheelZ);

            // Axes in world space (car is axis-aligned at creation time):
            // mAxisX = drive/spin axis = ±X (left wheels spin on -X axis, right on +X)
            // mAxisY = perpendicular = Y
            float axisXSign = isLeft ? -1f : 1f;
            constraintSettings.mAxisX1.Set(axisXSign, 0f, 0f);
            constraintSettings.mAxisY1.Set(0f, 1f, 0f);
            constraintSettings.mAxisX2.Set(axisXSign, 0f, 0f);
            constraintSettings.mAxisY2.Set(0f, 1f, 0f);

            // Translation: X and Z fixed, Y limited (suspension)
            constraintSettings.MakeFixedAxis(JPH.SixDOFConstraintSettings.EAxis.TranslationX);
            constraintSettings.SetLimitedAxis(JPH.SixDOFConstraintSettings.EAxis.TranslationY,
                -HalfWheelTravel, HalfWheelTravel);
            constraintSettings.MakeFixedAxis(JPH.SixDOFConstraintSettings.EAxis.TranslationZ);

            // Suspension motor (position motor on Y translation)
            {
                var ms = constraintSettings.mMotorSettings[(nint)JPH.SixDOFConstraintSettings.EAxis.TranslationY];
                ms.mSpringSettings.mFrequency = 2.0f;
                ms.mSpringSettings.mDamping   = 1.0f;
                ms.SetForceLimit(1.0e5f);
            }

            // Rotation: X free (spin), Y limited/fixed (steering), Z fixed
            constraintSettings.MakeFreeAxis(JPH.SixDOFConstraintSettings.EAxis.RotationX);

            if (isFront)
                constraintSettings.SetLimitedAxis(JPH.SixDOFConstraintSettings.EAxis.RotationY,
                    -MaxSteeringAngle, MaxSteeringAngle);
            else
                constraintSettings.MakeFixedAxis(JPH.SixDOFConstraintSettings.EAxis.RotationY);

            constraintSettings.MakeFixedAxis(JPH.SixDOFConstraintSettings.EAxis.RotationZ);

            // Motor on RotationX (drive)
            {
                var ms = constraintSettings.mMotorSettings[(nint)JPH.SixDOFConstraintSettings.EAxis.RotationX];
                ms.mSpringSettings.mFrequency = 2.0f;
                ms.mSpringSettings.mDamping   = 1.0f;
                ms.SetTorqueLimit(0.5e4f);
            }

            if (isFront)
            {
                // Motor on RotationY and RotationZ for steering
                {
                    var ms = constraintSettings.mMotorSettings[(nint)JPH.SixDOFConstraintSettings.EAxis.RotationY];
                    ms.mSpringSettings.mFrequency = 10.0f;
                    ms.mSpringSettings.mDamping   = 1.0f;
                    ms.SetTorqueLimit(1.0e6f);
                }
                {
                    var ms = constraintSettings.mMotorSettings[(nint)JPH.SixDOFConstraintSettings.EAxis.RotationZ];
                    ms.mSpringSettings.mFrequency = 10.0f;
                    ms.mSpringSettings.mDamping   = 1.0f;
                    ms.SetTorqueLimit(1.0e6f);
                }
            }

            // Create constraint (body1=car, body2=wheel)
            var raw = constraintSettings.Create(_carBody!, wheelBody!);
            var c   = (JPH.SixDOFConstraint)raw!;
            _wheelConstraints[i] = c;
            sys.AddConstraint(c);

            // Initialise suspension position and motors
            using var suspPos = new JPH.Vec3(0f, -HalfWheelTravel, 0f);
            c.SetTargetPositionCS(suspPos);
            c.SetMotorState(JPH.SixDOFConstraintSettings.EAxis.TranslationY, JPH.EMotorState.Position);

            if (isFront)
            {
                using var identity = JPH.Quat.SIdentity();
                c.SetTargetOrientationCS(identity);
                c.SetMotorState(JPH.SixDOFConstraintSettings.EAxis.RotationY, JPH.EMotorState.Position);
                c.SetMotorState(JPH.SixDOFConstraintSettings.EAxis.RotationZ, JPH.EMotorState.Position);
            }
        }
    }

    public override unsafe void Update(
        float dt,
        JPH.BodyInterface bi,
        List<PhysicsBody> bodies)
    {
        // ── Input ─────────────────────────────────────────────────────────────
        bool up    = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_UP);
        bool down  = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_DOWN);
        bool left  = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_LEFT);
        bool right = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_RIGHT);

        // Match C++ ProcessInput: later assignment wins (Down overrides Up)
        _speed = 0f;
        if (up)    _speed =  MaxRotationSpeed;
        if (down)  _speed = -MaxRotationSpeed;
        _steeringAngle = 0f;
        if (left)  _steeringAngle =  MaxSteeringAngle;
        if (right) _steeringAngle = -MaxSteeringAngle;

        // Determine if we should brake: pressing direction but still moving the other way
        bool brake = false;
        if (_speed != 0f && _carBody != null)
        {
            using var carVel  = _carBody.GetLinearVelocity();
            using var carRot  = _carBody.GetRotation();
            using var axisZ   = JPH.Vec3.SAxisZ();
            using var rotated = carRot * axisZ;
            float carSpeed = carVel.GetX() * rotated.GetX()
                           + carVel.GetY() * rotated.GetY()
                           + carVel.GetZ() * rotated.GetZ();
            if (_speed > 0f && carSpeed < 0f) brake = true;
            if (_speed < 0f && carSpeed > 0f) brake = true;
        }

        // ── Front wheels: steer + FWD drive ──────────────────────────────────
        for (int i = 0; i < 4; i++)
        {
            if (!_isFrontWheel[i]) continue;
            var c = _wheelConstraints[i];
            if (c == null) continue;

            using var steerQ = JPH.Quat.SRotation(JPH.Vec3.SAxisY(), _steeringAngle);
            c.SetTargetOrientationCS(steerQ);

            if (brake)
            {
                using var zero = JPH.Vec3.SZero();
                c.SetTargetAngularVelocityCS(zero);
                c.SetMotorState(JPH.SixDOFConstraintSettings.EAxis.RotationX, JPH.EMotorState.Velocity);
            }
            else if (_speed != 0f)
            {
                float spinSpeed = _isLeftWheel[i] ? -_speed : _speed;
                using var angVel = new JPH.Vec3(spinSpeed, 0f, 0f);
                c.SetTargetAngularVelocityCS(angVel);
                c.SetMotorState(JPH.SixDOFConstraintSettings.EAxis.RotationX, JPH.EMotorState.Velocity);
            }
            else
            {
                c.SetMotorState(JPH.SixDOFConstraintSettings.EAxis.RotationX, JPH.EMotorState.Off);
            }
        }

        // ── Rear wheels: brake or free-spin only (no drive) ───────────────────
        for (int i = 0; i < 4; i++)
        {
            if (_isFrontWheel[i]) continue;
            var c = _wheelConstraints[i];
            if (c == null) continue;

            if (brake)
            {
                using var zero = JPH.Vec3.SZero();
                c.SetTargetAngularVelocityCS(zero);
                c.SetMotorState(JPH.SixDOFConstraintSettings.EAxis.RotationX, JPH.EMotorState.Velocity);
            }
            else
            {
                c.SetMotorState(JPH.SixDOFConstraintSettings.EAxis.RotationX, JPH.EMotorState.Off);
            }
        }

        // Activate car on any input
        if ((_speed != 0f || _steeringAngle != 0f) && _carBody != null)
            bi.ActivateBody(_carBodyId);
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        foreach (var c in _bridgeConstraints)
            if (c != null) sys.RemoveConstraint(c);
        _bridgeConstraints.Clear();

        for (int i = 0; i < 4; i++)
        {
            if (_wheelConstraints[i] != null)
            {
                sys.RemoveConstraint(_wheelConstraints[i]!);
                _wheelConstraints[i]!.Dispose();
                _wheelConstraints[i] = null;
            }
        }
        _carBody = null;
    }
}
