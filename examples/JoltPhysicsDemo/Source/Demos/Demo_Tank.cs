using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of Samples/Tests/Vehicle/TankTest.cpp.
///
/// A tracked vehicle (tank) built with VehicleConstraint and TrackedVehicleController.
/// Features a rotating turret and an elevation-controlled barrel, both attached
/// via HingeConstraints with position motors.
///
/// Controls:
///   Up / Down       — drive forward / reverse
///   Left / Right    — turn (differential steering; at low speed: pivot turn)
///   Right Shift     — brake
///   A / D           — rotate turret left / right
///   Q / E           — elevate barrel up / down
/// </summary>
public sealed class Demo_Tank : DemoBase
{
    public override string Name     => "Tank (Tracked Vehicle)";
    public override string Category => "Vehicle";

    // ── Constants (match TankTest.cpp) ──────────────────────────────────────
    const float WheelRadius           = 0.3f;
    const float WheelWidth            = 0.1f;
    const float HalfVehicleLength     = 3.2f;
    const float HalfVehicleWidth      = 1.7f;
    const float HalfVehicleHeight     = 0.5f;
    const float SuspensionMinLength   = 0.3f;
    const float SuspensionMaxLength   = 0.5f;
    const float SuspensionFrequency   = 1.0f;

    const float HalfTurretWidth       = 1.4f;
    const float HalfTurretLength      = 2.0f;
    const float HalfTurretHeight      = 0.4f;
    const float HalfBarrelLength      = 1.5f;
    const float BarrelRadius          = 0.1f;
    const float BarrelRotationOffset  = 0.2f;  // offset along Z from turret origin to barrel hinge

    const float BarrelMinPitch = -10f * MathF.PI / 180f;
    const float BarrelMaxPitch =  40f * MathF.PI / 180f;

    // 9 wheel Z-positions (same for both tracks)
    static readonly float[] WheelZPos = { 2.95f, 2.1f, 1.4f, 0.7f, 0.0f, -0.7f, -1.4f, -2.1f, -2.75f };
    // Y-offsets: end-wheels at 0, middle wheels at -0.3
    static readonly float[] WheelYPos = { 0.0f, -0.3f, -0.3f, -0.3f, -0.3f, -0.3f, -0.3f, -0.3f, 0.0f };

    // ── State ────────────────────────────────────────────────────────────────
    JPH.VehicleConstraint?   _vehicleConstraint;
    JPH.Body?                _tankBody;
    JPH.BodyID               _tankBodyId;
    JPH.Body?                _turretBody;
    JPH.Body?                _barrelBody;
    JPH.HingeConstraint?     _turretHinge;
    JPH.HingeConstraint?     _barrelHinge;

    float _mForward       = 0f;
    float _mPreviousForward = 1f;
    float _mLeftRatio     = 1f;
    float _mRightRatio    = 1f;
    float _mBrake         = 0f;
    float _mTurretHeading = 0f;
    float _mBarrelPitch   = 0f;

    // Index in bodies[] where the 18 wheel cylinder entries start.
    int _wheelBodyStart = -1;

