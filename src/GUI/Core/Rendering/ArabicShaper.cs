using System;

namespace Sokol.GUI;

/// <summary>
/// Arabic contextual shaping: rewrites Arabic letters into the presentation form (isolated, final,
/// initial or medial) their neighbours call for, and fuses lam+alef into its ligature.
///
/// <para>Arabic is a joining script — the same letter has a different shape depending on whether it
/// connects to the letter before it, the one after it, both, or neither. A renderer that draws the raw
/// U+06xx code points produces disconnected, isolated letterforms: legible-ish to a machine, wrong and
/// hard to read for a person. Proper OpenType shaping would resolve this in the font's GSUB table, but
/// the NanoVG/fontstash text stack draws code points straight from the cmap, so the substitution has to
/// happen before the glyphs are looked up. That is what this does — the same approach GNU FriBidi takes
/// with fribidi_shape_arabic.</para>
///
/// <para>Shaping runs on LOGICAL order, before the BiDi reordering, because joining is a property of a
/// letter's logical neighbours. Everything that is not an Arabic joining letter passes through
/// untouched, so Latin, Hebrew, digits and punctuation are unaffected.</para>
///
/// <para>⛔ The output uses the Arabic Presentation Forms blocks (U+FB50.. and U+FE70..). The bundled
/// NotoSansArabic-Regular.ttf covers them; a font that does not would render .notdef, so keep the
/// coverage check in mind before swapping the Arabic font.</para>
/// </summary>
public static class ArabicShaper
{
    // Joining_Type of the characters that participate. 'D' = dual-joining (connects on both sides),
    // 'R' = right-joining (connects only to the preceding letter). Derived from the Unicode
    // presentation-form decompositions: a letter with initial+medial forms is dual, one with only a
    // final form is right-joining. Tuple order: (isolated, final, initial, medial, joiningType).
    static (int iso, int fin, int ini, int med, char jt) Forms(int c) => c switch
    {
            0x0622 => (0xFE81, 0xFE82, 0x0622, 0x0622, 'R'),   // ARABIC LETTER ALEF WITH MADDA ABOVE
            0x0623 => (0xFE83, 0xFE84, 0x0623, 0x0623, 'R'),   // ARABIC LETTER ALEF WITH HAMZA ABOVE
            0x0624 => (0xFE85, 0xFE86, 0x0624, 0x0624, 'R'),   // ARABIC LETTER WAW WITH HAMZA ABOVE
            0x0625 => (0xFE87, 0xFE88, 0x0625, 0x0625, 'R'),   // ARABIC LETTER ALEF WITH HAMZA BELOW
            0x0626 => (0xFE89, 0xFE8A, 0xFE8B, 0xFE8C, 'D'),   // ARABIC LETTER YEH WITH HAMZA ABOVE
            0x0627 => (0xFE8D, 0xFE8E, 0x0627, 0x0627, 'R'),   // ARABIC LETTER ALEF
            0x0628 => (0xFE8F, 0xFE90, 0xFE91, 0xFE92, 'D'),   // ARABIC LETTER BEH
            0x0629 => (0xFE93, 0xFE94, 0x0629, 0x0629, 'R'),   // ARABIC LETTER TEH MARBUTA
            0x062A => (0xFE95, 0xFE96, 0xFE97, 0xFE98, 'D'),   // ARABIC LETTER TEH
            0x062B => (0xFE99, 0xFE9A, 0xFE9B, 0xFE9C, 'D'),   // ARABIC LETTER THEH
            0x062C => (0xFE9D, 0xFE9E, 0xFE9F, 0xFEA0, 'D'),   // ARABIC LETTER JEEM
            0x062D => (0xFEA1, 0xFEA2, 0xFEA3, 0xFEA4, 'D'),   // ARABIC LETTER HAH
            0x062E => (0xFEA5, 0xFEA6, 0xFEA7, 0xFEA8, 'D'),   // ARABIC LETTER KHAH
            0x062F => (0xFEA9, 0xFEAA, 0x062F, 0x062F, 'R'),   // ARABIC LETTER DAL
            0x0630 => (0xFEAB, 0xFEAC, 0x0630, 0x0630, 'R'),   // ARABIC LETTER THAL
            0x0631 => (0xFEAD, 0xFEAE, 0x0631, 0x0631, 'R'),   // ARABIC LETTER REH
            0x0632 => (0xFEAF, 0xFEB0, 0x0632, 0x0632, 'R'),   // ARABIC LETTER ZAIN
            0x0633 => (0xFEB1, 0xFEB2, 0xFEB3, 0xFEB4, 'D'),   // ARABIC LETTER SEEN
            0x0634 => (0xFEB5, 0xFEB6, 0xFEB7, 0xFEB8, 'D'),   // ARABIC LETTER SHEEN
            0x0635 => (0xFEB9, 0xFEBA, 0xFEBB, 0xFEBC, 'D'),   // ARABIC LETTER SAD
            0x0636 => (0xFEBD, 0xFEBE, 0xFEBF, 0xFEC0, 'D'),   // ARABIC LETTER DAD
            0x0637 => (0xFEC1, 0xFEC2, 0xFEC3, 0xFEC4, 'D'),   // ARABIC LETTER TAH
            0x0638 => (0xFEC5, 0xFEC6, 0xFEC7, 0xFEC8, 'D'),   // ARABIC LETTER ZAH
            0x0639 => (0xFEC9, 0xFECA, 0xFECB, 0xFECC, 'D'),   // ARABIC LETTER AIN
            0x063A => (0xFECD, 0xFECE, 0xFECF, 0xFED0, 'D'),   // ARABIC LETTER GHAIN
            0x0641 => (0xFED1, 0xFED2, 0xFED3, 0xFED4, 'D'),   // ARABIC LETTER FEH
            0x0642 => (0xFED5, 0xFED6, 0xFED7, 0xFED8, 'D'),   // ARABIC LETTER QAF
            0x0643 => (0xFED9, 0xFEDA, 0xFEDB, 0xFEDC, 'D'),   // ARABIC LETTER KAF
            0x0644 => (0xFEDD, 0xFEDE, 0xFEDF, 0xFEE0, 'D'),   // ARABIC LETTER LAM
            0x0645 => (0xFEE1, 0xFEE2, 0xFEE3, 0xFEE4, 'D'),   // ARABIC LETTER MEEM
            0x0646 => (0xFEE5, 0xFEE6, 0xFEE7, 0xFEE8, 'D'),   // ARABIC LETTER NOON
            0x0647 => (0xFEE9, 0xFEEA, 0xFEEB, 0xFEEC, 'D'),   // ARABIC LETTER HEH
            0x0648 => (0xFEED, 0xFEEE, 0x0648, 0x0648, 'R'),   // ARABIC LETTER WAW
            0x0649 => (0xFEEF, 0xFEF0, 0xFBE8, 0xFBE9, 'D'),   // ARABIC LETTER ALEF MAKSURA
            0x064A => (0xFEF1, 0xFEF2, 0xFEF3, 0xFEF4, 'D'),   // ARABIC LETTER YEH
            0x0671 => (0xFB50, 0xFB51, 0x0671, 0x0671, 'R'),   // ARABIC LETTER ALEF WASLA
            0x0679 => (0xFB66, 0xFB67, 0xFB68, 0xFB69, 'D'),   // ARABIC LETTER TTEH
            0x067A => (0xFB5E, 0xFB5F, 0xFB60, 0xFB61, 'D'),   // ARABIC LETTER TTEHEH
            0x067B => (0xFB52, 0xFB53, 0xFB54, 0xFB55, 'D'),   // ARABIC LETTER BEEH
            0x067E => (0xFB56, 0xFB57, 0xFB58, 0xFB59, 'D'),   // ARABIC LETTER PEH
            0x067F => (0xFB62, 0xFB63, 0xFB64, 0xFB65, 'D'),   // ARABIC LETTER TEHEH
            0x0680 => (0xFB5A, 0xFB5B, 0xFB5C, 0xFB5D, 'D'),   // ARABIC LETTER BEHEH
            0x0683 => (0xFB76, 0xFB77, 0xFB78, 0xFB79, 'D'),   // ARABIC LETTER NYEH
            0x0684 => (0xFB72, 0xFB73, 0xFB74, 0xFB75, 'D'),   // ARABIC LETTER DYEH
            0x0686 => (0xFB7A, 0xFB7B, 0xFB7C, 0xFB7D, 'D'),   // ARABIC LETTER TCHEH
            0x0687 => (0xFB7E, 0xFB7F, 0xFB80, 0xFB81, 'D'),   // ARABIC LETTER TCHEHEH
            0x0688 => (0xFB88, 0xFB89, 0x0688, 0x0688, 'R'),   // ARABIC LETTER DDAL
            0x068C => (0xFB84, 0xFB85, 0x068C, 0x068C, 'R'),   // ARABIC LETTER DAHAL
            0x068D => (0xFB82, 0xFB83, 0x068D, 0x068D, 'R'),   // ARABIC LETTER DDAHAL
            0x068E => (0xFB86, 0xFB87, 0x068E, 0x068E, 'R'),   // ARABIC LETTER DUL
            0x0691 => (0xFB8C, 0xFB8D, 0x0691, 0x0691, 'R'),   // ARABIC LETTER RREH
            0x0698 => (0xFB8A, 0xFB8B, 0x0698, 0x0698, 'R'),   // ARABIC LETTER JEH
            0x06A4 => (0xFB6A, 0xFB6B, 0xFB6C, 0xFB6D, 'D'),   // ARABIC LETTER VEH
            0x06A6 => (0xFB6E, 0xFB6F, 0xFB70, 0xFB71, 'D'),   // ARABIC LETTER PEHEH
            0x06A9 => (0xFB8E, 0xFB8F, 0xFB90, 0xFB91, 'D'),   // ARABIC LETTER KEHEH
            0x06AD => (0xFBD3, 0xFBD4, 0xFBD5, 0xFBD6, 'D'),   // ARABIC LETTER NG
            0x06AF => (0xFB92, 0xFB93, 0xFB94, 0xFB95, 'D'),   // ARABIC LETTER GAF
            0x06B1 => (0xFB9A, 0xFB9B, 0xFB9C, 0xFB9D, 'D'),   // ARABIC LETTER NGOEH
            0x06B3 => (0xFB96, 0xFB97, 0xFB98, 0xFB99, 'D'),   // ARABIC LETTER GUEH
            0x06BA => (0xFB9E, 0xFB9F, 0x06BA, 0x06BA, 'R'),   // ARABIC LETTER NOON GHUNNA
            0x06BB => (0xFBA0, 0xFBA1, 0xFBA2, 0xFBA3, 'D'),   // ARABIC LETTER RNOON
            0x06BE => (0xFBAA, 0xFBAB, 0xFBAC, 0xFBAD, 'D'),   // ARABIC LETTER HEH DOACHASHMEE
            0x06C0 => (0xFBA4, 0xFBA5, 0x06C0, 0x06C0, 'R'),   // ARABIC LETTER HEH WITH YEH ABOVE
            0x06C1 => (0xFBA6, 0xFBA7, 0xFBA8, 0xFBA9, 'D'),   // ARABIC LETTER HEH GOAL
            0x06C5 => (0xFBE0, 0xFBE1, 0x06C5, 0x06C5, 'R'),   // ARABIC LETTER KIRGHIZ OE
            0x06C6 => (0xFBD9, 0xFBDA, 0x06C6, 0x06C6, 'R'),   // ARABIC LETTER OE
            0x06C7 => (0xFBD7, 0xFBD8, 0x06C7, 0x06C7, 'R'),   // ARABIC LETTER U
            0x06C8 => (0xFBDB, 0xFBDC, 0x06C8, 0x06C8, 'R'),   // ARABIC LETTER YU
            0x06C9 => (0xFBE2, 0xFBE3, 0x06C9, 0x06C9, 'R'),   // ARABIC LETTER KIRGHIZ YU
            0x06CB => (0xFBDE, 0xFBDF, 0x06CB, 0x06CB, 'R'),   // ARABIC LETTER VE
            0x06CC => (0xFBFC, 0xFBFD, 0xFBFE, 0xFBFF, 'D'),   // ARABIC LETTER FARSI YEH
            0x06D0 => (0xFBE4, 0xFBE5, 0xFBE6, 0xFBE7, 'D'),   // ARABIC LETTER E
            0x06D2 => (0xFBAE, 0xFBAF, 0x06D2, 0x06D2, 'R'),   // ARABIC LETTER YEH BARREE
            0x06D3 => (0xFBB0, 0xFBB1, 0x06D3, 0x06D3, 'R'),   // ARABIC LETTER YEH BARREE WITH HAMZA ABOVE
        _ => (0, 0, 0, 0, 'U'),
    };

