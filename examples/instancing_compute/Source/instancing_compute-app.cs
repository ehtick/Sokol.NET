using System;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static instancing_compute_sapp_shader_cs.Shaders;

public static unsafe class InstancingComputeApp
{
    const int MAX_PARTICLES = 512 * 1024;
    const int NUM_PARTICLES_EMITTED_PER_FRAME = 10;

    struct State
    {
        public int num_particles;
        public float ry;
        public sg_buffer buf;
        public ComputeState compute;
        public DisplayState display;
    }

    struct ComputeState
    {
        public sg_view sbuf_view;
        public sg_pipeline pip;
    }

    struct DisplayState
    {
        public sg_buffer vbuf;
        public sg_buffer ibuf;
        public sg_pipeline pip;
        public sg_pass_action pass_action;
    }

    static State state = new State();


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = {
                func = &slog_func,
            }
        });

        // Initialize display pass action
        state.display.pass_action = default;
        state.display.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.display.pass_action.colors[0].clear_value = new sg_color { r = 0.0f, g = 0.2f, b = 0.1f, a = 1.0f };

        // Create an uninitialized storage buffer for the particle state,
        // this will be initialized and updated by compute shaders and then
        // used as vertex buffer to provide per-instance data
        state.buf = sg_make_buffer(new sg_buffer_desc()
        {
            size = (nuint)(MAX_PARTICLES * Marshal.SizeOf<particle_t>()),
            usage = new sg_buffer_usage
            {
                vertex_buffer = true,
                storage_buffer = true,
            },
            label = "particle-buffer",
        });

        // Create a storage-buffer-view on the buffer
        state.compute.sbuf_view = sg_make_view(new sg_view_desc()
        {
            storage_buffer = new sg_buffer_view_desc { buffer = state.buf },
            label = "particle-buffer-view",
        });

        // A compute shader and pipeline object for updating particle positions
        state.compute.pip = sg_make_pipeline(new sg_pipeline_desc()
        {
            compute = true,
            shader = sg_make_shader(update_shader_desc(sg_query_backend())),
            label = "update-pipeline",
        });

        // Vertex and index buffer for the particle geometry
        const float r = 0.05f;
        float[] vertices = {
            // positions            colors
            0.0f,   -r, 0.0f,       1.0f, 0.0f, 0.0f, 1.0f,
               r, 0.0f, r,          0.0f, 1.0f, 0.0f, 1.0f,
               r, 0.0f, -r,         0.0f, 0.0f, 1.0f, 1.0f,
              -r, 0.0f, -r,         1.0f, 1.0f, 0.0f, 1.0f,
              -r, 0.0f, r,          0.0f, 1.0f, 1.0f, 1.0f,
            0.0f,    r, 0.0f,       1.0f, 0.0f, 1.0f, 1.0f
        };
        ushort[] indices = {
            0, 1, 2,    0, 2, 3,    0, 3, 4,    0, 4, 1,
            5, 1, 2,    5, 2, 3,    5, 3, 4,    5, 4, 1
        };

        state.display.vbuf = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE(vertices),
            label = "geometry-vbuf",
        });

        state.display.ibuf = sg_make_buffer(new sg_buffer_desc()
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data = SG_RANGE(indices),
            label = "geometry-ibuf",
        });

        // Shader and pipeline for rendering the particles, this uses
        // the compute-updated storage buffer to provide the particle positions
        var pipeline_desc = new sg_pipeline_desc
        {
            shader = sg_make_shader(display_shader_desc(sg_query_backend())),
            index_type = sg_index_type.SG_INDEXTYPE_UINT16,
            depth = new sg_depth_state
            {
                compare = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                write_enabled = true,
            },
            cull_mode = sg_cull_mode.SG_CULLMODE_BACK,
            label = "render-pipeline",
        };
        
        // Configure buffer 1 for per-instance data
        pipeline_desc.layout.buffers[1].step_func = sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE;
        pipeline_desc.layout.buffers[1].stride = (int)Marshal.SizeOf<particle_t>();
        
        // Configure vertex attributes
        pipeline_desc.layout.attrs[ATTR_display_pos] = new sg_vertex_attr_state { format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
        pipeline_desc.layout.attrs[ATTR_display_color0] = new sg_vertex_attr_state { format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
        pipeline_desc.layout.attrs[ATTR_display_inst_pos] = new sg_vertex_attr_state { format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4, buffer_index = 1 };
        
        state.display.pip = sg_make_pipeline(pipeline_desc);

        // One-time init of particle velocities in a compute shader
        sg_pipeline init_pip = sg_make_pipeline(new sg_pipeline_desc()
        {
            compute = true,
            shader = sg_make_shader(init_shader_desc(sg_query_backend())),
        });

        sg_begin_pass(new sg_pass() { compute = true });
        sg_apply_pipeline(init_pip);
        var init_bindings = new sg_bindings();
        init_bindings.views[VIEW_cs_ssbo] = state.compute.sbuf_view;
        sg_apply_bindings(init_bindings);
        sg_dispatch(MAX_PARTICLES / 64, 1, 1);
        sg_end_pass();
        sg_destroy_pipeline(init_pip);
    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        state.num_particles += NUM_PARTICLES_EMITTED_PER_FRAME;
        if (state.num_particles > MAX_PARTICLES)
        {
            state.num_particles = MAX_PARTICLES;
        }
        float dt = (float)sapp_frame_duration();

        // Compute pass to update particle positions
        cs_params_t cs_params = new cs_params_t
        {
            dt = dt,
            num_particles = state.num_particles,
        };

        sg_begin_pass(new sg_pass() { compute = true, label = "compute-pass" });
        sg_apply_pipeline(state.compute.pip);
        var compute_bindings = new sg_bindings();
        compute_bindings.views[VIEW_cs_ssbo] = state.compute.sbuf_view;
        sg_apply_bindings(compute_bindings);
        sg_apply_uniforms(UB_cs_params, SG_RANGE<cs_params_t>(ref cs_params));
        sg_dispatch((state.num_particles + 63) / 64, 1, 1);
        sg_end_pass();

        // Render pass to render the particles via hardware instancing,
        // the per-instance positions are provided by the storage buffer
        // bound as vertex buffer at slot 1
        vs_params_t vs_params = compute_vsparams(dt);

        sg_begin_pass(new sg_pass()
        {
            action = state.display.pass_action,
            swapchain = sglue_swapchain(),
            label = "render-pass",
        });
        sg_apply_pipeline(state.display.pip);
        var display_bindings = new sg_bindings();
        display_bindings.vertex_buffers[0] = state.display.vbuf;
        display_bindings.vertex_buffers[1] = state.buf;
        display_bindings.index_buffer = state.display.ibuf;
        sg_apply_bindings(display_bindings);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
        sg_draw(0, 24, (uint)state.num_particles);
        sg_end_pass();
        sg_commit();
    }


    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        sg_shutdown();

        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
        }
    }

    static vs_params_t compute_vsparams(float frame_time)
    {
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(
            (float)(60.0 * Math.PI / 180.0),
            sapp_widthf() / sapp_heightf(),
            0.01f,
            50.0f
        );
        Matrix4x4 view = Matrix4x4.CreateLookAt(
            new Vector3(0.0f, 1.5f, 8.0f),
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f)
        );
        Matrix4x4 view_proj = view * proj;
        state.ry += 60.0f * frame_time;

        return new vs_params_t
        {
            mvp = Matrix4x4.CreateRotationY((float)(state.ry * Math.PI / 180.0)) * view_proj,
        };
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb = &Init,
            frame_cb = &Frame,
            cleanup_cb = &Cleanup,
            width = 800,
            height = 600,
            sample_count = 4,
            window_title = "instancing-compute (sokol-app)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}
