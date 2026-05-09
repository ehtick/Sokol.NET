using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Staircase: a static staircase of boxes, with a wave of colourful spheres
/// released at the top.  The spheres bounce down each step, mixing and
/// scattering at the bottom.
/// </summary>
public sealed class Demo_Stairs : DemoBase
{
    public override string Name     => "Stairs";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 100,
        Latitude  = 20,
        Longitude = -20,
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
        // Landing pad at the bottom
        AddFloor(bi, bodies, hx: 40f, hy: 1f, hz: 30f, cx: 0f, cy: -1f, cz: -50f);

        // Staircase: 16 steps, each 3 m wide (X), 1 m tall, 2 m deep (Z)
        const int   Steps     = 16;
        const float StepW     = 30f;  // half-width
        const float StepH     = 0.5f; // half-height of one step slab
        const float StepDepth = 2.0f; // depth of each step (Z)

        for (int i = 0; i < Steps; i++)
        {
            float cy = i * (StepH * 2f) - StepH;           // top surface at i * 1 m
            float cz = -i * StepDepth;                     // steps march in -Z

            AddBox(bi, bodies,
                StepW, StepH, StepDepth,
                0f, cy, cz,
                Quaternion.Identity,
                JPH.EMotionType.Static, LayerNonMoving,
                new Vector3(0.75f, 0.6f, 0.45f));
        }

        // Side walls to keep spheres on the staircase
        float wallH  = Steps * StepH + 4f;
        float wallCY = wallH - StepH;
        float wallCZ = -(Steps - 1) * StepDepth * 0.5f;
        AddBox(bi, bodies,  1f, wallH, Steps * StepDepth * 0.5f, -StepW - 1f, wallCY, wallCZ,
            Quaternion.Identity, JPH.EMotionType.Static, LayerNonMoving, new Vector3(0.6f, 0.5f, 0.4f));
        AddBox(bi, bodies,  1f, wallH, Steps * StepDepth * 0.5f,  StepW + 1f, wallCY, wallCZ,
            Quaternion.Identity, JPH.EMotionType.Static, LayerNonMoving, new Vector3(0.6f, 0.5f, 0.4f));

        // Wave of spheres released from the top step
        const int   SphCols = 14;
        const int   SphRows = 5;
        const float R       = 0.9f;
        const float spacing = R * 2.2f;

        // Top step surface: step i=(Steps-1), y_surface = (Steps-1)*StepH*2
        float topY = (Steps - 1) * (StepH * 2f) + R + 0.3f;
        // Top step is at z = -(Steps-1)*StepDepth; spread rows toward the bottom
        float topZ = -(Steps - 1) * StepDepth;

        for (int row = 0; row < SphRows; row++)
        for (int col = 0; col < SphCols; col++)
        {
            // Center columns within the stair half-width
            float x = -(SphCols - 1) * spacing * 0.5f + col * spacing;
            float y = topY + row * spacing;
            float z = topZ + row * spacing;

            float hue   = (float)(col + row * SphCols) / (SphCols * SphRows);
            var   color = HsvToRgb(hue, 0.8f, 0.95f);

            AddSphere(bi, bodies, R,
                x, y, z,
                JPH.EMotionType.Dynamic, LayerMoving,
                color,
                friction: 0.4f,
                restitution: 0.4f,
                linearDamping: 0.02f);
        }
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
