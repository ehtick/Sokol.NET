using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of Samples/Tests/Character/CharacterTest.cpp.
///
/// A kinematic character controller using JPH::Character. The character stands on a
/// variety of obstacles (ramps, stairs, dynamic boxes, a kinematic rotating platform).
///
/// Controls:
///   W / S / A / D   — move forward / back / left / right
///   Space           — jump
///   Left Shift      — toggle crouch
/// </summary>
public sealed class Demo_CharacterTest : DemoBase
{
    public override string Name     => "Character";
    public override string Category => "Character";

    // ── Constants (match CharacterTest.cpp / CharacterBaseTest.cpp) ──────────
    const float cCharacterHeightStanding = 1.35f;
    const float cCharacterRadiusStanding = 0.3f;
    const float cCharacterHeightCrouching = 0.8f;
    const float cCharacterRadiusCrouching = 0.3f;
    const float cCharacterSpeed  = 6.0f;
    const float cJumpSpeed       = 6.0f;
    const float cCollisionTolerance = 0.05f;

    // ── State ────────────────────────────────────────────────────────────────
    JPH.Character?    _character;
    JPH.Const_Shape?  _standingShape;
    JPH.Const_Shape?  _crouchingShape;
    bool              _isCrouching;
    JPH.BodyID        _kinematicBodyId;
    JPH.BodyID        _rotatingWallBodyId;
    JPH.BodyID        _rotatingAndTranslatingBodyId;
    JPH.BodyID        _smoothVertBodyId;
    JPH.BodyID        _reversingVertBodyId;
    JPH.BodyID        _horzMovingBodyId;
    readonly JPH.BodyID[] _rampBlocks = new JPH.BodyID[4];
    float             _time;
    float             _rampBlocksTimeLeft;
    float             _reversingVertY;
    float             _reversingVelocity = 1.0f;
    JPH.PhysicsSystem? _sys;
    List<PhysicsBody>? _bodies;
    int               _charBodyIdx = -1;
    float             _facingYaw   = float.NaN;
    bool              _prevLeftDown;
    bool              _prevRightDown;

    // ── Camera ───────────────────────────────────────────────────────────────
    public override bool CameraFollowsPlayer => true;
    public override VirtualControlsType VirtualControls => VirtualControlsType.WASD;
    public override VirtualActionButton[] VirtualActionButtons => new[]
    {
        new VirtualActionButton { Label = "Jump", Key = SApp.sapp_keycode.SAPP_KEYCODE_SPACE },
        new VirtualActionButton { Label = "Crouch", Key = SApp.sapp_keycode.SAPP_KEYCODE_LEFT_SHIFT },
    };

    public override Vector3 GetFollowPosition(JPH.BodyInterface bi)
    {
        if (_character == null) return Vector3.Zero;
        using var p = _character.GetPosition();
        // Offset up to capsule center so camera orbits the character's mid-body
        float h = _isCrouching ? cCharacterHeightCrouching : cCharacterHeightStanding;
        float r = _isCrouching ? cCharacterRadiusCrouching : cCharacterRadiusStanding;
        return new Vector3(p.GetX(), p.GetY() + 0.5f * h + r, p.GetZ());
    }

