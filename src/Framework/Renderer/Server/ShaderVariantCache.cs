// ShaderVariantCache.cs — maps (MaterialKind, flags) → sg_shader + sg_pipeline.
//
// Depends on blinn_phong_shader_cs.Shaders (auto-generated from
// src/Framework/Renderer/Server/shaders/blinn_phong.glsl by sokol-shdc).
// The shader is compiled for all platforms (glsl430/hlsl5/metal_macos/metal_ios/glsl300es)
// into blinn_phong_shader.cs which is part of Framework.csproj.
//
// Vertex layout (buffer 0, per-vertex — ObjVertex 48 B):
//   loc 0 : in_pos      float3
//   loc 1 : in_normal   float3
//   loc 2 : in_uv       float2
//   loc 3 : in_tangent  float4
//
// Vertex layout (buffer 1, per-instance — InstanceData 80 B):
//   loc 4-7 : in_model_{0..3}  float4  (mat4 row)
//   loc 8   : in_custom        float4

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using static Sokol.SG;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SGlue;
using static blinn_phong_shader_cs.Shaders;
using PbrShaders = pbr_shader_cs_pbr.Shaders;
using PbrCsm1 = pbr_csm1_shader_cs_pbr_csm1.Shaders;
using PbrCsm4 = pbr_csm4_shader_cs_pbr_csm4.Shaders;
using PbrSkinning = pbr_skinning_shader_cs_pbr_skinning.Shaders;
using PbrSkinningCsm4 = pbr_skinning_csm4_shader_cs_pbr_skinning_csm4.Shaders;
using PbrSkinningCsm1 = pbr_skinning_csm1_shader_cs_pbr_skinning_csm1.Shaders;
using GameEditor.Framework.Core;
using GameEditor.Framework.Renderer.Server.Materials;

namespace GameEditor.Framework.Renderer.Server
{
    [Flags]
    public enum PipelineFlags : byte
    {
        None         = 0,
        AlphaBlend   = 1 << 0,
        DoubleSided  = 1 << 1,
        OffscreenRt  = 1 << 2,   // rendering to an offscreen RenderView
    }

    public sealed class ShaderVariantCache : IDisposable
    {
        // For M1 we have at most 4 variants (alpha × doublesided × rt).
        private readonly sg_pipeline[] _pipelines = new sg_pipeline[8];
        private readonly sg_shader[]   _shaders   = new sg_shader[8];

        // M4: PBR base-variant pipelines, keyed by the same flag combination as blinn.
        private readonly sg_pipeline[] _pbrPipelines = new sg_pipeline[8];
        private sg_shader _pbrShader;

        // M4: PBR + 1-cascade directional-shadow variant (samples the slice-0 shadow map).
        private readonly sg_pipeline[] _pbrCsm1Pipelines = new sg_pipeline[8];
        private sg_shader _pbrCsm1Shader;

        // M4: PBR + 4-cascade directional-shadow variant (samples atlas slices 0-3).
        private readonly sg_pipeline[] _pbrCsm4Pipelines = new sg_pipeline[8];
        private sg_shader _pbrCsm4Shader;

        // M4: PBR + GPU skinning variant (per-vertex buffer extended to 80 B with joints/weights).
        private readonly sg_pipeline[] _pbrSkinningPipelines = new sg_pipeline[8];
        private sg_shader _pbrSkinningShader;

        // M4: PBR + GPU skinning + 4-cascade directional shadow (skinned chars receive CSM).
        private readonly sg_pipeline[] _pbrSkinningCsm4Pipelines = new sg_pipeline[8];
        private sg_shader _pbrSkinningCsm4Shader;

        // M4: PBR + GPU skinning + 1-cascade directional shadow (default CSM1-mode receive).
        private readonly sg_pipeline[] _pbrSkinningCsm1Pipelines = new sg_pipeline[8];
        private sg_shader _pbrSkinningCsm1Shader;
        private bool _disposed;

        // ── lifecycle ────────────────────────────────────────────────────────────────

