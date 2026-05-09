using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Domino chain: a long row of thin standing boxes spaced so each one knocks
/// the next over.  The first domino is given a small initial velocity to start
/// the chain reaction.
/// </summary>
public sealed class Demo_Dominos : DemoBase
{
    public override string Name     => "Dominos";
    public override string Category => "General";

    // Body ID of the first domino so we can push it in Init
    JPH.BodyID _firstDomino;

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 80,
        Latitude  = 20,
        Longitude = 10,
        Center    = new Vector3(0, 3, 0),
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
        AddFloor(bi, bodies, hx: 60f, hy: 1f, hz: 10f, cy: -1f);

        const int   Count    = 30;
        const float Spacing  = 2.8f;   // centre-to-centre gap
        const float HalfW    = 0.15f;  // half-thickness (along Z, direction of fall)
        const float HalfH    = 2.0f;   // half-height
        const float HalfD    = 0.8f;   // half-depth

        float startX = -Count * Spacing * 0.5f;

        // Rainbow hue across the chain
        for (int i = 0; i < Count; i++)
        {
            float x    = startX + i * Spacing;
            float hue  = (float)i / Count;
            var   color = HsvToRgb(hue, 0.75f, 0.9f);

            var id = AddBox(bi, bodies,
                HalfW, HalfH, HalfD,
                x, HalfH, 0f,
                Quaternion.Identity,
                JPH.EMotionType.Dynamic, LayerMoving,
                color,
                friction: 0.6f,
                restitution: 0.1f);

            if (i == 0) _firstDomino = id;
        }

        // Give the first domino a gentle push along +X so it falls into its neighbour
        using var vel = new JPH.Vec3(3f, 0f, 0f);
        bi.SetLinearVelocity(_firstDomino, vel);
    }

    static Vector3 HsvToRgb(float h, float s, float v)
    {
        int   i = (int)(h * 6f);
        float f = h * 6f - i;
        float p = v * (1f - s);
        float q = v * (1f - f * s);
        float t = v * (1f - (1f - f) * s);
        return (i % 6) switch
        {
            0 => new Vector3(v, t, p),
            1 => new Vector3(q, v, p),
            2 => new Vector3(p, v, t),
            3 => new Vector3(p, q, v),
            4 => new Vector3(t, p, v),
            _ => new Vector3(v, p, q),
        };
    }
}
