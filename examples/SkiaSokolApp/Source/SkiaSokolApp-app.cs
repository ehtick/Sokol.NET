using System;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static System.Numerics.Matrix4x4;
using static Sokol.SDebugUI;
using static skia_shader_cs.Shaders;
using SkiaSharp;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static Sokol.SImgui;
using Imgui;
using static Imgui.ImguiNative;
using SkiaSharpSample.Samples;

using ImTextureID = ulong;
public static unsafe partial class SkiaSokolApp
{
    struct _state
    {
        public sg_pass_action pass_action;
        public sg_pipeline pip;
        public sg_image img;
        public sg_bindings bind;
        public float rx, ry;

        public Bitmap bitmap;
        public int current_sample;
        public float fps;
    }

    static _state state = new _state();
    static SampleBase[] samples = null!;
    static SampleBase? currentSample = null;

    static Task? drawTask;
    static volatile bool textureNeedsUpdate = false;
    static object drawLock = new object();

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

        var width = SApp.sapp_width();
        var height = SApp.sapp_height();

        state.bitmap = new Bitmap(1024, 1024);

        // cube vertex buffer with normals
        float[] vertices = {
        // pos                       uvs               normals
        // Front face (z = -1)
        -1.0f, -1.0f, -1.0f,         0.0f, 0.0f,      0.0f, 0.0f, -1.0f,
         1.0f, -1.0f, -1.0f,         1.0f, 0.0f,      0.0f, 0.0f, -1.0f,
         1.0f,  1.0f, -1.0f,         1.0f, 1.0f,      0.0f, 0.0f, -1.0f,
        -1.0f,  1.0f, -1.0f,         0.0f, 1.0f,      0.0f, 0.0f, -1.0f,

        // Back face (z = 1)
        -1.0f, -1.0f,  1.0f,         0.0f, 0.0f,      0.0f, 0.0f, 1.0f,
         1.0f, -1.0f,  1.0f,         1.0f, 0.0f,      0.0f, 0.0f, 1.0f,
         1.0f,  1.0f,  1.0f,         1.0f, 1.0f,      0.0f, 0.0f, 1.0f,
        -1.0f,  1.0f,  1.0f,         0.0f, 1.0f,      0.0f, 0.0f, 1.0f,

        // Left face (x = -1)
        -1.0f, -1.0f, -1.0f,         0.0f, 0.0f,      -1.0f, 0.0f, 0.0f,
        -1.0f,  1.0f, -1.0f,         1.0f, 0.0f,      -1.0f, 0.0f, 0.0f,
        -1.0f,  1.0f,  1.0f,         1.0f, 1.0f,      -1.0f, 0.0f, 0.0f,
        -1.0f, -1.0f,  1.0f,         0.0f, 1.0f,      -1.0f, 0.0f, 0.0f,

        // Right face (x = 1)
         1.0f, -1.0f, -1.0f,         0.0f, 0.0f,      1.0f, 0.0f, 0.0f,
         1.0f,  1.0f, -1.0f,         1.0f, 0.0f,      1.0f, 0.0f, 0.0f,
         1.0f,  1.0f,  1.0f,         1.0f, 1.0f,      1.0f, 0.0f, 0.0f,
         1.0f, -1.0f,  1.0f,         0.0f, 1.0f,      1.0f, 0.0f, 0.0f,

        // Bottom face (y = -1)
        -1.0f, -1.0f, -1.0f,         0.0f, 0.0f,      0.0f, -1.0f, 0.0f,
        -1.0f, -1.0f,  1.0f,         1.0f, 0.0f,      0.0f, -1.0f, 0.0f,
         1.0f, -1.0f,  1.0f,         1.0f, 1.0f,      0.0f, -1.0f, 0.0f,
         1.0f, -1.0f, -1.0f,         0.0f, 1.0f,      0.0f, -1.0f, 0.0f,

        // Top face (y = 1)
        -1.0f,  1.0f, -1.0f,         0.0f, 0.0f,      0.0f, 1.0f, 0.0f,
        -1.0f,  1.0f,  1.0f,         1.0f, 0.0f,      0.0f, 1.0f, 0.0f,
         1.0f,  1.0f,  1.0f,         1.0f, 1.0f,      0.0f, 1.0f, 0.0f,
         1.0f,  1.0f, -1.0f,         0.0f, 1.0f,      0.0f, 1.0f, 0.0f
    };


