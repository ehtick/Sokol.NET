using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Port of SoftBodyFrictionTest — 11 spheres and 11 cubes with varying friction,
/// sliding along a high-friction floor.
/// </summary>
public class Demo_SoftBody_Friction : DemoBase
{
    public override string Name     => "SoftBody: Friction";
    public override string Category  => "Soft Body";

    public override void Init(JPH.BodyInterface bi, JPH.PhysicsSystem physicsSystem,
                               List<PhysicsBody> bodies, Random rng)
    {
        // High-friction floor
        AddBox(bi, bodies, 200f, 1f, 100f, 0f, -1f, 0f,
            Quaternion.Identity,
            JPH.EMotionType.Static, LayerNonMoving,
            new Vector3(0.9f, 0.7f, 0.3f), friction: 1.0f);
        var last = bodies[bodies.Count - 1];
        last.shape = RenderShape.Floor;
        bodies[bodies.Count - 1] = last;

        for (int i = 0; i <= 10; i++)
        {
            float friction = i * 0.1f;
            float x = -50f + i * 10f;
            string labelText = $"Friction: {friction:F1}";

            // Pressurised sphere — velocity set per-vertex before adding to simulation
            var sphereFaces    = new List<(uint, uint, uint)>();
            var sphereSettings = CreateSphereSettings(1f, 20, 10, sphereFaces);
            uint sphereVertCount = (uint)sphereSettings.mVertices.Size();

            using var sPos = new JPH.Vec3(x, 1f, 0f);
            using var sRot = new JPH.Quat(0f, 0f, 0f, 1f);
            using var sCs  = new JPH.SoftBodyCreationSettings(
                sphereSettings, sPos, sRot, LayerMoving);
            sCs.mPressure = 2000f;
            sCs.mFriction = friction;

            var sBody = bi.CreateSoftBody(sCs);
            var sMp   = (JPH.SoftBodyMotionProperties)sBody.GetMotionProperties()!;
            for (uint vi = 0; vi < sphereVertCount; vi++)
                sMp.GetVertex(vi).mVelocity.Set(0f, 0f, 10f);
            var sId = sBody.GetID();
            bi.AddBody(in sId, JPH.EActivation.Activate);
            BuildSoftBodyRenderEntry(sId, sphereVertCount, sphereFaces, ColorForIndex(i));
            SetBodyLabel(sId, labelText);

            // Soft cube — velocity set per-vertex before adding to simulation
            var cubeSettings  = SoftBodySharedSettings.CreateCube(5, 0.5f)!;
            var cubeFaces     = new List<(uint, uint, uint)>();
            ReadFacesFromSettings(cubeSettings, cubeFaces);
            uint cubeVertCount = (uint)cubeSettings.mVertices.Size();

            using var cPos = new JPH.Vec3(x, 1f, -5f);
            using var cRot = new JPH.Quat(0f, 0f, 0f, 1f);
            using var cCs  = new JPH.SoftBodyCreationSettings(
                cubeSettings, cPos, cRot, LayerMoving);
            cCs.mFriction = friction;

            var cBody = bi.CreateSoftBody(cCs);
            var cMp   = (JPH.SoftBodyMotionProperties)cBody.GetMotionProperties()!;
            for (uint vi = 0; vi < cubeVertCount; vi++)
                cMp.GetVertex(vi).mVelocity.Set(0f, 0f, 10f);
            var cId = cBody.GetID();
            bi.AddBody(in cId, JPH.EActivation.Activate);
            BuildSoftBodyRenderEntry(cId, cubeVertCount, cubeFaces, ColorForIndex(i));
            SetBodyLabel(cId, labelText);
        }
    }

    private static Vector3 ColorForIndex(int i)
    {
        float t = i / 10f;
        return new Vector3(1f - t, 0.3f, t);
    }
}
