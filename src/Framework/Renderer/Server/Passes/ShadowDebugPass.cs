using static Sokol.SG;
using static Sokol.Utils;
using static shadow_debug_shader_cs.Shaders;

namespace GameEditor.Framework.Renderer.Server.Passes
{
    /// <summary>
    /// Renders a shadow atlas slice as a greyscale overlay quad.
    /// Must be called inside an active <c>sg_begin_pass / sg_end_pass</c>.
    /// </summary>
    internal sealed class ShadowDebugPass
    {
        private sg_shader   _shader;
        private sg_pipeline _pipeline;
        private sg_buffer   _quad;
        private sg_sampler  _sampler;

        // [0,1]×[0,1] quad, triangle-strip order.
        private static readonly float[] QuadVerts = { 0f, 0f,  1f, 0f,  0f, 1f,  1f, 1f };

        public void Init()
        {
            // Vertex buffer
            _quad = sg_make_buffer(new sg_buffer_desc
            {
                data  = SG_RANGE(QuadVerts),
                label = "shadow-debug-quad",
            });

            // Comparison sampler (LESS_EQUAL — same convention as the main shadow atlas sampler)
            _sampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter = sg_filter.SG_FILTER_NEAREST,
                mag_filter = sg_filter.SG_FILTER_NEAREST,
                compare    = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                wrap_u     = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                wrap_v     = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                label      = "shadow-debug-sampler",
            });

            // Shader
            _shader = sg_make_shader(shadow_debug_shader_desc(sg_query_backend()));

            // Pipeline: overlay quad, no depth test, RGBA8 to match the scene offscreen RT
            _pipeline = sg_make_pipeline(new sg_pipeline_desc
            {
                shader        = _shader,
                primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLE_STRIP,
                layout = {
                    attrs = {
                        [ATTR_shadow_debug_in_pos] = new sg_vertex_attr_state
                        {
                            format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2,
                        },
                    },
                },
                colors = {
                    [0] = new sg_color_target_state
                    {
                        pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                    },
                },
                depth = new sg_depth_state
                {
                    // No depth test — always draw on top.
                    compare      = sg_compare_func.SG_COMPAREFUNC_ALWAYS,
                    write_enabled = false,
                    pixel_format  = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
                },
                label = "shadow-debug-pipeline",
            });
        }

        public void Shutdown()
        {
            if (_pipeline.id != 0) { sg_destroy_pipeline(_pipeline); _pipeline = default; }
            if (_shader.id != 0)   { sg_destroy_shader(_shader);     _shader   = default; }
            if (_quad.id != 0)     { sg_destroy_buffer(_quad);       _quad     = default; }
            if (_sampler.id != 0)  { sg_destroy_sampler(_sampler);   _sampler  = default; }
        }

        /// <summary>
        /// Draws the specified atlas slice into a pixel-space rectangle inside the current pass.
        /// Restores the full viewport afterwards.
        /// </summary>
        /// <param name="atlasView">The shadow atlas <c>sg_view</c> (full array, for sampling).</param>
        /// <param name="sliceIndex">Array layer to visualise (0–11).</param>
        /// <param name="x">Left edge in render-target pixels (origin at top-left).</param>
        /// <param name="y">Top edge in render-target pixels (origin at top-left).</param>
        /// <param name="size">Width and height of the overlay square in pixels.</param>
        /// <param name="rtWidth">Full render-target width (to restore viewport).</param>
        /// <param name="rtHeight">Full render-target height (to restore viewport).</param>
        public void Draw(sg_view atlasView, int sliceIndex, int x, int y, int size,
                         int rtWidth, int rtHeight)
        {
            if (_pipeline.id == 0) return;

            sg_apply_viewport(x, y, size, size, true);
            sg_apply_scissor_rect(x, y, size, size, true);

            sg_apply_pipeline(_pipeline);

            var fsParams = new shadow_debug_fs_params_t
            {
                slice = sliceIndex,
                ref_z = 0.9999f,
            };
            sg_apply_uniforms(UB_shadow_debug_fs_params, SG_RANGE(ref fsParams));

            sg_apply_bindings(new sg_bindings
            {
                vertex_buffers = { [0] = _quad },
                views          = { [VIEW_shadow_atlas] = atlasView },
                samplers       = { [SMP_shadow_smp]    = _sampler  },
            });

            sg_draw(0, 4, 1);

            // Restore full viewport.
            sg_apply_viewport(0, 0, rtWidth, rtHeight, true);
            sg_apply_scissor_rect(0, 0, rtWidth, rtHeight, true);
        }
    }
}
