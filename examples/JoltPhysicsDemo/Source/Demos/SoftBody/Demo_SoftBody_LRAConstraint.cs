using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Compares two hanging cloths: one without LRA (Long Range Attachment) constraints
/// and one with LRA constraints, which prevents excessive stretching.
/// Ported from SoftBodyLRAConstraintTest.cpp.
/// </summary>
public class Demo_SoftBody_LRAConstraint : DemoBase
{
    public override string Name     => "SoftBody: LRA Constraint";
    public override string Category => "Soft Body";

    const int   GridX    = 10;
    const int   GridZ    = 50;
    const float Spacing  = 0.5f;

    public override void Init(JPH.BodyInterface bi, JPH.PhysicsSystem sys, List<PhysicsBody> bodies, Random rng)
    {
        AddFloor(bi, bodies);

        // Cloth without LRA: top row pinned (z == 0), hanging down
        {
            var faces = new List<(uint, uint, uint)>();
            var settings = CreateClothSettings(
                GridX, GridZ, Spacing,
                (x, z) => z == 0 ? 0f : 1f,
                null,
                JPH.SoftBodySharedSettings.EBendType.None,
                faces,
                new JPH.SoftBodySharedSettings.Const_VertexAttributes(1e-3f, 1e-3f, float.MaxValue));

            RegisterSoftBody(bi, settings, faces,
                -10f, 25f, 0f,
                0f, 0f, 0f, 1f,
                new Vector3(0.2f, 0.5f, 0.9f));
        }

        // Cloth with LRA: same setup, but CalculateLRALengths called before Optimize.
        // We build settings inline so we can call LRA before Optimize.
        {
            var faces    = new List<(uint, uint, uint)>();
            var settings = new JPH.SoftBodySharedSettings();

            for (uint z = 0; z < GridZ; z++)
            for (uint x = 0; x < GridX; x++)
            {
                float invMass = (z == 0) ? 0f : 1f;
                float px = x * Spacing;
                float pz = z * Spacing;
                using var pos = new JPH.Const_Float3(px, 0f, pz);
                using var v   = new JPH.SoftBodySharedSettings.Const_Vertex(pos, null, invMass);
                settings.mVertices.PushBack(v);
            }

            for (uint z = 0; z < GridZ - 1; z++)
            for (uint x = 0; x < GridX - 1; x++)
            {
                uint a = z * GridX + x;
                uint b = a + 1;
                uint c = a + GridX;
                uint d = c + 1;
                using var f1 = new JPH.SoftBodySharedSettings.Const_Face(a, c, b);
                using var f2 = new JPH.SoftBodySharedSettings.Const_Face(b, c, d);
                settings.AddFace(f1);
                settings.AddFace(f2);
            }

            using var defaultAttr = new JPH.SoftBodySharedSettings.Const_VertexAttributes(1e-3f, 1e-3f, float.MaxValue, JPH.SoftBodySharedSettings.ELRAType.EuclideanDistance);
            settings.CreateConstraints(defaultAttr, 1u,
                JPH.SoftBodySharedSettings.EBendType.None);

            // Add LRA constraints (must be called after CreateConstraints, before Optimize)
            settings.CalculateLRALengths();

            settings.Optimize();
            ReadFacesFromSettings(settings, faces);

            RegisterSoftBody(bi, settings, faces,
                10f, 25f, 0f,
                0f, 0f, 0f, 1f,
                new Vector3(0.9f, 0.5f, 0.2f));
        }
    }
}
