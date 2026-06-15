using System;
using System.Collections.Generic;
using Sokol;
using Sokol.GUI;
using static Sokol.NanoSVG;

namespace Sokol.Render2D.Particles;

/// <summary>Lazily loads and shares particle sprites keyed by asset path (design §6). PNG/JPEG decode
/// through NanoVG's built-in stb_image (<see cref="UIImage.LoadFromMemory"/>); <c>.svg</c> is parsed +
/// rasterised by NanoSVG to an RGBA buffer and uploaded via <see cref="UIImage.FromRgba"/>. Loads are
/// async (the same <see cref="SFilesystem"/> fetcher fonts use) so the first <see cref="Get"/> returns
/// null and a later frame returns the image — call <see cref="Preload"/> ahead of time to avoid a miss.
/// All images are disposed with the cache.</summary>
public sealed class ParticleTextureCache : IDisposable
{
    const int SvgRasterSize = 128;   // rasterise SVGs to fit this square (scaling down in draw is fine)

    readonly Dictionary<string, UIImage?> _images = new();   // null entry = requested / loading / failed
    IntPtr _rasterizer;
    bool   _disposed;

    /// <summary>Return the sprite for <paramref name="key"/>, kicking an async load on first request.
    /// Returns null until the load completes (or if it failed).</summary>
    public UIImage? Get(string? key)
    {
        if (_disposed || string.IsNullOrEmpty(key)) return null;
        if (_images.TryGetValue(key, out var img)) return img;
        _images[key] = null;            // mark requested so we don't re-queue every frame
        BeginLoad(key);
        return null;
    }

    /// <summary>Kick the load for a sprite ahead of when it's drawn (e.g. when a screen opens).</summary>
    public void Preload(string key) => Get(key);

    void BeginLoad(string key)
    {
        // App-supplied sprites win (consumer override); the framework's baked-in defaults
        // (Render2DEmbeddedAssets) are the fallback so an app that ships no asset folder still gets them.
        SFilesystem.LoadFileAsync(key, (path, bytes, status) =>
        {
            if (_disposed) return;
            byte[] data;
            if (status == SFileLoadStatus.Success && bytes != null && bytes.Length > 0)
                data = bytes;                                       // app override
            else if (!Render2DEmbeddedAssets.TryGet(key, out data))
                return;                                             // no file + no baked-in default
            IntPtr vg = Screen.Instance.Renderer.VGContext;
            if (vg == IntPtr.Zero) return;
            _images[key] = IsSvg(key) ? RasterizeSvg(vg, data) : UIImage.LoadFromMemory(vg, data);
        });
    }

    static bool IsSvg(string k) => k.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

    // Parse + rasterise an SVG to a transparent RGBA buffer (no white composite — particles keep alpha).
    unsafe UIImage? RasterizeSvg(IntPtr vg, byte[] svg)
    {
        if (_rasterizer == IntPtr.Zero) _rasterizer = nsvgCreateRasterizer();
        if (_rasterizer == IntPtr.Zero) return null;

        svg = SvgPreprocess.Apply(svg);   // inline CSS classes + strip clipPaths (NanoSVG limits)
        // nsvgParse mutates its input (in-place XML parse), so hand it a null-terminated mutable copy.
        byte[] buf = new byte[svg.Length + 1];
        Array.Copy(svg, buf, svg.Length);
        fixed (byte* p = buf)
        {
            NSVGimage* image = nsvgParse((IntPtr)p, "px", 96.0f);
            if (image == null || image->width <= 0 || image->height <= 0)
            {
                if (image != null) nsvgDelete(image);
                return null;
            }
            float scale = SvgRasterSize / MathF.Max(image->width, image->height);
            int w = Math.Max(1, (int)(image->width * scale));
            int h = Math.Max(1, (int)(image->height * scale));
            byte[] pixels = new byte[w * h * 4];
            fixed (byte* dst = pixels)
                nsvgRasterize(_rasterizer, image, 0, 0, scale, dst, w, h, w * 4);
            nsvgDelete(image);
            return UIImage.FromRgba(vg, w, h, pixels);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var img in _images.Values) img?.Dispose();
        _images.Clear();
        if (_rasterizer != IntPtr.Zero) { nsvgDeleteRasterizer(_rasterizer); _rasterizer = IntPtr.Zero; }
    }
}
