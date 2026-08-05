using System;
using System.Runtime.CompilerServices;
using static Sokol.NanoVG;

namespace Sokol.GUI;

/// <summary>Horizontal text alignment for <see cref="Renderer.SetTextAlign(TextHAlign,TextVAlign)"/>.</summary>
public enum TextHAlign { Left = 1, Center = 2, Right = 4 }

/// <summary>Vertical text alignment for <see cref="Renderer.SetTextAlign(TextHAlign,TextVAlign)"/>.</summary>
public enum TextVAlign { Top = 8, Middle = 16, Bottom = 32, Baseline = 64 }

/// <summary>Compositing mode for <see cref="Renderer.SetBlend(BlendMode)"/>.</summary>
public enum BlendMode
{
    /// <summary>Standard source-over alpha blending (NVG_SOURCE_OVER).</summary>
    Normal,
    /// <summary>Additive blending — source adds to destination (NVG_LIGHTER); fire/glow/sparks.</summary>
    Additive,
}

/// <summary>Result of <see cref="Renderer.MeasureTextMetrics()"/>.</summary>
public record struct TextMetrics(float Ascender, float Descender, float lineHeight);

/// <summary>
/// Wraps the raw NanoVG context behind a typed API so widgets never
/// handle unsafe pointers directly.  All coordinates are logical pixels
/// (already divided by dpiScale by the caller).
/// </summary>
public sealed class Renderer
{
    private IntPtr _vg;
    private float  _dpiScale = 1f;

    /// <summary>The raw NanoVG context — escape hatch for advanced use.</summary>
    public IntPtr VGContext => _vg;

    /// <summary>Current device-pixel ratio (set on each BeginFrame).</summary>
    public float DpiScale => _dpiScale;

    public Renderer(IntPtr vg) => _vg = vg;

    // -------------------------------------------------------------------------
    // Frame lifecycle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Begin a NanoVG frame.  Call once per Sokol frame after
    /// <c>sg_begin_pass</c>.
    /// </summary>
    public void BeginFrame(float logicalW, float logicalH, float dpiScale)
    {
        _dpiScale = dpiScale;
        nvgBeginFrame(_vg, logicalW, logicalH, dpiScale);
    }

    /// <summary>End the NanoVG frame.  Call before <c>sg_end_pass</c>.</summary>
    public void EndFrame() => nvgEndFrame(_vg);

    // -------------------------------------------------------------------------
    // State save / restore
    // -------------------------------------------------------------------------

    public void Save()    => nvgSave(_vg);
    public void Restore() => nvgRestore(_vg);

    // -------------------------------------------------------------------------
    // Transform
    // -------------------------------------------------------------------------

    public void ResetTransform()              => nvgResetTransform(_vg);
    public void Translate(Vector2 offset)     => nvgTranslate(_vg, offset.X, offset.Y);
    public void Translate(float x, float y)  => nvgTranslate(_vg, x, y);
    public void Scale(Vector2 scale)          => nvgScale(_vg, scale.X, scale.Y);
    public void Scale(float s)                => nvgScale(_vg, s, s);
    public void Rotate(float radians)         => nvgRotate(_vg, radians);

    // -------------------------------------------------------------------------
    // Clip / scissor
    // -------------------------------------------------------------------------

    public void ClipRect(Rect r)          => nvgScissor(_vg, r.X, r.Y, r.Width, r.Height);
    public void IntersectClip(Rect r)     => nvgIntersectScissor(_vg, r.X, r.Y, r.Width, r.Height);
    public void ResetClip()               => nvgResetScissor(_vg);

    // -------------------------------------------------------------------------
    // Color / style
    // -------------------------------------------------------------------------

    public void SetFillColor(UIColor c)   => nvgFillColor(_vg, c.ToNVGcolor());
    public void SetStrokeColor(UIColor c) => nvgStrokeColor(_vg, c.ToNVGcolor());
    public void SetStrokeWidth(float w)   => nvgStrokeWidth(_vg, w);
    public void SetGlobalAlpha(float a)   => nvgGlobalAlpha(_vg, a);

