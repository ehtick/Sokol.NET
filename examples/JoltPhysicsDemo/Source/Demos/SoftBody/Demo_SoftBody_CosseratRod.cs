using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Demonstrates Cosserat rod constraints: hanging helix, binary tree, and sea-weed cluster.
/// Ported from SoftBodyCosseratRodConstraintTest.cpp.
/// </summary>
public class Demo_SoftBody_CosseratRod : DemoBase
{
    public override string Name     => "SoftBody: Cosserat Rod";
    public override string Category => "Soft Body";

    public override void Init(JPH.BodyInterface bi, JPH.PhysicsSystem sys, List<PhysicsBody> bodies, Random rng)
    {
        AddFloor(bi, bodies);
        CreateHelix(bi);
        CreateTree(bi);
        CreateWeed(bi);
    }

    // ── Helix ──────────────────────────────────────────────────────────────

    void CreateHelix(JPH.BodyInterface bi)
    {
        const float cRadius    = 0.5f;
        const int   cNumVerts  = 128;
        const float cHeight    = 5.0f;
        const float cNumCycles = 10f;

        using var settings = new JPH.SoftBodySharedSettings();

        for (int i = 0; i < cNumVerts; i++)
        {
            float fraction = (float)i / (cNumVerts - 1);
            float alpha    = cNumCycles * 2.0f * MathF.PI * fraction;
            float invMass  = i == 0 ? 0.0f : 1e-2f;

            using var pos = new JPH.Const_Float3(
                cRadius * MathF.Sin(alpha),
                0.5f * (1.0f - fraction * cHeight),
                cRadius * MathF.Cos(alpha));
            using var v = new JPH.SoftBodySharedSettings.Const_Vertex(pos, inInvMass: invMass);
            settings.SoftBodySettingsAddVertex(v);

            if (i > 0)
                settings.SoftBodySettingsAddRodStretchShear((uint)(i - 1), (uint)i);
            if (i > 1)
                settings.SoftBodySettingsAddRodBendTwist((uint)(i - 2), (uint)(i - 1));
        }

        settings.CalculateRodProperties();
        settings.Optimize();

        using var origin = new JPH.Vec3(0f, 10f, 0f);
        using var rot    = JPH.Quat.SIdentity();
        using var cs     = new JPH.SoftBodyCreationSettings(
            settings, origin, rot, LayerMoving);

        var bodyId = bi.CreateAndAddSoftBody(cs, JPH.EActivation.Activate);
        RegisterRodBody(bodyId, settings, new Vector3(0.85f, 0.85f, 0.85f));
    }

    // ── Binary tree ────────────────────────────────────────────────────────

    struct TreeBranch
    {
        public uint    PrevVertex;
        public uint    PrevRod;       // uint.MaxValue = no predecessor rod
        public Vector3 Direction;
        public uint    Depth;
        public float   PrevInvMass;
        public Vector3 PrevPos;
    }