        UInt16[] indices = {
        0, 1, 2,  0, 2, 3,
        6, 5, 4,  7, 6, 4,
        8, 9, 10,  8, 10, 11,
        14, 13, 12,  15, 14, 12,
        16, 17, 18,  16, 18, 19,
        22, 21, 20,  23, 22, 20
    };

        sg_buffer vbuf = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE(vertices),
            label = "cube-vertices"
        });

        sg_buffer ibuf = sg_make_buffer(new sg_buffer_desc()
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data = SG_RANGE(indices),
            label = "cube-indices"
        });


        sg_shader shd = sg_make_shader(skia_shader_desc(sg_query_backend()));

        var pipeline_desc = default(sg_pipeline_desc);
        pipeline_desc.layout.attrs[ATTR_skia_position].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[ATTR_skia_texcoord0].format = SG_VERTEXFORMAT_FLOAT2;
        pipeline_desc.layout.attrs[ATTR_skia_normal].format = SG_VERTEXFORMAT_FLOAT3;

        pipeline_desc.shader = shd;
        pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
        pipeline_desc.cull_mode = SG_CULLMODE_BACK;
        pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pipeline_desc.depth.write_enabled = true;
        pipeline_desc.label = "cube-pipeline";
        state.pip = sg_make_pipeline(pipeline_desc);

        state.bind = new sg_bindings();
        state.bind.vertex_buffers[0] = vbuf;
        state.bind.index_buffer = ibuf;
        state.bind.views[VIEW_tex] =  state.bitmap.SokolTexture.View;
        state.bind.samplers[SMP_smp] =  state.bitmap.SokolTexture.Sampler;

        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.5f, b = 0.75f, a = 1.0f };
        
        // Initialize samples
        //On Web , every Sample is drawn once by default to avoid unnecessary redraws , if you want continuous redraw set IsDrawOnce = false
        samples = new SampleBase[]
        {
            new SkiaSampleShapes(){ IsDrawOnce = false },
            new BitmapAnnotationSample(),
            new BitmapDecoderSample(),
            new BitmapLatticeSample(),
            new BitmapShaderSample(),
            new BitmapSubsetDecoderSample(),
            new BlurImageFilterSample(),
            new BlurMaskFilterSample(),
            new ChainedImageFilterSample(),
            new ColorMatrixColorFilterSample(),
            new ColorTableColorFilterSample(),
            new ComposeShaderSample(),
            new CustomFontsSample(),
            new DecodeGifFramesSample(){ IsDrawOnce = false },
            new DilateImageFilterSample(),
            new DngDecoderSample(),
            new DrawMatrixSample(),
            new DrawVerticesSample(),
            new ErodeImageFilterSample(),
            new FilledHeptagramSample(),
            new FractalPerlinNoiseShaderSample(),
            new GradientSample(),
            new HighContrastColorFilterSample(),
            new LumaColorFilterSample(),
            new MagnifierImageFilterSample(),
            new ManipulatedBitmapShaderSample(),
            new MeasureTextSample(),
            new PathBoundsSample(),
            new PathConicToQuadsSample(),
            new PathEffect2DPathSample(),
            new PathEffectsSample(),
            new PathMeasureSample(),
            new SkottieSample(){ IsDrawOnce = false },
            new SweepGradientShaderSample(),
            new TextOnPathSample(){ IsDrawOnce = false },
            new TextSample(),
            new TextShapingSample(),
            new ThreeDSample(){ IsDrawOnce = false },
            new ThreeDSamplePerspective(){ IsDrawOnce = false },
            new TurbulencePerlinNoiseShaderSample(),
            new UnicodeTextSample(),
            new XamagonSample(),
            new XferModeColorFilterSample(),
            new XfermodeSample()
        };
        
        state.current_sample = 0;
        currentSample = samples[0];
        currentSample.Init();
    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        vs_params_t vs_params = default;
        float deltaTime = (float)(sapp_frame_duration());
        
        // Calculate FPS
        state.fps = deltaTime > 0 ? 1.0f / deltaTime : 0;

        var proj = CreatePerspectiveFieldOfView(
                        (float)(60.0f * Math.PI / 180),
                        sapp_widthf() / sapp_heightf(),
                        0.01f,
                        10.0f);

        var view = CreateLookAt(new Vector3(0.0f, 1.5f, 4.0f), Vector3.Zero, Vector3.UnitY);

        state.rx += 0.1f * deltaTime;
        state.ry += 0.2f * deltaTime;
        var rxm = CreateFromAxisAngle(Vector3.UnitX, state.rx);
        var rym = CreateFromAxisAngle(Vector3.UnitY, state.ry);
        var model = rxm * rym;

        vs_params.mvp = model * view * proj;
        vs_params.model = model;

        fs_params_t fs_params = default;
        fs_params.light_dir = Vector3.Normalize(new Vector3(1.0f, 1.0f, -1.0f));
        fs_params.view_pos = new Vector3(0.0f, 1.5f, 4.0f);

        draw_ui();