    /// <summary>Set the global composite operation. <see cref="BlendMode.Additive"/> maps to
    /// NVG_LIGHTER (glow/fire), <see cref="BlendMode.Normal"/> to NVG_SOURCE_OVER. NanoVG stores this
    /// in its state stack, so it is restored by <see cref="Restore"/>; callers that change it outside a
    /// Save/Restore pair must reset it to <see cref="BlendMode.Normal"/> when done.</summary>
    public void SetBlend(BlendMode mode) =>
        nvgGlobalCompositeOperation(_vg, (int)(mode == BlendMode.Additive
            ? NVGcompositeOperation.NVG_LIGHTER
            : NVGcompositeOperation.NVG_SOURCE_OVER));

    // -------------------------------------------------------------------------
    // Draw recording (DrawRecorder)
    // -------------------------------------------------------------------------
    // Rects are recorded through the CURRENT NVG transform (widgets translate/scale before drawing),
    // so entries land in the same logical-px screen space regardless of the caller's local coords.
    // Zero cost when DrawRecorder.Recording is off: one static-bool test per draw call; the
    // transform query and text measuring below only run while recording.

    // Pending-path bbox for the low-level path API (BeginPath..Fill/Stroke); only maintained while
    // recording, and only by the PUBLIC path verbs — the shape helpers above talk to nvg directly.
    float _pMinX, _pMinY, _pMaxX, _pMaxY;
    bool  _pAny;

