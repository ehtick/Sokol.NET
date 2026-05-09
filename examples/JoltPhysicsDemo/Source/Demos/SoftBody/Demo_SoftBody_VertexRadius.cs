using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Demonstrates the mVertexRadius setting: a cloth draped over a static sphere,
/// with vertex radius giving the cloth visible thickness.
/// Ported from SoftBodyVertexRadiusTest.cpp.
/// </summary>
public class Demo_SoftBody_VertexRadius : DemoBase
{
    public override string Name     => "SoftBody: Vertex Radius";
    public override string Category => "Soft Body";

    public override void Init(JPH.BodyInterface bi, JPH.PhysicsSystem sys, List<PhysicsBody> bodies, Random rng)
    {
        AddFloor(bi, bodies);

        // Static sphere obstacle
        AddSphere(bi, bodies, 2f, 0f, 0f, 0f,
            JPH.EMotionType.Static, LayerNonMoving,
            new Vector3(0.6f, 0.6f, 0.6f));

        // Cloth with vertex radius
        var clothFaces = new List<(uint, uint, uint)>();
        var clothSettings = CreateClothSettings(
            30, 30, 0.5f,
            null, null,
            JPH.SoftBodySharedSettings.EBendType.None,
            clothFaces);

        RegisterSoftBody(bi, clothSettings, clothFaces,
            0f, 5f, 0f,
            0f, MathF.Sin(MathF.PI / 8f), 0f, MathF.Cos(MathF.PI / 8f),
            new Vector3(0.2f, 0.7f, 0.4f),
            cs => cs.mVertexRadius = 0.1f);
    }
}
