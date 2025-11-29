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
using static Sokol.SImgui;
using static Sokol.SGImgui;
using Imgui;
using static Imgui.ImguiNative;
using static computeboids_sapp_shader_cs.Shaders;

public static unsafe class ComputeboidsApp
{
    const int MAX_PARTICLES = 10000;

    [StructLayout(LayoutKind.Sequential)]
    struct particle_t
    {
        public Vector2 pos;
        public Vector2 vel;
    }

    struct ComputeState
    {
        public sg_buffer[] buf;
        public sg_view[] view;
        public sg_pipeline pip;
    }

    struct DisplayState
    {
        public sg_pipeline pip;
        public sg_pass_action pass_action;
    }

    struct _state
    {
        public sim_params_t sim_params;
        public ComputeState compute;
        public DisplayState display;
        public uint xorshift_state;
    }

    static _state state = new _state();


    static float rnd()
    {
        return Random.Shared.NextSingle() * 2.0f - 1.0f;
    }

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

        simgui_setup(new simgui_desc_t()
        {
            logger = {
                func = &slog_func,
            }
        });

        // Initialize simulation parameters
        state.sim_params = new sim_params_t()
        {
            dt = 0.04f,
            rule1_distance = 0.1f,
            rule2_distance = 0.025f,
            rule3_distance = 0.025f,
            rule1_scale = 0.02f,
            rule2_scale = 0.05f,
            rule3_scale = 0.005f,
            num_particles = 1500,
        };

        // Initialize display state
        state.display = new DisplayState();
        state.display.pass_action = default;
        state.display.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.display.pass_action.colors[0].clear_value = new sg_color { r = 0.0f, g = 0.15f, b = 0.3f, a = 1.0f };

        // Two storage buffers and views with pre-initialized positions and velocities
        state.compute = new ComputeState();
        state.compute.buf = new sg_buffer[2];
        state.compute.view = new sg_view[2];

        particle_t[] initial_data = new particle_t[MAX_PARTICLES];
        for (int i = 0; i < MAX_PARTICLES; i++)
        {
            initial_data[i] = new particle_t()
            {
                pos = new Vector2(rnd(), rnd()),
                vel = new Vector2(rnd() * 0.1f, rnd() * 0.1f),
            };
        }

        for (int i = 0; i < 2; i++)
        {
            state.compute.buf[i] = sg_make_buffer(new sg_buffer_desc()
            {
                usage = new sg_buffer_usage { storage_buffer = true },
                data = SG_RANGE(initial_data),
                label = (i == 0) ? "particle-buffer-0" : "particle-buffer-1",
            });

            state.compute.view[i] = sg_make_view(new sg_view_desc()
            {
                storage_buffer = new sg_buffer_view_desc { buffer = state.compute.buf[i] },
                label = (i == 0) ? "particle-view-0" : "particle-view-1",
            });
        }

        // Compute shader and pipeline
        state.compute.pip = sg_make_pipeline(new sg_pipeline_desc()
        {
            compute = true,
            shader = sg_make_shader(compute_shader_desc(sg_query_backend())),
            label = "compute-pipeline",
        });

        // Render pipeline and shader for displaying boids
        state.display.pip = sg_make_pipeline(new sg_pipeline_desc()
        {
            shader = sg_make_shader(display_shader_desc(sg_query_backend())),
            label = "render-pipeline",
        });
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        draw_ui();

        // Input and output storage buffers for this frame (ping-pong)
        sg_view in_view = state.compute.view[sapp_frame_count() & 1];
        sg_view out_view = state.compute.view[(sapp_frame_count() + 1) & 1];

        // Compute pass to update boid positions and velocities
        sg_begin_pass(new sg_pass() 
        { 
            compute = true, 
            label = "compute-pass" 
        });
        
        sg_apply_pipeline(state.compute.pip);
        var bindings = new sg_bindings();
        bindings.views[VIEW_cs_ssbo_in] = in_view;
        bindings.views[VIEW_cs_ssbo_out] = out_view;
        sg_apply_bindings(bindings);
        sg_apply_uniforms(UB_sim_params, SG_RANGE<sim_params_t>(ref state.sim_params));
        sg_dispatch((state.sim_params.num_particles + 63) / 64, 1, 1);
        sg_end_pass();

        // Render pass for displaying the boids
        sg_begin_pass(new sg_pass()
        {
            action = state.display.pass_action,
            swapchain = sglue_swapchain(),
        });
        
        sg_apply_pipeline(state.display.pip);
        var displayBindings = new sg_bindings();
        displayBindings.views[VIEW_vs_ssbo] = out_view;
        sg_apply_bindings(displayBindings);
        sg_draw(0, 3, (uint)state.sim_params.num_particles);
        simgui_render();
        sg_end_pass();
        sg_commit();
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        simgui_handle_event(*e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        simgui_shutdown();
        sg_shutdown();

        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
        }
    }

    static unsafe void draw_ui()
    {
        simgui_new_frame(new simgui_frame_desc_t()
        {
            width = sapp_width(),
            height = sapp_height(),
            delta_time = sapp_frame_duration(),
            dpi_scale = 1//sapp_dpi_scale(),
        });

        igSetNextWindowBgAlpha(0.8f);
        igSetNextWindowPos(new Vector2(10, 30), ImGuiCond.Once, Vector2.Zero);
        
        const ImGuiWindowFlags flags =
            ImGuiWindowFlags.NoDecoration |
            ImGuiWindowFlags.AlwaysAutoResize |
            ImGuiWindowFlags.NoBringToFrontOnFocus |
            ImGuiWindowFlags.NoFocusOnAppearing;

        byte open = 1;
        if (igBegin("controls", ref open, flags))
        {
            igSliderFloat("Delta T", ref state.sim_params.dt, 0.01f, 0.1f, "%.3f", ImGuiSliderFlags.None);
            igSliderFloat("Rule1 Distance", ref state.sim_params.rule1_distance, 0.0f, 0.2f, "%.3f", ImGuiSliderFlags.None);
            igSliderFloat("Rule2 Distance", ref state.sim_params.rule2_distance, 0.0f, 0.1f, "%.3f", ImGuiSliderFlags.None);
            igSliderFloat("Rule3 Distance", ref state.sim_params.rule3_distance, 0.0f, 0.1f, "%.3f", ImGuiSliderFlags.None);
            igSliderFloat("Rule1 Scale", ref state.sim_params.rule1_scale, 0.0f, 0.1f, "%.3f", ImGuiSliderFlags.None);
            igSliderFloat("Rule2 Scale", ref state.sim_params.rule2_scale, 0.0f, 0.1f, "%.3f", ImGuiSliderFlags.None);
            igSliderFloat("Rule3 Scale", ref state.sim_params.rule3_scale, 0.0f, 0.1f, "%.3f", ImGuiSliderFlags.None);
            igSliderInt("Num Boids", ref state.sim_params.num_particles, 0, MAX_PARTICLES, "%d", ImGuiSliderFlags.None);
        }
        igEnd();
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb = &Init,
            frame_cb = &Frame,
            event_cb = &Event,
            cleanup_cb = &Cleanup,
            width = 800,
            height = 600,
            sample_count = 4,
            window_title = "computeboids (sokol-app)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }
}
