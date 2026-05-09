using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Tests the mUpdatePosition and mMakeRotationIdentity flags on soft bodies.
/// Four cubes are placed at different positions with each combination of flags.
/// Ported from SoftBodyUpdatePositionTest.cpp.
/// </summary>
public class Demo_SoftBody_UpdatePosition : DemoBase
{
    public override string Name     => "SoftBody: Update Position";
    public override string Category => "Soft Body";

    public override void Init(JPH.BodyInterface bi, JPH.PhysicsSystem sys, List<PhysicsBody> bodies, Random rng)
    {
        AddFloor(bi, bodies);

        // Slight tilt rotation (same as the Shapes cube)
        float s = MathF.Sqrt(1f / 3f);
        float halfAngle = MathF.PI / 4f;
        float sa = MathF.Sin(halfAngle);
        float ca = MathF.Cos(halfAngle);

        float[] positions = { 0f, 0f, 10f, 10f };
        float[] positionsZ = { 0f, 10f, 0f, 10f };
        bool[] updatePos = { false, false, true, true };
        bool[] makeRotId = { false, true, false, true };

        for (int i = 0; i < 4; i++)
        {
            var id = RegisterCubeSoftBody(bi,
                5, 0.5f,
                positions[i], 10f, positionsZ[i],
                s * sa, s * sa, s * sa, ca,
                new Vector3(0.2f, 0.5f, 0.9f),
                cs =>
                {
                    cs.mUpdatePosition       = updatePos[i];
                    cs.mMakeRotationIdentity = makeRotId[i];
                });
            SetBodyLabel(id, $"UpdatePosition: {(updatePos[i] ? "On" : "Off")}\nMakeRotationIdentity: {(makeRotId[i] ? "On" : "Off")}");
        }
    }
}
