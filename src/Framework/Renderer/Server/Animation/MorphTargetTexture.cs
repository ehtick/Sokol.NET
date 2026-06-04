// MorphTargetTexture.cs — builds the immutable RGBA32F texture2DArray that stores a primitive's
// morph-target (blend-shape) vertex displacements. Ported in spirit from CGltfViewer's
// CreateMorphTargetTexture (Frame.cs:1419) but per-primitive and with a FIXED slice layout.
//
// Why immutable: the displacement vectors are static mesh data, so the texture is supplied once at
// sg_make_image time and never re-uploaded — only the small per-frame morph *weights* uniform varies
// (contrast the joint-matrix texture, which streams every frame). One texture per morphed primitive.
//
// Slice layout — MUST match the offsets HARDCODED in pbr.glsl / animation.glsl
//   getTargetPosition(vid, 8)      → slices 0..7
//   getTargetNormal  (vid, 8, 8)   → slices 8..15
//   getTargetTangent (vid, 8, 16)  → slices 16..23
// i.e. a fixed 8 slices reserved per attribute (24 total), regardless of the actual target count.
// Each texel holds one vertex's vec3 displacement for one (target, attribute) in RGB (A = 0), at
// (vertexID % texSize, vertexID / texSize). Unused targets stay zero (their weight is 0 too).
//
// The texture is read with texelFetch only (never filtered), so it is declared unfilterable_float in
// pbr_vs_uniforms.glsl and bound with a NEAREST / CLAMP_TO_EDGE nonfiltering sampler — the same
// RGBA32F-not-filterable constraint that bit the joint texture on low-tier GLES3 (Mali-G52).
using System;
using Sokol;
using static Sokol.CGltf;
using static Sokol.SG;

namespace GameEditor.Framework.Renderer.Server.Animation
{
    public static unsafe class MorphTargetTexture
    {
        /// <summary>Shader cap: the morph accumulation loops run <c>i &lt; 8</c>, so at most 8 targets
        /// contribute. Extra glTF targets beyond this are dropped at build time.</summary>
        public const int MaxTargets = 8;

        private const int SlicesPerAttribute = MaxTargets;          // 8
        private const int TotalSlices        = SlicesPerAttribute * 3; // position + normal + tangent = 24

        /// <summary>
        /// Builds the morph texture for one glTF primitive. Returns false (with default image/view) when
        /// the primitive has no morph targets. <paramref name="targetCount"/> reports the active count
        /// (≤ <see cref="MaxTargets"/>). <paramref name="vertexCount"/> must be the primitive's POSITION
        /// accessor count — the morph texture is indexed by <c>gl_VertexIndex</c>, which equals that
        /// accessor index because the importer keeps glTF vertex order (no welding). Render-thread only.
        /// </summary>
        public static bool Build(cgltf_primitive* prim, int vertexCount,
                                 out sg_image image, out sg_view view, out int targetCount)
        {
            image = default;
            view  = default;
            targetCount = Math.Min((int)prim->targets_count, MaxTargets);
            if (targetCount == 0 || vertexCount <= 0) return false;

            // Square texture sized to hold every vertex once per slice.
            int texSize = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(vertexCount)));
            int texelsPerSlice = texSize * texSize;
            var data = new float[texelsPerSlice * TotalSlices * 4]; // RGBA32F

            for (int t = 0; t < targetCount; t++)
            {
                cgltf_morph_target* target = &prim->targets[t];
                for (int ai = 0; ai < (int)target->attributes_count; ai++)
                {
                    cgltf_attribute* attr = &target->attributes[ai];
                    cgltf_accessor*  acc  = attr->data;
                    if (acc == null) continue;

                    int slice;
                    switch (attr->type)
                    {
                        case cgltf_attribute_type.cgltf_attribute_type_position: slice = t;                          break;
                        case cgltf_attribute_type.cgltf_attribute_type_normal:   slice = SlicesPerAttribute     + t; break;
                        case cgltf_attribute_type.cgltf_attribute_type_tangent:  slice = SlicesPerAttribute * 2 + t; break;
                        default: continue; // glTF morph targets only carry POSITION/NORMAL/TANGENT deltas (vec3)
                    }

                    int count = Math.Min((int)acc->count, vertexCount);
                    if (count <= 0) continue;
                    var values = new float[count * 3];
                    fixed (float* vp = values)
                        cgltf_accessor_unpack_floats(*acc, vp, (nuint)(count * 3));

                    int sliceBase = slice * texelsPerSlice * 4;
                    for (int v = 0; v < count; v++)
                    {
                        int o = sliceBase + v * 4;
                        data[o + 0] = values[v * 3 + 0];
                        data[o + 1] = values[v * 3 + 1];
                        data[o + 2] = values[v * 3 + 2];
                        data[o + 3] = 0f;
                    }
                }
            }

            fixed (float* ptr = data)
            {
                var imgData = new sg_image_data();
                imgData.mip_levels[0].ptr  = ptr;
                imgData.mip_levels[0].size = (nuint)(data.Length * sizeof(float));
                image = sg_make_image(new sg_image_desc
                {
                    type         = sg_image_type.SG_IMAGETYPE_ARRAY,
                    width        = texSize,
                    height       = texSize,
                    num_slices   = TotalSlices,
                    pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA32F,
                    data         = imgData,
                    label        = "morph-target-texture",
                });
            }
            view = sg_make_view(new sg_view_desc
            {
                texture = new sg_texture_view_desc { image = image },
                label   = "morph-target-view",
            });
            return true;
        }
    }
}
