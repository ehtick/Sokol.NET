using System;
using System.Collections.Generic;
using Sokol;
using static Sokol.SG;
using static Sokol.StbImage;
using static Sokol.NanoSVG;

namespace Sokol.Render2D.Particles.Rendering;

/// <summary>The Sokol.SG analogue of <see cref="ParticleTextureCache"/>: lazily loads particle sprites
/// as <b>sokol images</b> (a NanoVG image can't be sampled by an SG pipeline). PNG/JPEG decode through
/// stb_image (the <c>examples/loadpng</c> pattern); <c>.svg</c> rasterises via NanoSVG to an RGBA
/// buffer uploaded with <see cref="sg_make_image"/>. Loads are async (same <see cref="SFilesystem"/>
/// fetcher), so the first <see cref="Get"/> returns an invalid view and a later frame returns the real
/// one — call <see cref="Preload"/> ahead of time. All images/views are destroyed with the cache.</summary>
public sealed unsafe class SgParticleTextureCache : IDisposable
{
    const int SvgRasterSize = 128;

    struct Tex { public sg_image Img; public sg_view View; }

    readonly Dictionary<string, Tex> _tex = new();   // present (even with id 0) = requested / loading
    IntPtr _rasterizer;
    bool   _disposed;

    /// <summary>Texture view for <paramref name="key"/> (id 0 until loaded / on failure); kicks the load.</summary>
    public sg_view Get(string? key)
    {
        if (_disposed || string.IsNullOrEmpty(key)) return default;
        if (_tex.TryGetValue(key, out var t)) return t.View;
        _tex[key] = default;            // mark requested so we don't re-queue every frame
        BeginLoad(key);
        return default;
    }

    public void Preload(string key) => Get(key);

    void BeginLoad(string key)
    {
        // App-supplied sprites win — a consumer can override/extend the presets by shipping its own asset
        // at this path. The framework's baked-in defaults (Render2DEmbeddedAssets) are the fallback, so an
        // app that ships NO asset folder still gets the built-in sprites. Decode runs in the fetch callback
        // (frame boundary), i.e. outside any sg pass.
        SFilesystem.LoadFileAsync(key, (path, bytes, status) =>
        {
            if (_disposed) return;
            byte[] data;
            if (status == SFileLoadStatus.Success && bytes != null && bytes.Length > 0)
                data = bytes;                                       // app override
            else if (!Render2DEmbeddedAssets.TryGet(key, out data))
                return;                                             // no file + no baked-in default
            var tex = IsSvg(key) ? FromSvg(data) : FromImage(data);
            if (tex.Img.id != 0) _tex[key] = tex;
        });
    }

    static bool IsSvg(string k) => k.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

    Tex FromImage(byte[] bytes)
    {
        int w = 0, h = 0, ch = 0;
        byte* pixels = stbi_load_csharp(in bytes[0], bytes.Length, ref w, ref h, ref ch, 4);
        if (pixels == null) return default;
        var tex = Upload(w, h, pixels);
        stbi_image_free_csharp(pixels);
        return tex;
    }

    Tex FromSvg(byte[] svg)
    {
        if (_rasterizer == IntPtr.Zero) _rasterizer = nsvgCreateRasterizer();
        if (_rasterizer == IntPtr.Zero) return default;

        svg = SvgPreprocess.Apply(svg);             // inline CSS classes + strip clipPaths (NanoSVG limits)
        byte[] buf = new byte[svg.Length + 1];      // nsvgParse mutates + needs a null terminator
        Array.Copy(svg, buf, svg.Length);
        fixed (byte* p = buf)
        {
            NSVGimage* image = nsvgParse((IntPtr)p, "px", 96.0f);
            if (image == null || image->width <= 0 || image->height <= 0)
            {
                if (image != null) nsvgDelete(image);
                return default;
            }
            float scale = SvgRasterSize / MathF.Max(image->width, image->height);
            int w = Math.Max(1, (int)(image->width * scale));
            int h = Math.Max(1, (int)(image->height * scale));
            byte[] pixels = new byte[w * h * 4];
            fixed (byte* dst = pixels)
            {
                nsvgRasterize(_rasterizer, image, 0, 0, scale, dst, w, h, w * 4);
                nsvgDelete(image);
                return Upload(w, h, dst);
            }
        }
    }

    Tex Upload(int w, int h, byte* pixels)
    {
        var desc = new sg_image_desc { width = w, height = h, pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8, label = "r2d-particle-sprite" };
        desc.data.mip_levels[0] = new sg_range { ptr = pixels, size = (nuint)(w * h * 4) };
        sg_image img = sg_make_image(desc);
        sg_view view = sg_make_view(new sg_view_desc { texture = { image = img }, label = "r2d-particle-sprite-view" });
        return new Tex { Img = img, View = view };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var t in _tex.Values)
        {
            if (t.View.id != 0) sg_destroy_view(t.View);
            if (t.Img.id  != 0) sg_destroy_image(t.Img);
        }
        _tex.Clear();
        if (_rasterizer != IntPtr.Zero) { nsvgDeleteRasterizer(_rasterizer); _rasterizer = IntPtr.Zero; }
    }
}
