// Pure-C# score card renderer — no GPU, no font library, no external deps.
// Output: 480×270 RGBA PNG matching the game's warm-cream palette.
public static class ScoreCardRenderer
{
    const int W = 480;
    const int H = 270;

    // Warm palette matching SmilingFruits HUD
    static readonly (byte r, byte g, byte b) Cream      = (245, 230, 200);
    static readonly (byte r, byte g, byte b) OrangeDark = (232, 133,  61);
    static readonly (byte r, byte g, byte b) BrownText  = (140,  95,  61);

    // Returns a PNG-encoded byte[] ready to write via SFilesystem.
    public static byte[] Render(int score, string gameName = "Smiling Fruits")
    {
        var px = new byte[W * H * 4];

        // ── Background ───────────────────────────────────────────────────────────
        FillRect(px, 0, 0, W, H, Cream);

        // ── Border stripes ───────────────────────────────────────────────────────
        FillRect(px, 0, 0,     W,  8, OrangeDark);  // top
        FillRect(px, 0, H - 8, W,  8, OrangeDark);  // bottom
        FillRect(px, 0,     8, 6, H - 16, OrangeDark);  // left
        FillRect(px, W - 6, 8, 6, H - 16, OrangeDark);  // right

        // ── Game name (top, small) ───────────────────────────────────────────────
        // Scale 1: 8×8 per glyph — fine for a subtitle line.
        // Clamp to fit within the safe area (x 8..472).
        string displayName = TruncateToFit(gameName, scaleX: 1, maxPx: W - 20);
        GlyphAtlas.DrawCentered(px, W, H, displayName,
            scaleX: 1, scaleY: 1, cx: W / 2, cy: 30, color: BrownText);

        // ── Score (centre, large) ────────────────────────────────────────────────
        string scoreStr = FormatScore(score);
        // Pick scale so the score string fits horizontally within the safe area.
        int scale = ScoreScale(scoreStr.Length);
        GlyphAtlas.DrawCentered(px, W, H, scoreStr,
            scaleX: scale, scaleY: scale, cx: W / 2, cy: 145, color: BrownText);

        // ── "pts" label (below score) ────────────────────────────────────────────
        GlyphAtlas.DrawCentered(px, W, H, "pts",
            scaleX: 2, scaleY: 2, cx: W / 2, cy: 200, color: OrangeDark);

        return PngEncoder.Encode(px, W, H);
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

    // Choose magnification so the score string (each glyph 8px wide × scaleX)
    // fits within the canvas safe area (W − 20 px side margins).
    static int ScoreScale(int charCount)
    {
        int safeW = W - 20;
        for (int s = 4; s >= 1; s--)
            if (charCount * 8 * s <= safeW) return s;
        return 1;
    }

    // Truncate text so it fits within maxPx at scale 1.
    static string TruncateToFit(string text, int scaleX, int maxPx)
    {
        int maxChars = maxPx / (8 * scaleX);
        return text.Length <= maxChars ? text : text[..maxChars];
    }

    // Fill a rectangle in the RGBA pixel buffer (stride = W).
    static void FillRect(byte[] px, int x, int y, int w, int h,
                         (byte r, byte g, byte b) c)
    {
        int xEnd = System.Math.Min(x + w, W);
        int yEnd = System.Math.Min(y + h, H);
        for (int row = y; row < yEnd; row++)
        for (int col = x; col < xEnd; col++)
        {
            int i = (row * W + col) * 4;
            px[i]   = c.r;
            px[i+1] = c.g;
            px[i+2] = c.b;
            px[i+3] = 255;
        }
    }
}
