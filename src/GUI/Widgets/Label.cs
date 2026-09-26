using System.Collections.Generic;

namespace Sokol.GUI;

public enum TextAlign { Left, Center, Right }
public enum TextWrap  { None, Wrap }

/// <summary>
/// Single- or multi-line read-only text widget.
/// </
public class Label : Widget
{
    public virtual string  Text      { get; set; } = string.Empty;
    public UIColor? ForeColor { get; set; }
    public Font?   Font      { get; set; }
    public string? FontName  { get; set; }          // font name override (resolved at render time)
    public float   FontSize  { get; set; } = 0f;   // 0 = theme default
    public TextAlign Align   { get; set; } = TextAlign.Left;
    public TextWrap  Wrap    { get; set; } = TextWrap.None;

    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        ApplyFont(renderer);
        string text = Text;
        if (Wrap == TextWrap.Wrap && Bounds.Width > 0)
        {
            // Measure at the width Draw actually wraps at — the padding-deflated inner box, NOT the full
            // bounds. Measuring wider wraps to fewer lines than get drawn, so the reported height comes
            // up short: inside a ScrollView that shortfall is exactly the tail of the text, and it can
            // never be scrolled to (the rules overlay lost its last line on a phone).
            float innerW = MathF.Max(1f, Bounds.Width - Padding.Horizontal);
            var key = StyleKey(renderer);
            if (!string.Equals(text, _mText, StringComparison.Ordinal) || key != _mKey)
            { _mW0 = _mW1 = -1f; _mText = text; _mKey = key; }
            if (innerW == _mW0) return _mR0;
            if (innerW == _mW1) return _mR1;
            var (w, h) = renderer.MeasureTextBounds(0, 0, innerW, text);
            var r = new Vector2(w + Padding.Horizontal, h + Padding.Vertical);
            _mW1 = _mW0; _mR1 = _mR0; _mW0 = innerW; _mR0 = r;
            return r;
        }
        float tw = string.IsNullOrEmpty(text) ? 0f : renderer.MeasureTextRaw(Visual(text));
        var   m  = renderer.MeasureTextMetrics();
        return new Vector2(tw + Padding.Horizontal, m.lineHeight + Padding.Vertical);
    }

    public override void Draw(Renderer renderer)
    {
        string text = Text;
        if (!Visible || string.IsNullOrEmpty(text)) return;

        var theme    = ThemeManager.Current;
        var fg       = ForeColor ?? theme.TextColor;
        var fsize    = FontSize > 0 ? FontSize : theme.FontSize;
        var bounds   = new Rect(0, 0, Bounds.Width, Bounds.Height);
        var inner    = bounds.Deflate(Padding);
        Visual(text);   // refreshes _rtlPara

        // Resolve effective alignment: if Auto flow + RTL paragraph, flip to Right
        var effectiveAlign = Align;
        if (effectiveAlign == TextAlign.Left &&
            ResolvedFlowDirection == FlowDirection.Auto &&
            _rtlPara)
        {
            effectiveAlign = TextAlign.Right;
        }
        else if (ResolvedFlowDirection == FlowDirection.RightToLeft &&
                 effectiveAlign == TextAlign.Left)
        {
            effectiveAlign = TextAlign.Right;
        }

        ApplyFont(renderer);
        var hAlign = effectiveAlign switch
        {
            TextAlign.Center => TextHAlign.Center,
            TextAlign.Right  => TextHAlign.Right,
            _                => TextHAlign.Left,
        };
        // Wrapped text is a box anchored on the FIRST line's TOP (it grows downward from inner.Y);
        // single-line text is centred vertically in the widget. Anchoring a text box on the line
        // MIDDLE (the old default) drew the first line half a line high — harmless normally, but its
        // top was clipped inside a scroll view.
        renderer.SetTextAlign(hAlign, Wrap == TextWrap.Wrap ? TextVAlign.Top : TextVAlign.Middle);

        // ⛔ While DrawRecorder records, draw through the Renderer's own text calls: they are what record
        // each text block (with its LOGICAL text as the tag), and the overlap/fit audits read those entries.
        if (Wrap == TextWrap.Wrap)
        {
            if (DrawRecorder.Recording) renderer.DrawTextBox(inner.X, inner.Y, inner.Width, text, fg);
            else DrawRows(renderer, text, inner, hAlign, fg);
        }
        else
        {
            float x = effectiveAlign switch
            {
                TextAlign.Center => inner.X + inner.Width * 0.5f,
                TextAlign.Right  => inner.Right,
                _                => inner.X,
            };
            if (DrawRecorder.Recording) renderer.DrawText(x, inner.Y + inner.Height * 0.5f, text, fg);
            else { renderer.SetFillColor(fg); renderer.DrawTextRaw(x, inner.Y + inner.Height * 0.5f, Visual(text)); }
        }

        base.Draw(renderer);
    }

    protected void ApplyFont(Renderer renderer)
    {
        var theme = ThemeManager.Current;
        renderer.SetFont(FontName ?? Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }

    // ─── Text caches ─────────────────────────────────────────────────────────────────────────────
    // ⛔ Performance: a page of wrapped labels (licences, rules, settings help) was slow on phones because
    // every frame each label (1) ran the BiDi algorithm over its whole text twice — measure and draw,
    // (2) was re-measured several times by the layout, on screen or not, at widths that ALTERNATE by one
    // pixel within the same frame (box-layout rounding, 668 ↔ 669), and (3) had its rows re-broken by
    // nvgTextBox. So: the visual (BiDi) form is kept per text; the wrapped size is kept for TWO widths
    // (one entry would miss on every call); and the rows are broken once per width, then drawn one
    // nvgText each, skipping rows outside the cull clip. Every cache is keyed on everything the result
    // depends on: the text, the font name and size, letter spacing, line height, the text pixel scale
    // and FontRegistry.Version (a font or fallback arriving changes the widths).

    readonly record struct TextStyle(string Font, float Size, float LetterSpacing, float LineHeight,
                                     float PixelScale, int FontVersion);

    /// <summary>The style the next measurement runs under. Call after <see cref="ApplyFont"/>.</summary>
    TextStyle StyleKey(Renderer renderer)
    {
        var theme = ThemeManager.Current;
        return new TextStyle(FontName ?? Font?.Name ?? theme.DefaultFont, FontSize > 0 ? FontSize : theme.FontSize,
                             renderer.LetterSpacing, renderer.LineHeight, renderer.TextPixelScale,
                             FontRegistry.Instance.Version);
    }

    // BiDi: a pure function of the text.
    string? _bidiText;
    string  _visual = string.Empty;
    bool    _rtlPara;

    string Visual(string text)
    {
        if (!string.Equals(text, _bidiText, StringComparison.Ordinal))
        {
            _bidiText = text;
            _visual   = BidiHelper.ToVisual(text);
            _rtlPara  = BidiHelper.IsRTLParagraph(text);
        }
        return _visual;
    }

    // Wrapped size, two widths.
    string?   _mText;
    TextStyle _mKey;
    float     _mW0 = -1f, _mW1 = -1f;
    Vector2   _mR0, _mR1;

    // Wrapped rows: byte ranges into one UTF-8 buffer of visual text, each with its width and its y below
    // the box top. Alignment is applied at draw time, so it is not part of the key.
    record struct Row(int Start, int End, float Width, float Y);
    string?   _rowsText;
    TextStyle _rowsKey;
    float     _rowsW = -1f;
    readonly List<byte> _rowBytes = [];   // reused across rebuilds: a label whose width animates rebuilds every frame
    readonly List<Row>  _rows = [];
    float     _rowStep;

    /// <summary>What <see cref="Renderer.DrawTextBox(float,float,float,string)"/> draws, row for row:
    /// same breaks, same positions (nvgTextBox's x per alignment and its <c>lineh * lineHeight</c> step;
    /// an RTL paragraph is broken on the LOGICAL text, then each line's visual form is broken again at the
    /// same width and the lines step by the font line height, exactly as the Renderer does it).</summary>
    unsafe void DrawRows(Renderer renderer, string text, Rect inner, TextHAlign hAlign, UIColor fg)
    {
        float maxW = inner.Width;
        var key = StyleKey(renderer);
        if (maxW != _rowsW || key != _rowsKey || !string.Equals(text, _rowsText, StringComparison.Ordinal))
        {
            BuildRows(renderer, text, maxW);
            _rowsW = maxW; _rowsKey = key; _rowsText = text;
        }

        // nvgTextBox draws each row left-aligned at a computed x; do the same, then put the align back.
        renderer.SetTextAlign(TextHAlign.Left, TextVAlign.Top);
        renderer.SetFillColor(fg);
        // Rows wholly outside the clip are skipped; a generous margin keeps any glyph that reaches past
        // its line (accents, descenders) drawn.
        float top = float.MinValue, bottom = float.MaxValue;
        if (renderer.CanCull)
        {
            var clip = renderer.CullClip;
            top = clip.Y - 2f * _rowStep; bottom = clip.Bottom + _rowStep;
        }
        fixed (byte* p = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_rowBytes))
        {
            foreach (var row in System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_rows))
            {
                float y = inner.Y + row.Y;
                if (y < top || y > bottom) continue;
                float x = hAlign switch
                {
                    TextHAlign.Center => inner.X + maxW * 0.5f - row.Width * 0.5f,
                    TextHAlign.Right  => inner.X + maxW - row.Width,
                    _                 => inner.X,
                };
                renderer.DrawTextRaw(x, y, p + row.Start, p + row.End);
            }
        }
        renderer.SetTextAlign(hAlign, TextVAlign.Top);
    }

    unsafe void BuildRows(Renderer renderer, string text, float maxW)
    {
        var   m    = renderer.MeasureTextMetrics();
        float step = m.lineHeight * renderer.LineHeight;
        _rowStep = MathF.Max(step, m.lineHeight);

        // The visual lines and the y each one starts at.
        _lines.Clear();
        if (!_rtlPara) _lines.Add((Visual(text), 0f));
        else
        {
            // Broken under the label's own alignment, as Renderer.DrawTextBox does.
            float lineY = 0f;
            foreach (var line in renderer.BreakLogicalLines(text, maxW))
            {
                if (line.Length > 0) _lines.Add((BidiHelper.ToVisual(line), lineY));
                lineY += step;   // honour the SetLineHeight factor, like the LTR rows below
            }
        }

        var bytes = _rowBytes; bytes.Clear();
        var rows  = _rows;     rows.Clear();
        renderer.SetTextAlign(TextHAlign.Left, TextVAlign.Top);   // nvgTextBox breaks left-aligned
        Sokol.NanoVG.NVGtextRowRaw* br = stackalloc Sokol.NanoVG.NVGtextRowRaw[2];
        foreach (var (lineText, lineY) in _lines)
        {
            var utf8 = System.Text.Encoding.UTF8.GetBytes(lineText);
            int baseOff = bytes.Count;
            bytes.AddRange(utf8);
            float y = lineY;
            fixed (byte* p = utf8)
            {
                // Two rows per call and restart at the last row's `next`, exactly like nvgTextBox's loop.
                byte* cur = p, end = p + utf8.Length;
                int n;
                while ((n = Sokol.NanoVG.nvgTextBreakLines(renderer.VGContext, cur, end, maxW, br, 2)) > 0)
                {
                    for (int i = 0; i < n; i++)
                    {
                        rows.Add(new Row(baseOff + (int)(br[i].start - p), baseOff + (int)(br[i].end - p), br[i].width, y));
                        y += step;
                    }
                    cur = br[n - 1].next;
                }
            }
        }
    }

    readonly List<(string Visual, float Y)> _lines = [];
}
