using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Sokol;
using System.Runtime.InteropServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.Utils;
using static Sokol.SLog;
using static Sokol.SImgui;
using static Sokol.NanoSVG;
using static Sokol.NanoVG;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_load_action;
using static Sokol.SG.sg_pixel_format;
using static nanosvg_demo_shader_cs.Shaders;
using static Imgui.ImguiNative;
using Imgui;
using System.Diagnostics;

public static unsafe class NanosvgdemoApp
{
    private static readonly string[] SvgFiles =
    {
        "svg/14thWarrior-Cartoon-Elephant.svg",
        "svg/Anonymous_Architetto_--_Casa_dei_sogni.svg",
        "svg/Clown-Illustration-6.svg",
        "svg/Clown-Illustration-8.svg",
        "svg/Clown-Illustration-9.svg",
        "svg/Gerald-G-Simple-Fruit-FF-Menu-4.svg",
        "svg/Gerald-G-Simple-Fruit-FF-Menu.svg",
        "svg/Little-funny-penguin.svg",
        "svg/Machovka_House_3.svg",
        "svg/PeterM_Sad_tiger_cat.svg",
        "svg/StudioFibonacci-Cartoon-leopard.svg",
        "svg/Sweet2.svg",
        "svg/apples.svg",
        "svg/candle.svg",
        "svg/candy.svg",
        "svg/cherries.svg",
        "svg/cool-home.svg",
        "svg/donut.svg",
        "svg/flower.svg",
        "svg/frame-5c.svg",
        "svg/fruits.svg",
        "svg/goose.svg",
        "svg/haunted_house.svg",
        "svg/house.svg",
        "svg/maweki-Nimm2-type-candy.svg",
        "svg/nicubunu-Monkey-head.svg",
        "svg/owl-punk.svg",
        "svg/owl_santa.svg",
        "svg/parrot.svg",
        "svg/present.svg",
        "svg/tomato.svg",
    };

    private const float PanelWidth = 160.0f;

    private class State
    {
        public sg_pipeline pip;
        public sg_bindings bind;
        public sg_pass_action pass_action;
        public sg_buffer vertexBuffer;
        public sg_buffer indexBuffer;
        public IntPtr rasterizer;
        public IntPtr nvgCtx;
        public Texture? svgTexture;
        public NSVGimage* parsedImage;
        public bool vectorMode;
        public bool isLoading;
        public int currentSvgIndex = -1;
        public int svgWidth = 1;
        public int svgHeight = 1;
        public string svgName = "";
    }

