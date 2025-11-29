using Android.Content;
using Android.Opengl;
using Android.Util;
using Android.Views;
using Javax.Microedition.Khronos.Opengles;
using EGLConfig = Javax.Microedition.Khronos.Egl.EGLConfig;

using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static cube_app_shader_cs.Shaders;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;

namespace AndroidSokolApp;

public class OpenGLView : GLSurfaceView
{

    private readonly SokolRenderer _renderer;

    public OpenGLView(Context context) : base(context)
    {
        _renderer = new SokolRenderer();
        Init();
    }

    public OpenGLView(Context context, IAttributeSet attrs) : base(context, attrs)
    {
        _renderer = new SokolRenderer();
        Init();
    }

    private void Init()
    {
        // Request an OpenGL ES 3.1 context
        SetEGLContextClientVersion(3);

        // Set the Renderer for drawing on the GLSurfaceView
        SetRenderer(_renderer);
    }

    public void ChangeRotation()
    {
        Android.Util.Log.Debug("OpenGLView", "ChangeRotation called");
        _renderer.ChangeRotation();
        RequestRender();
    }
}

public class SokolRenderer : Java.Lang.Object, GLSurfaceView.IRenderer
{

    struct _state
    {
        public float rx, ry;
        public sg_pipeline pip;
        public sg_bindings bind;
        public bool PauseUpdate;
    }

    static _state state = new _state();

    private readonly Random _random = new Random();
    private float _rotationSpeedX = 1.0f;
    private float _rotationSpeedY = 2.0f;

    // Android-specific environment (for GLSurfaceView context, not Sokol App)
    private static sg_environment AndroidEnvironment()
    {
        // Cannot use sapp_* functions as Sokol App is not initialized
        return new sg_environment
        {
            defaults = new sg_environment_defaults
            {
                color_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                depth_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH_STENCIL,
                sample_count = 1
            }
        };
    }

    // Android-specific swapchain (for GLSurfaceView context, not Sokol App)
    private static sg_swapchain AndroidSwapchain()
    {
        // In GLSurfaceView, the default framebuffer is 0
        // We cannot use sapp_* functions as Sokol App is not initialized
        // GL_VIEWPORT = 0x0BA2
        int[] viewport = new int[4];
        GLES31.GlGetIntegerv(0x0BA2, viewport, 0);
        int width = viewport[2];
        int height = viewport[3];

        return new sg_swapchain
        {
            width = width,
            height = height,
            sample_count = 1, // default for GLSurfaceView
            color_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
            depth_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH_STENCIL,
            gl = new sg_gl_swapchain
            {
                framebuffer = 0  // Default framebuffer in GLSurfaceView
            }
        };
    }