#if WEB
        // Web platform: synchronous drawing (no threading support)
        if(currentSample?.IsDrawOnce == false)
        {
            state.bitmap.Prepare();
        }
        currentSample?.DrawSample(state.bitmap.canvas, state.bitmap.Width, state.bitmap.Height);
        state.bitmap.FlushCanvas();
        state.bitmap.UpdateTexture();
#else
        // Desktop/mobile: async drawing on background thread
        // Start drawing on background thread if previous task is complete
        if (drawTask == null || drawTask.IsCompleted)
        {
            var currentDeltaTime = deltaTime;
            drawTask = Task.Run(() =>
            {
                lock (drawLock)
                {
                    state.bitmap.Prepare();
                    currentSample?.DrawSample(state.bitmap.canvas, state.bitmap.Width, state.bitmap.Height);
                    state.bitmap.FlushCanvas();
                    textureNeedsUpdate = true;
                }
            });
        }

        // Update texture on main thread if drawing is complete
        if (textureNeedsUpdate)
        {
            lock (drawLock)
            {
                if (textureNeedsUpdate)
                {
                    state.bitmap.UpdateTexture();
                    textureNeedsUpdate = false;
                }
            }
        }
#endif
        
        sg_begin_pass(new sg_pass() { action = state.pass_action, swapchain = sglue_swapchain() });
        sg_apply_pipeline(state.pip);
        sg_apply_bindings(state.bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
        sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fs_params));
        sg_draw(0, 36, 1);

        simgui_render();
        sg_end_pass();
        sg_commit();
    }


    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        simgui_handle_event(*e);
        
        // Handle mouse input (desktop)
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN && 
            e->mouse_button == sapp_mousebutton.SAPP_MOUSEBUTTON_LEFT)
        {
            samples[state.current_sample].Tap();
        }
        
        // Handle touch input (mobile)
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN)
        {
            // Tap on first touch point
            if (e->num_touches > 0)
            {
                samples[state.current_sample].Tap();
            }
        }
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        simgui_shutdown();
        state.bitmap.Dispose();
        sg_shutdown();
        // Force a complete shutdown if debugging
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
            dpi_scale = 1// too small on Android sapp_dpi_scale()
        });

        byte open = 1;
        if (igBegin("SkiaSharp Gallery", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            igText(samples[state.current_sample].Title);
            igText($"FPS: {state.fps:F1}");
            igSeparator();
            
            if (igButton("<- Prev", Vector2.Zero))
            {
                int newIndex = (state.current_sample - 1 + samples.Length) % samples.Length;
                SwitchToSample(newIndex);
            }
            igSameLine(0, 10);
            if (igButton("Next ->", Vector2.Zero))
            {
                int newIndex = (state.current_sample + 1) % samples.Length;
                SwitchToSample(newIndex);
            }

            var view = state.bitmap.SokolTexture.View;
            var sampler = state.bitmap.SokolTexture.Sampler;
            ImTextureID texid = simgui_imtextureid_with_sampler(view, sampler);
            int size = Math.Min((int)(sapp_width()/1.5f), (int)(sapp_height()/1.5f));
            igImage(new ImTextureRef(){_TexID = texid }, new Vector2(size, size), new Vector2(0, 0), new Vector2(1, 1));
        }
        igEnd();
    }

    static void SwitchToSample(int newIndex)
    {
        if (state.current_sample != newIndex)
        {
            currentSample?.Destroy();
            state.current_sample = newIndex;
            currentSample = samples[newIndex];
            currentSample.Init();
        }
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb = &Init,
            frame_cb = &Frame,
            event_cb = &Event,
            cleanup_cb = &Cleanup,
            width = 0,
            height = 0,
            sample_count = 4,
            window_title = "Template (sokol-app)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}
