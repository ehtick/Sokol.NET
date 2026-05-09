using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of SoftBodyGravityFactorTest — 11 spheres and 11 cubes with varying gravity factors.
/// </summary>
public class Demo_SoftBody_GravityFactor : DemoBase
{
    public override string Name     => "SoftBody: Gravity Factor";
    public override string Category  => "Soft Body";

    public override void Init(JPH.BodyInterface bi, JPH.PhysicsSystem physicsSystem,
                               List<PhysicsBody> bodies, Random rng)
    {

        AddFloor(bi, bodies);

        for (int i = 0; i <= 10; i++)
        {
            float gravityFactor = i * 0.1f;
            float x = -50f + i * 10f;

            string label = $"GravityFactor: {gravityFactor:F1}";

            // Pressurised sphere
            var sphereFaces = new List<(uint, uint, uint)>();
            var sphereSettings = CreateSphereSettings(1f, 20, 10, sphereFaces);
            var sId = RegisterSoftBody(bi, sphereSettings, sphereFaces,
                x, 10f, 0f,
                0f, 0f, 0f, 1f,
                ColorForIndex(i),
                cs => { cs.mPressure = 2000f; cs.mGravityFactor = gravityFactor; });
            SetBodyLabel(sId, label);

            // Soft cube
            var cId = RegisterCubeSoftBody(bi, 5, 0.5f,
                x, 10f, -5f,
                0f, 0f, 0f, 1f,
                ColorForIndex(i),
                cs => { cs.mGravityFactor = gravityFactor; });
            SetBodyLabel(cId, label);
        }
    }

    private static Vector3 ColorForIndex(int i)
    {
        float t = i / 10f;
        return new Vector3(t, 0.5f, 1f - t);
    }
}
