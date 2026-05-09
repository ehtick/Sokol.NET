using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;

/// <summary>
/// Port of PyramidTest.cpp: a 15-layer square pyramid of 2 m boxes (~1 240 bodies).
/// </summary>
public sealed class Demo_Pyramid : DemoBase
{
    public override string Name     => "Pyramid";
    public override string Category => "General";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 70,
        Latitude  = 25,
        Longitude = 45,
        Center    = new Vector3(0, 15, 0),
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
        AddFloor(bi, bodies);

        const float boxSize       = 2.0f;
        const float boxSeparation = 0.5f;
        const float halfBox       = boxSize * 0.5f;  // = 1.0
        const int   pyramidHeight = 15;

        for (int i = 0; i < pyramidHeight; i++)
        {
            int jStart = i / 2;
            int jEnd   = pyramidHeight - (i + 1) / 2;
            for (int j = jStart; j < jEnd; j++)
            {
                for (int k = jStart; k < jEnd; k++)
                {
                    float x = -pyramidHeight + boxSize * j + ((i & 1) != 0 ? halfBox : 0f);
                    float y = 1.0f + (boxSize + boxSeparation) * i;
                    float z = -pyramidHeight + boxSize * k + ((i & 1) != 0 ? halfBox : 0f);
                    AddBox(bi, bodies, halfBox, halfBox, halfBox, x, y, z,
                        Quaternion.Identity,
                        JPH.EMotionType.Dynamic, LayerMoving,
                        LayerColor(i, pyramidHeight));
                }
            }
        }
    }

    static Vector3 LayerColor(int layer, int totalLayers)
    {
        float t = (float)layer / (totalLayers - 1);
        // Gradient: base = warm orange, apex = cool blue
        return Vector3.Lerp(new Vector3(0.9f, 0.5f, 0.1f), new Vector3(0.2f, 0.5f, 0.95f), t);
    }
}
