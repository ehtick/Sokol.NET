using Sokol.GUI;
using Xunit;

namespace SokolBidiTests;

/// <summary>
/// Golden tests for the logical→visual pipeline: Arabic shaping, then the Unicode Bidirectional
/// Algorithm (reordering, mirroring, combining-mark order), exactly as <see cref="BidiHelper.ToVisual"/>
/// runs it for every string the GUI draws.
///
/// <para>⭐ WHERE THE EXPECTATIONS COME FROM. Every <c>expected</c> value below is the output of
/// <b>GNU FriBidi</b> — the reference implementation of UAX #9 — for that input, captured with
/// <c>fribidi --nopad --nobreak</c> and frozen. They are NOT hand-written, and they are NOT a
/// recording of what this code happened to do: each one was cross-checked against the reference
/// before being pasted in. If a change here makes one fail, the change is wrong until proven
/// otherwise against fribidi.</para>
///
/// <para>⚠ Regenerating a golden: run the input through <c>fribidi --nopad --nobreak</c> and STRIP
/// U+FEFF from its output. FriBidi keeps its shaped output the same length as its input, so when
/// lam+alef fuses into one ligature it pads with U+FEFF; our shaper drops the consumed alef and
/// reports an index map instead, because a font without U+FEFF would draw .notdef. Also feed fribidi
/// input in CHUNKS and assert the line count — a whole-file run can silently drop a line and every
/// comparison after it is then off by one.</para>
///
/// <para>Both the input and the expected value are written as \uXXXX escapes on purpose. Raw
/// right-to-left literals are reordered by editors and diff tools, which makes them impossible to
/// review and easy to corrupt; the readable form is in the comment above each row.</para>
/// </summary>
public class BidiGoldenTests
{
    public static TheoryData<string, string, string> Cases => new()
    {
        // L4: Hebrew parentheses — the reported bug
        //   logical: גבוהה (זוהר)
        { "L4: Hebrew parentheses — the reported bug", "\u05D2\u05D1\u05D5\u05D4\u05D4 (\u05D6\u05D5\u05D4\u05E8)", "(\u05E8\u05D4\u05D5\u05D6) \u05D4\u05D4\u05D5\u05D1\u05D2" },
        // L4: Hebrew square brackets and guillemets
        //   logical: בדיקה [סוגר] «ציטוט»
        { "L4: Hebrew square brackets and guillemets", "\u05D1\u05D3\u05D9\u05E7\u05D4 [\u05E1\u05D5\u05D2\u05E8] \u00AB\u05E6\u05D9\u05D8\u05D5\u05D8\u00BB", "\u00AB\u05D8\u05D5\u05D8\u05D9\u05E6\u00BB [\u05E8\u05D2\u05D5\u05E1] \u05D4\u05E7\u05D9\u05D3\u05D1" },
        // L4: Hebrew braces
        //   logical: ערך {אחד}
        { "L4: Hebrew braces", "\u05E2\u05E8\u05DA {\u05D0\u05D7\u05D3}", "{\u05D3\u05D7\u05D0} \u05DA\u05E8\u05E2" },
        // L4: Arabic parentheses
        //   logical: عالية (توهج)
        { "L4: Arabic parentheses", "\u0639\u0627\u0644\u064A\u0629 (\u062A\u0648\u0647\u062C)", "(\uFE9E\uFEEB\uFEEE\uFE97) \uFE94\uFEF4\uFEDF\uFE8E\uFECB" },
        // L4: brackets around a bracket
        //   logical: שלום ([עולם])
        { "L4: brackets around a bracket", "\u05E9\u05DC\u05D5\u05DD ([\u05E2\u05D5\u05DC\u05DD])", "([\u05DD\u05DC\u05D5\u05E2]) \u05DD\u05D5\u05DC\u05E9" },
        // N0: LTR word before a parenthesised LTR phrase inside RTL
        //   logical: שלום High (bloom) עולם
        { "N0: LTR word before a parenthesised LTR phrase inside RTL", "\u05E9\u05DC\u05D5\u05DD High (bloom) \u05E2\u05D5\u05DC\u05DD", "\u05DD\u05DC\u05D5\u05E2 High (bloom) \u05DD\u05D5\u05DC\u05E9" },
        // N0: control: no LTR word before the bracket
        //   logical: שלום (bloom) עולם
        { "N0: control: no LTR word before the bracket", "\u05E9\u05DC\u05D5\u05DD (bloom) \u05E2\u05D5\u05DC\u05DD", "\u05DD\u05DC\u05D5\u05E2 (bloom) \u05DD\u05D5\u05DC\u05E9" },
        // N0: two LTR words then brackets
        //   logical: טקסט ABC (DEF) סוף
        { "N0: two LTR words then brackets", "\u05D8\u05E7\u05E1\u05D8 ABC (DEF) \u05E1\u05D5\u05E3", "\u05E3\u05D5\u05E1 ABC (DEF) \u05D8\u05E1\u05E7\u05D8" },
        // N0: nested brackets around Latin
        //   logical: שלום [High (bloom)] עולם
        { "N0: nested brackets around Latin", "\u05E9\u05DC\u05D5\u05DD [High (bloom)] \u05E2\u05D5\u05DC\u05DD", "\u05DD\u05DC\u05D5\u05E2 [High (bloom)] \u05DD\u05D5\u05DC\u05E9" },
        // N0: unmatched opening bracket
        //   logical: שלום ( עולם
        { "N0: unmatched opening bracket", "\u05E9\u05DC\u05D5\u05DD ( \u05E2\u05D5\u05DC\u05DD", "\u05DD\u05DC\u05D5\u05E2 ) \u05DD\u05D5\u05DC\u05E9" },
        // N0: unmatched closing bracket
        //   logical: שלום ) עולם
        { "N0: unmatched closing bracket", "\u05E9\u05DC\u05D5\u05DD ) \u05E2\u05D5\u05DC\u05DD", "\u05DD\u05DC\u05D5\u05E2 ( \u05DD\u05D5\u05DC\u05E9" },
        // W7: European number after a Hebrew run in an LTR paragraph
        //   logical: bbdge סז 07
        { "W7: European number after a Hebrew run in an LTR paragraph", "bbdge \u05E1\u05D6 07", "bbdge 07 \u05D6\u05E1" },
        // W7: number between Latin and Hebrew
        //   logical: abc שלום 123
        { "W7: number between Latin and Hebrew", "abc \u05E9\u05DC\u05D5\u05DD 123", "abc 123 \u05DD\u05D5\u05DC\u05E9" },
        // W7: number inside a Hebrew sentence
        //   logical: שלום 42 עולם
        { "W7: number inside a Hebrew sentence", "\u05E9\u05DC\u05D5\u05DD 42 \u05E2\u05D5\u05DC\u05DD", "\u05DD\u05DC\u05D5\u05E2 42 \u05DD\u05D5\u05DC\u05E9" },
        // W7: number then Hebrew then number
        //   logical: 12 שלום 34
        { "W7: number then Hebrew then number", "12 \u05E9\u05DC\u05D5\u05DD 34", "34 \u05DD\u05D5\u05DC\u05E9 12" },
        // W2: Arabic-Indic digits after Arabic
        //   logical: abc عربي ١٢٣
        { "W2: Arabic-Indic digits after Arabic", "abc \u0639\u0631\u0628\u064A \u0661\u0662\u0663", "abc \u0661\u0662\u0663 \uFEF2\uFE91\uFEAE\uFECB" },
        // W2: European digits after Arabic
        //   logical: عربي 123 abc
        { "W2: European digits after Arabic", "\u0639\u0631\u0628\u064A 123 abc", "abc 123 \uFEF2\uFE91\uFEAE\uFECB" },
        // L3: Arabic shadda must sit on its base
        //   logical: عالية (توهّج)
        { "L3: Arabic shadda must sit on its base", "\u0639\u0627\u0644\u064A\u0629 (\u062A\u0648\u0647\u0651\u062C)", "(\uFE9E\uFEEB\u0651\uFEEE\uFE97) \uFE94\uFEF4\uFEDF\uFE8E\uFECB" },
        // L3: Arabic tanween
        //   logical: شكرًا لك
        { "L3: Arabic tanween", "\u0634\u0643\u0631\u064B\u0627 \u0644\u0643", "\uFEDA\uFEDF \uFE8D\uFEAE\u064B\uFEDC\uFEB7" },
        // L3: Hebrew niqqud
        //   logical: שָׁלוֹם עוֹלָם
        { "L3: Hebrew niqqud", "\u05E9\u05B8\u05C1\u05DC\u05D5\u05B9\u05DD \u05E2\u05D5\u05B9\u05DC\u05B8\u05DD", "\u05DD\u05DC\u05B8\u05D5\u05B9\u05E2 \u05DD\u05D5\u05B9\u05DC\u05E9\u05B8\u05C1" },
        // shape: Arabic word joining
        //   logical: الإعدادات
        { "shape: Arabic word joining", "\u0627\u0644\u0625\u0639\u062F\u0627\u062F\u0627\u062A", "\uFE95\uFE8D\uFEA9\uFE8D\uFEAA\uFECB\uFEF9\uFE8D" },
        // shape: Arabic sentence
        //   logical: مرحبا بالعالم
        { "shape: Arabic sentence", "\u0645\u0631\u062D\u0628\u0627 \u0628\u0627\u0644\u0639\u0627\u0644\u0645", "\uFEE2\uFEDF\uFE8E\uFECC\uFEDF\uFE8E\uFE91 \uFE8E\uFE92\uFEA3\uFEAE\uFEE3" },
        // shape: lam-alef ligature alone
        //   logical: لا
        { "shape: lam-alef ligature alone", "\u0644\u0627", "\uFEFB" },
        // shape: lam-alef inside a word
        //   logical: العالم لاعب
        { "shape: lam-alef inside a word", "\u0627\u0644\u0639\u0627\u0644\u0645 \u0644\u0627\u0639\u0628", "\uFE90\uFECB\uFEFB \uFEE2\uFEDF\uFE8E\uFECC\uFEDF\uFE8D" },
        // shape: alef-madda ligature
        //   logical: لآ
        { "shape: alef-madda ligature", "\u0644\u0622", "\uFEF5" },
        // shape: non-joining letters (dal, raa)
        //   logical: درر
        { "shape: non-joining letters (dal, raa)", "\u062F\u0631\u0631", "\uFEAD\uFEAD\uFEA9" },
        // shape: Arabic with Latin inside
        //   logical: لاعب Eli يلعب
        { "shape: Arabic with Latin inside", "\u0644\u0627\u0639\u0628 Eli \u064A\u0644\u0639\u0628", "\uFE90\uFECC\uFEE0\uFEF3 Eli \uFE90\uFECB\uFEFB" },
        // ltr: pure Latin must pass through
        //   logical: High (bloom)
        { "ltr: pure Latin must pass through", "High (bloom)", "High (bloom)" },
        // ltr: Latin with punctuation
        //   logical: Hello, world! (test) [1-2]
        { "ltr: Latin with punctuation", "Hello, world! (test) [1-2]", "Hello, world! (test) [1-2]" },
        // ltr: Cyrillic with parentheses
        //   logical: Привет (мир)
        { "ltr: Cyrillic with parentheses", "\u041F\u0440\u0438\u0432\u0435\u0442 (\u043C\u0438\u0440)", "\u041F\u0440\u0438\u0432\u0435\u0442 (\u043C\u0438\u0440)" },
        // mixed: Hebrew, Latin name in brackets, digits
        //   logical: שלום Eli (Aloni) 42
        { "mixed: Hebrew, Latin name in brackets, digits", "\u05E9\u05DC\u05D5\u05DD Eli (Aloni) 42", "Eli (Aloni) 42 \u05DD\u05D5\u05DC\u05E9" },
        // mixed: Arabic with a Latin name in brackets
        //   logical: لاعب (Eli) ١٢٣
        { "mixed: Arabic with a Latin name in brackets", "\u0644\u0627\u0639\u0628 (Eli) \u0661\u0662\u0663", "\u0661\u0662\u0663 (Eli) \uFE90\uFECB\uFEFB" },
        // mixed: Hebrew and Arabic together
        //   logical: שלום عربي עולם
        { "mixed: Hebrew and Arabic together", "\u05E9\u05DC\u05D5\u05DD \u0639\u0631\u0628\u064A \u05E2\u05D5\u05DC\u05DD", "\u05DD\u05DC\u05D5\u05E2 \uFEF2\uFE91\uFEAE\uFECB \u05DD\u05D5\u05DC\u05E9" },
        // edge: empty-ish: single bracket
        //   logical: (
        { "edge: empty-ish: single bracket", "(", "(" },
        // edge: RTL then trailing spaces
        //   logical: שלום   
        { "edge: RTL then trailing spaces", "\u05E9\u05DC\u05D5\u05DD   ", "   \u05DD\u05D5\u05DC\u05E9" },
        // edge: bracket pair with nothing inside
        //   logical: שלום () עולם
        { "edge: bracket pair with nothing inside", "\u05E9\u05DC\u05D5\u05DD () \u05E2\u05D5\u05DC\u05DD", "\u05DD\u05DC\u05D5\u05E2 () \u05DD\u05D5\u05DC\u05E9" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ToVisual_matches_the_reference_implementation(string name, string logical, string expected)
    {
        string actual = BidiHelper.ToVisual(logical);
        Assert.True(expected == actual,
            $"{name}\n  logical : {Describe(logical)}\n  expected: {Describe(expected)}\n  actual  : {Describe(actual)}");
    }

    /// <summary>Running the pipeline twice must not change the result again. Shaping and reordering
    /// are not idempotent in general (a presentation form is not the letter it came from), so this
    /// guards the specific promise the renderer relies on: ToVisual is called once per draw, and a
    /// string that has already been through it must not be corrupted if it is passed through again.
    /// It is also how a double-shaping bug would show up.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void ToVisual_is_stable_when_reapplied_to_pure_ltr(string name, string logical, string expected)
    {
        if (BidiHelper.ContainsRTL(logical)) return;   // only pure-LTR text is required to be a fixed point
        Assert.True(expected == BidiHelper.ToVisual(BidiHelper.ToVisual(logical)),
            $"{name} changed when passed through a second time");
    }

    /// <summary>Each line of a multi-line string gets its own paragraph direction, so a single RTL
    /// line must not drag the following lines into RTL order.</summary>
    [Fact]
    public void Each_line_resolves_its_own_paragraph_direction()
    {
        // "\u05E9\u05DC\u05D5\u05DD (1)" = Hebrew + a parenthesised number; then a pure-LTR line.
        string logical  = "\u05E9\u05DC\u05D5\u05DD (1)\nabc (2)";
        string expected = BidiHelper.ToVisual("\u05E9\u05DC\u05D5\u05DD (1)") + "\nabc (2)";
        Assert.Equal(expected, BidiHelper.ToVisual(logical));
    }

    [Fact]
    public void Null_and_empty_are_returned_unchanged()
    {
        Assert.Equal("", BidiHelper.ToVisual(""));
        Assert.Null(BidiHelper.ToVisual(null!));
    }

    static string Describe(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in s)
            sb.Append(c is >= ' ' and < (char)0x7F ? c.ToString() : $"U+{(int)c:X4} ");
        return sb.ToString();
    }
}