    readonly List<JPH.TwoBodyConstraint?> _bridgeConstraints = new();

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 20,
        Latitude  = 20,
        Longitude = 180,
        Center    = new Vector3(0f, 4f, 0f),
    };

    public override bool CameraFollowsPlayer => true;
    public override VirtualControlsType VirtualControls => VirtualControlsType.Arrows;
    public override VirtualActionButton[] VirtualActionButtons => new[]
    {
        new VirtualActionButton { Label = "Brake", Key = SApp.sapp_keycode.SAPP_KEYCODE_RIGHT_SHIFT },
    };

    public override Vector3 GetFollowPosition(JPH.BodyInterface bi)
    {
        if (_tankBodyId.IsInvalid()) return Vector3.Zero;
        using var pos = bi.GetPosition(_tankBodyId);
        return new Vector3(pos.GetX(), pos.GetY(), pos.GetZ());
    }

    public override float GetFollowYaw(JPH.BodyInterface bi)
    {
        if (_tankBodyId.IsInvalid() || _tankBody == null) return float.NaN;
        using var rot = bi.GetRotation(_tankBodyId);
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
        CreatePlaygroundTerrain(bi, bodies, sampleCount: 200, cellSize: 1.5f, maxHeight: 8.0f);
        for (int w = 0; w < 6; w++)
        {
            float wallZ = 25f + w * 35f + (float)(random.NextDouble() * 15f - 7.5f);
            float wallX = (float)(random.NextDouble() * 60f - 30f);
            CreateWallObstacle(bi, bodies, wallZ, wallX);
        }
        CreateRubble(bi, bodies, random);
        CreateBridge(bi, sys, bodies, _bridgeConstraints);

        // ── Collision group ────────────────────────────────────────────────
        using var groupFilter = new JPH.GroupFilterTable();

        // ── Tank body ──────────────────────────────────────────────────────
        // Body placed at (0, 2, 0). COM shifted down by HalfVehicleHeight.
        using var bodyHE  = new JPH.Vec3(HalfVehicleWidth, HalfVehicleHeight, HalfVehicleLength);
        using var bodyBox = new JPH.BoxShapeSettings(bodyHE);
        using var comOff  = new JPH.Vec3(0f, -HalfVehicleHeight, 0f);
        using var comSS   = new JPH.OffsetCenterOfMassShapeSettings(comOff, bodyBox);

        using var tankCS = new JPH.BodyCreationSettings();
        tankCS.SetShapeSettings(comSS);
        tankCS.mPosition.Set(0f, 2f, 0f);
        tankCS.mMotionType  = JPH.EMotionType.Dynamic;
        tankCS.mObjectLayer = LayerMoving;
        tankCS.mCollisionGroup.SetGroupFilter(groupFilter);
        tankCS.mCollisionGroup.SetGroupID(0);
        tankCS.mCollisionGroup.SetSubGroupID(0);
        tankCS.SetOverrideMassProperties(1);
        tankCS.SetMassOverride(4000f);

        _tankBody   = bi.CreateBody(tankCS);
        _tankBodyId = _tankBody!.GetID();
        bi.AddBody(_tankBodyId, JPH.EActivation.Activate);

        bodies.Add(new PhysicsBody
        {
            bodyId = _tankBodyId,
            color  = new Vector3(0.3f, 0.5f, 0.3f),
            shape  = RenderShape.Box,
            scale  = new Vector3(HalfVehicleWidth * 2f, HalfVehicleHeight * 2f, HalfVehicleLength * 2f),
        });

        // ── Vehicle constraint ─────────────────────────────────────────────
        using var vehicle = new JPH.VehicleConstraintSettings();
        vehicle.mMaxPitchRollAngle = 60f * MathF.PI / 180f;

        using var ctrlSettings = new JPH.TrackedVehicleControllerSettings();
        vehicle.VehicleSettingsSetTrackedController(ctrlSettings);

        uint totalWheels = 0;
        for (int t = 0; t < 2; t++)
        {
            // t=0: right track (positive X), t=1: left track (negative X)
            float trackX = (t == 0) ? HalfVehicleWidth : -HalfVehicleWidth;
            var track    = ctrlSettings.mTracks[t];

            // Driven wheel = last wheel added for this track
            track.mDrivenWheel = totalWheels + (uint)WheelZPos.Length - 1;

            for (int w = 0; w < WheelZPos.Length; w++)
            {
                using var ws = new JPH.WheelSettingsTV();
                ws.mPosition.Set(trackX, WheelYPos[w], WheelZPos[w]);
                ws.mRadius              = WheelRadius;
                ws.mWidth               = WheelWidth;
                ws.mSuspensionMinLength = SuspensionMinLength;
                ws.mSuspensionMaxLength = (w == 0 || w == WheelZPos.Length - 1)
                    ? SuspensionMinLength
                    : SuspensionMaxLength;
                ws.mSuspensionSpring.mFrequency = SuspensionFrequency;

                track.mWheels.PushBack(totalWheels + (uint)w);
                vehicle.VehicleSettingsAddWheelTV(ws);
            }
            totalWheels += (uint)WheelZPos.Length;
        }

        _vehicleConstraint = new JPH.VehicleConstraint(_tankBody!, vehicle);

        using var tester = new JPH.VehicleCollisionTesterRay(LayerMoving);
        _vehicleConstraint.SetVehicleCollisionTester(tester);

        sys.AddConstraint(_vehicleConstraint);
        sys.AddStepListener(_vehicleConstraint);

        // ── Wheel render placeholders ──────────────────────────────────────────
        // 18 wheels (2 tracks × 9 wheels). Not physics bodies; transforms are
        // updated each frame via GetWheelWorldTransform, matching TankTest.cpp.
        _wheelBodyStart = bodies.Count;
        for (int i = 0; i < 18; i++)
        {
            bodies.Add(new PhysicsBody
            {
                bodyId        = _tankBodyId,
                color         = new Vector3(0.72f, 0.72f, 0.72f),
                shape         = RenderShape.Cylinder,
                // Cylinder scale: (diameter, height, diameter)
                scale         = new Vector3(WheelRadius * 2f, WheelWidth, WheelRadius * 2f),
                localOffset   = Vector3.Zero,
                localRotation = Quaternion.Identity,
            });
        }

        // ── Turret body ────────────────────────────────────────────────────
        // Placed on top of tank body: y = 2 + HalfVehicleHeight + HalfTurretHeight = 2.9
        float turretY = 2f + HalfVehicleHeight + HalfTurretHeight;
        using var turretHE = new JPH.Vec3(HalfTurretWidth, HalfTurretHeight, HalfTurretLength);
        using var turretSS = new JPH.BoxShapeSettings(turretHE);

        using var turretCS = new JPH.BodyCreationSettings();
        turretCS.SetShapeSettings(turretSS);
        turretCS.mPosition.Set(0f, turretY, 0f);
        turretCS.mMotionType  = JPH.EMotionType.Dynamic;
        turretCS.mObjectLayer = LayerMoving;
        turretCS.mCollisionGroup.SetGroupFilter(groupFilter);
        turretCS.mCollisionGroup.SetGroupID(0);
        turretCS.mCollisionGroup.SetSubGroupID(0);
        turretCS.SetOverrideMassProperties(1);
        turretCS.SetMassOverride(2000f);

        _turretBody = bi.CreateBody(turretCS);
        bi.AddBody(_turretBody!.GetID(), JPH.EActivation.Activate);

        bodies.Add(new PhysicsBody
        {
            bodyId = _turretBody!.GetID(),
            color  = new Vector3(0.4f, 0.6f, 0.4f),
            shape  = RenderShape.Box,
            scale  = new Vector3(HalfTurretWidth * 2f, HalfTurretHeight * 2f, HalfTurretLength * 2f),
        });

        // Turret hinge on Y-axis (rotate around vertical axis)
        float turretHingeY = 2f + HalfVehicleHeight;   // = 2.5
        using var turretHS = new JPH.HingeConstraintSettings();
        turretHS.mPoint1.Set(0f, turretHingeY, 0f);
        turretHS.mPoint2.Set(0f, turretHingeY, 0f);
        turretHS.mHingeAxis1.Set(0f, 1f, 0f);
        turretHS.mHingeAxis2.Set(0f, 1f, 0f);
        turretHS.mNormalAxis1.Set(0f, 0f, 1f);
        turretHS.mNormalAxis2.Set(0f, 0f, 1f);
        {
            var ms = turretHS.mMotorSettings;
            ms.mSpringSettings.mFrequency = 0.5f;
            ms.mSpringSettings.mDamping   = 1.0f;
        }

        var rawTurret   = turretHS.Create(_tankBody!, _turretBody!);
        _turretHinge    = (JPH.HingeConstraint)rawTurret!;
        _turretHinge.SetMotorState(JPH.EMotorState.Position);
        sys.AddConstraint(_turretHinge);

        // ── Barrel body ────────────────────────────────────────────────────
        // Barrel is a cylinder along Z; placed so its back end is at the turret front edge + BarrelRotationOffset.
        // Barrel center in world: z = HalfTurretLength + HalfBarrelLength - BarrelRotationOffset = 2.0+1.5-0.2 = 3.3
        float barrelCenterZ = HalfTurretLength + HalfBarrelLength - BarrelRotationOffset;
        float barrelY       = turretY;   // same height as turret
        using var barrelRot = JPH.Quat.SRotation(JPH.Vec3.SAxisX(), 0.5f * MathF.PI);

        // CylinderShapeSettings(halfHeight, radius)
        using var barrelSS = new JPH.CylinderShapeSettings(HalfBarrelLength, BarrelRadius);

        using var barrelCS = new JPH.BodyCreationSettings();
        barrelCS.SetShapeSettings(barrelSS);
        barrelCS.mPosition.Set(0f, barrelY, barrelCenterZ);
        barrelCS.mRotation.Set(barrelRot.GetX(), barrelRot.GetY(), barrelRot.GetZ(), barrelRot.GetW());
        barrelCS.mMotionType  = JPH.EMotionType.Dynamic;
        barrelCS.mObjectLayer = LayerMoving;
        barrelCS.mCollisionGroup.SetGroupFilter(groupFilter);
        barrelCS.mCollisionGroup.SetGroupID(0);
        barrelCS.mCollisionGroup.SetSubGroupID(0);
        barrelCS.SetOverrideMassProperties(1);
        barrelCS.SetMassOverride(200f);

        _barrelBody = bi.CreateBody(barrelCS);
        bi.AddBody(_barrelBody!.GetID(), JPH.EActivation.Activate);

        bodies.Add(new PhysicsBody
        {
            bodyId = _barrelBody!.GetID(),
            color  = new Vector3(0.6f, 0.6f, 0.4f),
            shape  = RenderShape.Cylinder,
            scale  = new Vector3(BarrelRadius * 2f, HalfBarrelLength * 2f, BarrelRadius * 2f),
        });

        // Barrel hinge on -X axis (pitch up/down); pivot at back of barrel
        // Hinge point: barrel center - (0, 0, HalfBarrelLength) = (0, barrelY, barrelCenterZ - HalfBarrelLength)
        float barrelHingeZ = barrelCenterZ - HalfBarrelLength;   // = 3.3 - 1.5 = 1.8
        using var barrelHS = new JPH.HingeConstraintSettings();
        barrelHS.mPoint1.Set(0f, barrelY, barrelHingeZ);
        barrelHS.mPoint2.Set(0f, barrelY, barrelHingeZ);
        barrelHS.mHingeAxis1.Set(-1f, 0f, 0f);
        barrelHS.mHingeAxis2.Set(-1f, 0f, 0f);
        barrelHS.mNormalAxis1.Set(0f, 0f, 1f);
        barrelHS.mNormalAxis2.Set(0f, 0f, 1f);
        barrelHS.mLimitsMin = BarrelMinPitch;
        barrelHS.mLimitsMax = BarrelMaxPitch;
        {
            var ms = barrelHS.mMotorSettings;
            ms.mSpringSettings.mFrequency = 10.0f;
            ms.mSpringSettings.mDamping   = 1.0f;
        }

        var rawBarrel  = barrelHS.Create(_turretBody!, _barrelBody!);
        _barrelHinge   = (JPH.HingeConstraint)rawBarrel!;
        _barrelHinge.SetMotorState(JPH.EMotorState.Position);
        sys.AddConstraint(_barrelHinge);
    }

    public override unsafe void Update(
        float dt,
        JPH.BodyInterface bi,
        List<PhysicsBody> bodies)
    {
        // ── Input ─────────────────────────────────────────────────────────
        bool up     = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_UP);
        bool down   = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_DOWN);
        bool left   = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_LEFT);
        bool right  = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_RIGHT);
        bool brake  = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_RIGHT_SHIFT);
        bool turnA  = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_A);
        bool turnD  = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_D);
        bool pitchQ = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_Q);
        bool pitchE = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_E);

        // ── Turret / barrel control ────────────────────────────────────────
        if (turnA) _mTurretHeading += dt * 1.0f;
        if (turnD) _mTurretHeading -= dt * 1.0f;
        if (pitchQ) _mBarrelPitch = MathF.Min(_mBarrelPitch + dt * 0.5f, BarrelMaxPitch);
        if (pitchE) _mBarrelPitch = MathF.Max(_mBarrelPitch - dt * 0.5f, BarrelMinPitch);

        // ── Drive + steering (matches TankTest.cpp::ProcessInput) ───────────
        // Determine forward / brake from keys (exclusive: RShift > Up > Down)
        float newForward = 0f;
        float newBrake   = 0f;
        if (brake)       newBrake   = 1f;
        else if (up)     newForward = 1f;
        else if (down)   newForward = -1f;

        float newLeftRatio  = 1f;
        float newRightRatio = 1f;

        // Local-space velocity along Z (forward axis) via Conjugated rotation.
        float velocity = 0f;
        if (_tankBody != null)
        {
            using var tankRot  = _tankBody.GetRotation();
            using var tankVel  = _tankBody.GetLinearVelocity();
            using var conjRot  = tankRot.Conjugated();
            using var localVel = conjRot * tankVel;
            velocity = localVel.GetZ();
        }

        const float MinVelocityPivotTurn = 1.0f;
        if (left)
        {
            if (newBrake == 0f && newForward == 0f && MathF.Abs(velocity) < MinVelocityPivotTurn)
            { newLeftRatio = -1f; newForward = 1f; }   // pivot turn
            else
                newLeftRatio = 0.6f;
        }
        else if (right)
        {
            if (newBrake == 0f && newForward == 0f && MathF.Abs(velocity) < MinVelocityPivotTurn)
            { newRightRatio = -1f; newForward = 1f; }  // pivot turn
            else
                newRightRatio = 0.6f;
        }

        // Direction reversal: brake until we've stopped, then accept new direction.
        if (_mPreviousForward * newForward < 0f)
        {
            if ((newForward > 0f && velocity < -0.1f) || (newForward < 0f && velocity > 0.1f))
            {
                newForward = 0f;
                newBrake   = 1f;
            }
            else
            {
                _mPreviousForward = newForward;
            }
        }

        _mForward    = newForward;
        _mBrake      = newBrake;
        _mLeftRatio  = newLeftRatio;
        _mRightRatio = newRightRatio;

        // ── Apply to vehicle and hinges ────────────────────────────────────
        if (_vehicleConstraint != null)
        {
            var ctrl = (JPH.TrackedVehicleController)_vehicleConstraint.GetController()!;
            ctrl.SetDriverInput(_mForward, _mLeftRatio, _mRightRatio, _mBrake);
        }

        _turretHinge?.SetTargetAngle(_mTurretHeading);
        _barrelHinge?.SetTargetAngle(_mBarrelPitch);

        if (_tankBody != null)
            bi.ActivateBody(_tankBodyId);

        // ── Update wheel render transforms ─────────────────────────────────
        // Decompose GetWheelWorldTransform into car-relative offset + rotation
        // (same approach as Demo_VehicleConstraint).
        if (_vehicleConstraint != null && _wheelBodyStart >= 0)
        {
            using var axisY = JPH.Vec3.SAxisY();
            using var axisX = JPH.Vec3.SAxisX();
            using var carWt = bi.GetWorldTransform(_tankBodyId);
            using var carTr = carWt.GetTranslation();
            using var carRQ = carWt.GetQuaternion();

            var carQ    = new Quaternion(carRQ.GetX(), carRQ.GetY(), carRQ.GetZ(), carRQ.GetW());
            var carQInv = Quaternion.Inverse(carQ);

            for (int i = 0; i < 18; i++)
            {
                using var wheelMat  = _vehicleConstraint.GetWheelWorldTransform((uint)i, axisY, axisX);
                using var wheelPos  = wheelMat.GetTranslation();
                using var wheelQuat = wheelMat.GetQuaternion();

                var delta  = new Vector3(
                    wheelPos.GetX() - carTr.GetX(),
                    wheelPos.GetY() - carTr.GetY(),
                    wheelPos.GetZ() - carTr.GetZ());
                var relPos = Vector3.Transform(delta, carQInv);
                var relRot = carQInv * new Quaternion(wheelQuat.GetX(), wheelQuat.GetY(), wheelQuat.GetZ(), wheelQuat.GetW());

                if (relRot.W == 0f) relRot.W = 1e-6f;

                var wb = bodies[_wheelBodyStart + i];
                wb.localOffset   = relPos;
                wb.localRotation = relRot;
                bodies[_wheelBodyStart + i] = wb;
            }
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
        if (_turretHinge != null)
        {
            sys.RemoveConstraint(_turretHinge);
            _turretHinge.Dispose();
            _turretHinge = null;
        }
        if (_barrelHinge != null)
        {
            sys.RemoveConstraint(_barrelHinge);
            _barrelHinge.Dispose();
            _barrelHinge = null;
        }
        _tankBody = _turretBody = _barrelBody = null;
    }
}