    // LAM + ALEF is not merely joined, it is a mandatory ligature: the pair becomes ONE glyph.
    // Keyed by the alef; the lam is always U+0644.
    static (int iso, int fin) LamAlef(int alef) => alef switch
    {
            0x0622 => (0xFEF5, 0xFEF6),   // LAM + ARABIC LETTER ALEF WITH MADDA ABOVE
            0x0623 => (0xFEF7, 0xFEF8),   // LAM + ARABIC LETTER ALEF WITH HAMZA ABOVE
            0x0625 => (0xFEF9, 0xFEFA),   // LAM + ARABIC LETTER ALEF WITH HAMZA BELOW
            0x0627 => (0xFEFB, 0xFEFC),   // LAM + ARABIC LETTER ALEF
        _ => (0, 0),
    };

    const int Lam = 0x0644, Tatweel = 0x0640, Zwj = 0x200D;

    // Transparent characters (Joining_Type T) are invisible to joining: a letter joins straight through
    // a harakat or a hamza mark to the letter beyond it.
    static bool IsTransparent(char c)
    {
        var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
        return cat == System.Globalization.UnicodeCategory.NonSpacingMark
            || cat == System.Globalization.UnicodeCategory.EnclosingMark
            || cat == System.Globalization.UnicodeCategory.Format;
    }

