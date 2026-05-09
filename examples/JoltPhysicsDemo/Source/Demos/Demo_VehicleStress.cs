using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of Samples/Tests/Vehicle/VehicleStressTest.cpp.
///
/// Spawns 15×15 = 225 identical vehicles on a flat grid, stress-testing the
/// vehicle constraint solver.  Collision test is done every other step
/// (SetNumStepsBetweenCollisionTestActive = 2) to keep the simulation fast.
///
/// Controls (apply to all vehicles simultaneously):
///   Up / Down   — accelerate / reverse
///   Left / Right — steer
///   Z           — hand brake
/// </summary>
public sealed class Demo_VehicleStress : DemoBase
{
    public override string Name     => "Vehicle Stress";
    public override string Category => "Vehicle";

    const float WheelRadius        = 0.3f;
    const float WheelWidth         = 0.1f;
    const float HalfVehicleLength  = 2.0f;
    const float HalfVehicleWidth   = 0.9f;
    const float HalfVehicleHeight  = 0.2f;
    const float MaxSteeringAngle   = MathF.PI / 6f;  // 30°

    const int GridCols = 15;
    const int GridRows = 15;

    readonly List<JPH.VehicleConstraint> _vehicles = new();
    JPH.VehicleCollisionTesterRay?       _tester;