        public void Init()
        {
            sg_shader shader = sg_make_shader(blinn_phong_shader_desc(sg_query_backend()));

            // Create all pipeline variants we'll need at runtime.
            // Variants enumerated at init time → AOT-safe (no runtime generic instantiation).
            for (int i = 0; i < _pipelines.Length; i++)
            {
                var flags = (PipelineFlags)i;
                bool alpha      = (flags & PipelineFlags.AlphaBlend)  != 0;
                bool doubleSide = (flags & PipelineFlags.DoubleSided)  != 0;
                bool offscreen  = (flags & PipelineFlags.OffscreenRt)  != 0;

                _shaders[i]   = shader; // all variants share the same shader object
                _pipelines[i] = BuildPipeline(shader, alpha, doubleSide, offscreen);
            }

            // ── M4: PBR base-variant pipelines (shared per-vertex + per-instance layout) ──
            _pbrShader = sg_make_shader(PbrShaders.pbr_pbr_program_shader_desc(sg_query_backend()));
            for (int i = 0; i < _pbrPipelines.Length; i++)
            {
                var flags = (PipelineFlags)i;
                _pbrPipelines[i] = BuildPbrPipeline(
                    _pbrShader,
                    (flags & PipelineFlags.AlphaBlend)  != 0,
                    (flags & PipelineFlags.DoubleSided) != 0,
                    (flags & PipelineFlags.OffscreenRt) != 0);
            }

            // ── M4: PBR CSM1 (1-cascade directional shadow) — same vertex layout as base PBR ──
            _pbrCsm1Shader = sg_make_shader(PbrCsm1.pbr_csm1_pbr_program_shader_desc(sg_query_backend()));
            for (int i = 0; i < _pbrCsm1Pipelines.Length; i++)
            {
                var flags = (PipelineFlags)i;
                _pbrCsm1Pipelines[i] = BuildPbrCsm1Pipeline(
                    _pbrCsm1Shader,
                    (flags & PipelineFlags.AlphaBlend)  != 0,
                    (flags & PipelineFlags.DoubleSided) != 0,
                    (flags & PipelineFlags.OffscreenRt) != 0);
            }

            // ── M4: PBR CSM4 (4-cascade directional shadow) — same vertex layout as base PBR ──
            _pbrCsm4Shader = sg_make_shader(PbrCsm4.pbr_csm4_pbr_program_shader_desc(sg_query_backend()));
            for (int i = 0; i < _pbrCsm4Pipelines.Length; i++)
            {
                var flags = (PipelineFlags)i;
                _pbrCsm4Pipelines[i] = BuildPbrCsm4Pipeline(
                    _pbrCsm4Shader,
                    (flags & PipelineFlags.AlphaBlend)  != 0,
                    (flags & PipelineFlags.DoubleSided) != 0,
                    (flags & PipelineFlags.OffscreenRt) != 0);
            }

            // ── M4: PBR skinning (GPU bone skinning) — buffer 0 extended to 80 B (joints/weights) ──
            _pbrSkinningShader = sg_make_shader(PbrSkinning.pbr_skinning_pbr_program_shader_desc(sg_query_backend()));
            for (int i = 0; i < _pbrSkinningPipelines.Length; i++)
            {
                var flags = (PipelineFlags)i;
                _pbrSkinningPipelines[i] = BuildPbrSkinningPipeline(
                    _pbrSkinningShader,
                    (flags & PipelineFlags.AlphaBlend)  != 0,
                    (flags & PipelineFlags.DoubleSided) != 0,
                    (flags & PipelineFlags.OffscreenRt) != 0);
            }

            // ── M4: PBR skinning + CSM4 — same 80 B vertex layout (attribute locations match), so
            //        BuildPbrSkinningPipeline is reused with the skinning+csm4 shader. ──
            _pbrSkinningCsm4Shader = sg_make_shader(PbrSkinningCsm4.pbr_skinning_csm4_pbr_program_shader_desc(sg_query_backend()));
            for (int i = 0; i < _pbrSkinningCsm4Pipelines.Length; i++)
            {
                var flags = (PipelineFlags)i;
                _pbrSkinningCsm4Pipelines[i] = BuildPbrSkinningPipeline(
                    _pbrSkinningCsm4Shader,
                    (flags & PipelineFlags.AlphaBlend)  != 0,
                    (flags & PipelineFlags.DoubleSided) != 0,
                    (flags & PipelineFlags.OffscreenRt) != 0);
            }

            // ── M4: PBR skinning + CSM1 — same 80 B layout, reuses BuildPbrSkinningPipeline. ──
            _pbrSkinningCsm1Shader = sg_make_shader(PbrSkinningCsm1.pbr_skinning_csm1_pbr_program_shader_desc(sg_query_backend()));
            for (int i = 0; i < _pbrSkinningCsm1Pipelines.Length; i++)
            {
                var flags = (PipelineFlags)i;
                _pbrSkinningCsm1Pipelines[i] = BuildPbrSkinningPipeline(
                    _pbrSkinningCsm1Shader,
                    (flags & PipelineFlags.AlphaBlend)  != 0,
                    (flags & PipelineFlags.DoubleSided) != 0,
                    (flags & PipelineFlags.OffscreenRt) != 0);
            }
        }

