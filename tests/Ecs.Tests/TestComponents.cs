using Frent;

namespace GameEditor.Framework.ECS.Components
{
    // Lean test doubles of the three production components ECSWorld touches in CreateEntity /
    // DestroyEntity / AddComponent. Only the fields ECSWorld actually reads are present — the
    // parent→children index + cascade-delete logic depends solely on Transform.Parent, so these
    // exercise the *real* ECSWorld.cs faithfully without pulling in the production Transform's
    // System.Numerics math or its physics/graphics dependencies.

    public struct NameTag
    {
        public string? Name;
    }

    public struct ActiveFlag
    {
        public bool Active;
    }

    public struct Transform
    {
        public Entity? Parent;

        public static Transform Default => new Transform { Parent = null };
    }
}
