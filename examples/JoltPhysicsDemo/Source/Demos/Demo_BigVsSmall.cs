using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Demonstrates scale disparity: a giant sphere falls into a dense grid of tiny
/// cubes on the floor, barely slowing down.  Then a second pass drops small spheres
/// onto a large static slab — they bounce off like pebbles.
/// Corresponds to BigVsSmallTest.cpp.
/// </summary>
public class Demo_BigVsSmall : DemoBase
{
    public override string Name     => "Big vs Small";
    public override string Category => "General";

    public override unsafe void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random)
    {
        AddFloor(bi, bodies, 80f, 0.5f, 40f);

        // ── Left side: giant sphere falling into a bed of tiny cubes ──────
        const float SmallHE     = 0.25f;
        const float SmallSpacing = SmallHE * 2.2f;

        var cA = new Vector3(0.25f, 0.60f, 0.85f);
        var cB = new Vector3(0.85f, 0.50f, 0.20f);

        for (int row = 0; row < 10; row++)
        {
            for (int col = 0; col < 10; col++)
            {
                float x = -22f + col * SmallSpacing;
                float z = -5f  + row * SmallSpacing;
                float y = SmallHE;
                AddBox(bi, bodies,
                    SmallHE, SmallHE, SmallHE,
                    x, y, z,
                    Quaternion.Identity,
                    JPH.EMotionType.Dynamic, LayerMoving,
                    (row + col) % 2 == 0 ? cA : cB,
                    friction: 0.4f, restitution: 0.3f);
            }
        }

        // Giant sphere dropped from above the small-cube grid
        AddSphere(bi, bodies,
            3.5f,
            -19f, 28f, -2f,
            JPH.EMotionType.Dynamic, LayerMoving,
            new Vector3(0.80f, 0.20f, 0.15f),
            friction: 0.3f, restitution: 0.1f,
            linearDamping: 0.02f);

        // ── Right side: many small spheres falling on a large static slab ─
        const float SlabX = 16f;

        AddBox(bi, bodies,
            5f, 1.5f, 5f,
            SlabX, 5f, 0f,
            Quaternion.Identity,
            JPH.EMotionType.Static, LayerNonMoving,
            new Vector3(0.55f, 0.50f, 0.45f));

        var sc1 = new Vector3(0.30f, 0.85f, 0.35f);
        var sc2 = new Vector3(0.85f, 0.75f, 0.15f);

        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                float x = SlabX - 4f + j * 1.5f;
                float z = -3.5f + i * 1.5f;
                float y = 18f + i * 1.5f;
                AddSphere(bi, bodies,
                    0.35f, x, y, z,
                    JPH.EMotionType.Dynamic, LayerMoving,
                    (i + j) % 2 == 0 ? sc1 : sc2,
                    friction: 0.3f, restitution: 0.6f,
                    linearDamping: 0.01f);
            }
        }
    }

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 80,
        Latitude  = 22,
        Longitude = 10,
        Center    = new Vector3(0f, 6f, 0f),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000f
    };
}
