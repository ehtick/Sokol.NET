// Pure-C# score card renderer — no GPU, no font library, no external deps.
public static class ScoreCardRenderer
{
    const int DefaultW = 480;
    const int DefaultH = 270;

    // Warm palette matching SmilingFruits HUD
    static readonly (byte r, byte g, byte b) Cream      = (245, 230, 200);
    static readonly (byte r, byte g, byte b) OrangeDark = (232, 133,  61);
    static readonly (byte r, byte g, byte b) BrownText  = (140,  95,  61);
    static readonly (byte r, byte g, byte b) White      = (255, 255, 255);

    // Returns a PNG-encoded byte[] ready to write via SFilesystem.
    // Uses the warm-cream palette as background.
    public static byte[] Render(int score, string gameName = "Smiling Fruits",
                                int w = DefaultW, int h = DefaultH)
    {
        var px = new byte[w * h * 4];
        FillRect(px, w, 0, 0, w, h, Cream);
        DrawOverlay(px, w, h, score, gameName);
        return PngEncoder.Encode(px, w, h);
    }

    // Overload that composites the score card overlay onto a caller-supplied RGB image.
    // rgbBackground is srcW*srcH*3 bytes (3 bytes per pixel, no alpha).
    // The image is scaled to w×h using nearest-neighbor.
    public static byte[] Render(byte[] rgbBackground, int srcW, int srcH,
                                int score, string gameName = "Smiling Fruits",
                                int w = DefaultW, int h = DefaultH)
    {
        var px = new byte[w * h * 4];
        if (srcW == w && srcH == h)
        {
            for (int i = 0; i < w * h; i++)
            {
                px[i * 4]     = rgbBackground[i * 3];
                px[i * 4 + 1] = rgbBackground[i * 3 + 1];
                px[i * 4 + 2] = rgbBackground[i * 3 + 2];
                px[i * 4 + 3] = 255;
            }
        }
        else
        {
            for (int row = 0; row < h; row++)
            for (int col = 0; col < w; col++)
            {
                int sx = col * srcW / w;
                int sy = row * srcH / h;
                int si = (sy * srcW + sx) * 3;
                int di = (row * w + col) * 4;
                px[di]     = rgbBackground[si];
                px[di + 1] = rgbBackground[si + 1];
                px[di + 2] = rgbBackground[si + 2];
                px[di + 3] = 255;
            }
        }
        DrawOverlay(px, w, h, score, gameName, imageBackground: true);
        return PngEncoder.Encode(px, w, h);
    }

    // Draws borders, game name, score, and "pts" label onto an existing RGBA buffer.
    // Layout positions scale proportionally with w×h relative to the 480×270 reference.
    // imageBackground: when true, uses white text for contrast against photo backgrounds.
    static void DrawOverlay(byte[] px, int w, int h, int score, string gameName,
                            bool imageBackground = false)
    {
        int bt = Math.Max(1, h * 8  / DefaultH);  // border thickness
        int bs = Math.Max(1, w * 6  / DefaultW);  // border side width

        // ── Border stripes ───────────────────────────────────────────────────────
        FillRect(px, w, 0,      0,      w,  bt, OrangeDark);  // top
        FillRect(px, w, 0,      h - bt, w,  bt, OrangeDark);  // bottom
        FillRect(px, w, 0,      bt,     bs, h - bt * 2, OrangeDark);  // left
        FillRect(px, w, w - bs, bt,     bs, h - bt * 2, OrangeDark);  // right

        if (imageBackground)
            AlphaFillRect(px, w, bs, bt, w - bs * 2, h - bt * 2, 0, 0, 0, 120);

        var textColor = imageBackground ? White : BrownText;
        var ptsColor  = imageBackground ? White : OrangeDark;

        // ── Game name (top, small) ───────────────────────────────────────────────
        string displayName = TruncateToFit(gameName, scaleX: 2, maxPx: w - 20);
        GlyphAtlas.DrawCentered(px, w, h, displayName,
            scaleX: 2, scaleY: 2, cx: w / 2, cy: h * 30 / DefaultH, color: textColor);

        // ── Score (centre, large) ────────────────────────────────────────────────
        string scoreStr = FormatScore(score);
        int scale = ScoreScale(scoreStr.Length, w);
        GlyphAtlas.DrawCentered(px, w, h, scoreStr,
            scaleX: scale, scaleY: scale, cx: w / 2, cy: h * 145 / DefaultH, color: textColor);

        // ── "pts" label (below score) ────────────────────────────────────────────
        GlyphAtlas.DrawCentered(px, w, h, "pts",
            scaleX: 3, scaleY: 3, cx: w / 2, cy: h * 200 / DefaultH, color: ptsColor);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    // Group digits with comma separators (e.g. 12345 → "12,345").
    static string FormatScore(int score)
    {
        string raw = score.ToString();
        if (raw.Length <= 3) return raw;
        var sb = new System.Text.StringBuilder();
        int rem = raw.Length % 3;
        if (rem > 0) sb.Append(raw, 0, rem);
        for (int i = rem; i < raw.Length; i += 3)
        {
            if (sb.Length > 0) sb.Append(',');
            sb.Append(raw, i, 3);
        }
        return sb.ToString();
    }

    // Choose magnification so the score string fits within the canvas safe area.
    static int ScoreScale(int charCount, int w)
    {
        int safeW = w - 20;
        for (int s = 4; s >= 1; s--)
            if (charCount * 8 * s <= safeW) return s;
        return 1;
    }

    // Truncate text so it fits within maxPx at the given scale.
    static string TruncateToFit(string text, int scaleX, int maxPx)
    {
        int maxChars = maxPx / (8 * scaleX);
        return text.Length <= maxChars ? text : text[..maxChars];
    }

    // Fill a rectangle in the RGBA pixel buffer. stride = canvas width in pixels.
    static void FillRect(byte[] px, int stride, int x, int y, int rw, int rh,
                         (byte r, byte g, byte b) c)
    {
        int xEnd = x + rw;
        int yEnd = y + rh;
        for (int row = y; row < yEnd; row++)
        for (int col = x; col < xEnd; col++)
        {
            int i = (row * stride + col) * 4;
            px[i]     = c.r;
            px[i + 1] = c.g;
            px[i + 2] = c.b;
            px[i + 3] = 255;
        }
    }

    // Alpha-blend a solid colour over an existing RGBA buffer (porter-duff "over").
    static void AlphaFillRect(byte[] px, int stride, int x, int y, int rw, int rh,
                               byte r, byte g, byte b, byte alpha)
    {
        int xEnd = x + rw;
        int yEnd = y + rh;
        int ia   = 255 - alpha;
        for (int row = y; row < yEnd; row++)
        for (int col = x; col < xEnd; col++)
        {
            int i = (row * stride + col) * 4;
            px[i]     = (byte)((r * alpha + px[i]     * ia) / 255);
            px[i + 1] = (byte)((g * alpha + px[i + 1] * ia) / 255);
            px[i + 2] = (byte)((b * alpha + px[i + 2] * ia) / 255);
            px[i + 3] = 255;
        }
    }
}