    private static State state = new State();

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc
        {
            environment = sglue_environment(),
            logger = { func = &slog_func }
        });

        simgui_setup(new simgui_desc_t
        {
            logger = { func = &slog_func }
        });

        SFilesystem.Initialize();

        state.rasterizer = nsvgCreateRasterizer();

        var nvgDesc = new snvg_desc_t { max_vertices = 1 << 20 }; // 1M vertices for complex SVGs
        state.nvgCtx = nvgCreateSokolWithDesc(NVG_ANTIALIAS, in nvgDesc);

        float[] vertices =
        {
            -1.0f,  1.0f, 0.0f, 0.0f,
             1.0f,  1.0f, 1.0f, 0.0f,
             1.0f, -1.0f, 1.0f, 1.0f,
            -1.0f, -1.0f, 0.0f, 1.0f,
        };
        state.vertexBuffer = sg_make_buffer(new sg_buffer_desc
        {
            data = SG_RANGE(vertices),
            label = "quad-vertices"
        });

        ushort[] indices = { 0, 1, 2, 0, 2, 3 };
        state.indexBuffer = sg_make_buffer(new sg_buffer_desc
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data = SG_RANGE(indices),
            label = "quad-indices"
        });

        var pipDesc = new sg_pipeline_desc();
        pipDesc.shader = sg_make_shader(nanosvg_shader_desc(sg_query_backend()));
        pipDesc.layout.attrs[ATTR_nanosvg_pos].format = SG_VERTEXFORMAT_FLOAT2;
        pipDesc.layout.attrs[ATTR_nanosvg_uv0].format = SG_VERTEXFORMAT_FLOAT2;
        pipDesc.index_type = SG_INDEXTYPE_UINT16;
        pipDesc.label = "nanosvg-pipeline";
        state.pip = sg_make_pipeline(pipDesc);

        state.bind.vertex_buffers[0] = state.vertexBuffer;
        state.bind.index_buffer = state.indexBuffer;

        state.pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.12f, g = 0.12f, b = 0.12f, a = 1.0f };

        LoadSvg(0);
    }

    // nanosvg has no <clipPath> support — inner paths are parsed as solid black shapes and
    // cover the actual content. Strip clipPath blocks and clip-path attributes entirely.
    // (All clipPath elements in Pixelmator-exported SVGs are near-full-viewport rectangles
    // that don't clip anything meaningful.)
    private static byte[] StripClipPaths(byte[] data)
    {
        string svg = Encoding.UTF8.GetString(data);
        svg = Regex.Replace(svg, @"<clipPath\b[^>]*>.*?</clipPath>", "", RegexOptions.Singleline);
        svg = Regex.Replace(svg, @"\s*clip-path=""[^""]*""", "");
        return Encoding.UTF8.GetBytes(svg);
    }

    // nanosvg only handles inline style= attributes, not CSS class selectors.
    // This converts <style> class rules into inline style attributes so colors render correctly.
    private static byte[] InlineCSSClasses(byte[] data)
    {
        string svg = Encoding.UTF8.GetString(data);

        var styleBlock = Regex.Match(svg, @"<style[^>]*>(.*?)</style>", RegexOptions.Singleline);
        if (!styleBlock.Success)
            return data;

        var classMap = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(styleBlock.Groups[1].Value, @"\.([\w-]+)\s*\{([^}]*)\}"))
            classMap[m.Groups[1].Value] = m.Groups[2].Value.Trim();

        if (classMap.Count == 0)
            return data;

        string result = Regex.Replace(svg, @"class=""([\w\s-]+)""", m =>
        {
            var sb = new StringBuilder();
            foreach (var cls in m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (classMap.TryGetValue(cls, out var props))
                    sb.Append(props).Append(';');
            return sb.Length > 0 ? $"style=\"{sb}\"" : m.Value;
        });

        return Encoding.UTF8.GetBytes(result);
    }

    private static void LoadSvg(int index)
    {
        if (state.isLoading) return;
        state.isLoading = true;
        state.currentSvgIndex = index;
        state.svgName = Path.GetFileNameWithoutExtension(SvgFiles[index]);
        SFilesystem.LoadFileAsync(SvgFiles[index], OnSvgLoaded);
    }

    private static void OnSvgLoaded(string filePath, byte[]? data, SFileLoadStatus status)
    {
        state.isLoading = false;
        if (status != SFileLoadStatus.Success || data == null)
        {
            Info($"NanoSVGDemo: failed to load {filePath}: {status}");
            return;
        }

        // nanosvg doesn't support CSS class-based styles — inline them first
        data = InlineCSSClasses(data);
        // nanosvg has no <clipPath> support — strip them to prevent black rectangles
        data = StripClipPaths(data);

        // nsvgParse modifies the input buffer in-place (XML parsing), so we need a mutable null-terminated copy
        byte[] svgBuf = new byte[data.Length + 1];
        Array.Copy(data, svgBuf, data.Length);

        fixed (byte* svgPtr = svgBuf)
        {
            NSVGimage* image = nsvgParse((IntPtr)svgPtr, "px", 96.0f);
            if (image == null || image->width <= 0 || image->height <= 0)
            {
                if (image != null) nsvgDelete(image);
                Info($"NanoSVGDemo: nsvgParse failed for {filePath}");
                return;
            }

            // Rasterize to fill the display area (window minus the panel)
            int displayW = sapp_width() - (int)PanelWidth;
            int displayH = sapp_height();
            float scale = Math.Min((float)displayW / image->width, (float)displayH / image->height);
            int w = Math.Max(1, (int)(image->width * scale));
            int h = Math.Max(1, (int)(image->height * scale));

            byte[] pixels = new byte[w * h * 4];
            fixed (byte* pixelPtr = pixels)
            {
                nsvgRasterize(state.rasterizer, image, 0, 0, scale, pixelPtr, w, h, w * 4);
            }
            // nsvgRasterize clears the buffer to zero before drawing, so composite over white afterwards
            for (int p = 0; p < pixels.Length; p += 4)
            {
                int a = pixels[p + 3];
                if (a < 255)
                {
                    int ia = 255 - a;
                    pixels[p + 0] = (byte)((pixels[p + 0] * a + 255 * ia) / 255);
                    pixels[p + 1] = (byte)((pixels[p + 1] * a + 255 * ia) / 255);
                    pixels[p + 2] = (byte)((pixels[p + 2] * a + 255 * ia) / 255);
                    pixels[p + 3] = 255;
                }
            }

            // Keep parsedImage alive for vector mode; free the previous one first
            if (state.parsedImage != null)
                nsvgDelete(state.parsedImage);
            state.parsedImage = image;

            state.svgTexture?.Dispose();
            state.svgTexture = null;

            fixed (byte* pixelPtr = pixels)
            {
                state.svgTexture = new Texture(pixelPtr, w, h, state.svgName, SG_PIXELFORMAT_RGBA8);
            }

            state.svgWidth = w;
            state.svgHeight = h;
            Info($"NanoSVGDemo: loaded {state.svgName} ({w}x{h})");
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        SFilesystem.Update();

        simgui_new_frame(new simgui_frame_desc_t
        {
            width = sapp_width(),
            height = sapp_height(),
            delta_time = sapp_frame_duration(),
        });

        DrawUI();

        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });

        if (!state.isLoading)
        {
            if (!state.vectorMode && state.svgTexture != null && state.svgTexture.IsValid)
            {
                float svgAspect = (float)state.svgWidth / state.svgHeight;
                float winAspect = sapp_widthf() / sapp_heightf();
                float sx = svgAspect > winAspect ? 1.0f : svgAspect / winAspect;
                float sy = svgAspect > winAspect ? winAspect / svgAspect : 1.0f;

                var vsParams = new vs_params_t();
                vsParams.quad_xform[0] = sx;
                vsParams.quad_xform[1] = sy;

                state.bind.views[VIEW_tex] = state.svgTexture.View;
                state.bind.samplers[SMP_smp] = state.svgTexture.Sampler;

                sg_apply_pipeline(state.pip);
                sg_apply_bindings(state.bind);
                sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vsParams));
                sg_draw(0, 6, 1);
            }
            else if (state.vectorMode && state.parsedImage != null)
            {
                DrawVectorSVG();
            }
        }

        simgui_render();
        sg_end_pass();
        sg_commit();
    }

    private static NVGcolor NsvgColorToNvg(uint color, float opacity)
    {
#if WEB
        // NativeAOT AOT (-O3): P/Invoke with ref NVGcolor fails to flush writes back
        // to WASM locals in large methods. Construct NVGcolor with pure C# arithmetic.
        return new NVGcolor(
            (color & 0xFF) * (1f / 255f),
            ((color >> 8) & 0xFF) * (1f / 255f),
            ((color >> 16) & 0xFF) * (1f / 255f),
            ((color >> 24) & 0xFF) * (1f / 255f) * opacity
        );
#else
        byte r = (byte)(color & 0xFF);
        byte g = (byte)((color >> 8) & 0xFF);
        byte b = (byte)((color >> 16) & 0xFF);
        byte a = (byte)((color >> 24) & 0xFF);
        return nvgRGBA(r, g, b, (byte)(a * opacity));
#endif
    }

    // grad->xform is the user→gradient inverse transform (set by nsvg__xformInverse in nsvg__scaleToViewbox).
    // We recover gradient endpoints by solving the 2x2 system at gy=0 and gy=1 (linear) or gd=0 (radial center).
    private static NVGpaint GradientPaint(IntPtr ctx, NSVGpaint* paint, float opacity)
    {
#if WEB || __ANDROID_ARM32__
        NSVGgradient* grad = (NSVGgradient*)(nuint)paint->gradient;  // gradient ptr is same union member as color on WASM
#else
        NSVGgradient* grad = paint->gradient;
#endif
        if (grad == null || grad->nstops == 0)
            return default;

        var stops = MemoryMarshal.CreateSpan(ref grad->stops[0], grad->nstops);
        NVGcolor icol = NsvgColorToNvg(stops[0].color, opacity);
        NVGcolor ocol = NsvgColorToNvg(stops[grad->nstops - 1].color, opacity);

        float t0 = grad->xform[0], t1 = grad->xform[1];
        float t2 = grad->xform[2], t3 = grad->xform[3];
        float t4 = grad->xform[4], t5 = grad->xform[5];

        // det of the 2x2 linear part (row-vector convention: x'=x*t0+y*t2, y'=x*t1+y*t3)
        float det = t0 * t3 - t1 * t2;
        if (MathF.Abs(det) < 1e-10f) det = 1e-10f;

        // user-space point at gradient param (gx=0, gy=0)
        float cx = (-t3 * t4 + t2 * t5) / det;
        float cy = (t1 * t4 - t0 * t5) / det;

        if ((NSVGpaintType)paint->type == NSVGpaintType.NSVG_PAINT_LINEAR_GRADIENT)
        {
            // user-space point at (gx=0, gy=1)
            float ex = cx - t2 / det;
            float ey = cy + t0 / det;
            return nvgLinearGradient(ctx, cx, cy, ex, ey, icol, ocol);
        }
        else
        {
            float r = 1.0f / MathF.Sqrt(MathF.Abs(det));
            return nvgRadialGradient(ctx, cx, cy, 0, r, icol, ocol);
        }
    }

    // Shoelace signed area of all control points in a nanosvg path.
    // In screen space (Y-down): area < 0 → CCW (solid), area > 0 → CW (hole).
    private static float PathSignedArea(float* pts, int npts)
    {
        float area = 0;
        for (int i = 0; i < npts; i++)
        {
            int j = (i + 1) % npts;
            area += pts[i * 2] * pts[j * 2 + 1] - pts[j * 2] * pts[i * 2 + 1];
        }
        return area;
    }

    private static void DrawVectorSVG()
    {
        float winW = sapp_widthf();
        float winH = sapp_heightf();
        float svgW = state.parsedImage->width;
        float svgH = state.parsedImage->height;
        float displayW = winW - PanelWidth;

        float scale = Math.Min(displayW / svgW, winH / svgH);
        float tx = PanelWidth + (displayW - svgW * scale) * 0.5f;
        float ty = (winH - svgH * scale) * 0.5f;

        nvgBeginFrame(state.nvgCtx, winW, winH, 1.0f);
        nvgSave(state.nvgCtx);
        nvgTranslate(state.nvgCtx, tx, ty);
        nvgScale(state.nvgCtx, scale, scale);

        for (NSVGshape* shape = state.parsedImage->shapes; shape != null; shape = shape->next)
        {
            if ((shape->flags & (byte)NSVGflags.NSVG_FLAGS_VISIBLE) == 0)
                continue;

            var fillType   = (NSVGpaintType)shape->fill.type;
            var strokeType = (NSVGpaintType)shape->stroke.type;
            bool hasFill = fillType == NSVGpaintType.NSVG_PAINT_COLOR ||
                           fillType == NSVGpaintType.NSVG_PAINT_LINEAR_GRADIENT ||
                           fillType == NSVGpaintType.NSVG_PAINT_RADIAL_GRADIENT;
            bool hasStroke = (strokeType == NSVGpaintType.NSVG_PAINT_COLOR ||
                              strokeType == NSVGpaintType.NSVG_PAINT_LINEAR_GRADIENT ||
                              strokeType == NSVGpaintType.NSVG_PAINT_RADIAL_GRADIENT)
                             && shape->strokeWidth > 0;

            if (!hasFill && !hasStroke)
                continue;

            // Build the full compound path once, then fill and/or stroke
            nvgBeginPath(state.nvgCtx);
            for (NSVGpath* path = shape->paths; path != null; path = path->next)
            {
                float* pts = path->pts;
                nvgMoveTo(state.nvgCtx, pts[0], pts[1]);
                for (int i = 0; i < path->npts - 1; i += 3)
                {
                    float* p = pts + i * 2;
                    nvgBezierTo(state.nvgCtx, p[2], p[3], p[4], p[5], p[6], p[7]);
                }
                if (path->closed != 0)
                    nvgClosePath(state.nvgCtx);
                // Mark holes: CW on screen (area>0) → NVG_CW=2=NVG_HOLE
                nvgPathWinding(state.nvgCtx, PathSignedArea(pts, path->npts) < 0 ? (int)NVGwinding.NVG_CCW : (int)NVGwinding.NVG_CW);
            }

            if (hasFill)
            {
                if (fillType == NSVGpaintType.NSVG_PAINT_COLOR)
                    nvgFillColor(state.nvgCtx, NsvgColorToNvg(shape->fill.color, shape->opacity));
                else
                    nvgFillPaint(state.nvgCtx, GradientPaint(state.nvgCtx, &shape->fill, shape->opacity));
                nvgFill(state.nvgCtx);
            }
            if (hasStroke)
            {
                if (strokeType == NSVGpaintType.NSVG_PAINT_COLOR)
                    nvgStrokeColor(state.nvgCtx, NsvgColorToNvg(shape->stroke.color, shape->opacity));
                else
                    nvgStrokePaint(state.nvgCtx, GradientPaint(state.nvgCtx, &shape->stroke, shape->opacity));
                nvgStrokeWidth(state.nvgCtx, shape->strokeWidth);
                nvgStroke(state.nvgCtx);
            }
        }

        nvgRestore(state.nvgCtx);
        nvgEndFrame(state.nvgCtx);
    }

    private static void DrawUI()
    {
        igSetNextWindowPos(new Vector2(10, 10), ImGuiCond.Always, Vector2.Zero);
        igSetNextWindowSize(new Vector2(PanelWidth, 0), ImGuiCond.Always);

        byte open = 1;
        igBegin("SVG", ref open,
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize);

        igText(state.isLoading ? "Loading..." : state.svgName);
        igText($"{state.currentSvgIndex + 1} / {SvgFiles.Length}");
        igSeparator();

        float btnW = (PanelWidth - 28) * 0.5f;
        if (igButton("< Back", new Vector2(btnW, 0)) && !state.isLoading)
            LoadSvg((state.currentSvgIndex + SvgFiles.Length - 1) % SvgFiles.Length);
        igSameLine(0, 8);
        if (igButton("Next >", new Vector2(btnW, 0)) && !state.isLoading)
            LoadSvg((state.currentSvgIndex + 1) % SvgFiles.Length);

        igSeparator();
        igText(state.vectorMode ? "Mode: Vector" : "Mode: Raster");
        if (igButton(state.vectorMode ? "Switch Raster" : "Switch Vector", new Vector2(-1, 0)))
            state.vectorMode = !state.vectorMode;

        igEnd();
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        simgui_handle_event(in *e);

        if (e->type == sapp_event_type.SAPP_EVENTTYPE_KEY_UP && !state.isLoading)
        {
            if (e->key_code == sapp_keycode.SAPP_KEYCODE_RIGHT ||
                e->key_code == sapp_keycode.SAPP_KEYCODE_SPACE)
                LoadSvg((state.currentSvgIndex + 1) % SvgFiles.Length);
            else if (e->key_code == sapp_keycode.SAPP_KEYCODE_LEFT)
                LoadSvg((state.currentSvgIndex + SvgFiles.Length - 1) % SvgFiles.Length);
        }
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        state.svgTexture?.Dispose();

        if (state.parsedImage != null)
            nsvgDelete(state.parsedImage);
        if (state.rasterizer != IntPtr.Zero)
            nsvgDeleteRasterizer(state.rasterizer);
        if (state.nvgCtx != IntPtr.Zero)
            nvgDeleteSokol(state.nvgCtx);

        if (state.vertexBuffer.id != 0) sg_destroy_buffer(state.vertexBuffer);
        if (state.indexBuffer.id != 0) sg_destroy_buffer(state.indexBuffer);
        if (state.pip.id != 0) sg_destroy_pipeline(state.pip);

        TextureCache.Instance.Shutdown();
        SFilesystem.Shutdown();
        simgui_shutdown();
        sg_shutdown();

        if (Debugger.IsAttached)
            Environment.Exit(0);
    }

    public static sapp_desc sokol_main()
    {
        return new sapp_desc
        {
            init_cb = &Init,
            frame_cb = &Frame,
            event_cb = &Event,
            cleanup_cb = &Cleanup,
            width = 1024,
            height = 768,
            sample_count = 4,
            window_title = "NanoSVG Demo (sokol-app)",
            icon = { sokol_default = true },
            logger = { func = &slog_func }
        };
    }
}