    // Join-causing characters connect on both sides without having forms of their own.
    static bool IsJoinCausing(int c) => c == Tatweel || c == Zwj;

    /// <summary>True if the string contains anything this shaper would rewrite. Lets callers skip the
    /// work — and the allocation — for the overwhelmingly common non-Arabic case.</summary>
    public static bool NeedsShaping(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c >= '\u0600' && c <= '\u06FF') return true;
            if (c >= '\u0750' && c <= '\u077F') return true;
            if (c >= '\u08A0' && c <= '\u08FF') return true;
        }
        return false;
    }

    /// <summary>Shape <paramref name="text"/> in place of its raw code points.</summary>
    public static string Shape(string text) => Shape(text, out _);

    /// <summary>Shape <paramref name="text"/> and report where each output character came from.
    /// <paramref name="shapedToLogical"/>[k] is the index in <paramref name="text"/> of the character
    /// that produced output character k — needed because the lam-alef ligature turns two characters
    /// into one, so the output is not always the same length as the input.</summary>
    public static string Shape(string text, out int[] shapedToLogical)
    {
        if (string.IsNullOrEmpty(text) || !NeedsShaping(text))
        {
            shapedToLogical = new int[text?.Length ?? 0];
            for (int i = 0; i < shapedToLogical.Length; i++) shapedToLogical[i] = i;
            return text;
        }

        var outChars = new System.Text.StringBuilder(text.Length);
        var outMap   = new System.Collections.Generic.List<int>(text.Length);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            // LAM + ALEF -> ligature. Its own form depends only on whether the LAM joins backwards,
            // because the alef half never connects forwards.
            if (c == Lam)
            {
                int j = NextVisible(text, i);
                if (j >= 0)
                {
                    var (li, lf) = LamAlef(text[j]);
                    if (li != 0)
                    {
                        bool joinsPrev = JoinsToPrevious(text, i);
                        outChars.Append((char)(joinsPrev ? lf : li));
                        outMap.Add(i);
                        // carry any transparent marks that sat between the lam and the alef
                        for (int k = i + 1; k < j; k++) { outChars.Append(text[k]); outMap.Add(k); }
                        i = j;
                        continue;
                    }
                }
            }

            var f = Forms(c);
            if (f.jt == 'U') { outChars.Append(c); outMap.Add(i); continue; }

            bool prev = JoinsToPrevious(text, i);
            bool next = JoinsToNext(text, i);

            int shaped = f.jt == 'D'
                ? (prev && next ? f.med : prev ? f.fin : next ? f.ini : f.iso)
                : (prev ? f.fin : f.iso);          // 'R' joins only to what precedes it

            outChars.Append((char)(shaped != 0 ? shaped : c));
            outMap.Add(i);
        }

        shapedToLogical = outMap.ToArray();
        return outChars.ToString();
    }

    // Index of the next character that joining can "see" (skipping transparent marks), or -1.
    static int NextVisible(string t, int i)
    {
        for (int k = i + 1; k < t.Length; k++) if (!IsTransparent(t[k])) return k;
        return -1;
    }

    static int PrevVisible(string t, int i)
    {
        for (int k = i - 1; k >= 0; k--) if (!IsTransparent(t[k])) return k;
        return -1;
    }

    // Can the letter at i connect to the one before it? Only if that one is able to join on its
    // left-hand (following) side, i.e. it is dual-joining or join-causing.
    static bool JoinsToPrevious(string t, int i)
    {
        int p = PrevVisible(t, i);
        if (p < 0) return false;
        if (IsJoinCausing(t[p])) return true;
        return Forms(t[p]).jt == 'D';
    }

    // Can the letter at i connect to the one after it? Only if that one is able to join on its
    // right-hand (preceding) side, i.e. it is dual- or right-joining, or join-causing.
    static bool JoinsToNext(string t, int i)
    {
        int n = NextVisible(t, i);
        if (n < 0) return false;
        if (IsJoinCausing(t[n])) return true;
        char jt = Forms(t[n]).jt;
        return jt == 'D' || jt == 'R';
    }
}
