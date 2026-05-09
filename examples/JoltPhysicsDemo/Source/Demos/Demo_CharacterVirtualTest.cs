using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of Samples/Tests/Character/CharacterVirtualTest.cpp.
///
/// A virtual character controller using JPH::CharacterVirtual. Compared to the
/// kinematic Character, CharacterVirtual is not a physics body and uses ExtendedUpdate
/// each frame to resolve collisions and handle step-up / stick-to-floor.
///
/// Controls:
///   W / S / A / D   — move forward / back / left / right
///   Space           — jump
///   Left Shift      — toggle crouch
/// </summary>
public sealed class Demo_CharacterVirtualTest : DemoBase
{
    public override string Name     => "Character Virtual";
    public override string Category => "Character";

    // ── Constants (match CharacterVirtualTest.cpp / CharacterBaseTest.cpp) ──
    const float cCharacterHeightStanding  = 1.35f;
    const float cCharacterRadiusStanding  = 0.3f;
    const float cCharacterHeightCrouching = 0.8f;
    const float cCharacterRadiusCrouching = 0.3f;
    const float cCharacterSpeed           = 6.0f;
    const float cJumpSpeed                = 4.0f;
    const float cInnerShapeFraction       = 0.9f;

    // ── State ────────────────────────────────────────────────────────────────
    JPH.CharacterVirtual?                    _character;
    JPH.CharacterVsCharacterCollisionSimple? _cvsc;
    JPH.Const_Shape?  _standingShape;
    JPH.Const_Shape?  _crouchingShape;
    JPH.Const_Shape?  _innerStandingShape;
    JPH.Const_Shape?  _innerCrouchingShape;
    bool              _isCrouching;
    Vector3           _desiredVelocity;
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
    JPH.PhysicsSystem?       _sys;
    JPH.TempAllocatorImpl?   _alloc;
    List<PhysicsBody>?       _bodies;
    int                      _charBodyIdx = -1;
    float                    _facingYaw   = float.NaN;
    bool                     _prevLeftDown;
    bool                     _prevRightDown;

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
        Center    = new Vector3(-5.0f, 1, 3),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 500.0f
    };

    // ── Shape factory helpers ─────────────────────────────────────────────────
    static JPH.Const_Shape MakeCapsuleShape(float height, float radius, float fraction = 1.0f)
    {
        float r  = radius * fraction;
        float hh = 0.5f * height * fraction;
        using var capsule = new JPH.CapsuleShape(hh, r);
        using var offset  = new JPH.Vec3(0, 0.5f * height + radius, 0);
        using var rot     = JPH.Quat.SIdentity();
        return (JPH.Const_RotatedTranslatedShape)new JPH.RotatedTranslatedShape(offset, rot, capsule);
    }

    // ── Init ─────────────────────────────────────────────────────────────────
    public override void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random random)
    {
        _sys = sys;
        _facingYaw = -180f; // start facing -Z

        // Outer shapes
        _standingShape  = MakeCapsuleShape(cCharacterHeightStanding,  cCharacterRadiusStanding);
        _crouchingShape = MakeCapsuleShape(cCharacterHeightCrouching, cCharacterRadiusCrouching);

        // Inner shapes (scaled down for inner body)
        _innerStandingShape  = MakeCapsuleShape(cCharacterHeightStanding,  cCharacterRadiusStanding,  cInnerShapeFraction);
        _innerCrouchingShape = MakeCapsuleShape(cCharacterHeightCrouching, cCharacterRadiusCrouching, cInnerShapeFraction);

        // CharacterVirtualSettings
        using var settings = new JPH.CharacterVirtualSettings();
        settings.mMaxSlopeAngle = 45f * MathF.PI / 180f;
        settings.mMaxStrength   = 100f;
        settings.mMass          = 70f;
        settings.mInnerBodyLayer = LayerMoving;
        settings.SetShape(_standingShape);
        settings.SetInnerBodyShape(_innerStandingShape);
        var supVol = settings.mSupportingVolume;
        using var axisY = new JPH.Vec3(0, 1, 0);
        supVol.SetNormal(axisY);
        supVol.SetConstant(-cCharacterRadiusStanding);

        using var startPos = new JPH.Vec3(-5.0f, 0.5f, 3.0f);
        using var startRot = JPH.Quat.SIdentity();
        _character = new JPH.CharacterVirtual(settings, startPos, startRot, 0, sys);

        // Add character capsule to the render list via the inner body.
        // MakeCapsuleShape uses offset = (0, 0.5*height + radius, 0) for the RTS,
        // regardless of fraction, so the inner body COM == outer capsule center.
        _bodies = bodies;
        _charBodyIdx = bodies.Count;
        bodies.Add(new PhysicsBody
        {
            bodyId      = _character.GetInnerBodyID(),
            shape       = RenderShape.Capsule,
            scale       = new Vector3(cCharacterRadiusStanding, 0.5f * cCharacterHeightStanding, cCharacterRadiusStanding),
            localOffset = new Vector3(0, 0.5f * cCharacterHeightStanding + cCharacterRadiusStanding, 0),
            color       = new Vector3(0.2f, 0.6f, 1.0f)
        });

        _cvsc = new JPH.CharacterVsCharacterCollisionSimple();
        _cvsc.Add(_character);
        _character.SetCharacterVsCharacterCollision(_cvsc);

        // Own allocator for ExtendedUpdate / SetShape calls
        _alloc = new JPH.TempAllocatorImpl(4 * 1024 * 1024);

        // ── Obstacle course (same as CharacterTest) ──────────────────────────

        // Large floor
        AddFloor(bi, bodies, hx: 175f, hy: 1f, hz: 175f);

        var gray   = new Vector3(0.6f, 0.6f, 0.6f);
        var orange = new Vector3(1.0f, 0.5f, 0.1f);
        var blue   = new Vector3(0.2f, 0.4f, 0.9f);
        var green  = new Vector3(0.2f, 0.8f, 0.3f);
        var red    = new Vector3(0.9f, 0.2f, 0.2f);

        // Ramps
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

        // Stairs
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

        // Dynamic boxes to push
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

        // Pillars
        for (int i = 0; i < 5; i++)
        {
            AddBox(bi, bodies, 0.2f, 1.5f, 0.2f,
                -8f + i * 2f, 1.5f, 25f, Quaternion.Identity,
                JPH.EMotionType.Static, LayerNonMoving, gray);
        }

        // Platform
        AddBox(bi, bodies, 2f, 0.5f, 2f,
            0f, 0.5f, 8f, Quaternion.Identity,
            JPH.EMotionType.Static, LayerNonMoving, green);

        // Obstacle cylinder
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
        if (_character == null || _sys == null || _alloc == null) return;

        var alloc = _alloc;

        // Refresh ground velocity from previous step
        _character.UpdateGroundVelocity();

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
        Vector3 moveDir = Vector3.Zero;
        if (moveForward) moveDir += charForward;
        if (moveBack)    moveDir -= charForward;
        if (moveDir != Vector3.Zero)
            moveDir = Vector3.Normalize(moveDir);

        // Smooth desired velocity
        _desiredVelocity = 0.75f * _desiredVelocity + 0.25f * moveDir * cCharacterSpeed;

        // Read current state
        using var curVelJph = _character.GetLinearVelocity();
        var curVel = new Vector3(curVelJph.GetX(), curVelJph.GetY(), curVelJph.GetZ());

        using var gravJph = _sys.GetGravity();
        var gravity = new Vector3(gravJph.GetX(), gravJph.GetY(), gravJph.GetZ());

        var up = Vector3.UnitY;
        float vertCurVel = Vector3.Dot(curVel, up);

        var groundState = _character.GetGroundState();
        bool onGround   = groundState == JPH.CharacterBase.EGroundState.OnGround;

        Vector3 newVel;
        if (onGround && vertCurVel <= 0)
        {
            // On ground: follow ground velocity
            using var gVelJph = _character.GetGroundVelocity();
            newVel = new Vector3(gVelJph.GetX(), gVelJph.GetY(), gVelJph.GetZ());
            if (jumpPressed)
                newVel.Y += cJumpSpeed;
        }
        else
        {
            // In air: preserve vertical, apply gravity
            newVel = vertCurVel * up;
            newVel += gravity * dt;
        }

        // Add desired horizontal velocity (only suppress on genuinely too-steep ground)
        using var gNormJph = _character.GetGroundNormal();
        bool tooSteep = onGround && _character.IsSlopeTooSteep(gNormJph);
        if (!tooSteep)
            newVel += _desiredVelocity;

        using var newVelJph = new JPH.Vec3(newVel.X, newVel.Y, newVel.Z);
        _character.SetLinearVelocity(newVelJph);

        // Crouch toggle
        if (crouchPressed && !_isCrouching && _crouchingShape != null)
        {
            using JPH.Const_DefaultBroadPhaseLayerFilter bpF = _sys.GetDefaultBroadPhaseLayerFilter(LayerMoving);
            using JPH.Const_DefaultObjectLayerFilter     lF  = _sys.GetDefaultLayerFilter(LayerMoving);
            using var bF  = new JPH.BodyFilter();
            using var sF  = new JPH.ShapeFilter();
            float slop = 1.5f * _sys.GetPhysicsSettings().mPenetrationSlop;
            if (_character.SetShape(_crouchingShape, slop, bpF, lF, bF, sF, alloc))
            {
                _character.SetInnerBodyShape(_innerCrouchingShape);
                _isCrouching = true;
            }
        }
        else if (!crouchPressed && _isCrouching && _standingShape != null)
        {
            using JPH.Const_DefaultBroadPhaseLayerFilter bpF = _sys.GetDefaultBroadPhaseLayerFilter(LayerMoving);
            using JPH.Const_DefaultObjectLayerFilter     lF  = _sys.GetDefaultLayerFilter(LayerMoving);
            using var bF  = new JPH.BodyFilter();
            using var sF  = new JPH.ShapeFilter();
            float slop = 1.5f * _sys.GetPhysicsSettings().mPenetrationSlop;
            if (_character.SetShape(_standingShape, slop, bpF, lF, bF, sF, alloc))
            {
                _character.SetInnerBodyShape(_innerStandingShape);
                _isCrouching = false;
            }
        }

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

        // ExtendedUpdate: resolve collisions, stick-to-floor, walk-stairs
        using var extSettings = new JPH.CharacterVirtual.ExtendedUpdateSettings();
        using var gravForExt = new JPH.Vec3(gravity.X, gravity.Y, gravity.Z);
        using JPH.Const_DefaultBroadPhaseLayerFilter bpFilter    = _sys.GetDefaultBroadPhaseLayerFilter(LayerMoving);
        using JPH.Const_DefaultObjectLayerFilter     layerFilter  = _sys.GetDefaultLayerFilter(LayerMoving);
        using var bodyFilter  = new JPH.BodyFilter();
        using var shapeFilter = new JPH.ShapeFilter();
        _character.ExtendedUpdate(dt, gravForExt, extSettings, bpFilter, layerFilter, bodyFilter, shapeFilter, alloc);

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
        // Remove the inner-body render entry before _character.Dispose() destroys
        // the inner body, so the app's cleanup loop doesn't also try to destroy it.
        if (_bodies != null && _charBodyIdx >= 0 && _charBodyIdx < _bodies.Count)
            _bodies.RemoveAt(_charBodyIdx);
        _bodies = null;
        _charBodyIdx = -1;

        _cvsc?.Dispose();
        _cvsc = null;
        _character?.Dispose();
        _character = null;
        _standingShape?.Dispose();
        _standingShape = null;
        _crouchingShape?.Dispose();
        _crouchingShape = null;
        _innerStandingShape?.Dispose();
        _innerStandingShape = null;
        _innerCrouchingShape?.Dispose();
        _innerCrouchingShape = null;
        _isCrouching = false;
        _desiredVelocity = Vector3.Zero;
        _time = 0;
        _rampBlocksTimeLeft = 0;
        _reversingVertY = 0;
        _reversingVelocity = 1.0f;
        _facingYaw = float.NaN;
        _prevLeftDown  = false;
        _prevRightDown = false;
        _sys = null;
        _alloc = null;
    }
}
