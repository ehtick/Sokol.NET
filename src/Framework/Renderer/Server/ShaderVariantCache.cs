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
using static blinn_phong_shader_cs.Shaders;
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
        }

        /// <summary>Look up the cached pipeline for the given flag combination.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sg_pipeline GetPipeline(PipelineFlags flags)
            => _pipelines[(int)flags & 0x07];

        /// <summary>The shared shader object (for inspecting reflection).</summary>
        public sg_shader Shader => _shaders[0];

        // ── pipeline builder ─────────────────────────────────────────────────────────

        private static sg_pipeline BuildPipeline(sg_shader shader, bool alpha, bool doubleSide, bool offscreen)
        {
            var desc = new sg_pipeline_desc
            {
                shader     = shader,
                index_type = sg_index_type.SG_INDEXTYPE_UINT32,
                cull_mode  = doubleSide
                             ? sg_cull_mode.SG_CULLMODE_NONE
                             : sg_cull_mode.SG_CULLMODE_BACK,
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
                // Swapchain — use the same settings as SceneRenderer.
                desc.colors[0].pixel_format = SG_PIXELFORMAT_RGBA8;
                desc.depth.pixel_format     = SG_PIXELFORMAT_DEPTH;
                desc.sample_count           = 1;
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