    void CreateTree(JPH.BodyInterface bi)
    {
        using var settings = new JPH.SoftBodySharedSettings();

        // Root particle (kinematic)
        using var rootPos = new JPH.Const_Float3(0f, 0f, 0f);
        using var rootV   = new JPH.SoftBodySharedSettings.Const_Vertex(rootPos, inInvMass: 0f);
        settings.SoftBodySettingsAddVertex(rootV);

        uint nextVertex = 1;

        var queue = new Queue<TreeBranch>();
        queue.Enqueue(new TreeBranch
        {
            PrevVertex  = 0,
            PrevRod     = uint.MaxValue,
            Direction   = Vector3.UnitY,
            Depth       = 0,
            PrevInvMass = 0f,
            PrevPos     = Vector3.Zero,
        });

        while (queue.Count > 0)
        {
            var branch = queue.Dequeue();

            float  newInvMass = branch.Depth > 0 ? 2f * branch.PrevInvMass : 1e-3f;
            var    newPos     = branch.PrevPos + branch.Direction;
            uint   newVertex  = nextVertex++;

            using var vPos = new JPH.Const_Float3(newPos.X, newPos.Y, newPos.Z);
            using var vNew = new JPH.SoftBodySharedSettings.Const_Vertex(vPos, inInvMass: newInvMass);
            settings.SoftBodySettingsAddVertex(vNew);

            uint newRod = (settings).SoftBodySettingsGetRodStretchShearCount();
            settings.SoftBodySettingsAddRodStretchShear(branch.PrevVertex, newVertex);

            if (branch.PrevRod != uint.MaxValue)
                settings.SoftBodySettingsAddRodBendTwist(branch.PrevRod, newRod);

            if (branch.Depth < 10)
            {
                for (int i = 0; i < 2; i++)
                {
                    float  angle   = (float)((-15.0 + i * 30.0) * Math.PI / 180.0);
                    Vector3 axis   = (branch.Depth & 1) == 0 ? Vector3.UnitX : Vector3.UnitZ;
                    var     q      = System.Numerics.Quaternion.CreateFromAxisAngle(axis, angle);
                    Vector3 newDir = Vector3.Transform(branch.Direction, q);

                    queue.Enqueue(new TreeBranch
                    {
                        PrevVertex  = newVertex,
                        PrevRod     = newRod,
                        Direction   = newDir,
                        Depth       = branch.Depth + 1,
                        PrevInvMass = newInvMass,
                        PrevPos     = newPos,
                    });
                }
            }
        }

        settings.CalculateRodProperties();
        settings.Optimize();

        using var origin = new JPH.Vec3(10f, 0f, 0f);
        using var rot    = JPH.Quat.SIdentity();
        using var cs     = new JPH.SoftBodyCreationSettings(
            settings, origin, rot, LayerMoving);

        var bodyId = bi.CreateAndAddSoftBody(cs, JPH.EActivation.Activate);
        RegisterRodBody(bodyId, settings, new Vector3(0.3f, 0.8f, 0.3f));
    }

    // ── Sea-weed cluster ───────────────────────────────────────────────────

    void CreateWeed(JPH.BodyInterface bi)
    {
        const int cNumVerts   = 64;
        const int cNumStrands = 50;

        using var settings = new JPH.SoftBodySharedSettings();

        var rand  = new Random(0);

        for (int strand = 0; strand < cNumStrands; strand++)
        {
            // Random root position inside unit circle
            float radius = (float)rand.NextDouble();
            float theta  = (float)(rand.NextDouble() * 2.0 * Math.PI);
            float rootX  = radius * MathF.Sin(theta);
            float rootZ  = radius * MathF.Cos(theta);

            // Random wave phases
            float phase1 = (float)(rand.NextDouble() * 2.0 * Math.PI);
            float phase2 = (float)(rand.NextDouble() * 2.0 * Math.PI);

            uint firstVertex = (settings).SoftBodySettingsGetVertexCount();

            for (int i = 0; i < cNumVerts; i++)
            {
                float amplitude = 0.1f * MathF.Sin(phase1 + i * 2.0f * MathF.PI / 8f);
                float px = rootX + MathF.Sin(phase2) * amplitude;
                float py = 0.1f * i;
                float pz = rootZ + MathF.Cos(phase2) * amplitude;
                float invMass = i == 0 ? 0.0f : 0.1f;

                using var vPos = new JPH.Const_Float3(px, py, pz);
                using var v    = new JPH.SoftBodySharedSettings.Const_Vertex(vPos, inInvMass: invMass);
                settings.SoftBodySettingsAddVertex(v);
            }

            uint firstRod = (settings).SoftBodySettingsGetRodStretchShearCount();

            for (int i = 0; i < cNumVerts - 1; i++)
                settings.SoftBodySettingsAddRodStretchShear(
                    firstVertex + (uint)i, firstVertex + (uint)i + 1);

            for (int i = 0; i < cNumVerts - 2; i++)
                settings.SoftBodySettingsAddRodBendTwist(
                    firstRod + (uint)i, firstRod + (uint)i + 1);
        }

        settings.CalculateRodProperties();
        settings.Optimize();

        using var origin = new JPH.Vec3(20f, 0f, 0f);
        using var rot    = JPH.Quat.SIdentity();
        using var cs     = new JPH.SoftBodyCreationSettings(
            settings, origin, rot, LayerMoving);
        cs.mGravityFactor = 0.8f;

        var bodyId = bi.CreateAndAddSoftBody(cs, JPH.EActivation.Activate);
        RegisterRodBody(bodyId, settings, new Vector3(0.2f, 0.7f, 0.3f));
    }
}