        /// <summary>Look up the cached pipeline for the given flag combination.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sg_pipeline GetPipeline(PipelineFlags flags)
            => _pipelines[(int)flags & 0x07];

        /// <summary>Look up the cached PBR (base-variant) pipeline for the given flag combination.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sg_pipeline GetPbrPipeline(PipelineFlags flags)
            => _pbrPipelines[(int)flags & 0x07];

        /// <summary>PBR pipeline that also samples the 1-cascade directional shadow.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sg_pipeline GetPbrCsm1Pipeline(PipelineFlags flags)
            => _pbrCsm1Pipelines[(int)flags & 0x07];

        /// <summary>PBR pipeline that also samples the 4-cascade directional shadow.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sg_pipeline GetPbrCsm4Pipeline(PipelineFlags flags)
            => _pbrCsm4Pipelines[(int)flags & 0x07];

        /// <summary>PBR pipeline with GPU skinning (80 B per-vertex buffer: +joints_0,+weights_0).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sg_pipeline GetPbrSkinningPipeline(PipelineFlags flags)
            => _pbrSkinningPipelines[(int)flags & 0x07];

        /// <summary>GPU-skinning PBR pipeline that also samples the 4-cascade directional shadow.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sg_pipeline GetPbrSkinningCsm4Pipeline(PipelineFlags flags)
            => _pbrSkinningCsm4Pipelines[(int)flags & 0x07];

        /// <summary>GPU-skinning PBR pipeline that also samples the 1-cascade directional shadow.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sg_pipeline GetPbrSkinningCsm1Pipeline(PipelineFlags flags)
            => _pbrSkinningCsm1Pipelines[(int)flags & 0x07];

        /// <summary>The shared shader object (for inspecting reflection).</summary>
        public sg_shader Shader => _shaders[0];

        // ── pipeline builder ─────────────────────────────────────────────────────────

