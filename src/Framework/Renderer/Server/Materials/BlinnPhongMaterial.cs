using System.Numerics;
using static Sokol.SG;

namespace GameEditor.Framework.Renderer.Server.Materials
{
    /// <summary>
    /// Blinn-Phong material — diffuse + specular + normal map channels.
    /// Maps directly onto the Blinn-Phong uniform block in blinn_phong.glsl.
    /// </summary>
    public sealed class BlinnPhongMaterial : Material
    {
        public override MaterialKind Kind => MaterialKind.BlinnPhong;

        // ── colour scalars ───────────────────────────────────────────────────────────
        public Vector3 DiffuseColor  = Vector3.One;
        public Vector3 SpecularColor = new(0.5f);
        public float   Shininess     = 32f;
        public float   Opacity       = 1f;

        // ── texture views + sampler (GPU handles) ────────────────────────────────────
        // Set by MaterialRegistry after texture load; default to placeholder views.
        public sg_view    DiffuseMap;   // map_Kd  (albedo)
        public sg_view    SpecularMap;  // map_Ks
        public sg_view    NormalMap;    // map_Bump
        public sg_view    OpacityMap;   // map_d
        // Shared sampler for all texture slots (from TextureCache.DefaultSampler).
        public sg_sampler Sampler;

        // ── source paths (for reload / TextureCache keying) ──────────────────────────
        public string DiffuseMapPath  = "";
        public string SpecularMapPath = "";
        public string NormalMapPath   = "";
        public string OpacityMapPath  = "";

        /// <summary>True when any map_d or d < 1 requires alpha blending.</summary>
        public bool HasAlpha => Opacity < 1f || !string.IsNullOrEmpty(OpacityMapPath);
    }
}