    public unsafe void OnSurfaceCreated(IGL10? gl, EGLConfig? config)
    {
        try
        {


            sg_setup(new sg_desc()
            {
                environment = AndroidEnvironment(),
                logger = {
                    func = &slog_func,
                }
            });

            /* cube vertex buffer */
            float[] vertices =  {
            -1.0f, -1.0f, -1.0f,   1.0f, 0.0f, 0.0f, 1.0f,
            1.0f, -1.0f, -1.0f,   1.0f, 0.0f, 0.0f, 1.0f,
            1.0f,  1.0f, -1.0f,   1.0f, 0.0f, 0.0f, 1.0f,
            -1.0f,  1.0f, -1.0f,   1.0f, 0.0f, 0.0f, 1.0f,

            -1.0f, -1.0f,  1.0f,   0.0f, 1.0f, 0.0f, 1.0f,
            1.0f, -1.0f,  1.0f,   0.0f, 1.0f, 0.0f, 1.0f,
            1.0f,  1.0f,  1.0f,   0.0f, 1.0f, 0.0f, 1.0f,
            -1.0f,  1.0f,  1.0f,   0.0f, 1.0f, 0.0f, 1.0f,

            -1.0f, -1.0f, -1.0f,   0.0f, 0.0f, 1.0f, 1.0f,
            -1.0f,  1.0f, -1.0f,   0.0f, 0.0f, 1.0f, 1.0f,
            -1.0f,  1.0f,  1.0f,   0.0f, 0.0f, 1.0f, 1.0f,
            -1.0f, -1.0f,  1.0f,   0.0f, 0.0f, 1.0f, 1.0f,

            1.0f, -1.0f, -1.0f,   1.0f, 0.5f, 0.0f, 1.0f,
            1.0f,  1.0f, -1.0f,   1.0f, 0.5f, 0.0f, 1.0f,
            1.0f,  1.0f,  1.0f,   1.0f, 0.5f, 0.0f, 1.0f,
            1.0f, -1.0f,  1.0f,   1.0f, 0.5f, 0.0f, 1.0f,

            -1.0f, -1.0f, -1.0f,   0.0f, 0.5f, 1.0f, 1.0f,
            -1.0f, -1.0f,  1.0f,   0.0f, 0.5f, 1.0f, 1.0f,
            1.0f, -1.0f,  1.0f,   0.0f, 0.5f, 1.0f, 1.0f,
            1.0f, -1.0f, -1.0f,   0.0f, 0.5f, 1.0f, 1.0f,

            -1.0f,  1.0f, -1.0f,   1.0f, 0.0f, 0.5f, 1.0f,
            -1.0f,  1.0f,  1.0f,   1.0f, 0.0f, 0.5f, 1.0f,
            1.0f,  1.0f,  1.0f,   1.0f, 0.0f, 0.5f, 1.0f,
            1.0f,  1.0f, -1.0f,   1.0f, 0.0f, 0.5f, 1.0f
        };

            sg_buffer vbuf = sg_make_buffer(new sg_buffer_desc()
            {
                data = SG_RANGE(vertices),
                label = "cube-vertices"
            }
                );


            UInt16[] indices = {
                0, 1, 2,  0, 2, 3,
                6, 5, 4,  7, 6, 4,
                8, 9, 10,  8, 10, 11,
                14, 13, 12,  15, 14, 12,
                16, 17, 18,  16, 18, 19,
                22, 21, 20,  23, 22, 20
            };

            sg_buffer ibuf = sg_make_buffer(new sg_buffer_desc()
            {
                usage = new sg_buffer_usage { index_buffer = true },
                data = SG_RANGE(indices),
                label = "cube-indices"
            }
                );



            sg_shader shd = sg_make_shader(cube_app_shader_cs.Shaders.cube_shader_desc(sg_query_backend()));

            var pipeline_desc = default(sg_pipeline_desc);
            pipeline_desc.layout.buffers[0].stride = 28;
            pipeline_desc.layout.attrs[ATTR_cube_position].format = SG_VERTEXFORMAT_FLOAT3;
            pipeline_desc.layout.attrs[ATTR_cube_color0].format = SG_VERTEXFORMAT_FLOAT4;

            pipeline_desc.shader = shd;
            pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
            pipeline_desc.cull_mode = SG_CULLMODE_BACK;
            pipeline_desc.depth.write_enabled = true;
            pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
            pipeline_desc.label = "cube-pipeline";

            state.pip = sg_make_pipeline(pipeline_desc);

            state.bind = new sg_bindings();
            state.bind.vertex_buffers[0] = vbuf;
            state.bind.index_buffer = ibuf;

            Android.Util.Log.Info("OpenGLView", "OpenGL ES 3.1 initialized successfully");
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("OpenGLView", $"Error in OnSurfaceCreated: {ex.Message}");
        }
    }

    private long _lastFrameTime = 0;

    public void OnDrawFrame(IGL10? gl)
    {

        try
        {
            vs_params_t vs_params = default;

            // Calculate delta time manually since we're not using Sokol App
            long currentTime = Java.Lang.JavaSystem.CurrentTimeMillis();
            float deltaSeconds = _lastFrameTime == 0 ? (1.0f / 60.0f) : (currentTime - _lastFrameTime) / 1000.0f;
            _lastFrameTime = currentTime;

            state.rx += _rotationSpeedX * deltaSeconds;
            state.ry += _rotationSpeedY * deltaSeconds;
            var rotationMatrixX = Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, state.rx);
            var rotationMatrixY = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, state.ry);
            var modelMatrix = rotationMatrixX * rotationMatrixY;


            // Get viewport dimensions from OpenGL instead of sapp_* functions
            // GL_VIEWPORT = 0x0BA2
            int[] viewport = new int[4];
            GLES31.GlGetIntegerv(0x0BA2, viewport, 0);
            float width = viewport[2];
            float height = viewport[3];

            var projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
                (float)(60.0f * Math.PI / 180),
                width / height,
                0.01f,
                10.0f);
            var viewMatrix = Matrix4x4.CreateLookAt(
                new Vector3(0.0f, 1.5f, 6.0f),
                Vector3.Zero,
                Vector3.UnitY);

            vs_params.mvp = modelMatrix * viewMatrix * projectionMatrix;

            sg_pass pass = default;
            pass.action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
            pass.action.colors[0].clear_value = new float[4] { 0.25f, 0.5f, 0.75f, 1.0f };
            pass.swapchain = AndroidSwapchain();
            sg_begin_pass(pass);

            sg_apply_pipeline(state.pip);
            sg_apply_bindings(state.bind);
            sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
            sg_draw(0, 36, 1);
            sg_end_pass();
            sg_commit();
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("OpenGLView", $"Error in OnDrawFrame: {ex.Message}");
        }
    }

    public void OnSurfaceChanged(IGL10? gl, int width, int height)
    {
        GLES31.GlViewport(0, 0, width, height);
    }


    public void ChangeRotation()
    {
        // Generate random rotation speeds between -5.0 and 5.0
        _rotationSpeedX = (float)(_random.NextDouble() * 10.0 - 5.0);
        _rotationSpeedY = (float)(_random.NextDouble() * 10.0 - 5.0);
        Android.Util.Log.Debug("SokolRenderer", $"New rotation speeds: X={_rotationSpeedX:F2}, Y={_rotationSpeedY:F2}");
    }
}