    public override float GetFollowYaw(JPH.BodyInterface bi) => _facingYaw;

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 10,
        Latitude  = 20,
        Longitude = 45,
        Center    = new Vector3(-3.5f, 1, 3),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 500.0f
    };

    // ── Init ─────────────────────────────────────────────────────────────────
    public override void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random random)
    {
        _sys = sys;
        _facingYaw = -180f; // start facing -Z

        // Build standing shape: capsule offset up so bottom sits at y=0
        using var capsuleStand = new JPH.CapsuleShape(
            0.5f * cCharacterHeightStanding, cCharacterRadiusStanding);
        using var standOffset = new JPH.Vec3(0, 0.5f * cCharacterHeightStanding + cCharacterRadiusStanding, 0);
        using var standRot = JPH.Quat.SIdentity();
        _standingShape  = (JPH.Const_RotatedTranslatedShape)new JPH.RotatedTranslatedShape(standOffset, standRot, capsuleStand);

        // Build crouching shape
        using var capsuleCrouch = new JPH.CapsuleShape(
            0.5f * cCharacterHeightCrouching, cCharacterRadiusCrouching);
        using var crouchOffset = new JPH.Vec3(0, 0.5f * cCharacterHeightCrouching + cCharacterRadiusCrouching, 0);
        using var crouchRot = JPH.Quat.SIdentity();
        _crouchingShape = (JPH.Const_RotatedTranslatedShape)new JPH.RotatedTranslatedShape(crouchOffset, crouchRot, capsuleCrouch);

        // CharacterSettings
        using var settings = new JPH.CharacterSettings();
        settings.mMaxSlopeAngle = 45f * MathF.PI / 180f;
        settings.mLayer = LayerMoving;
        settings.mFriction = 0.5f;
        settings.SetShape(_standingShape);
        var supVol = settings.mSupportingVolume;
        using var axisY = new JPH.Vec3(0, 1, 0);
        supVol.SetNormal(axisY);
        supVol.SetConstant(-cCharacterRadiusStanding);

        using var startPos = new JPH.Vec3(-3.5f, 0.5f, 3.0f);
        using var startRot = JPH.Quat.SIdentity();
        _character = new JPH.Character(settings, startPos, startRot, 0, sys);
        _character.AddToPhysicsSystem(JPH.EActivation.Activate);

        // Add character capsule to the render list
        // bi.GetPositionAndRotation returns the body COM, which equals the capsule
        // center because the RotatedTranslatedShape offsets the capsule upward by (0.5*h+r).
        _bodies = bodies;
        _charBodyIdx = bodies.Count;
        bodies.Add(new PhysicsBody
        {
            bodyId      = _character.GetBodyID(),
            shape       = RenderShape.Capsule,
            scale       = new Vector3(cCharacterRadiusStanding, 0.5f * cCharacterHeightStanding, cCharacterRadiusStanding),
            localOffset = new Vector3(0, 0.5f * cCharacterHeightStanding + cCharacterRadiusStanding, 0),
            color       = new Vector3(0.2f, 0.6f, 1.0f)
        });

        // ── Obstacle course ──────────────────────────────────────────────────

        // Large floor
        AddFloor(bi, bodies, hx: 175f, hy: 1f, hz: 175f);

        var gray      = new Vector3(0.6f, 0.6f, 0.6f);
        var orange    = new Vector3(1.0f, 0.5f, 0.1f);
        var blue      = new Vector3(0.2f, 0.4f, 0.9f);
        var green     = new Vector3(0.2f, 0.8f, 0.3f);
        var red       = new Vector3(0.9f, 0.2f, 0.2f);

        // Ramps at various angles
        float[] rampAngles = { 10f, 20f, 30f, 40f };
        for (int i = 0; i < rampAngles.Length; i++)
        {
            float ang = rampAngles[i] * MathF.PI / 180f;
            var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, ang);
            float rampLen = 5f;
            float cx = -10f;
            float cz = 5f + i * 12f;
            float cy = rampLen * 0.5f * MathF.Sin(ang);
            AddBox(bi, bodies, rampLen * 0.5f, 0.1f, rampLen,
                cx, cy, cz, rot,
                JPH.EMotionType.Static, LayerNonMoving, gray);
        }

        // Stairs: series of steps going up
        {
            float stepH = 0.3f, stepD = 0.4f;
            int numSteps = 10;
            for (int i = 0; i < numSteps; i++)
            {
                float sx = 5f;
                float sy = (i + 1) * stepH * 0.5f;
                float sz = 10f + i * stepD;
                AddBox(bi, bodies, 1.5f, (i + 1) * stepH * 0.5f, stepD * 0.5f,
                    sx, sy, sz, Quaternion.Identity,
                    JPH.EMotionType.Static, LayerNonMoving, gray);
            }
        }

        // Small bumps
        for (int i = 0; i < 10; i++)
        {
            float bx = -5f + i * 2f;
            AddBox(bi, bodies, 0.5f, 0.1f, 0.5f,
                bx, 0.1f, 20f, Quaternion.Identity,
                JPH.EMotionType.Static, LayerNonMoving, gray);
        }

        // Dynamic boxes to push around
        for (int i = 0; i < 5; i++)
        {
            AddBox(bi, bodies, 0.25f, 0.25f, 0.25f,
                -3f + i * 1.5f, 0.25f, 15f, Quaternion.Identity,
                JPH.EMotionType.Dynamic, LayerMoving, orange);
        }

        // ── Kinematic moving obstacles (CharacterBaseTest.cpp ObstacleCourse) ──────

        // 1. Rotating platform at (-5, 0.15, 70) — spins Y by π·sin(t)
        {
            using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(1f, 0.15f, 3f));
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(ss);
            cs.mPosition.Set(-5f, 0.15f, 70f);
            cs.mMotionType  = JPH.EMotionType.Kinematic;
            cs.mObjectLayer = LayerMoving;
            cs.mFriction    = 0.5f;
            _kinematicBodyId = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = _kinematicBodyId, color = blue,
                shape = RenderShape.Box, scale = new Vector3(2f, 0.3f, 6f) });
        }

        // 2. Smooth elevator at (8, 2.0, 80) — oscillates Y, Z-rotated π/2
        {
            using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(1f, 0.15f, 3f));
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(ss);
            cs.mPosition.Set(8f, 2.0f, 80f);
            var zRotQ = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI * 0.5f);
            cs.mRotation.Set(zRotQ.X, zRotQ.Y, zRotQ.Z, zRotQ.W);
            cs.mMotionType  = JPH.EMotionType.Kinematic;
            cs.mObjectLayer = LayerMoving;
            _smoothVertBodyId = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = _smoothVertBodyId, color = green,
                shape = RenderShape.Box, scale = new Vector3(2f, 0.3f, 6f) });
        }

        // 3. Horizontally sweeping slab at (-5, 1, 80) — sweeps X ±3, Z-rotated π/2
        {
            using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(1f, 0.15f, 3f));
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(ss);
            cs.mPosition.Set(-5f, 1f, 80f);
            var zRotQ2 = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI * 0.5f);
            cs.mRotation.Set(zRotQ2.X, zRotQ2.Y, zRotQ2.Z, zRotQ2.W);
            cs.mMotionType  = JPH.EMotionType.Kinematic;
            cs.mObjectLayer = LayerMoving;
            _horzMovingBodyId = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = _horzMovingBodyId, color = orange,
                shape = RenderShape.Box, scale = new Vector3(2f, 0.3f, 6f) });
        }

        // 4. Rotating wall at (0, 1.0, 90)
        {
            using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(3f, 1f, 0.15f));
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(ss);
            cs.mPosition.Set(0f, 1.0f, 90f);
            cs.mMotionType  = JPH.EMotionType.Kinematic;
            cs.mObjectLayer = LayerMoving;
            _rotatingWallBodyId = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = _rotatingWallBodyId, color = blue,
                shape = RenderShape.Box, scale = new Vector3(6f, 2f, 0.3f) });
        }

        // 5. Reversing elevator at (-10, 0.15, 90) — bounces Y between 0.15 and 5.15
        {
            using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(1f, 0.15f, 3f));
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(ss);
            cs.mPosition.Set(-10f, 0.15f, 90f);
            cs.mMotionType  = JPH.EMotionType.Kinematic;
            cs.mObjectLayer = LayerMoving;
            _reversingVertBodyId = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = _reversingVertBodyId, color = red,
                shape = RenderShape.Box, scale = new Vector3(2f, 0.3f, 6f) });
        }

        // 6. Rotating + orbiting platform at (0, 0.15, 100)
        {
            using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(1f, 0.15f, 3f));
            using var cs = new JPH.BodyCreationSettings();
            cs.SetShapeSettings(ss);
            cs.mPosition.Set(0f, 0.15f, 100f);
            cs.mMotionType  = JPH.EMotionType.Kinematic;
            cs.mObjectLayer = LayerMoving;
            _rotatingAndTranslatingBodyId = bi.CreateAndAddBody(cs, JPH.EActivation.Activate);
            bodies.Add(new PhysicsBody { bodyId = _rotatingAndTranslatingBodyId, color = orange,
                shape = RenderShape.Box, scale = new Vector3(2f, 0.3f, 6f) });
        }

        // 7. Ramp at (15, 2.2, 70) with 4 dynamic sliding blocks (reset every 5s)
        {
            var rampRot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI * 0.25f);
            AddBox(bi, bodies, 4f, 0.1f, 3f,
                15f, 2.2f, 70f, rampRot,
                JPH.EMotionType.Static, LayerNonMoving, gray);
            for (int i = 0; i < 4; i++)
            {
                _rampBlocks[i] = AddBox(bi, bodies, 0.5f, 0.5f, 0.5f,
                    12f + i * 2f, 5.2f, 71.5f, rampRot,
                    JPH.EMotionType.Dynamic, LayerMoving, orange);
            }
            _rampBlocksTimeLeft = 5f;
        }

        // A wall of pillars
        for (int i = 0; i < 5; i++)
        {
            AddBox(bi, bodies, 0.2f, 1.5f, 0.2f,
                -8f + i * 2f, 1.5f, 25f, Quaternion.Identity,
                JPH.EMotionType.Static, LayerNonMoving, gray);
        }

        // A tall platform to jump onto
        AddBox(bi, bodies, 2f, 0.5f, 2f,
            0f, 0.5f, 8f, Quaternion.Identity,
            JPH.EMotionType.Static, LayerNonMoving, green);

        // Sloped capsule / cylinder obstacles
        AddCylinder(bi, bodies, 0.5f, 0.3f,
            3f, 0.5f, 5f, Quaternion.Identity,
            JPH.EMotionType.Static, LayerNonMoving, red);

        // ── Extra obstacles & debris ─────────────────────────────────────────

        var yellow = new Vector3(1.0f, 0.9f, 0.1f);
        var purple = new Vector3(0.7f, 0.2f, 0.9f);
        var teal   = new Vector3(0.1f, 0.8f, 0.7f);
        var white  = new Vector3(0.9f, 0.9f, 0.9f);

        // Dynamic sphere pile — loose debris to kick through
        float[] sOffX = { -1.0f, 0.0f, 1.0f, -0.5f, 0.5f, 0.0f, -1.2f, 1.2f };
        float[] sOffZ = {  30f,  30f,  30f,  31f,   31f,  32f,  30.5f, 31.5f };
        float[] sOffY = {  0.2f, 0.2f, 0.2f, 0.6f,  0.6f, 1.0f, 0.2f,  0.2f };
        for (int i = 0; i < sOffX.Length; i++)
            AddSphere(bi, bodies, 0.2f,
                sOffX[i], sOffY[i], sOffZ[i],
                JPH.EMotionType.Dynamic, LayerMoving, orange);

        // Stacked crate tower — topple it!
        AddBox(bi, bodies, 0.3f, 0.3f, 0.3f,  8f, 0.3f, 20f, Quaternion.Identity, JPH.EMotionType.Dynamic, LayerMoving, yellow);
        AddBox(bi, bodies, 0.3f, 0.3f, 0.3f,  8f, 0.9f, 20f, Quaternion.Identity, JPH.EMotionType.Dynamic, LayerMoving, yellow);
        AddBox(bi, bodies, 0.3f, 0.3f, 0.3f,  8f, 1.5f, 20f, Quaternion.Identity, JPH.EMotionType.Dynamic, LayerMoving, yellow);
        AddBox(bi, bodies, 0.3f, 0.3f, 0.3f,  8f, 2.1f, 20f, Quaternion.Identity, JPH.EMotionType.Dynamic, LayerMoving, yellow);
        AddBox(bi, bodies, 0.3f, 0.3f, 0.3f,  8f, 2.7f, 20f, Quaternion.Identity, JPH.EMotionType.Dynamic, LayerMoving, yellow);

        // Zigzag slalom walls — alternate left/right half-walls
        for (int i = 0; i < 5; i++)
        {
            float side = (i % 2 == 0) ? -2.5f : 2.5f;
            AddBox(bi, bodies, 2.5f, 1.0f, 0.15f,
                side, 1.0f, 35f + i * 4f, Quaternion.Identity,
                JPH.EMotionType.Static, LayerNonMoving, teal);
        }

        // Narrow corridor — two parallel walls forcing single-file passage
        AddBox(bi, bodies, 0.15f, 1.5f, 6f,  -0.6f, 1.5f, 55f, Quaternion.Identity, JPH.EMotionType.Static, LayerNonMoving, gray);
        AddBox(bi, bodies, 0.15f, 1.5f, 6f,   0.6f, 1.5f, 55f, Quaternion.Identity, JPH.EMotionType.Static, LayerNonMoving, gray);

        // Low ceiling section — forces crouching
        AddBox(bi, bodies, 3f, 0.15f, 5f, -5f, 0.9f, 42f, Quaternion.Identity, JPH.EMotionType.Static, LayerNonMoving, purple);
        // Side walls for the tunnel
        AddBox(bi, bodies, 0.15f, 0.9f, 5f, -8f,  0.9f, 42f, Quaternion.Identity, JPH.EMotionType.Static, LayerNonMoving, purple);
        AddBox(bi, bodies, 0.15f, 0.9f, 5f, -2f,  0.9f, 42f, Quaternion.Identity, JPH.EMotionType.Static, LayerNonMoving, purple);

        // Stepping stones across a gap — varied heights
        float[] stoneX = { -12f, -14f, -16f, -18f, -20f };
        float[] stoneY = {  0.3f,  0.5f,  0.8f,  0.5f,  0.3f };
        float[] stoneZ = {  10f,  12f,  14f,  16f,  18f };
        for (int i = 0; i < stoneX.Length; i++)
            AddBox(bi, bodies, 0.6f, stoneY[i], 0.6f,
                stoneX[i], stoneY[i], stoneZ[i], Quaternion.Identity,
                JPH.EMotionType.Static, LayerNonMoving, white);

        // Hanging capsule pendulums above the main path (static obstacles to duck under)
        for (int i = 0; i < 4; i++)
        {
            float ang = i * 0.5f;  // vary tilt
            var tiltRot = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, ang);
            AddCapsule(bi, bodies, 0.05f, 0.6f,
                -5f + i * 3f, 2.2f, 25f, tiltRot,
                JPH.EMotionType.Static, LayerNonMoving, red);
        }

        // Dynamic barrels (cylinders) rolling area
        for (int i = 0; i < 4; i++)
        {
            var tilt = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI * 0.5f);
            AddCylinder(bi, bodies, 0.35f, 0.25f,
                -3f + i * 2f, 0.35f, 45f, tilt,
                JPH.EMotionType.Dynamic, LayerMoving, orange);
        }
    }

    // ── Update ───────────────────────────────────────────────────────────────
    public override void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies)
    {
        if (_character == null || _sys == null) return;

        // PostSimulation: refresh ground contact info after the previous physics step
        _character.PostSimulation(cCollisionTolerance);

        // --- Input ---
        bool moveForward   = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_W);
        bool moveBack      = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_S);
        bool leftDown      = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_A);
        bool rightDown     = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_D);
        bool jumpPressed   = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_SPACE);
        bool crouchPressed = IsKeyDown(SApp.sapp_keycode.SAPP_KEYCODE_LEFT_SHIFT);

        // A/D: continuously rotate facing direction (120°/s).
        // Invert when moving backward so joystick left/right always feels correct on screen.
        const float TurnSpeed = 120f; // degrees per second
        float turnSign = (moveBack && !moveForward) ? -1f : 1f;
        if (leftDown)  _facingYaw += TurnSpeed * dt * turnSign;
        if (rightDown) _facingYaw -= TurnSpeed * dt * turnSign;

        // W/S: move along the character's current facing direction
        float facingRad = (_facingYaw + 180f) * MathF.PI / 180f;
        var charForward = new Vector3(-MathF.Sin(facingRad), 0, -MathF.Cos(facingRad));
        Vector3 desiredVel = Vector3.Zero;
        if (moveForward) desiredVel += charForward;
        if (moveBack)    desiredVel -= charForward;
        if (desiredVel != Vector3.Zero)
            desiredVel = Vector3.Normalize(desiredVel) * cCharacterSpeed;

        // Read current character state
        using var curVelJph = _character.GetLinearVelocity();
        var curVel = new Vector3(curVelJph.GetX(), curVelJph.GetY(), curVelJph.GetZ());

        var groundState = _character.GetGroundState();
        bool onGround   = groundState == JPH.CharacterBase.EGroundState.OnGround;
        bool notSupported = groundState == JPH.CharacterBase.EGroundState.NotSupported;

        // If on steep slope, cancel horizontal movement toward the slope
        Vector3 newVel;
        if (onGround || notSupported)
        {
            using var gNormJph = _character.GetGroundNormal();
            if (_character.IsSlopeTooSteep(gNormJph))
                desiredVel = Vector3.Zero;
        }

        // Crouch toggle
        if (crouchPressed && !_isCrouching && _crouchingShape != null)
        {
            float slop = 1.5f * _sys.GetPhysicsSettings().mPenetrationSlop;
            if (_character.SetShape(_crouchingShape, slop))
                _isCrouching = true;
        }
        else if (!crouchPressed && _isCrouching && _standingShape != null)
        {
            float slop = 1.5f * _sys.GetPhysicsSettings().mPenetrationSlop;
            if (_character.SetShape(_standingShape, slop))
                _isCrouching = false;
        }

        // Blend horizontal velocity
        if (onGround)
        {
            using var gVelJph = _character.GetGroundVelocity();
            // Apply ground velocity + desired + jump
            newVel = 0.75f * new Vector3(curVel.X, 0, curVel.Z)
                   + 0.25f * new Vector3(desiredVel.X, 0, desiredVel.Z);
            newVel.Y = gVelJph.GetY(); // inherit ground Y velocity (conveyors, elevators)
            if (jumpPressed)
                newVel.Y += cJumpSpeed;
        }
        else
        {
            // In air: preserve Y velocity, blend XZ
            newVel = 0.75f * new Vector3(curVel.X, curVel.Y, curVel.Z)
                   + 0.25f * new Vector3(desiredVel.X, curVel.Y, desiredVel.Z);
            // Gravity is applied by the physics system
        }

        using var velJph = new JPH.Vec3(newVel.X, newVel.Y, newVel.Z);
        _character.SetLinearVelocity(velJph);

        // Sync capsule render scale and localOffset with current crouch state
        if (_bodies != null && _charBodyIdx >= 0 && _charBodyIdx < _bodies.Count)
        {
            float h = _isCrouching ? cCharacterHeightCrouching : cCharacterHeightStanding;
            float r = _isCrouching ? cCharacterRadiusCrouching : cCharacterRadiusStanding;
            var e = _bodies[_charBodyIdx];
            e.scale       = new Vector3(r, 0.5f * h, r);
            e.localOffset = new Vector3(0, 0.5f * h + r, 0);
            _bodies[_charBodyIdx] = e;
        }

        // ── Animate kinematic bodies (faithful to CharacterBaseTest.cpp) ────────────
        _time += dt;

        // Rotating platform: π·sin(t) around Y
        {
            using var yAxis = JPH.Vec3.SAxisY();
            using var rot = JPH.Quat.SRotation(yAxis, MathF.PI * MathF.Sin(_time));
            using var pos = new JPH.Vec3(-5f, 0.15f, 70f);
            bi.MoveKinematic(_kinematicBodyId, pos, rot, dt);
        }

        // Smooth elevator: Y = 2.0 ± 1.75·sin(t)
        {
            using var zAxis = JPH.Vec3.SAxisZ();
            using var rot = JPH.Quat.SRotation(zAxis, MathF.PI * 0.5f);
            using var pos = new JPH.Vec3(8f, 2.0f + 1.75f * MathF.Sin(_time), 80f);
            bi.MoveKinematic(_smoothVertBodyId, pos, rot, dt);
        }

        // Horizontally sweeping slab: ±3 on X
        {
            using var zAxis = JPH.Vec3.SAxisZ();
            using var rot = JPH.Quat.SRotation(zAxis, MathF.PI * 0.5f);
            using var pos = new JPH.Vec3(-5f + 3f * MathF.Sin(_time), 1f, 80f);
            bi.MoveKinematic(_horzMovingBodyId, pos, rot, dt);
        }

        // Rotating wall: same Y spin
        {
            using var yAxis = JPH.Vec3.SAxisY();
            using var rot = JPH.Quat.SRotation(yAxis, MathF.PI * MathF.Sin(_time));
            using var pos = new JPH.Vec3(0f, 1.0f, 90f);
            bi.MoveKinematic(_rotatingWallBodyId, pos, rot, dt);
        }

        // Reversing elevator: bounces between y=0.15 and y=5.15
        {
            _reversingVertY += _reversingVelocity * 3f * dt;
            if (_reversingVertY < 0f) { _reversingVertY = 0f; _reversingVelocity =  1f; }
            if (_reversingVertY > 5f) { _reversingVertY = 5f; _reversingVelocity = -1f; }
            using var idRot = JPH.Quat.SIdentity();
            using var pos = new JPH.Vec3(-10f, 0.15f + _reversingVertY, 90f);
            bi.MoveKinematic(_reversingVertBodyId, pos, idRot, dt);
        }

        // Rotating + orbiting: circular XZ orbit + Y spin
        {
            float tx = 0f + 5f * MathF.Sin(MathF.PI * _time);
            float tz = 100f + 5f * MathF.Cos(MathF.PI * _time);
            using var yAxis = JPH.Vec3.SAxisY();
            using var rot = JPH.Quat.SRotation(yAxis, MathF.PI * MathF.Sin(_time));
            using var pos = new JPH.Vec3(tx, 0.15f, tz);
            bi.MoveKinematic(_rotatingAndTranslatingBodyId, pos, rot, dt);
        }

        // Ramp blocks: reset to ramp every 5 seconds
        _rampBlocksTimeLeft -= dt;
        if (_rampBlocksTimeLeft < 0f)
        {
            using var xAxis = JPH.Vec3.SAxisX();
            using var rampRot = JPH.Quat.SRotation(xAxis, -MathF.PI * 0.25f);
            using var zero = new JPH.Vec3(0f, 0f, 0f);
            for (int i = 0; i < 4; i++)
            {
                using var bPos = new JPH.Vec3(12f + i * 2f, 5.2f, 71.5f);
                bi.SetPositionAndRotation(_rampBlocks[i], bPos, rampRot, JPH.EActivation.Activate);
                bi.SetLinearAndAngularVelocity(_rampBlocks[i], zero, zero);
            }
            _rampBlocksTimeLeft = 5f;
        }
    }

    // ── Cleanup ──────────────────────────────────────────────────────────────
    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        // Remove the capsule render entry before RemoveFromPhysicsSystem so the
        // app's cleanup loop does not also try to destroy this body.
        if (_bodies != null && _charBodyIdx >= 0 && _charBodyIdx < _bodies.Count)
            _bodies.RemoveAt(_charBodyIdx);
        _bodies = null;
        _charBodyIdx = -1;

        if (_character != null)
        {
            _character.RemoveFromPhysicsSystem();
            _character.Dispose();
            _character = null;
        }
        _standingShape?.Dispose();
        _standingShape = null;
        _crouchingShape?.Dispose();
        _crouchingShape = null;
        _isCrouching = false;
        _time = 0;
        _rampBlocksTimeLeft = 0;
        _reversingVertY = 0;
        _reversingVelocity = 1.0f;
        _facingYaw = float.NaN;
        _sys = null;
    }
}
