using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of SoftBodyRestitutionTest — 11 spheres and 11 cubes with varying restitution
/// dropped onto a zero-restitution floor.
/// </summary>
public class Demo_SoftBody_Restitution : DemoBase
{
    public override string Name     => "SoftBody: Restitution";
    public override string Category  => "Soft Body";

    public override void Init(JPH.BodyInterface bi, JPH.PhysicsSystem physicsSystem,
                               List<PhysicsBody> bodies, Random rng)
    {

        // Zero-restitution floor
        AddBox(bi, bodies, 200f, 1f, 100f, 0f, -1f, 0f,
            Quaternion.Identity,
            JPH.EMotionType.Static, LayerNonMoving,
            new Vector3(0.9f, 0.7f, 0.3f), restitution: 0.0f);
        var last = bodies[bodies.Count - 1];
        last.shape = RenderShape.Floor;
        bodies[bodies.Count - 1] = last;

        for (int i = 0; i <= 10; i++)
        {
            float restitution = i * 0.1f;
            float x = -50f + i * 10f;

            string label = $"Restitution: {restitution:F1}";

            // Pressurised sphere
            var sphereFaces = new List<(uint, uint, uint)>();
            var sphereSettings = CreateSphereSettings(1f, 20, 10, sphereFaces);
            var sId = RegisterSoftBody(bi, sphereSettings, sphereFaces,
                x, 10f, 0f,
                0f, 0f, 0f, 1f,
                ColorForIndex(i),
                cs => { cs.mPressure = 2000f; cs.mRestitution = restitution; });
            SetBodyLabel(sId, label);

            // Soft cube
            var cId = RegisterCubeSoftBody(bi, 5, 0.5f,
                x, 10f, -5f,
                0f, 0f, 0f, 1f,
                ColorForIndex(i),
                cs => { cs.mRestitution = restitution; });
            SetBodyLabel(cId, label);
        }
    }

    private static Vector3 ColorForIndex(int i)
    {
        float t = i / 10f;
        return new Vector3(1f - t, 0.3f, t);
    }
}