        private static sg_pipeline BuildPipeline(sg_shader shader, bool alpha, bool doubleSide, bool offscreen)
        {
            var cullMode    = doubleSide ? sg_cull_mode.SG_CULLMODE_NONE : sg_cull_mode.SG_CULLMODE_BACK;
            var faceWinding = sg_face_winding.SG_FACEWINDING_CCW; // Sokol default
            Logger.Info($"[ShaderVariantCache] Building pipeline: alpha={alpha} doubleSide={doubleSide} offscreen={offscreen}");
            Logger.Info($"[ShaderVariantCache]   cull_mode={cullMode} face_winding={faceWinding}");
            var desc = new sg_pipeline_desc
            {
                shader       = shader,
                index_type   = sg_index_type.SG_INDEXTYPE_UINT32,
                cull_mode    = cullMode,
                face_winding = faceWinding,
                depth = new sg_depth_state
                {
                    pixel_format  = SG_PIXELFORMAT_DEPTH,
                    compare       = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                    write_enabled = !alpha,   // don't write depth for transparent geometry
                },
                label = $"blinn-phong-pip-{(int)(alpha?PipelineFlags.AlphaBlend:0)}"
            };

            // ── Per-vertex attributes (buffer slot 0) ────────────────────────────────
            // ObjVertex layout: vec3 pos + vec3 normal + vec2 uv + vec4 tangent = 48 B
            desc.layout.buffers[0].stride = 48;
            desc.layout.attrs[ATTR_blinn_phong_in_pos]     = new sg_vertex_attr_state
            {
                buffer_index = 0, offset =  0,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3
            };
            desc.layout.attrs[ATTR_blinn_phong_in_normal]  = new sg_vertex_attr_state
            {
                buffer_index = 0, offset = 12,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3
            };
            desc.layout.attrs[ATTR_blinn_phong_in_uv]      = new sg_vertex_attr_state
            {
                buffer_index = 0, offset = 24,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2
            };
            desc.layout.attrs[ATTR_blinn_phong_in_tangent] = new sg_vertex_attr_state
            {
                buffer_index = 0, offset = 32,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4
            };

            // ── Per-instance attributes (buffer slot 1) ──────────────────────────────
            // InstanceData layout: mat4 model (64 B) + vec4 custom (16 B) = 80 B
            desc.layout.buffers[1].stride    = 80;
            desc.layout.buffers[1].step_func = sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE;

            desc.layout.attrs[ATTR_blinn_phong_in_model_0] = new sg_vertex_attr_state
            {
                buffer_index = 1, offset =  0,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4
            };
            desc.layout.attrs[ATTR_blinn_phong_in_model_1] = new sg_vertex_attr_state
            {
                buffer_index = 1, offset = 16,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4
            };
            desc.layout.attrs[ATTR_blinn_phong_in_model_2] = new sg_vertex_attr_state
            {
                buffer_index = 1, offset = 32,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4
            };
            desc.layout.attrs[ATTR_blinn_phong_in_model_3] = new sg_vertex_attr_state
            {
                buffer_index = 1, offset = 48,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4
            };
            desc.layout.attrs[ATTR_blinn_phong_in_custom]  = new sg_vertex_attr_state
            {
                buffer_index = 1, offset = 64,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4
            };

            // ── Blend state ──────────────────────────────────────────────────────────
            if (alpha)
            {
                desc.colors[0].blend = new sg_blend_state
                {
                    enabled        = true,
                    src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA,
                    dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                    src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE,
                    dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ZERO,
                };
            }

            // ── Pixel format ─────────────────────────────────────────────────────────
            if (offscreen)
            {
                desc.colors[0].pixel_format = SG_PIXELFORMAT_RGBA8;
                desc.depth.pixel_format     = SG_PIXELFORMAT_DEPTH;
                desc.sample_count           = 1;
            }
            else
            {
                // Swapchain: match the real swapchain formats so Sokol validation passes.
                var sc = sglue_swapchain();
                desc.colors[0].pixel_format = sc.color_format;
                desc.depth.pixel_format     = sc.depth_format;
                desc.sample_count           = sc.sample_count;
            }

            return sg_make_pipeline(desc);
        }

        // ── PBR pipeline builder ───────────────────────────────────────────────────────
        // Same per-vertex (buffer 0, 48 B) + per-instance (buffer 1, 80 B) layout as the
        // blinn-phong pipeline; only the shader and the ATTR_ binding constants differ.
        // Kept separate from BuildPipeline because the skinned PBR variants will extend
        // buffer 0 with joints/weights at attribute locations 9-10.
        private static sg_pipeline BuildPbrPipeline(sg_shader shader, bool alpha, bool doubleSide, bool offscreen)
        {
            var cullMode = doubleSide ? sg_cull_mode.SG_CULLMODE_NONE : sg_cull_mode.SG_CULLMODE_BACK;
            var desc = new sg_pipeline_desc
            {
                shader       = shader,
                index_type   = sg_index_type.SG_INDEXTYPE_UINT32,
                cull_mode    = cullMode,
                face_winding = sg_face_winding.SG_FACEWINDING_CCW,
                depth = new sg_depth_state
                {
                    pixel_format  = SG_PIXELFORMAT_DEPTH,
                    compare       = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                    write_enabled = !alpha,
                },
                label = "pbr-pip"
            };

            // Per-vertex attributes (buffer slot 0) — PbrVertex 48 B: pos+normal+uv+tangent.
            desc.layout.buffers[0].stride = 48;
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_position]   = new sg_vertex_attr_state
            { buffer_index = 0, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_normal]     = new sg_vertex_attr_state
            { buffer_index = 0, offset = 12, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_texcoord_0] = new sg_vertex_attr_state
            { buffer_index = 0, offset = 24, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_tangent]    = new sg_vertex_attr_state
            { buffer_index = 0, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };

