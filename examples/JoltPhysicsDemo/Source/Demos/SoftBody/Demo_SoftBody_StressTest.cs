using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Stress test: 11x11 grid of pressurised spheres, each with a heavy box sitting on top.
/// Ported from SoftBodyStressTest.cpp.
/// </summary>
public class Demo_SoftBody_StressTest : DemoBase
{
    public override string Name     => "SoftBody: Stress Test";
    public override string Category => "Soft Body";

    public override void Init(JPH.BodyInterface bi, JPH.PhysicsSystem sys, List<PhysicsBody> bodies, Random rng)
    {
        CreatePlaygroundTerrain(bi, bodies);

        var sphereFaces = new List<(uint, uint, uint)>();
        var sphereSettings = CreateSphereSettings(1f, 20, 10, sphereFaces);

        for (int x = 0; x < 11; x++)
        for (int z = 0; z < 11; z++)
        {
            float px = -20f + 4f * x;
            float pz = -20f + 4f * z;

            var id = RegisterSoftBody(bi, sphereSettings, sphereFaces,
                px, 5f, pz,
                0f, 0f, 0f, 1f,
                new Vector3(0.3f, 0.6f, 0.9f),
                cs => cs.mPressure = 2000f);

            // Heavy box sitting on top of each sphere
            AddBox(bi, bodies,
                1f, 1f, 1f,
                px, 9f, pz,
                Quaternion.Identity,
                JPH.EMotionType.Dynamic, LayerMoving,
                new Vector3(0.8f, 0.4f, 0.2f));
        }
    }
}
