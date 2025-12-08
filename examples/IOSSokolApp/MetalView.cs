using Foundation;
using UIKit;
using Metal;
using MetalKit;
using CoreGraphics;
using CoreAnimation;

using Sokol;
using System.Runtime.InteropServices;
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
using static Sokol.SLog;

namespace IOSSokolApp;

public class MetalView : MTKView
{
    private readonly SokolRenderer _renderer;

    public MetalView(CGRect frame) : base(frame, MTLDevice.SystemDefault)
    {
        if (Device == null)
        {
            Console.WriteLine("Failed to create Metal device");
            return;
        }

        // Configure Metal view
        ColorPixelFormat = MTLPixelFormat.BGRA8Unorm;
        DepthStencilPixelFormat = MTLPixelFormat.Depth32Float_Stencil8;
        SampleCount = 1;
        Paused = false;
        EnableSetNeedsDisplay = false;

        _renderer = new SokolRenderer();
        _renderer.Initialize(Device, ColorPixelFormat, DepthStencilPixelFormat, this);

        // Set up rendering delegate
        Delegate = new MetalViewDelegate(_renderer);
    }

    public void ChangeRotation()
    {
        _renderer.ChangeRotation();
    }

    public void Cleanup()
    {
        _renderer.Cleanup();
    }
}

public class MetalViewDelegate : NSObject, IMTKViewDelegate
{
    private readonly SokolRenderer _renderer;

    public MetalViewDelegate(SokolRenderer renderer)
    {
        _renderer = renderer;
    }

    public void Draw(MTKView view)
    {
        _renderer.Render(view);
    }

    public void DrawableSizeWillChange(MTKView view, CGSize size)
    {
        // Handle resize if needed
    }
}

public class SokolRenderer
{
    struct _state
    {
        public float rx, ry;
        public sg_pipeline pip;
        public sg_bindings bind;
    }

    private _state state = new _state();
    private readonly Random _random = new Random();
    private float _rotationSpeedX = 1.0f;
    private float _rotationSpeedY = 2.0f;
    private long _lastFrameTime = 0;
    private bool _initialized = false;
    private IMTLDevice? _device;
    private MTKView? _view;
    private sg_swapchain _cachedSwapchain;
    private int _lastWidth = 0;
    private int _lastHeight = 0;
    private bool _swapchainNeedsUpdate = true;

    // iOS-specific environment (for Metal, not Sokol App)
    private static sg_environment IOSEnvironment(MTLPixelFormat colorFormat, MTLPixelFormat depthFormat)
    {
        // Cannot use sapp_* functions as Sokol App is not initialized
        return new sg_environment
        {
            defaults = new sg_environment_defaults
            {
                color_format = ConvertPixelFormat(colorFormat),
                depth_format = ConvertPixelFormat(depthFormat),
                sample_count = 1
            }
        };
    }

    // Convert Metal pixel format to Sokol pixel format
    private static sg_pixel_format ConvertPixelFormat(MTLPixelFormat format)
    {
        return format switch
        {
            MTLPixelFormat.BGRA8Unorm => sg_pixel_format.SG_PIXELFORMAT_BGRA8,
            MTLPixelFormat.Depth32Float_Stencil8 => sg_pixel_format.SG_PIXELFORMAT_DEPTH_STENCIL,
            _ => sg_pixel_format.SG_PIXELFORMAT_RGBA8
        };
    }

    // iOS-specific swapchain (for Metal/MTKView context, not Sokol App)
    // Only creates new swapchain when view dimensions change, updates drawables every frame
    private unsafe sg_swapchain IOSSwapchain(MTKView view)
    {
        int currentWidth = (int)view.DrawableSize.Width;
        int currentHeight = (int)view.DrawableSize.Height;
        
        // Check if view dimensions changed
        if (_swapchainNeedsUpdate || currentWidth != _lastWidth || currentHeight != _lastHeight)
        {
            _lastWidth = currentWidth;
            _lastHeight = currentHeight;
            _swapchainNeedsUpdate = false;
            
            _cachedSwapchain = new sg_swapchain
            {
                width = currentWidth,
                height = currentHeight,
                sample_count = (int)view.SampleCount,
                color_format = ConvertPixelFormat(view.ColorPixelFormat),
                depth_format = ConvertPixelFormat(view.DepthStencilPixelFormat),
                metal = new sg_metal_swapchain
                {
                    current_drawable = (void*)IntPtr.Zero,
                    depth_stencil_texture = (void*)IntPtr.Zero,
                    msaa_color_texture = (void*)IntPtr.Zero
                }
            };
        }
        
        // Update Metal drawables every frame (these must be fresh per frame)
        var currentDrawable = view.CurrentDrawable;
        var depthTexture = view.DepthStencilTexture;
        _cachedSwapchain.metal.current_drawable = (void*)(currentDrawable?.Handle ?? IntPtr.Zero);
        _cachedSwapchain.metal.depth_stencil_texture = (void*)(depthTexture?.Handle ?? IntPtr.Zero);
        
        return _cachedSwapchain;
    }

    public unsafe void Initialize(IMTLDevice device, MTLPixelFormat colorFormat, MTLPixelFormat depthFormat, MTKView view)
    {
        if (_initialized) return;

        _device = device;
        _view = view;

        try
        {
            var env = IOSEnvironment(colorFormat, depthFormat);
            env.metal.device = (void*)device.Handle;
            
            sg_setup(new sg_desc()
            {
                environment = env,
                logger = {
                    func = &slog_func,
                }
            });

            /* cube vertex buffer */
            float[] vertices = {
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
            });

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
            });

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

            _initialized = true;
            Console.WriteLine("Sokol GFX initialized successfully on iOS with Metal");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in Initialize: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
    public void Render(MTKView view)
    {
        if (!_initialized || _view == null) return;

        try
        {
            int width = (int)view.DrawableSize.Width;
            int height = (int)view.DrawableSize.Height;
            
            vs_params_t vs_params = default;

            // Calculate delta time manually since we're not using Sokol App
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            float deltaSeconds = _lastFrameTime == 0 ? (1.0f / 60.0f) : (currentTime - _lastFrameTime) / 1000.0f;
            _lastFrameTime = currentTime;

            state.rx += _rotationSpeedX * deltaSeconds;
            state.ry += _rotationSpeedY * deltaSeconds;
            var rotationMatrixX = Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, state.rx);
            var rotationMatrixY = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, state.ry);
            var modelMatrix = rotationMatrixX * rotationMatrixY;

            var projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
                (float)(60.0f * Math.PI / 180),
                (float)width / height,
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
            pass.swapchain = IOSSwapchain(view);
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
            Console.WriteLine($"Error in Render: {ex.Message}");
        }
    }

    public void ChangeRotation()
    {
        // Generate random rotation speeds between -5.0 and 5.0
        _rotationSpeedX = (float)(_random.NextDouble() * 10.0 - 5.0);
        _rotationSpeedY = (float)(_random.NextDouble() * 10.0 - 5.0);
        Console.WriteLine($"New rotation speeds: X={_rotationSpeedX:F2}, Y={_rotationSpeedY:F2}");
    }

    public void Cleanup()
    {
        if (_initialized)
        {
            sg_shutdown();
            _initialized = false;
        }
    }
}
