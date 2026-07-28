using System;
using System.Collections.Generic;
using System.Text;
using Sokol.GUI;
using Xunit;

namespace SokolBidiTests;

/// <summary>
/// Properties that must hold for EVERY string, checked over a generated corpus rather than a fixed
/// list. Goldens pin down known answers; these catch the failures nobody thought to write a golden
/// for — an out-of-range caret index, a dropped character, a mangled Latin UI.
///
/// <para>The corpus is generated from a FIXED seed so a failure is reproducible: the same run
/// produces the same strings on every machine.</para>
/// </summary>
public class BidiInvariantTests
{
    const string Hebrew = "אבגדהוזחטיכלמנסעפצקרשת";
    const string Arabic = "ابتثجحخدذرزسشصضطظعغفقكلمنهوي";
    const string Latin  = "abcdefghiABCDEFGH";
    const string Digits = "0123456789";
    const string ArabicIndic = "٠١٢٣٤٥٦٧٨٩";
    static readonly string[] Brackets = { "()", "[]", "{}", "«»", "‹›", "⟨⟩" };

    static IEnumerable<string> Corpus(int count = 500)
    {
        var rng = new Random(20260728);
        string Word(string alphabet, int n) =>
            string.Concat(Enumerable_Repeat(n, () => alphabet[rng.Next(alphabet.Length)]));

        for (int i = 0; i < count; i++)
        {
            var parts = new List<string>();
            for (int p = 0, n = rng.Next(1, 7); p < n; p++)
            {
                double r = rng.NextDouble();
                if      (r < .26) parts.Add(Word(Hebrew, rng.Next(1, 6)));
                else if (r < .48) parts.Add(Word(Arabic, rng.Next(1, 6)));
                else if (r < .64) parts.Add(Word(Latin,  rng.Next(1, 6)));
                else if (r < .72) parts.Add(Word(Digits, rng.Next(1, 3)));
                else if (r < .78) parts.Add(Word(ArabicIndic, rng.Next(1, 3)));
                else
                {
                    string b = Brackets[rng.Next(Brackets.Length)];
                    string inner = rng.Next(4) switch
                    {
                        0 => Word(Hebrew, rng.Next(1, 5)),
                        1 => Word(Latin,  rng.Next(1, 5)),
                        2 => Word(Arabic, rng.Next(1, 5)),
                        _ => Word(Digits, 2),
                    };
                    parts.Add(b[0] + inner + b[1]);
                }
            }
            yield return string.Join(" ", parts);
        }
    }

    static IEnumerable<char> Enumerable_Repeat(int n, Func<char> f)
    {
        for (int i = 0; i < n; i++) yield return f();
    }

    /// <summary>The visual→logical map is what a caret is positioned from. Every entry must index a
    /// real character of the input, and there must be exactly one entry per visual character —
    /// otherwise a click in an RTL text box throws or lands on the wrong character.</summary>
    [Fact]
    public void ToVisualWithMap_returns_one_in_range_index_per_visual_character()
    {
        foreach (string s in Corpus())
        {
            var (visual, map) = BidiHelper.ToVisualWithMap(s);
            Assert.True(map.Length == visual.Length,
                $"map length {map.Length} != visual length {visual.Length} for {Escape(s)}");
            foreach (int m in map)
                Assert.True(m >= 0 && m < s.Length,
                    $"index {m} outside [0,{s.Length}) for {Escape(s)}");
        }
    }

    /// <summary>Reordering and mirroring are permutations plus glyph substitution — no character may
    /// be invented or lost. Arabic shaping is the one exception, and only downwards: lam+alef fuses
    /// two characters into one, so the output can be shorter but never longer.</summary>
    [Fact]
    public void ToVisual_never_grows_a_string_and_only_shrinks_where_a_ligature_forms()
    {
        foreach (string s in Corpus())
        {
            string visual = BidiHelper.ToVisual(s);
            Assert.True(visual.Length <= s.Length, $"grew: {Escape(s)}");

            int lamAlef = 0;
            for (int i = 0; i + 1 < s.Length; i++)
                if (s[i] == 'ل' && s[i + 1] is 'آ' or 'أ' or 'إ' or 'ا')
                    lamAlef++;
            Assert.True(visual.Length >= s.Length - lamAlef, $"lost characters: {Escape(s)}");
        }
    }

    /// <summary>⛔ The regression that matters most to the six other languages: text with no RTL in it
    /// must come back byte-identical. The BiDi and shaping work touches shared code, and a change
    /// that quietly reorders English or Russian would be far worse than the bug it fixed.</summary>
    [Fact]
    public void Text_without_rtl_is_returned_byte_identical()
    {
        string[] samples =
        {
            "Hello, world!",
            "High (bloom)",
            "Players (2-4)",
            "Привет, мир (тест)",
            "Größe: {0} [x]",
            "Élevée (bloom) — 100%",
            "a(b[c{d}e]f)g",
            "1 < 2 > 0",
            "",
        };
        foreach (string s in samples)
        {
            Assert.False(BidiHelper.ContainsRTL(s), $"sample is not pure LTR: {s}");
            Assert.Equal(s, BidiHelper.ToVisual(s));
        }
    }

    /// <summary>Mirroring is an involution, so a bracket that survives a round trip through an RTL
    /// context must come back as itself. This is what would catch a mirror table where some pair
    /// points at the wrong partner.</summary>
    [Fact]
    public void Mirrored_brackets_round_trip()
    {
        // A lone Hebrew word plus one bracket pair: reversing the visual result restores the input,
        // because the only transformation applied is the reversal plus the mirroring of the pair.
        foreach (string b in Brackets)
        {
            string logical = "שלום " + b[0] + "א" + b[1];
            string visual  = BidiHelper.ToVisual(logical);
            var reversed   = new StringBuilder();
            for (int i = visual.Length - 1; i >= 0; i--) reversed.Append(Mirror(visual[i]));
            Assert.Equal(logical, reversed.ToString());
        }
    }

    static char Mirror(char c) => c switch
    {
        '(' => ')', ')' => '(', '[' => ']', ']' => '[', '{' => '}', '}' => '{',
        '«' => '»', '»' => '«',
        '‹' => '›', '›' => '‹',
        '⟨' => '⟩', '⟩' => '⟨',
        _ => c,
    };

    static string Escape(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s) sb.Append(c is >= ' ' and < (char)0x7F ? c.ToString() : $"\\u{(int)c:X4}");
        return sb.ToString();
    }
}