    void Rec(float x, float y, float w, float h, DrawRecorder.Kind kind, string tag)
    {
        if (!DrawRecorder.Recording) return;
        unsafe
        {
            float* t = stackalloc float[6];
            nvgCurrentTransform(_vg, ref t[0]);
            // bbox of the four transformed corners (sx = a·x + c·y + e; sy = b·x + d·y + f)
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float px = (i & 1) == 0 ? x : x + w;
                float py = (i & 2) == 0 ? y : y + h;
                float sx = t[0] * px + t[2] * py + t[4];
                float sy = t[1] * px + t[3] * py + t[5];
                if (sx < minX) minX = sx; if (sx > maxX) maxX = sx;
                if (sy < minY) minY = sy; if (sy > maxY) maxY = sy;
            }
            DrawRecorder.Add(new Rect(minX, minY, maxX - minX, maxY - minY), kind, tag);
        }
    }

    void PathPt(float x, float y)
    {
        if (!DrawRecorder.Recording) return;
        if (!_pAny) { _pMinX = _pMaxX = x; _pMinY = _pMaxY = y; _pAny = true; return; }
        if (x < _pMinX) _pMinX = x; if (x > _pMaxX) _pMaxX = x;
        if (y < _pMinY) _pMinY = y; if (y > _pMaxY) _pMaxY = y;
    }

    void PathRect(float x, float y, float w, float h) { PathPt(x, y); PathPt(x + w, y + h); }

    void RecPath(string tag)
    {
        if (!DrawRecorder.Recording || !_pAny) return;
        Rec(_pMinX, _pMinY, _pMaxX - _pMinX, _pMaxY - _pMinY, DrawRecorder.Kind.NvgShape, tag);
    }

    // -------------------------------------------------------------------------
    // Shapes
    // -------------------------------------------------------------------------

    public void FillRect(Rect r)
    {
        Rec(r.X, r.Y, r.Width, r.Height, DrawRecorder.Kind.NvgShape, "rect");
        nvgBeginPath(_vg);
        nvgRect(_vg, r.X, r.Y, r.Width, r.Height);
        nvgFill(_vg);
    }

    public void StrokeRect(Rect r)
    {
        Rec(r.X, r.Y, r.Width, r.Height, DrawRecorder.Kind.NvgShape, "rect-stroke");
        nvgBeginPath(_vg);
        nvgRect(_vg, r.X, r.Y, r.Width, r.Height);
        nvgStroke(_vg);
    }

    public void FillRoundedRect(Rect r, CornerRadius cr)
    {
        Rec(r.X, r.Y, r.Width, r.Height, DrawRecorder.Kind.NvgShape, "rrect");
        nvgBeginPath(_vg);
        if (cr.IsUniform)
            nvgRoundedRect(_vg, r.X, r.Y, r.Width, r.Height, cr.TopLeft);
        else
            nvgRoundedRectVarying(_vg, r.X, r.Y, r.Width, r.Height,
                cr.TopLeft, cr.TopRight, cr.BottomRight, cr.BottomLeft);
        nvgFill(_vg);
    }

    public void StrokeRoundedRect(Rect r, CornerRadius cr)
    {
        Rec(r.X, r.Y, r.Width, r.Height, DrawRecorder.Kind.NvgShape, "rrect-stroke");
        nvgBeginPath(_vg);
        if (cr.IsUniform)
            nvgRoundedRect(_vg, r.X, r.Y, r.Width, r.Height, cr.TopLeft);
        else
            nvgRoundedRectVarying(_vg, r.X, r.Y, r.Width, r.Height,
                cr.TopLeft, cr.TopRight, cr.BottomRight, cr.BottomLeft);
        nvgStroke(_vg);
    }

    public void FillCircle(Vector2 center, float radius)
    {
        Rec(center.X - radius, center.Y - radius, radius * 2f, radius * 2f, DrawRecorder.Kind.NvgShape, "circle");
        nvgBeginPath(_vg);
        nvgCircle(_vg, center.X, center.Y, radius);
        nvgFill(_vg);
    }

    public void StrokeCircle(Vector2 center, float radius)
    {
        Rec(center.X - radius, center.Y - radius, radius * 2f, radius * 2f, DrawRecorder.Kind.NvgShape, "circle-stroke");
        nvgBeginPath(_vg);
        nvgCircle(_vg, center.X, center.Y, radius);
        nvgStroke(_vg);
    }

    public void DrawLine(Vector2 a, Vector2 b, float width)
    {
        if (DrawRecorder.Recording)
            Rec(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y), MathF.Abs(b.X - a.X), MathF.Abs(b.Y - a.Y),
                DrawRecorder.Kind.NvgShape, "line");
        nvgBeginPath(_vg);
        nvgMoveTo(_vg, a.X, a.Y);
        nvgLineTo(_vg, b.X, b.Y);
        nvgStrokeWidth(_vg, width);
        nvgStroke(_vg);
    }

    public void FillTriangle(Vector2 a, Vector2 b, Vector2 c, UIColor color)
    {
        if (DrawRecorder.Recording)
        {
            float minX = MathF.Min(a.X, MathF.Min(b.X, c.X)), minY = MathF.Min(a.Y, MathF.Min(b.Y, c.Y));
            Rec(minX, minY, MathF.Max(a.X, MathF.Max(b.X, c.X)) - minX,
                MathF.Max(a.Y, MathF.Max(b.Y, c.Y)) - minY, DrawRecorder.Kind.NvgShape, "tri");
        }
        SetFillColor(color);
        nvgBeginPath(_vg);
        nvgMoveTo(_vg, a.X, a.Y);
        nvgLineTo(_vg, b.X, b.Y);
        nvgLineTo(_vg, c.X, c.Y);
        nvgClosePath(_vg);
        nvgFill(_vg);
    }

    // -------------------------------------------------------------------------
    // Low-level path API
    // -------------------------------------------------------------------------

    public void BeginPath()  { _pAny = false; nvgBeginPath(_vg); }
    public void MoveTo(float x, float y)  { PathPt(x, y); nvgMoveTo(_vg, x, y); }
    public void LineTo(float x, float y)  { PathPt(x, y); nvgLineTo(_vg, x, y); }
    public void ArcTo(float x1, float y1, float x2, float y2, float radius)
    { PathPt(x1, y1); PathPt(x2, y2); nvgArcTo(_vg, x1, y1, x2, y2, radius); }
    public void BezierTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
    { PathPt(c1x, c1y); PathPt(c2x, c2y); PathPt(x, y); nvgBezierTo(_vg, c1x, c1y, c2x, c2y, x, y); }
    public void QuadTo(float cx, float cy, float x, float y)
    { PathPt(cx, cy); PathPt(x, y); nvgQuadTo(_vg, cx, cy, x, y); }
    public void ClosePath()  => nvgClosePath(_vg);
    public void AddRect(float x, float y, float w, float h)  { PathRect(x, y, w, h); nvgRect(_vg, x, y, w, h); }
    public void AddRect(Rect r)  { PathRect(r.X, r.Y, r.Width, r.Height); nvgRect(_vg, r.X, r.Y, r.Width, r.Height); }
    public void AddRoundedRect(float x, float y, float w, float h, float cr)
    { PathRect(x, y, w, h); nvgRoundedRect(_vg, x, y, w, h, cr); }
    public void AddRoundedRectVarying(float x, float y, float w, float h,
        float tlr, float trr, float brr, float blr)
    { PathRect(x, y, w, h); nvgRoundedRectVarying(_vg, x, y, w, h, tlr, trr, brr, blr); }
    public void AddCircle(float cx, float cy, float radius)
    { PathRect(cx - radius, cy - radius, radius * 2f, radius * 2f); nvgCircle(_vg, cx, cy, radius); }
    public void Fill()   { RecPath("path"); nvgFill(_vg); }
    public void Stroke() { RecPath("path-stroke"); nvgStroke(_vg); }
    public void SetFillPaint(NVGpaint paint) => nvgFillPaint(_vg, paint);

    // -------------------------------------------------------------------------
    // Gradients
    // -------------------------------------------------------------------------

    public NVGpaint LinearGradient(Vector2 start, Vector2 end, UIColor inner, UIColor outer) =>
        nvgLinearGradient(_vg, start.X, start.Y, end.X, end.Y, inner.ToNVGcolor(), outer.ToNVGcolor());

    public NVGpaint LinearGradient(float x0, float y0, float x1, float y1, UIColor inner, UIColor outer) =>
        nvgLinearGradient(_vg, x0, y0, x1, y1, inner.ToNVGcolor(), outer.ToNVGcolor());

    public NVGpaint RadialGradient(Vector2 center, float innerR, float outerR, UIColor inner, UIColor outer) =>
        nvgRadialGradient(_vg, center.X, center.Y, innerR, outerR, inner.ToNVGcolor(), outer.ToNVGcolor());

    public NVGpaint BoxGradient(Rect r, float cornerR, float feather, UIColor inner, UIColor outer) =>
        nvgBoxGradient(_vg, r.X, r.Y, r.Width, r.Height, cornerR, feather, inner.ToNVGcolor(), outer.ToNVGcolor());

    public void FillWithPaint(NVGpaint paint)
    {
        RecPath("path-paint");
        nvgFillPaint(_vg, paint);
        nvgFill(_vg);
    }

    public void FillRoundedRectWithPaint(Rect r, CornerRadius cr, NVGpaint paint)
    {
        Rec(r.X, r.Y, r.Width, r.Height, DrawRecorder.Kind.NvgShape, "rrect-paint");
        nvgBeginPath(_vg);
        if (cr.IsUniform)
            nvgRoundedRect(_vg, r.X, r.Y, r.Width, r.Height, cr.TopLeft);
        else
            nvgRoundedRectVarying(_vg, r.X, r.Y, r.Width, r.Height,
                cr.TopLeft, cr.TopRight, cr.BottomRight, cr.BottomLeft);
        nvgFillPaint(_vg, paint);
        nvgFill(_vg);
    }

    public void FillRoundedRectWithPaint(Rect r, float cr, NVGpaint paint) =>
        FillRoundedRectWithPaint(r, new CornerRadius(cr), paint);

    /// <summary>Fill a rect with only the top-left and top-right corners rounded, filled with a paint (gradient/image).</summary>
    public void FillRoundedRectTopWithPaint(Rect r, float cr, NVGpaint paint)
    {
        Rec(r.X, r.Y, r.Width, r.Height, DrawRecorder.Kind.NvgShape, "rrect-paint");
        nvgBeginPath(_vg);
        nvgRoundedRectVarying(_vg, r.X, r.Y, r.Width, r.Height, cr, cr, 0f, 0f);
        nvgFillPaint(_vg, paint);
        nvgFill(_vg);
    }

    public void FillCircleWithPaint(float cx, float cy, float radius, NVGpaint paint)
    {
        Rec(cx - radius, cy - radius, radius * 2f, radius * 2f, DrawRecorder.Kind.NvgShape, "circle-paint");
        nvgBeginPath(_vg);
        nvgCircle(_vg, cx, cy, radius);
        nvgFillPaint(_vg, paint);
        nvgFill(_vg);
    }

    // -------------------------------------------------------------------------
    // Images
    // -------------------------------------------------------------------------

    /// <summary>Draw a crosshair (two perpendicular lines) centered at (cx, cy) with the given radius.</summary>
    public void DrawCrossHair(float cx, float cy, float radius, float strokeWidth, UIColor color)
    {
        SetStrokeColor(color);
        SetStrokeWidth(strokeWidth);
        nvgBeginPath(_vg);
        nvgMoveTo(_vg, cx, cy - radius); nvgLineTo(_vg, cx, cy + radius);
        nvgMoveTo(_vg, cx - radius, cy); nvgLineTo(_vg, cx + radius, cy);
        nvgStroke(_vg);
    }

    public void DrawImage(UIImage image, Rect dest, float alpha = 1f) =>
        DrawImage(image.Id, dest, alpha);

    public void DrawImage(int nvgImageId, Rect dest, float alpha = 1f)
    {
        Rec(dest.X, dest.Y, dest.Width, dest.Height, DrawRecorder.Kind.NvgImage, "image");
        var paint = nvgImagePattern(_vg, dest.X, dest.Y, dest.Width, dest.Height, 0f, nvgImageId, alpha);
        nvgBeginPath(_vg);
        nvgRect(_vg, dest.X, dest.Y, dest.Width, dest.Height);
        nvgFillPaint(_vg, paint);
        nvgFill(_vg);
    }

    /// <summary>Draw one frame of a sprite sheet (a <paramref name="cols"/>×<paramref name="rows"/>
    /// grid) into <paramref name="dest"/>. Frame 0 is top-left, advancing left-to-right then
    /// top-to-bottom. The sheet is scaled so a single cell exactly fills <paramref name="dest"/>, and
    /// the fill is clipped to that rect so only the chosen cell shows.</summary>
    public void DrawImageFrame(UIImage image, Rect dest, int cols, int rows, int frame, float alpha = 1f)
        => DrawImageFrame(image.Id, dest, cols, rows, frame, alpha);

    public void DrawImageFrame(int nvgImageId, Rect dest, int cols, int rows, int frame, float alpha = 1f)
    {
        Rec(dest.X, dest.Y, dest.Width, dest.Height, DrawRecorder.Kind.NvgImage, "image");
        int c = Math.Max(1, cols), r = Math.Max(1, rows);
        int f = Math.Clamp(frame, 0, c * r - 1);
        int col = f % c, row = f / c;
        // Map the whole sheet so one cell == dest, offsetting so the chosen cell lands on dest.
        float ox = dest.X - col * dest.Width;
        float oy = dest.Y - row * dest.Height;
        var paint = nvgImagePattern(_vg, ox, oy, dest.Width * c, dest.Height * r, 0f, nvgImageId, alpha);
        nvgBeginPath(_vg);
        nvgRect(_vg, dest.X, dest.Y, dest.Width, dest.Height);
        nvgFillPaint(_vg, paint);
        nvgFill(_vg);
    }

    /// <summary>Create a mutable (stream-update) image. Pass the handle to UpdateImage each frame.</summary>
    public int CreateImageRGBA(int w, int h, int flags)
        => nvgCreateImageRGBA(_vg, w, h, flags, in Unsafe.NullRef<byte>());

    /// <summary>Upload new pixel data to a mutable image created with CreateImageRGBA.</summary>
    public void UpdateImage(int imageId, in byte pixels)
        => nvgUpdateImage(_vg, imageId, in pixels);

    // -------------------------------------------------------------------------
    // Text
    // -------------------------------------------------------------------------

    public void SetFont(Font font)      => nvgFontFaceId(_vg, font.Id);
    public void SetFont(string name)    => nvgFontFace(_vg, name);
    public void SetFontSize(float size) => nvgFontSize(_vg, size);
    public void SetTextAlign(NVGalign align) => nvgTextAlign(_vg, (int)align);
    public void SetLetterSpacing(float sp)   => nvgTextLetterSpacing(_vg, sp);
    public void SetLineHeight(float lh)      => nvgTextLineHeight(_vg, lh);

    /// <summary>Record a single-line string's on-screen bounds. Only called while recording; the
    /// bounds respect the current font/size/align state, positioned at the draw origin.</summary>
    void RecText(float x, float y, string visual, string logical)
    {
        unsafe
        {
            // 4-element buffer, same NativeAOT rule as MeasureText below.
            float* b = stackalloc float[4];
            nvgTextBounds(_vg, x, y, visual, null, ref b[0]);
            Rec(b[0], b[1], b[2] - b[0], b[3] - b[1], DrawRecorder.Kind.NvgText, "text:" + logical);
        }
    }

    void RecTextBox(float x, float y, float maxW, string visual, string logical)
    {
        unsafe
        {
            float* b = stackalloc float[4];
            nvgTextBoxBounds(_vg, x, y, maxW, visual, null, ref b[0]);
            Rec(b[0], b[1], b[2] - b[0], b[3] - b[1], DrawRecorder.Kind.NvgText, "text:" + logical);
        }
    }

    /// <summary>Draw a single-line text string with automatic BiDi reordering. Returns the x-advance.</summary>
    public float DrawText(Vector2 pos, string text) =>
        DrawText(pos.X, pos.Y, text);

    public float DrawText(float x, float y, string text)
    {
        var visual = BidiHelper.ToVisual(text);
        if (DrawRecorder.Recording) RecText(x, y, visual, text);
        return nvgText(_vg, x, y, visual, null);
    }

    /// <summary>Draw text wrapped inside a bounding box with automatic BiDi reordering.</summary>
    public void DrawTextBox(Rect bounds, string text) =>
        DrawTextBox(bounds.X, bounds.Y, bounds.Width, text);

    /// <summary>Draw a single-line text string without BiDi reordering (for widgets that handle BiDi at a higher level).</summary>
    public float DrawTextRaw(float x, float y, string text)
    {
        if (DrawRecorder.Recording) RecText(x, y, text, text);
        return nvgText(_vg, x, y, text, null);
    }

    /// <summary>Draw text wrapped inside a bounding box without BiDi reordering.</summary>
    public void DrawTextBoxRaw(float x, float y, float maxW, string text)
    {
        if (DrawRecorder.Recording) RecTextBox(x, y, maxW, text, text);
        nvgTextBox(_vg, x, y, maxW, text, null);
    }

    /// <summary>Measure rendered advance width of a string at current font/size settings. Applies BiDi reordering.</summary>
    public float MeasureText(string text)
    {
        // nvgTextBounds writes 4 floats into the bounds array even though we only
        // care about the return value.  Using a single ref float is not safe in
        // NativeAOT Release builds where unused locals are eliminated — the C
        // function then writes out-of-bounds and corrupts the stack (SIGSEGV on
        // iOS/Android).  Always provide a proper 4-element buffer.
        if (string.IsNullOrEmpty(text)) return 0f;
        var visual = BidiHelper.ToVisual(text);
        unsafe
        {
            float* bounds = stackalloc float[4];
            return nvgTextBounds(_vg, 0f, 0f, visual, null, ref bounds[0]);
        }
    }

    /// <summary>Measure the bounding rect of a string at current font/size settings. Applies BiDi reordering.</summary>
    public Rect MeasureTextBounds(string text)
    {
        if (string.IsNullOrEmpty(text)) return default;
        var visual = BidiHelper.ToVisual(text);
        unsafe
        {
            float* bounds = stackalloc float[4];
            nvgTextBounds(_vg, 0f, 0f, visual, null, ref bounds[0]);
            return Rect.FromLTRB(bounds[0], bounds[1], bounds[2], bounds[3]);
        }
    }

    /// <summary>Measure rendered advance width without BiDi reordering.</summary>
    public float MeasureTextRaw(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        unsafe
        {
            float* bounds = stackalloc float[4];
            return nvgTextBounds(_vg, 0f, 0f, text, null, ref bounds[0]);
        }
    }

    /// <summary>Get font ascender, descender and line height at current font/size settings.</summary>
    public void MeasureTextMetrics(out float ascender, out float descender, out float lineH)
    {
        float asc = 0f, desc = 0f, lh = 0f;
        nvgTextMetrics(_vg, ref asc, ref desc, ref lh);
        ascender  = asc;
        descender = desc;
        lineH     = lh;
    }

    // -------------------------------------------------------------------------
    // Shadow helper
    // -------------------------------------------------------------------------

    /// <summary>Draw a drop shadow underneath a rounded rect.</summary>
    public void DrawDropShadow(Rect r, CornerRadius cr, float shadowBlur,
        Vector2 shadowOffset, UIColor shadowColor)
    {
        var shadowRect = r.Offset(shadowOffset);
        var paint      = nvgBoxGradient(_vg,
            shadowRect.X, shadowRect.Y, shadowRect.Width, shadowRect.Height,
            cr.IsUniform ? cr.TopLeft : MathF.Max(cr.TopLeft, cr.TopRight),
            shadowBlur,
            shadowColor.ToNVGcolor(),
            UIColor.Transparent.ToNVGcolor());

        // Draw a slightly expanded rect so the shadow is visible outside the widget.
        var outer = shadowRect.Inflate(new Thickness(shadowBlur));
        nvgBeginPath(_vg);
        nvgRect(_vg, outer.X, outer.Y, outer.Width, outer.Height);
        nvgFillPaint(_vg, paint);
        nvgFill(_vg);
    }

    // -------------------------------------------------------------------------
    // Typed text alignment (avoids widgets needing to import Sokol.NanoVG)
    // -------------------------------------------------------------------------

    public void SetTextAlign(TextHAlign h, TextVAlign v = TextVAlign.Middle)
        => nvgTextAlign(_vg, (int)h | (int)v);

    /// <summary>No-argument overload returning a <see cref="TextMetrics"/> value.</summary>
    public TextMetrics MeasureTextMetrics()
    {
        float asc = 0f, desc = 0f, lh = 0f;
        nvgTextMetrics(_vg, ref asc, ref desc, ref lh);
        return new TextMetrics(asc, desc, lh);
    }

    // -------------------------------------------------------------------------
    // Color-overloads (set fill/stroke inline, then draw)
    // -------------------------------------------------------------------------

    public void FillRect(Rect r, UIColor c)
    {
        SetFillColor(c);
        FillRect(r);
    }

    public void FillRoundedRect(Rect r, float cr, UIColor c)
    {
        SetFillColor(c);
        FillRoundedRect(r, new CornerRadius(cr));
    }

    public void FillRoundedRect(Rect r, CornerRadius cr, UIColor c)
    {
        SetFillColor(c);
        FillRoundedRect(r, cr);
    }

    /// <summary>Fill a rounded rect with only the top two corners rounded.</summary>
    public void FillRectWithPaint(Rect r, NVGpaint paint)
    {
        Rec(r.X, r.Y, r.Width, r.Height, DrawRecorder.Kind.NvgShape, "rect-paint");
        nvgBeginPath(_vg);
        nvgRect(_vg, r.X, r.Y, r.Width, r.Height);
        nvgFillPaint(_vg, paint);
        nvgFill(_vg);
    }

    public void FillRoundedRectTop(Rect r, float cr, UIColor c)
    {
        SetFillColor(c);
        nvgBeginPath(_vg);
        nvgRoundedRectVarying(_vg, r.X, r.Y, r.Width, r.Height, cr, cr, 0f, 0f);
        nvgFill(_vg);
    }

    /// <summary>Stroke a rect with only the top-left and top-right corners rounded.</summary>
    public void StrokeRoundedRectTop(Rect r, float cr, float width, UIColor c)
    {
        SetStrokeColor(c);
        SetStrokeWidth(width);
        nvgBeginPath(_vg);
        nvgRoundedRectVarying(_vg, r.X, r.Y, r.Width, r.Height, cr, cr, 0f, 0f);
        nvgStroke(_vg);
    }

    public void StrokeRect(Rect r, float width, UIColor c)
    {
        SetStrokeColor(c);
        SetStrokeWidth(width);
        StrokeRect(r);
    }

    public void StrokeRoundedRect(Rect r, float cr, float width, UIColor c)
    {
        SetStrokeColor(c);
        SetStrokeWidth(width);
        StrokeRoundedRect(r, new CornerRadius(cr));
    }

    public void StrokeRoundedRect(Rect r, CornerRadius cr, float width, UIColor c)
    {
        SetStrokeColor(c);
        SetStrokeWidth(width);
        StrokeRoundedRect(r, cr);
    }

    public void FillCircle(float cx, float cy, float radius, UIColor c)
    {
        SetFillColor(c);
        FillCircle(new Vector2(cx, cy), radius);
    }

    public void StrokeCircle(float cx, float cy, float radius, float width, UIColor c)
    {
        SetStrokeColor(c);
        SetStrokeWidth(width);
        StrokeCircle(new Vector2(cx, cy), radius);
    }

    public void DrawLine(float x1, float y1, float x2, float y2, float width, UIColor c)
    {
        SetStrokeColor(c);
        DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), width);
    }

    public float DrawText(float x, float y, string text, UIColor c)
    {
        SetFillColor(c);
        return DrawText(x, y, text);
    }

    public void DrawTextBox(float x, float y, float maxW, string text)
    {
        // Recorded once for the whole wrapped block (per-line RTL splitting below draws inside it).
        if (DrawRecorder.Recording) RecTextBox(x, y, maxW, BidiHelper.ToVisual(text), text);
        if (!BidiHelper.IsRTLParagraph(text))
        {
            nvgTextBox(_vg, x, y, maxW, BidiHelper.ToVisual(text), null);
            return;
        }
        // RTL: applying ToVisual to the whole paragraph reverses line order when
        // nvgTextBox subsequently wraps it. Break into lines first on the logical
        // text, then apply ToVisual per line so character order is correct and
        // line order is preserved top-to-bottom.
        var metrics = MeasureTextMetrics();
        var utf8 = System.Text.Encoding.UTF8.GetBytes(text);
        unsafe
        {
            fixed (byte* ptr = utf8)
            {
                const int MaxRows = 32;
                Sokol.NanoVG.NVGtextRowRaw* rows = stackalloc Sokol.NanoVG.NVGtextRowRaw[MaxRows];
                byte* cur = ptr;
                byte* end = ptr + utf8.Length;
                float lineY = y;
                int count;
                while ((count = nvgTextBreakLines(_vg, cur, end, maxW, rows, MaxRows)) > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        int len = (int)(rows[i].end - rows[i].start);
                        if (len > 0)
                        {
                            string line = System.Text.Encoding.UTF8.GetString(rows[i].start, len);
                            nvgTextBox(_vg, x, lineY, maxW, BidiHelper.ToVisual(line), null);
                        }
                        lineY += metrics.lineHeight;
                    }
                    cur = rows[count - 1].next;
                    if (cur >= end) break;
                }
            }
        }
    }

    public void DrawTextBox(float x, float y, float maxW, string text, UIColor c)
    {
        SetFillColor(c);
        DrawTextBox(x, y, maxW, text);
    }

    /// <summary>Returns (width, height) of the wrapped text at current font/size settings. Applies BiDi reordering.</summary>
    public (float width, float height) MeasureTextBounds(float x, float y, float maxW, string text)
    {
        var visual = BidiHelper.ToVisual(text);
        unsafe
        {
            float* b = stackalloc float[4];
            nvgTextBoxBounds(_vg, x, y, maxW, visual, null, ref b[0]);
            return (b[2] - b[0], b[3] - b[1]);
        }
    }

    /// <summary>
    /// Drop-shadow overload taking a uniform float corner radius instead of <see cref="CornerRadius"/>.
    /// </summary>
    public void DrawDropShadow(Rect r, float cornerRadius, Vector2 shadowOffset, float shadowBlur, UIColor shadowColor)
        => DrawDropShadow(r, new CornerRadius(cornerRadius), shadowBlur, shadowOffset, shadowColor);
}
