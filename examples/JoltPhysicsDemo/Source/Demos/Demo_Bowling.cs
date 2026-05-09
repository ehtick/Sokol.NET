using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Bowling simulation: ten narrow box pins arranged in the standard
/// triangular formation, hit by a single rolling sphere.
///
/// Pin layout (viewed from behind the ball):
///       O                 ← row 1 (1 pin, closest to camera)
///      O O                ← row 2
///     O O O               ← row 3
///    O O O O              ← row 4
///
/// The ball starts at z=+30 and rolls toward -Z, striking the pin rack at z≈0.
/// Pins are cream-coloured, ball is red.
/// </summary>
public sealed class Demo_Bowling : DemoBase
{
    public override string Name     => "Bowling";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 80,
        Latitude  = 20,
        Longitude = 0,
        Center    = new Vector3(0, 3, 5),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000.0f
    };

    public override void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies, hx: 80f, hy: 1f, hz: 120f);

        // Pin dimensions: narrow tower
        const float phx = 0.4f;  // half-width X
        const float phy = 2.5f;  // half-height Y
        const float phz = 0.4f;  // half-depth Z
        const float pinY = phy;  // bottom at y=0, center at y=phy
        const float rowSpacing  = 2.4f;   // Z gap between rows
        const float colSpacing  = 2.4f;   // X gap between pins in a row

        var pinColor  = new Vector3(0.96f, 0.96f, 0.86f);  // cream white

        // Standard 10-pin triangle: rows 1..4 (row 1 = front, fewest pins)
        for (int row = 1; row <= 4; row++)
        {
            float rowZ = -(row - 1) * rowSpacing;           // row 1 at z=0, deeper rows go negative Z
            for (int col = 0; col < row; col++)
            {
                float rowX = -(row - 1) * colSpacing * 0.5f + col * colSpacing;
                AddBox(bi, bodies, phx, phy, phz, rowX, pinY, rowZ,
                    Quaternion.Identity,
                    JPH.EMotionType.Dynamic, LayerMoving, pinColor,
                    friction: 0.4f, restitution: 0.3f);
            }
        }

        // Bowling ball: sphere rolling toward pins at -Z
        const float ballR = 1.5f;
        var ballColor = new Vector3(0.9f, 0.2f, 0.2f);

        var ballId = AddSphere(bi, bodies, ballR, 0f, ballR, 28f,
            JPH.EMotionType.Dynamic, LayerMoving, ballColor,
            friction: 0.6f, restitution: 0.2f);

        using var vel = new JPH.Vec3(0f, 0f, -45f);
        bi.SetLinearVelocity(ballId, vel);
    }
}
