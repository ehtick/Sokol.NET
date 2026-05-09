using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of SoftBodyPressureTest — 11 spheres with varying internal pressure.
/// </summary>
public class Demo_SoftBody_Pressure : DemoBase
{
    public override string Name     => "SoftBody: Pressure";
    public override string Category  => "Soft Body";

    public override void Init(JPH.BodyInterface bi, JPH.PhysicsSystem physicsSystem,
                               List<PhysicsBody> bodies, Random rng)
    {

        AddFloor(bi, bodies);

        for (int i = 0; i <= 10; i++)
        {
            float pressure = 1000f * i;
            float x = -50f + i * 10f;

            var sphereFaces = new List<(uint, uint, uint)>();
            var sphereSettings = CreateSphereSettings(2f, 20, 10, sphereFaces);
            var id = RegisterSoftBody(bi, sphereSettings, sphereFaces,
                x, 10f, 0f,
                0f, 0f, 0f, 1f,
                ColorForIndex(i),
                cs => { cs.mPressure = pressure; });
            SetBodyLabel(id, $"Pressure: {pressure:G}");
        }
    }

    private static Vector3 ColorForIndex(int i)
    {
        float t = i / 10f;
        return new Vector3(0.2f + 0.6f * t, 0.4f, 0.8f - 0.6f * t);
    }
}
