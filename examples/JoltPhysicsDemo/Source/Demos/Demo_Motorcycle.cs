using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;
using static Sokol.SG;
using static Sokol.Utils;

/// <summary>
/// Port of Samples/Tests/Vehicle/MotorcycleTest.cpp.
///
/// A two-wheel motorcycle using MotorcycleController.
/// Based loosely on Yamaha XJ-900 specs.
/// Collision tester: CastCylinder with convex-radius fraction = 1.0
/// (half wheel width, giving a rounded cylinder).
///
/// Controls:
///   Up / Down   — accelerate / reverse
///   Left / Right — steer (smoothed)
///   Z           — brake
///
/// </summary>
public sealed class Demo_Motorcycle : DemoBase
{
    public override string Name     => "Motorcycle";
    public override string Category => "Vehicle";

    // ── Physics constants (from MotorcycleTest.cpp — Yamaha XJ-900) ────────
    const float WheelRadius          = 0.31f;
    const float WheelWidth           = 0.05f;
    const float FrontWheelPosZ       = 0.75f;
    const float BackWheelPosZ        = -0.75f;
    const float HalfLength           = 0.4f;
    const float HalfWidth            = 0.2f;
    const float HalfHeight           = 0.3f;
    const float WheelY               = -0.9f * HalfHeight;  // = -0.27

    const float MaxSteeringAngle     = MathF.PI / 6f;   // 30°
    const float MaxPitchRollAngle    = MathF.PI / 3f;   // 60°

    // Caster angle 30° → suspension direction = Normalize(0, -1, tan30)
    static readonly float TanCaster      = MathF.Tan(MathF.PI / 6f);    // ≈ 0.57735
    static readonly float CasterLen      = MathF.Sqrt(1f + TanCaster * TanCaster); // ≈ 1.15470

    const float SuspMinLength        = 0.3f;
    const float SuspMaxLength        = 0.5f;
    const float FrontSuspFreq        = 1.5f;
    const float BackSuspFreq         = 2.0f;
    const float FrontBrakeTorque     = 500.0f;
    const float BackBrakeTorque      = 250.0f;

    const float MaxEngineTorque      = 150.0f;
    const float MinRPM               = 1000.0f;
    const float MaxRPM               = 10000.0f;
    const float ShiftDownRPM         = 2000.0f;
    const float ShiftUpRPM           = 8000.0f;
    const float ClutchStrength       = 2.0f;
    const float DifferentialRatio    = 1.93f * 40.0f / 16.0f;  // primary + final drive ≈ 4.825

    const float SteerSpeed           = 4.0f;  // rad/s for steering smoothing

    // ── State ──────────────────────────────────────────────────────────────
    JPH.VehicleConstraint?                   _vehicleConstraint;
    JPH.Body?                                _bikeBody;
    JPH.VehicleCollisionTesterCastCylinder?  _tester;
    JPH.BodyID                               _bikeBodyId;

    float _forward         = 0f;
    float _previousForward = 1f;
    float _right           = 0f;
    float _brake           = 0f;

    // Index in 'bodies' where the 2 wheel cylinder entries start.
    int _wheelBodyStart = -1;