    float _forward   = 0f;
    float _right     = 0f;
    float _handBrake = 0f;

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 120,
        Latitude  = 35,
        Longitude = 180,
        Center    = new Vector3(0f, 0f, 0f),
    };

    public override bool CameraFollowsPlayer => true;
    public override VirtualControlsType VirtualControls => VirtualControlsType.Arrows;

    public override Vector3 GetFollowPosition(JPH.BodyInterface bi)
    {
        if (_vehicles.Count == 0) return Vector3.Zero;
        var firstBody = _vehicles[0].GetVehicleBody();
        if (firstBody == null) return Vector3.Zero;
        using var pos = bi.GetPosition(firstBody.GetID());
        return new Vector3(pos.GetX(), pos.GetY(), pos.GetZ());
    }

    public override unsafe void Init(
        JPH.BodyInterface  bi,
        JPH.PhysicsSystem  sys,
        List<PhysicsBody>  bodies,
        Random             random)
    {
        AddFloor(bi, bodies);

        // ── Boundary walls (±50 m) ──────────────────────────────────────────
        // North / South walls (50 m half-extent in X, 0.5 m in Z)
        AddBox(bi, bodies, 50f, 5f, 0.5f,  0f, 5f, -50f, Quaternion.Identity,
               JPH.EMotionType.Static, LayerNonMoving, new Vector3(0.5f, 0.5f, 0.5f));
        AddBox(bi, bodies, 50f, 5f, 0.5f,  0f, 5f,  50f, Quaternion.Identity,
               JPH.EMotionType.Static, LayerNonMoving, new Vector3(0.5f, 0.5f, 0.5f));
        // East / West walls (0.5 m in X, 50 m half-extent in Z)
        AddBox(bi, bodies, 0.5f, 5f, 50f, -50f, 5f, 0f, Quaternion.Identity,
               JPH.EMotionType.Static, LayerNonMoving, new Vector3(0.5f, 0.5f, 0.5f));
        AddBox(bi, bodies, 0.5f, 5f, 50f,  50f, 5f, 0f, Quaternion.Identity,
               JPH.EMotionType.Static, LayerNonMoving, new Vector3(0.5f, 0.5f, 0.5f));

        // ── Car body template ───────────────────────────────────────────────
        // No OffsetCenterOfMass in the stress test; plain box shape.
        // Use density override to achieve 1500 kg mass.
        float carVolume  = (2f * HalfVehicleWidth) * (2f * HalfVehicleHeight) * (2f * HalfVehicleLength);
        float carDensity = 1500f / carVolume;  // ≈ 520.83 kg/m³

        using var boxHE = new JPH.Vec3(HalfVehicleWidth, HalfVehicleHeight, HalfVehicleLength);
        using var boxSS = new JPH.BoxShapeSettings(boxHE);
        boxSS.SetDensity(carDensity);

        using var carCs = new JPH.BodyCreationSettings();
        carCs.SetShapeSettings(boxSS);
        carCs.mMotionType  = JPH.EMotionType.Dynamic;
        carCs.mObjectLayer = LayerMoving;
        carCs.mFriction    = 0.5f;

        // ── Vehicle constraint template ─────────────────────────────────────
        float wy = -0.9f * HalfVehicleHeight;
        float wz_front = HalfVehicleLength - 2f * WheelRadius;
        float wz_rear  = -(HalfVehicleLength - 2f * WheelRadius);

        using var vehicleSettings = new JPH.VehicleConstraintSettings();
        vehicleSettings.mMaxPitchRollAngle = MathF.PI / 3f;  // 60°

        using var w1 = new JPH.WheelSettingsWV();
        w1.mPosition.Set( HalfVehicleWidth, wy, wz_front);
        w1.mMaxSteerAngle      = MaxSteeringAngle;
        w1.mMaxHandBrakeTorque = 0f;
        w1.mRadius = WheelRadius;
        w1.mWidth  = WheelWidth;

        using var w2 = new JPH.WheelSettingsWV();
        w2.mPosition.Set(-HalfVehicleWidth, wy, wz_front);
        w2.mMaxSteerAngle      = MaxSteeringAngle;
        w2.mMaxHandBrakeTorque = 0f;
        w2.mRadius = WheelRadius;
        w2.mWidth  = WheelWidth;

        using var w3 = new JPH.WheelSettingsWV();
        w3.mPosition.Set( HalfVehicleWidth, wy, wz_rear);
        w3.mMaxSteerAngle = 0f;
        w3.mRadius = WheelRadius;
        w3.mWidth  = WheelWidth;

        using var w4 = new JPH.WheelSettingsWV();
        w4.mPosition.Set(-HalfVehicleWidth, wy, wz_rear);
        w4.mMaxSteerAngle = 0f;
        w4.mRadius = WheelRadius;
        w4.mWidth  = WheelWidth;

        vehicleSettings.VehicleSettingsAddWheel(w1);
        vehicleSettings.VehicleSettingsAddWheel(w2);
        vehicleSettings.VehicleSettingsAddWheel(w3);
        vehicleSettings.VehicleSettingsAddWheel(w4);

        using var ctrlSettings = new JPH.WheeledVehicleControllerSettings();
        vehicleSettings.VehicleSettingsSetController(ctrlSettings);

        using var diff = new JPH.VehicleDifferentialSettings();
        diff.mLeftWheel  = 0;
        diff.mRightWheel = 1;
        ctrlSettings.mDifferentials.PushBack(diff);

        // ── Shared collision tester ─────────────────────────────────────────
        _tester = new JPH.VehicleCollisionTesterRay(LayerMoving);

        // ── Spawn 15×15 vehicles ────────────────────────────────────────────
        var carColor = new Vector3(0.8f, 0.4f, 0.2f);
        var carScale = new Vector3(HalfVehicleWidth * 2f, HalfVehicleHeight * 2f, HalfVehicleLength * 2f);

        for (int col = 0; col < GridCols; col++)
        for (int row = 0; row < GridRows; row++)
        {
            float px = -28f + col * 4f;
            float pz = -35f + row * 5f;
            carCs.mPosition.Set(px, 2f, pz);

            var carBody = bi.CreateBody(carCs);
            if (carBody == null) continue;

            var carBodyId = carBody.GetID();
            bi.AddBody(carBodyId, JPH.EActivation.Activate);

            bodies.Add(new PhysicsBody
            {
                bodyId = carBodyId,
                color  = carColor,
                shape  = RenderShape.Box,
                scale  = carScale,
            });

            var constraint = new JPH.VehicleConstraint(carBody, vehicleSettings);
            constraint.SetNumStepsBetweenCollisionTestActive(2);
            constraint.SetNumStepsBetweenCollisionTestInactive(0);
            constraint.SetVehicleCollisionTester(_tester!);

            sys.AddConstraint(constraint);
            sys.AddStepListener(constraint);

            _vehicles.Add(constraint);
        }
    }

    public override unsafe void Update(
        float             dt,
        JPH.BodyInterface bi,
        List<PhysicsBody> bodies)
    {
        bool up    = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_UP);
        bool down  = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_DOWN);
        bool left  = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_LEFT);
        bool right = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_RIGHT);
        bool z     = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_Z);

        _forward   = up ? 1f : (down ? -1f : 0f);
        _right     = left ? -1f : (right ? 1f : 0f);
        _handBrake = z ? 1f : 0f;
        if (z) _forward = 0f;

        bool hasInput = _right != 0f || _forward != 0f || _handBrake != 0f;

        foreach (var c in _vehicles)
        {
            if (hasInput)
                bi.ActivateBody(c.GetVehicleBody()!.GetID());

            var ctrl = c.GetWheeledController();
            ctrl?.SetDriverInput(_forward, _right, 0f, _handBrake);
        }
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        foreach (var c in _vehicles)
        {
            sys.RemoveStepListener(c);
            sys.RemoveConstraint(c);
            c.Dispose();
        }
        _vehicles.Clear();

        _tester?.Dispose();
        _tester = null;
    }
}
