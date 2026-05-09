using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of IslandTest.cpp: eight parallel walls creating isolated physics islands.
/// Demonstrates island-based sleeping; each wall can fall independently.
/// </summary>
public sealed class Demo_Island : DemoBase
{
    public override string Name     => "Islands";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 80,
        Latitude  = 30,
        Longitude = 20,
        Center    = new Vector3(0, 10, 0),
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
        AddFloor(bi, bodies, hx: 20f, hy: 1f, hz: 50f, cy: -1f);

        // 8 parallel walls spaced along Z
        for (int k = 0; k < 8; k++)
        {
            float z = 8f * (k - 4);
            for (int i = 0; i < 10; i++)
            {
                int jStart = i / 2;
                int jEnd   = 10 - (i + 1) / 2;
                for (int j = jStart; j < jEnd; j++)
                {
                    float x = -10f + j * 2f + ((i & 1) != 0 ? 1f : 0f);
                    float y = 1f + i * 2f;
                    AddBox(bi, bodies, 1f, 1f, 1f, x, y, z,
                        Quaternion.Identity,
                        JPH.EMotionType.Dynamic, LayerMoving,
                        IslandColor(k));
                }
            }
        }
    }

    static Vector3 IslandColor(int island)
    {
        // 8 distinct hues
        float hue = island / 8f;
        return HsvToRgb(hue, 0.7f, 0.9f);
    }

    static Vector3 HsvToRgb(float h, float s, float v)
    {
        float r, g, b;
        int   i = (int)(h * 6f);
        float f = h * 6f - i;
        float p = v * (1f - s);
        float q = v * (1f - f * s);
        float t = v * (1f - (1f - f) * s);
        switch (i % 6)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }
        return new Vector3(r, g, b);
    }
}