    readonly List<JPH.TwoBodyConstraint?> _bridgeConstraints = new();

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 12,
        Latitude  = 20,
        Longitude = 180,
        Center    = new Vector3(0f, 2f, 0f),
    };

    public override bool CameraFollowsPlayer => true;
    public override VirtualControlsType VirtualControls => VirtualControlsType.Arrows;

    public override Vector3 GetFollowPosition(JPH.BodyInterface bi)
    {
        if (_bikeBodyId.IsInvalid()) return Vector3.Zero;
        using var pos = bi.GetPosition(_bikeBodyId);
        return new Vector3(pos.GetX(), pos.GetY(), pos.GetZ());
    }

    public override unsafe void Init(
        JPH.BodyInterface   bi,
        JPH.PhysicsSystem   sys,
        List<PhysicsBody>   bodies,
        Random              random)
    {
        CreatePlaygroundTerrain(bi, bodies);
        for (int w = 0; w < 6; w++)
        {
            float wallZ = 25f + w * 35f + (float)(random.NextDouble() * 15f - 7.5f);
            float wallX = (float)(random.NextDouble() * 60f - 30f);
            CreateWallObstacle(bi, bodies, wallZ, wallX);
        }
        CreateRubble(bi, bodies, random);
        CreateBridge(bi, sys, bodies, _bridgeConstraints);

        // ── Collision tester ────────────────────────────────────────────────
        // Use half wheel width as convex radius fraction = 1.0 (matches C++ default).
        _tester = new JPH.VehicleCollisionTesterCastCylinder((ushort)LayerMoving, 1.0f);

        // ── Motorcycle body ──────────────────────────────────────────────────
        using var boxHE  = new JPH.Vec3(HalfWidth, HalfHeight, HalfLength);
        using var boxSS  = new JPH.BoxShapeSettings(boxHE);
        using var comOff = new JPH.Vec3(0f, -HalfHeight, 0f);
        using var comSS  = new JPH.OffsetCenterOfMassShapeSettings(comOff, boxSS);

        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(comSS);
        cs.mPosition.Set(0f, 2f, 0f);
        cs.mMotionType  = JPH.EMotionType.Dynamic;
        cs.mObjectLayer = LayerMoving;
        cs.SetOverrideMassProperties(1); // 1 = CalculateInertia
        cs.SetMassOverride(240.0f);

        _bikeBody   = bi.CreateBody(cs);
        _bikeBodyId = _bikeBody!.GetID();
        bi.AddBody(_bikeBodyId, JPH.EActivation.Activate);

        bodies.Add(new PhysicsBody
        {
            bodyId = _bikeBodyId,
            color  = new Vector3(0.8f, 0.3f, 0.1f),
            shape  = RenderShape.Box,
            scale  = new Vector3(HalfWidth * 2f, HalfHeight * 2f, HalfLength * 2f),
        });

        // ── Wheel render placeholders ────────────────────────────────────────
        _wheelBodyStart = bodies.Count;
        bodies.Add(new PhysicsBody   // index 0: front wheel
        {
            bodyId        = _bikeBodyId,
            color         = new Vector3(0.1f, 0.8f, 0.1f),
            shape         = RenderShape.Cylinder,
            scale         = new Vector3(WheelRadius * 2f, WheelWidth, WheelRadius * 2f),
            localOffset   = new Vector3(0f, WheelY, FrontWheelPosZ),
            localRotation = Quaternion.Identity,
        });
        bodies.Add(new PhysicsBody   // index 1: back wheel
        {
            bodyId        = _bikeBodyId,
            color         = new Vector3(0.1f, 0.8f, 0.1f),
            shape         = RenderShape.Cylinder,
            scale         = new Vector3(WheelRadius * 2f, WheelWidth, WheelRadius * 2f),
            localOffset   = new Vector3(0f, WheelY, BackWheelPosZ),
            localRotation = Quaternion.Identity,
        });

        // ── Vehicle constraint settings ──────────────────────────────────────
        using var vehicleSettings = new JPH.VehicleConstraintSettings();
        vehicleSettings.mDrawConstraintSize = 0.1f;
        vehicleSettings.mMaxPitchRollAngle  = MaxPitchRollAngle;

        // Front wheel — caster angle tilts suspension direction forward.
        // suspDir = Normalize(0, -1, tan30)  ≈ (0, -0.866, 0.500)
        // steerAxis = -suspDir                ≈ (0, +0.866, -0.500)
        using var front = new JPH.WheelSettingsWV();
        front.mPosition.Set(0f, WheelY, FrontWheelPosZ);
        front.mSuspensionDirection.Set(0f, -1f / CasterLen, TanCaster / CasterLen);
        front.mSteeringAxis.Set(0f, 1f / CasterLen, -TanCaster / CasterLen);
        front.mWheelUp.Set(0f, 1f, 0f);
        front.mWheelForward.Set(0f, 0f, 1f);
        front.mSuspensionMinLength = SuspMinLength;
        front.mSuspensionMaxLength = SuspMaxLength;
        front.mSuspensionSpring.mFrequency = FrontSuspFreq;
        front.mMaxSteerAngle               = MaxSteeringAngle;
        front.mMaxBrakeTorque              = FrontBrakeTorque;
        front.mRadius = WheelRadius;
        front.mWidth  = WheelWidth;

        // Back wheel — no steering, default (vertical) suspension direction.
        using var back = new JPH.WheelSettingsWV();
        back.mPosition.Set(0f, WheelY, BackWheelPosZ);
        back.mSuspensionDirection.Set(0f, -1f, 0f);
        back.mSteeringAxis.Set(0f, 1f, 0f);
        back.mWheelUp.Set(0f, 1f, 0f);
        back.mWheelForward.Set(0f, 0f, 1f);
        back.mSuspensionMinLength = SuspMinLength;
        back.mSuspensionMaxLength = SuspMaxLength;
        back.mSuspensionSpring.mFrequency = BackSuspFreq;
        back.mMaxSteerAngle               = 0f;
        back.mMaxBrakeTorque              = BackBrakeTorque;
        back.mRadius = WheelRadius;
        back.mWidth  = WheelWidth;

        vehicleSettings.VehicleSettingsAddWheel(front);
        vehicleSettings.VehicleSettingsAddWheel(back);

        // ── Controller settings ──────────────────────────────────────────────
        using var ctrlSettings = new JPH.MotorcycleControllerSettings();

        ctrlSettings.mEngine.mMaxTorque = MaxEngineTorque;
        ctrlSettings.mEngine.mMinRPM    = MinRPM;
        ctrlSettings.mEngine.mMaxRPM    = MaxRPM;

        ctrlSettings.mTransmission.mShiftDownRPM   = ShiftDownRPM;
        ctrlSettings.mTransmission.mShiftUpRPM     = ShiftUpRPM;
        ctrlSettings.mTransmission.mClutchStrength  = ClutchStrength;
        // Yamaha XJ-900 gear ratios (from MotorcycleTest.cpp).
        using var trans = ctrlSettings.mTransmission;
        trans.SetGearRatios(new float[] { 2.27f, 1.63f, 1.3f, 1.09f, 0.96f, 0.88f });
        trans.SetReverseGearRatios(new float[] { -2.9f });

        vehicleSettings.VehicleSettingsSetController(ctrlSettings);

        // ── Differential (single rear-wheel drive) ───────────────────────────
        using var diff = new JPH.VehicleDifferentialSettings();
        diff.mLeftWheel          = -1;
        diff.mRightWheel         = 1;
        diff.mDifferentialRatio  = DifferentialRatio;
        ctrlSettings.mDifferentials.PushBack(diff);

        // ── Create constraint ────────────────────────────────────────────────
        _vehicleConstraint = new JPH.VehicleConstraint(_bikeBody!, vehicleSettings);
        _vehicleConstraint.SetVehicleCollisionTester(
            _tester!);

        sys.AddConstraint(_vehicleConstraint);
        sys.AddStepListener(_vehicleConstraint);
    }

    public override unsafe void Update(
        float              dt,
        JPH.BodyInterface  bi,
        List<PhysicsBody>  bodies)
    {
        if (_vehicleConstraint == null) return;

        // ── Driver input ─────────────────────────────────────────────────────
        bool up    = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_UP);
        bool down  = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_DOWN);
        bool left  = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_LEFT);
        bool right = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_RIGHT);
        bool z     = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_Z);

        float newForward = up ? 1f : (down ? -1f : 0f);
        _brake = 0f;

        if (z)
        {
            newForward = 0f;
            _brake     = 1f;
        }
        else if (_previousForward * newForward < 0f)
        {
            // Switching direction — brake until stopped.
            using var bikeRot  = _bikeBody!.GetRotation();
            using var bikeVel  = _bikeBody!.GetLinearVelocity();
            using var conjRot  = bikeRot.Conjugated();
            using var localVel = conjRot * bikeVel;
            float velocityZ    = localVel.GetZ();

            if ((newForward > 0f && velocityZ < -0.1f) || (newForward < 0f && velocityZ > 0.1f))
            {
                newForward = 0f;
                _brake     = 1f;
            }
            else
            {
                _previousForward = newForward;
            }
        }

        _forward = newForward;

        // ── Steering — smoothed at rate SteerSpeed ───────────────────────────
        float targetRight = left ? -1f : (right ? 1f : 0f);
        if (targetRight > _right)
            _right = MathF.Min(_right + SteerSpeed * dt, targetRight);
        else if (targetRight < _right)
            _right = MathF.Max(_right - SteerSpeed * dt, targetRight);

        // ── Lean-based brake reduction ────────────────────────────────────────
        // When the bike is leaned over, full braking causes a spin-out.
        // Reduce brake by (1 - sin_lean_angle)² as in MotorcycleTest.cpp.
        if (_brake > 0f)
        {
            using var bikeRotQ  = bi.GetRotation(_bikeBodyId);
            var       bikeQ     = new Quaternion(bikeRotQ.GetX(), bikeRotQ.GetY(), bikeRotQ.GetZ(), bikeRotQ.GetW());

            using var localUpJ  = _vehicleConstraint.GetLocalUp();
            using var localFwdJ = _vehicleConstraint.GetLocalForward();

            var worldUp  = Vector3.Transform(
                new Vector3(localUpJ.GetX(),  localUpJ.GetY(),  localUpJ.GetZ()),  bikeQ);
            var worldFwd = Vector3.Transform(
                new Vector3(localFwdJ.GetX(), localFwdJ.GetY(), localFwdJ.GetZ()), bikeQ);

            // world_up (gravity up = Y+), lean is how far bike's local-up deviates from Y+
            var gravityUp    = new Vector3(0f, 1f, 0f);
            float sinLean    = MathF.Abs(Vector3.Dot(Vector3.Cross(gravityUp, worldUp), worldFwd));
            float brakeMul   = (1f - sinLean) * (1f - sinLean);
            _brake          *= brakeMul;
        }

        // ── Activate bike on any input ────────────────────────────────────────
        if (_right != 0f || _forward != 0f || _brake != 0f)
            bi.ActivateBody(_bikeBodyId);

        // ── Send input to motorcycle controller ──────────────────────────────
        var whc  = _vehicleConstraint.GetWheeledController();
        var ctrl = (JPH.MotorcycleController)whc!;
        ctrl.SetDriverInput(_forward, _right, _brake, 0f);
        ctrl.EnableLeanController(true);

        // ── Update wheel render transforms ───────────────────────────────────
        using var axisY  = JPH.Vec3.SAxisY();
        using var axisX  = JPH.Vec3.SAxisX();
        using var bikeWt = bi.GetWorldTransform(_bikeBodyId);
        using var bikeTr = bikeWt.GetTranslation();
        using var bikeRQ = bikeWt.GetQuaternion();

        var bikeQuat    = new Quaternion(bikeRQ.GetX(), bikeRQ.GetY(), bikeRQ.GetZ(), bikeRQ.GetW());
        var bikeQuatInv = Quaternion.Inverse(bikeQuat);

        for (int i = 0; i < 2; i++)
        {
            using var wheelMat  = _vehicleConstraint.GetWheelWorldTransform((uint)i, axisY, axisX);
            using var wheelPos  = wheelMat.GetTranslation();
            using var wheelQuat = wheelMat.GetQuaternion();

            var delta  = new Vector3(
                wheelPos.GetX() - bikeTr.GetX(),
                wheelPos.GetY() - bikeTr.GetY(),
                wheelPos.GetZ() - bikeTr.GetZ());
            var relPos = Vector3.Transform(delta, bikeQuatInv);
            var relRot = bikeQuatInv * new Quaternion(
                wheelQuat.GetX(), wheelQuat.GetY(), wheelQuat.GetZ(), wheelQuat.GetW());

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
        _tester?.Dispose(); _tester = null;
        _bikeBody = null;
    }

    public override float GetFollowYaw(JPH.BodyInterface bi)
    {
        if (_bikeBodyId.IsInvalid() || _bikeBody == null) return float.NaN;
        using var rot = bi.GetRotation(_bikeBodyId);
        var q   = new Quaternion(rot.GetX(), rot.GetY(), rot.GetZ(), rot.GetW());
        var fwd = Vector3.Transform(Vector3.UnitZ, q);
        return MathF.Atan2(fwd.X, fwd.Z) * 180f / MathF.PI;
    }
}
