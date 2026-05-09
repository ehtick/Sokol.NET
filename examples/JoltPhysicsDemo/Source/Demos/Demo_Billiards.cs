using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Billiards (pool) break shot: 15 balls racked in the standard triangle
/// formation, struck by a fast-moving cue ball.
///
/// Rack layout (viewed from above, cue ball comes from +Z):
///      O              ← row 1 apex
///     O O
///    O O O
///   O O O O
///  O O O O O          ← row 5 base
///
/// All balls share the same radius and have high restitution + low friction
/// to emphasise the elastic collision chains spreading through the rack.
/// Balls are colour-coded by row.
/// </summary>
public sealed class Demo_Billiards : DemoBase
{
    public override string Name     => "Billiards";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 90,
        Latitude  = 45,
        Longitude = 0,
        Center    = new Vector3(0, 0, 0),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000.0f
    };

    // Row colours: apex → base
    static readonly Vector3[] RowColors = new[]
    {
        new Vector3(1.0f, 0.9f, 0.1f),  // yellow  – apex
        new Vector3(0.2f, 0.5f, 1.0f),  // blue
        new Vector3(0.9f, 0.2f, 0.2f),  // red
        new Vector3(0.2f, 0.8f, 0.3f),  // green
        new Vector3(0.9f, 0.5f, 0.1f),  // orange – base
    };

    public override void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies, hx: 100f, hy: 1f, hz: 100f);

        const float R          = 1.0f;
        const float diameter   = R * 2f;
        float       rowStep    = (float)Math.Sqrt(3.0) * diameter;  // row-to-row Z distance

        // Build 5-row triangle rack; apex at z=0, each deeper row at -rowStep*i
        for (int row = 0; row < 5; row++)
        {
            float rowZ = -row * rowStep;
            Vector3 color = RowColors[row];
            for (int col = 0; col <= row; col++)
            {
                float rowX = -(row * diameter * 0.5f) + col * diameter;
                AddSphere(bi, bodies, R, rowX, R, rowZ,
                    JPH.EMotionType.Dynamic, LayerMoving, color,
                    friction: 0.15f, restitution: 0.9f);
            }
        }

        // Cue ball: white, starts well behind the rack, fires toward -Z
        var cueId = AddSphere(bi, bodies, R, 0f, R, 28f,
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.95f, 0.95f, 0.95f),
            friction: 0.15f, restitution: 0.9f);

        using var vel = new JPH.Vec3(0f, 0f, -80f);
        bi.SetLinearVelocity(cueId, vel);
    }
}
