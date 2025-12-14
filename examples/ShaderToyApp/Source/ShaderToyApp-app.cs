using System; 
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_load_action;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SImgui;
using Imgui;
using static Imgui.ImguiNative;

public static unsafe class ShadertoyappApp
{

    enum ShaderType
    {
        Raymarching = 0,
        ProceduralOcean = 1,
        StormyTorus = 2,
        UniverseBall = 3,
        FractalLand = 4,
        FractalPyramid = 5,
        Gemmarium = 6,
    }

    struct State
    {
        public sg_pipeline[] pipelines;
        public sg_bindings bind;
        public sg_pass_action pass_action;
        public raymarching_shader_cs.Shaders.vs_params_t vs_params;
        public raymarching_shader_cs.Shaders.fs_params_t fs_params;
        public Vector2 mouse_pos;
        public bool mouse_down;
        public int current_shader;
        
        // Touch state for mobile
        public float last_touch_x;
        public float last_touch_y;
        public bool touch_active;
    }

    static State state = default;
    static readonly string[] shader_names = { "Raymarching Primitives", "Procedural Ocean", "Stormy Torus", "Universe Ball", "Fractal Land", "Fractal Pyramid", "Gemmarium" };

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

        // a vertex buffer to render a fullscreen quad (2 triangles)
        // Two triangles forming a quad from (-1,-1) to (1,1)
        float[] fsq_verts = { 
            -1.0f, -1.0f,  // bottom-left
             1.0f, -1.0f,  // bottom-right
             1.0f,  1.0f,  // top-right
            -1.0f, -1.0f,  // bottom-left
             1.0f,  1.0f,  // top-right
            -1.0f,  1.0f   // top-left
        };
        state.bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE(fsq_verts),
            label = "fsq vertices"
        });

        // Create pipelines for all shaders
        state.pipelines = new sg_pipeline[7];
        
        // Raymarching shader
        sg_pipeline_desc desc0 = default;
        desc0.layout.attrs[raymarching_shader_cs.Shaders.ATTR_shadertoy_position].format = SG_VERTEXFORMAT_FLOAT2;
        desc0.shader = sg_make_shader(raymarching_shader_cs.Shaders.shadertoy_shader_desc(sg_query_backend()));
        state.pipelines[0] = sg_make_pipeline(desc0);
        
        // Procedural Ocean shader
        sg_pipeline_desc desc1 = default;
        desc1.layout.attrs[proceduralocean_shader_cs.Shaders.ATTR_proceduralocean_position].format = SG_VERTEXFORMAT_FLOAT2;
        desc1.shader = sg_make_shader(proceduralocean_shader_cs.Shaders.proceduralocean_shader_desc(sg_query_backend()));
        state.pipelines[1] = sg_make_pipeline(desc1);
        
        // Stormy Torus shader
        sg_pipeline_desc desc2 = default;
        desc2.layout.attrs[stormytorus_shader_cs.Shaders.ATTR_stormytorus_position].format = SG_VERTEXFORMAT_FLOAT2;
        desc2.shader = sg_make_shader(stormytorus_shader_cs.Shaders.stormytorus_shader_desc(sg_query_backend()));
        state.pipelines[2] = sg_make_pipeline(desc2);
        
        // Universe Ball shader
        sg_pipeline_desc desc3 = default;
        desc3.layout.attrs[universeball_shader_cs.Shaders.ATTR_universeball_position].format = SG_VERTEXFORMAT_FLOAT2;
        desc3.shader = sg_make_shader(universeball_shader_cs.Shaders.universeball_shader_desc(sg_query_backend()));
        state.pipelines[3] = sg_make_pipeline(desc3);
        
        // Fractal Land shader
        sg_pipeline_desc desc4 = default;
        desc4.layout.attrs[fractalland_shader_cs.Shaders.ATTR_fractalland_position].format = SG_VERTEXFORMAT_FLOAT2;
        desc4.shader = sg_make_shader(fractalland_shader_cs.Shaders.fractalland_shader_desc(sg_query_backend()));
        state.pipelines[4] = sg_make_pipeline(desc4);
        
        // Fractal Pyramid shader
        sg_pipeline_desc desc5 = default;
        desc5.layout.attrs[fractalpyramid_shader_cs.Shaders.ATTR_fractalpyramid_position].format = SG_VERTEXFORMAT_FLOAT2;
        desc5.shader = sg_make_shader(fractalpyramid_shader_cs.Shaders.fractalpyramid_shader_desc(sg_query_backend()));
        state.pipelines[5] = sg_make_pipeline(desc5);
        
        // Gemmarium shader
        sg_pipeline_desc desc6 = default;
        desc6.layout.attrs[gemmarium_shader_cs.Shaders.ATTR_gemmarium_position].format = SG_VERTEXFORMAT_FLOAT2;
        desc6.shader = sg_make_shader(gemmarium_shader_cs.Shaders.gemmarium_shader_desc(sg_query_backend()));
        state.pipelines[6] = sg_make_pipeline(desc6);

        state.pass_action = default;
        state.pass_action.colors[0].load_action = SG_LOADACTION_DONTCARE;

        state.mouse_pos = Vector2.Zero;
        state.mouse_down = false;
        state.touch_active = false;
        state.current_shader = 0;
    }

    [UnmanagedCallersOnly]
    private static void Frame()
    {
        int w = sapp_width();
        int h = sapp_height();

        draw_ui();

  
            // Update vertex shader uniforms
            state.vs_params.time += (float)sapp_frame_duration();
            state.vs_params.aspect = (float)w / (float)h;

            // Update fragment shader uniforms
            state.fs_params.iResolution[0] = (float)w;
            state.fs_params.iResolution[1] = (float)h;
            state.fs_params.iTime = state.vs_params.time;
            state.fs_params.iMouse[0] = state.mouse_down ? state.mouse_pos.X : 0.0f;
            state.fs_params.iMouse[1] = state.mouse_down ? state.mouse_pos.Y : 0.0f;
            state.fs_params.iMouse[2] = state.mouse_down ? 1.0f : 0.0f;
            state.fs_params.iMouse[3] = 0.0f;
        

        sg_begin_pass(new sg_pass() { action = state.pass_action, swapchain = sglue_swapchain() });
        sg_apply_pipeline(state.pipelines[state.current_shader]);
        sg_apply_bindings(state.bind);
        
        // Apply uniforms based on current shader (all use same uniform layout)
        sg_apply_uniforms(raymarching_shader_cs.Shaders.UB_vs_params, SG_RANGE(ref state.vs_params));
        sg_apply_uniforms(raymarching_shader_cs.Shaders.UB_fs_params, SG_RANGE(ref state.fs_params));
        
        sg_draw(0, 6, 1);
        simgui_render();
        sg_end_pass();
        sg_commit();
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        if (simgui_handle_event(*e)) {
            return;
        }
        
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN)
        {
            state.mouse_down = true;
            state.mouse_pos = new Vector2(e->mouse_x, e->mouse_y);
        }
        else if (e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP)
        {
            state.mouse_down = false;
        }
        else if (e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE)
        {
            if (state.mouse_down)
            {
                state.mouse_pos = new Vector2(e->mouse_x, e->mouse_y);
            }
        }
        // Touch events for mobile
        else if (e->type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN)
        {
            if (e->num_touches >= 1)
            {
                // Single-finger touch simulates mouse down
                state.last_touch_x = e->touches[0].pos_x;
                state.last_touch_y = e->touches[0].pos_y;
                state.touch_active = true;
                state.mouse_down = true;
                state.mouse_pos = new Vector2(e->touches[0].pos_x, e->touches[0].pos_y);
            }
        }
        else if (e->type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED)
        {
            state.touch_active = false;
            state.mouse_down = false;
        }
        else if (e->type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_MOVED)
        {
            if (e->num_touches >= 1 && state.touch_active)
            {
                float current_x = e->touches[0].pos_x;
                float current_y = e->touches[0].pos_y;
                
                float dx = current_x - state.last_touch_x;
                float dy = current_y - state.last_touch_y;
                
                // Discontinuity detection (similar to Camera.cs)
                float delta_magnitude = (float)Math.Sqrt(dx * dx + dy * dy);
                if (delta_magnitude > 50.0f)
                {
                    // Skip large jumps (likely discontinuity)
                    state.last_touch_x = current_x;
                    state.last_touch_y = current_y;
                    return;
                }
                
                // Update mouse position to simulate mouse drag
                state.mouse_pos = new Vector2(current_x, current_y);
                
                state.last_touch_x = current_x;
                state.last_touch_y = current_y;
            }
        }
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
            dpi_scale = 1
        });

        igSetNextWindowPos(new Vector2(30, 30), ImGuiCond.Once, Vector2.Zero);
        igSetNextWindowBgAlpha(0.75f);
        byte open = 1;
        if (igBegin("ShaderToy Gallery", ref open, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize))
        {
            igText("Shader Selection");
            igSeparator();
            
            // Use buttons for previous/next navigation
            if (igButton("<- Prev", Vector2.Zero))
            {
                state.current_shader = (state.current_shader - 1 + shader_names.Length) % shader_names.Length;
            }
            igSameLine(0, 10);
            igText($"{shader_names[state.current_shader]}");
            igSameLine(0, 10);
            if (igButton("Next ->", Vector2.Zero))
            {
                state.current_shader = (state.current_shader + 1) % shader_names.Length;
            }
            
            igSeparator();
            igText($"FPS: {1.0f / sapp_frame_duration():F1}");
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
            sample_count = 1,
            window_title = "ShaderToy Gallery",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }
}
