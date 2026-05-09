using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Compares the four bend constraint types: None, Distance, Dihedral, and Cosserat rod.
/// Four cloths hang with top rows pinned; four spheres bounce — each using a different constraint type.
/// Ported from SoftBodyBendConstraintTest.cpp.
/// </summary>
public class Demo_SoftBody_BendConstraint : DemoBase
{
    public override string Name     => "SoftBody: Bend Constraint";
    public override string Category => "Soft Body";

    static readonly JPH.SoftBodySharedSettings.EBendType[] BendTypes =
    {
        JPH.SoftBodySharedSettings.EBendType.None,
        JPH.SoftBodySharedSettings.EBendType.Distance,
        JPH.SoftBodySharedSettings.EBendType.Dihedral,
    };

    static readonly string[] BendLabels =
    {
        "No bend constraints",
        "Distance bend constraints",
        "Dihedral angle bend constraints",
    };

    public override void Init(JPH.BodyInterface bi, JPH.PhysicsSystem sys, List<PhysicsBody> bodies, Random rng)
    {
        AddFloor(bi, bodies);

        float[] posX = { -5f, 0f, 5f };
        Vector3[] colors =
        {
            new Vector3(0.9f, 0.3f, 0.3f),
            new Vector3(0.3f, 0.9f, 0.3f),
            new Vector3(0.3f, 0.3f, 0.9f),
            new Vector3(0.9f, 0.9f, 0.3f),
        };

        // ── 3 cloths with standard bend types ──────────────────────────────
        for (int i = 0; i < 3; i++)
        {
            var r = new Random(1234);
            var faces = new List<(uint, uint, uint)>();
            var settings = CreateClothSettings(
                10, 10, 0.5f,
                (x, z) => z < 2 ? 0f : 1f,
                (x, z) => new Vector3(
                    (r.NextSingle() * 2f - 1f) * 0.1f,
                    (z & 1) != 0 ? 0.1f : -0.1f,
                    (r.NextSingle() * 2f - 1f) * 0.1f),
                BendTypes[i], faces);

            var id = RegisterSoftBody(bi, settings, faces,
                posX[i], 5f, 0f, 0f, 0f, 0f, 1f, colors[i]);
            SetBodyLabel(id, BendLabels[i]);
        }

        // ── Cosserat rod cloth ──────────────────────────────────────────────
        {
            const int nX = 10, nZ = 10;
            var r = new Random(1234);
            var cosseratFaces = new List<(uint, uint, uint)>();
            var clothSettings = CreateClothSettings(nX, nZ, 0.5f,
                (x, z) => z < 2 ? 0f : 1f,
                (x, z) => new Vector3(
                    (r.NextSingle() * 2f - 1f) * 0.1f,
                    (z & 1) != 0 ? 0.1f : -0.1f,
                    (r.NextSingle() * 2f - 1f) * 0.1f),
                JPH.SoftBodySharedSettings.EBendType.None, cosseratFaces,
                skipConstraints: true);

            // vertex_index(x, z) = x + z * nX  (matches C++ SoftBodyCreator::CreateCloth)
            uint VIdx(uint x, uint z) => z * (uint)nX + x;
            var rodMap = new Dictionary<(uint, uint), uint>();
            uint rodCount = 0;
            uint GetRod(uint x1, uint z1, uint x2, uint z2)
            {
                uint v0 = VIdx(x1, z1), v1 = VIdx(x2, z2);  // v0 < v1 always by loop construction
                var key = (v0, v1);
                if (rodMap.TryGetValue(key, out uint idx)) return idx;
                clothSettings.mRodStretchShearConstraints.PushBack(new JPH.SoftBodySharedSettings.RodStretchShear(v0, v1));
                rodMap[key] = rodCount;
                return rodCount++;
            }

            for (uint z = 1; z < nZ - 1; z++)
                for (uint x = 0; x < nX - 1; x++)
                {
                    if (z > 1 && x < nX - 2)
                        clothSettings.mRodBendTwistConstraints.PushBack(new JPH.SoftBodySharedSettings.RodBendTwist(
                            GetRod(x, z, x + 1, z), GetRod(x + 1, z, x + 2, z)));
                    if (z < nZ - 2)
                        clothSettings.mRodBendTwistConstraints.PushBack(new JPH.SoftBodySharedSettings.RodBendTwist(
                            GetRod(x, z, x, z + 1), GetRod(x, z + 1, x, z + 2)));
                    if (x < nX - 2 && z < nZ - 2)
                    {
                        clothSettings.mRodBendTwistConstraints.PushBack(new JPH.SoftBodySharedSettings.RodBendTwist(
                            GetRod(x, z, x + 1, z + 1), GetRod(x + 1, z + 1, x + 2, z + 2)));
                        clothSettings.mRodBendTwistConstraints.PushBack(new JPH.SoftBodySharedSettings.RodBendTwist(
                            GetRod(x + 2, z, x + 1, z + 1), GetRod(x + 1, z + 1, x, z + 2)));
                    }
                }

            // Set compliance on all rod constraints (matches C++ cCompliance = 1.0e-5f for cloth)
            uint clothSS = (uint)clothSettings.mRodStretchShearConstraints.Size();
            for (uint i = 0; i < clothSS; i++)
                clothSettings.mRodStretchShearConstraints[(UIntPtr)i].mCompliance = 1e-5f;
            uint clothBT = (uint)clothSettings.mRodBendTwistConstraints.Size();
            for (uint i = 0; i < clothBT; i++)
                clothSettings.mRodBendTwistConstraints[(UIntPtr)i].mCompliance = 1e-5f;

            clothSettings.CalculateRodProperties();
            clothSettings.Optimize();
            ReadFacesFromSettings(clothSettings, cosseratFaces);

            var clothId = RegisterSoftBody(bi, clothSettings, cosseratFaces,
                10f, 5f, 0f, 0f, 0f, 0f, 1f, colors[3]);
            SetBodyLabel(clothId, "Cosserat rod constraints");
        }

        // ── 3 spheres with standard bend types ─────────────────────────────
        for (int i = 0; i < 3; i++)
        {
            var sphereFaces = new List<(uint, uint, uint)>();
            var sphereSettings = CreateSphereSettings(1f, 10, 20, sphereFaces, BendTypes[i]);
            var id = RegisterSoftBody(bi, sphereSettings, sphereFaces,
                posX[i], 5f, 10f, 0f, 0f, 0f, 1f, colors[i]);
            SetBodyLabel(id, BendLabels[i]);
        }

        // ── Cosserat rod sphere (C++ vertex layout: poles at index 0/1) ─────
        {
            const int numTheta = 10, numPhi = 20;
            var cosFaces = new List<(uint, uint, uint)>();
            // Use CreateSphereSettings(skipConstraints:true) to get vertices+faces in exact
            // C++ SoftBodyCreator::CreateSphere order, so CalculateRodProperties computes
            // correct material frames from the right face normals.
            var sphereSettings = CreateSphereSettings(1f, numTheta, numPhi, cosFaces,
                skipConstraints: true);

            // vertex_index(theta, phi) — matches C++ SoftBodyCreator::CreateSphere layout
            uint VI(uint theta, uint phi) =>
                theta == 0 ? 0u :
                theta == numTheta - 1 ? 1u :
                2u + (theta - 1) * (uint)numPhi + phi % (uint)numPhi;

            var sphereRodMap = new Dictionary<(uint, uint), uint>();
            uint sphereRodCount = 0;
            uint GetSphereRod(uint theta1, uint phi1, uint theta2, uint phi2)
            {
                uint v0 = VI(theta1, phi1), v1 = VI(theta2, phi2);
                uint keyA = Math.Min(v0, v1), keyB = Math.Max(v0, v1);
                var key = (keyA, keyB);
                if (sphereRodMap.TryGetValue(key, out uint idx)) return idx;
                sphereSettings.mRodStretchShearConstraints.PushBack(new JPH.SoftBodySharedSettings.RodStretchShear(v0, v1));
                sphereRodMap[key] = sphereRodCount;
                return sphereRodCount++;
            }

            // Rings along the side
            for (uint phi = 0; phi < numPhi; phi++)
                for (uint theta = 0; theta < numTheta - 1; theta++)
                {
                    if (theta < numTheta - 2)
                        sphereSettings.mRodBendTwistConstraints.PushBack(new JPH.SoftBodySharedSettings.RodBendTwist(
                            GetSphereRod(theta, phi, theta + 1, phi),
                            GetSphereRod(theta + 1, phi, theta + 2, phi)));
                    if (theta > 0 && phi < numPhi - 1)
                        sphereSettings.mRodBendTwistConstraints.PushBack(new JPH.SoftBodySharedSettings.RodBendTwist(
                            GetSphereRod(theta, phi, theta, phi + 1),
                            GetSphereRod(theta, phi + 1, theta, (phi + 2) % (uint)numPhi)));
                }

            // Close the caps
            uint lastRing = (uint)(numTheta - 2);
            for (uint phi1 = 0, phi2 = (uint)(numPhi / 2); phi1 < numPhi / 2; phi1++, phi2 = (phi2 + 1) % (uint)numPhi)
            {
                sphereSettings.mRodBendTwistConstraints.PushBack(new JPH.SoftBodySharedSettings.RodBendTwist(
                    GetSphereRod(0u, phi1, 1u, phi1), GetSphereRod(0u, phi2, 1u, phi2)));
                sphereSettings.mRodBendTwistConstraints.PushBack(new JPH.SoftBodySharedSettings.RodBendTwist(
                    GetSphereRod(lastRing, phi1, (uint)(numTheta - 1), phi1),
                    GetSphereRod(lastRing, phi2, (uint)(numTheta - 1), phi2)));
            }

            // Set compliance on all rod constraints (matches C++ cCompliance = 1.0e-4f for sphere)
            uint sphereSS = (uint)sphereSettings.mRodStretchShearConstraints.Size();
            for (uint i = 0; i < sphereSS; i++)
                sphereSettings.mRodStretchShearConstraints[(UIntPtr)i].mCompliance = 1e-4f;
            uint sphereBT = (uint)sphereSettings.mRodBendTwistConstraints.Size();
            for (uint i = 0; i < sphereBT; i++)
                sphereSettings.mRodBendTwistConstraints[(UIntPtr)i].mCompliance = 1e-4f;

            sphereSettings.CalculateRodProperties();
            sphereSettings.Optimize();
            ReadFacesFromSettings(sphereSettings, cosFaces);

            var sphereId = RegisterSoftBody(bi, sphereSettings, cosFaces,
                10f, 5f, 10f, 0f, 0f, 0f, 1f, colors[3]);
            SetBodyLabel(sphereId, "Cosserat rod constraints");
        }
    }
}
