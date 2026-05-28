using System.Numerics;

namespace GameEditor.Framework.Renderer.Server.Materials
{
    /// <summary>Abstract base class for all materials used by RenderingServer.</summary>
    public abstract class Material
    {
        public ushort Id;           // registry-assigned 16-bit id
        public string Name = "";
        public bool   IsTransparent;

        public abstract MaterialKind Kind { get; }
    }

    /// <summary>Discriminant for shader variant selection.</summary>
    public enum MaterialKind : byte
    {
        BlinnPhong = 0,
        Unlit      = 1,
    }
}