            // Per-instance attributes (buffer slot 1) — InstanceData 80 B: mat4 model + vec4 custom.
            desc.layout.buffers[1].stride    = 80;
            desc.layout.buffers[1].step_func = sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE;
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_in_model_0] = new sg_vertex_attr_state
            { buffer_index = 1, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_in_model_1] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 16, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_in_model_2] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_in_model_3] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_in_custom]  = new sg_vertex_attr_state
            { buffer_index = 1, offset = 64, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };

            if (alpha)
            {
                desc.colors[0].blend = new sg_blend_state
                {
                    enabled          = true,
                    src_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA,
                    dst_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                    src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE,
                    dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ZERO,
                };
            }

            if (offscreen)
            {
                desc.colors[0].pixel_format = SG_PIXELFORMAT_RGBA8;
                desc.depth.pixel_format     = SG_PIXELFORMAT_DEPTH;
                desc.sample_count           = 1;
            }
            else
            {
                var sc = sglue_swapchain();
                desc.colors[0].pixel_format = sc.color_format;
                desc.depth.pixel_format     = sc.depth_format;
                desc.sample_count           = sc.sample_count;
            }

            return sg_make_pipeline(desc);
        }

        // PBR skinning pipeline — per-vertex buffer 0 is 80 B: the base 48 B (pos+normal+uv+tangent)
        // PLUS joints_0 (vec4 @48) + weights_0 (vec4 @64) at attribute locations 9-10. Per-instance
        // buffer 1 (80 B model+custom) is unchanged. The bone matrices ride in the binding-0 UBO.
        private static sg_pipeline BuildPbrSkinningPipeline(sg_shader shader, bool alpha, bool doubleSide, bool offscreen)
        {
            var cullMode = doubleSide ? sg_cull_mode.SG_CULLMODE_NONE : sg_cull_mode.SG_CULLMODE_BACK;
            var desc = new sg_pipeline_desc
            {
                shader       = shader,
                index_type   = sg_index_type.SG_INDEXTYPE_UINT32,
                cull_mode    = cullMode,
                face_winding = sg_face_winding.SG_FACEWINDING_CCW,
                depth = new sg_depth_state
                {
                    pixel_format  = SG_PIXELFORMAT_DEPTH,
                    compare       = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                    write_enabled = !alpha,
                },
                label = "pbr-skinning-pip"
            };

            // Per-vertex attributes (buffer slot 0) — SkinnedVertex 80 B.
            desc.layout.buffers[0].stride = 80;
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_position]   = new sg_vertex_attr_state
            { buffer_index = 0, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_normal]     = new sg_vertex_attr_state
            { buffer_index = 0, offset = 12, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_texcoord_0] = new sg_vertex_attr_state
            { buffer_index = 0, offset = 24, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_tangent]    = new sg_vertex_attr_state
            { buffer_index = 0, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_joints_0]   = new sg_vertex_attr_state
            { buffer_index = 0, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_weights_0]  = new sg_vertex_attr_state
            { buffer_index = 0, offset = 64, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };

            // Per-instance attributes (buffer slot 1) — InstanceData 80 B: mat4 model + vec4 custom.
            desc.layout.buffers[1].stride    = 80;
            desc.layout.buffers[1].step_func = sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE;
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_in_model_0] = new sg_vertex_attr_state
            { buffer_index = 1, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_in_model_1] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 16, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_in_model_2] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_in_model_3] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_in_custom]  = new sg_vertex_attr_state
            { buffer_index = 1, offset = 64, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };

            if (alpha)
            {
                desc.colors[0].blend = new sg_blend_state
                {
                    enabled          = true,
                    src_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA,
                    dst_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                    src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE,
                    dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ZERO,
                };
            }

            if (offscreen)
            {
                desc.colors[0].pixel_format = SG_PIXELFORMAT_RGBA8;
                desc.depth.pixel_format     = SG_PIXELFORMAT_DEPTH;
                desc.sample_count           = 1;
            }
            else
            {
                var sc = sglue_swapchain();
                desc.colors[0].pixel_format = sc.color_format;
                desc.depth.pixel_format     = sc.depth_format;
                desc.sample_count           = sc.sample_count;
            }

            return sg_make_pipeline(desc);
        }

        // PBR CSM1 pipeline — identical per-vertex (48 B) + per-instance (80 B) layout as the
        // base PBR pipeline; only the shader and the ATTR_ binding constants differ.
        private static sg_pipeline BuildPbrCsm1Pipeline(sg_shader shader, bool alpha, bool doubleSide, bool offscreen)
        {
            var cullMode = doubleSide ? sg_cull_mode.SG_CULLMODE_NONE : sg_cull_mode.SG_CULLMODE_BACK;
            var desc = new sg_pipeline_desc
            {
                shader       = shader,
                index_type   = sg_index_type.SG_INDEXTYPE_UINT32,
                cull_mode    = cullMode,
                face_winding = sg_face_winding.SG_FACEWINDING_CCW,
                depth = new sg_depth_state
                {
                    pixel_format  = SG_PIXELFORMAT_DEPTH,
                    compare       = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                    write_enabled = !alpha,
                },
                label = "pbr-csm1-pip"
            };

            desc.layout.buffers[0].stride = 48;
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_position]   = new sg_vertex_attr_state
            { buffer_index = 0, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_normal]     = new sg_vertex_attr_state
            { buffer_index = 0, offset = 12, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_texcoord_0] = new sg_vertex_attr_state
            { buffer_index = 0, offset = 24, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_tangent]    = new sg_vertex_attr_state
            { buffer_index = 0, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };

            desc.layout.buffers[1].stride    = 80;
            desc.layout.buffers[1].step_func = sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE;
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_in_model_0] = new sg_vertex_attr_state
            { buffer_index = 1, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_in_model_1] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 16, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_in_model_2] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_in_model_3] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_in_custom]  = new sg_vertex_attr_state
            { buffer_index = 1, offset = 64, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };

            if (alpha)
            {
                desc.colors[0].blend = new sg_blend_state
                {
                    enabled          = true,
                    src_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA,
                    dst_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                    src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE,
                    dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ZERO,
                };
            }

            if (offscreen)
            {
                desc.colors[0].pixel_format = SG_PIXELFORMAT_RGBA8;
                desc.depth.pixel_format     = SG_PIXELFORMAT_DEPTH;
                desc.sample_count           = 1;
            }
            else
            {
                var sc = sglue_swapchain();
                desc.colors[0].pixel_format = sc.color_format;
                desc.depth.pixel_format     = sc.depth_format;
                desc.sample_count           = sc.sample_count;
            }

            return sg_make_pipeline(desc);
        }

        // PBR CSM4 pipeline — identical layout to base/CSM1; only the shader + ATTR constants differ.
        private static sg_pipeline BuildPbrCsm4Pipeline(sg_shader shader, bool alpha, bool doubleSide, bool offscreen)
        {
            var cullMode = doubleSide ? sg_cull_mode.SG_CULLMODE_NONE : sg_cull_mode.SG_CULLMODE_BACK;
            var desc = new sg_pipeline_desc
            {
                shader       = shader,
                index_type   = sg_index_type.SG_INDEXTYPE_UINT32,
                cull_mode    = cullMode,
                face_winding = sg_face_winding.SG_FACEWINDING_CCW,
                depth = new sg_depth_state
                {
                    pixel_format  = SG_PIXELFORMAT_DEPTH,
                    compare       = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                    write_enabled = !alpha,
                },
                label = "pbr-csm4-pip"
            };

            desc.layout.buffers[0].stride = 48;
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_position]   = new sg_vertex_attr_state
            { buffer_index = 0, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_normal]     = new sg_vertex_attr_state
            { buffer_index = 0, offset = 12, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_texcoord_0] = new sg_vertex_attr_state
            { buffer_index = 0, offset = 24, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_tangent]    = new sg_vertex_attr_state
            { buffer_index = 0, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };

            desc.layout.buffers[1].stride    = 80;
            desc.layout.buffers[1].step_func = sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE;
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_in_model_0] = new sg_vertex_attr_state
            { buffer_index = 1, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_in_model_1] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 16, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_in_model_2] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_in_model_3] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_in_custom]  = new sg_vertex_attr_state
            { buffer_index = 1, offset = 64, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };

            if (alpha)
            {
                desc.colors[0].blend = new sg_blend_state
                {
                    enabled          = true,
                    src_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA,
                    dst_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                    src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE,
                    dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ZERO,
                };
            }

            if (offscreen)
            {
                desc.colors[0].pixel_format = SG_PIXELFORMAT_RGBA8;
                desc.depth.pixel_format     = SG_PIXELFORMAT_DEPTH;
                desc.sample_count           = 1;
            }
            else
            {
                var sc = sglue_swapchain();
                desc.colors[0].pixel_format = sc.color_format;
                desc.depth.pixel_format     = sc.depth_format;
                desc.sample_count           = sc.sample_count;
            }

            return sg_make_pipeline(desc);
        }

        // ── lifecycle ────────────────────────────────────────────────────────────────

        public void Shutdown()
        {
            // Destroy all pipeline variants.
            for (int i = 0; i < _pipelines.Length; i++)
            {
                if (_pipelines[i].id != 0)
                {
                    sg_destroy_pipeline(_pipelines[i]);
                    _pipelines[i] = default;
                }
            }

            // Destroy PBR pipeline variants + shader.
            for (int i = 0; i < _pbrPipelines.Length; i++)
            {
                if (_pbrPipelines[i].id != 0)
                {
                    sg_destroy_pipeline(_pbrPipelines[i]);
                    _pbrPipelines[i] = default;
                }
            }
            if (_pbrShader.id != 0)
            {
                sg_destroy_shader(_pbrShader);
                _pbrShader = default;
            }

            for (int i = 0; i < _pbrCsm1Pipelines.Length; i++)
            {
                if (_pbrCsm1Pipelines[i].id != 0)
                {
                    sg_destroy_pipeline(_pbrCsm1Pipelines[i]);
                    _pbrCsm1Pipelines[i] = default;
                }
            }
            if (_pbrCsm1Shader.id != 0)
            {
                sg_destroy_shader(_pbrCsm1Shader);
                _pbrCsm1Shader = default;
            }

            for (int i = 0; i < _pbrCsm4Pipelines.Length; i++)
            {
                if (_pbrCsm4Pipelines[i].id != 0)
                {
                    sg_destroy_pipeline(_pbrCsm4Pipelines[i]);
                    _pbrCsm4Pipelines[i] = default;
                }
            }
            if (_pbrCsm4Shader.id != 0)
            {
                sg_destroy_shader(_pbrCsm4Shader);
                _pbrCsm4Shader = default;
            }

            for (int i = 0; i < _pbrSkinningPipelines.Length; i++)
            {
                if (_pbrSkinningPipelines[i].id != 0)
                {
                    sg_destroy_pipeline(_pbrSkinningPipelines[i]);
                    _pbrSkinningPipelines[i] = default;
                }
            }
            if (_pbrSkinningShader.id != 0)
            {
                sg_destroy_shader(_pbrSkinningShader);
                _pbrSkinningShader = default;
            }

            for (int i = 0; i < _pbrSkinningCsm4Pipelines.Length; i++)
            {
                if (_pbrSkinningCsm4Pipelines[i].id != 0)
                {
                    sg_destroy_pipeline(_pbrSkinningCsm4Pipelines[i]);
                    _pbrSkinningCsm4Pipelines[i] = default;
                }
            }
            if (_pbrSkinningCsm4Shader.id != 0)
            {
                sg_destroy_shader(_pbrSkinningCsm4Shader);
                _pbrSkinningCsm4Shader = default;
            }

            for (int i = 0; i < _pbrSkinningCsm1Pipelines.Length; i++)
            {
                if (_pbrSkinningCsm1Pipelines[i].id != 0)
                {
                    sg_destroy_pipeline(_pbrSkinningCsm1Pipelines[i]);
                    _pbrSkinningCsm1Pipelines[i] = default;
                }
            }
            if (_pbrSkinningCsm1Shader.id != 0)
            {
                sg_destroy_shader(_pbrSkinningCsm1Shader);
                _pbrSkinningCsm1Shader = default;
            }

            // Destroy the single shader object (shared across all variants).
            if (_shaders[0].id != 0)
            {
                sg_destroy_shader(_shaders[0]);
                for (int i = 0; i < _shaders.Length; i++)
                    _shaders[i] = default;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Shutdown();
        }
    }
}
