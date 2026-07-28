using Sokol.GUI;
using Xunit;

namespace SokolBidiTests;

/// <summary>
/// Unit tests for <see cref="ArabicShaper"/> on its own, in LOGICAL order — before any BiDi
/// reordering. The golden suite proves the whole pipeline agrees with the reference implementation;
/// these pin down the individual joining decisions, so a failure says WHICH rule broke instead of
/// just "the sentence came out different".
///
/// <para>Naming: a letter's form depends on whether it connects to the letter before it and the one
/// after it in LOGICAL order — "initial" means it starts a joined group, "final" means it ends one.
/// Expected values are \uXXXX escapes into the Arabic Presentation Forms blocks.</para>
/// </summary>
public class ArabicShaperTests
{
    // U+0628 BEH is dual-joining: it has all four forms.
    const char BehIsolated = 'ﺏ', BehFinal = 'ﺐ', BehInitial = 'ﺑ', BehMedial = 'ﺒ';
    // U+0627 ALEF is right-joining: it connects only to what precedes it, so it has no initial/medial.
    const char AlefIsolated = 'ﺍ', AlefFinal = 'ﺎ';

    [Fact]
    public void A_lone_dual_joining_letter_is_isolated() =>
        Assert.Equal(BehIsolated.ToString(), ArabicShaper.Shape("ب"));

    [Fact]
    public void Two_dual_joining_letters_are_initial_then_final() =>
        Assert.Equal($"{BehInitial}{BehFinal}", ArabicShaper.Shape("بب"));

    [Fact]
    public void Three_dual_joining_letters_put_a_medial_in_the_middle() =>
        Assert.Equal($"{BehInitial}{BehMedial}{BehFinal}", ArabicShaper.Shape("ببب"));

    [Fact]
    public void A_right_joining_letter_does_not_connect_to_what_follows_it()
    {
        // ALEF then BEH: alef cannot join forwards, so the beh after it starts its own group.
        Assert.Equal($"{AlefIsolated}{BehIsolated}", ArabicShaper.Shape("اب"));
    }

    [Fact]
    public void A_right_joining_letter_still_connects_to_what_precedes_it()
    {
        // BEH then ALEF: the beh joins forwards, so it is initial and the alef takes its final form.
        Assert.Equal($"{BehInitial}{AlefFinal}", ArabicShaper.Shape("با"));
    }

    [Fact]
    public void Lam_alef_becomes_a_single_ligature_glyph()
    {
        // U+0644 LAM + U+0627 ALEF -> U+FEFB, and TWO characters become ONE.
        string shaped = ArabicShaper.Shape("لا");
        Assert.Equal("ﻻ", shaped);
        Assert.Equal(1, shaped.Length);
    }

    [Fact]
    public void Lam_alef_takes_its_final_form_when_the_lam_joins_backwards()
    {
        // BEH + LAM + ALEF: the lam connects to the beh, so the ligature is the final form U+FEFC.
        Assert.Equal($"{BehInitial}ﻼ", ArabicShaper.Shape("بلا"));
    }

    [Fact]
    public void Each_alef_variant_has_its_own_lam_ligature()
    {
        Assert.Equal("ﻵ", ArabicShaper.Shape("لآ"));   // alef with madda
        Assert.Equal("ﻷ", ArabicShaper.Shape("لأ"));   // alef with hamza above
        Assert.Equal("ﻹ", ArabicShaper.Shape("لإ"));   // alef with hamza below
        Assert.Equal("ﻻ", ArabicShaper.Shape("لا"));   // plain alef
    }

    [Fact]
    public void A_combining_mark_is_transparent_to_joining()
    {
        // BEH + FATHA + BEH: the harakat must not break the join, so the behs still see each other.
        string shaped = ArabicShaper.Shape("بَب");
        Assert.Equal($"{BehInitial}َ{BehFinal}", shaped);
    }

    [Fact]
    public void Tatweel_joins_on_both_sides_without_a_form_of_its_own()
    {
        // BEH + TATWEEL + BEH: tatweel is join-causing, so both behs connect through it.
        Assert.Equal($"{BehInitial}ـ{BehFinal}", ArabicShaper.Shape("بـب"));
    }

    [Fact]
    public void A_space_breaks_a_joining_group()
    {
        Assert.Equal($"{BehIsolated} {BehIsolated}", ArabicShaper.Shape("ب ب"));
    }

    [Theory]
    [InlineData("Hello, world!")]
    [InlineData("שלום")]        // Hebrew — a different RTL script, not shaped
    [InlineData("Привет")]   // Cyrillic
    [InlineData("123 (456)")]
    [InlineData("")]
    public void Text_with_no_arabic_is_returned_unchanged(string text)
    {
        Assert.False(ArabicShaper.NeedsShaping(text));
        Assert.Equal(text, ArabicShaper.Shape(text));
    }

    [Fact]
    public void Latin_embedded_in_arabic_is_left_alone()
    {
        string shaped = ArabicShaper.Shape("بب abc بب");
        Assert.Contains("abc", shaped);
        Assert.Equal($"{BehInitial}{BehFinal} abc {BehInitial}{BehFinal}", shaped);
    }

    /// <summary>The map is what caret placement rides on, so its shape is part of the contract:
    /// one entry per OUTPUT character, each pointing at the input character that produced it.</summary>
    [Fact]
    public void The_index_map_accounts_for_the_ligature_collapsing_two_characters_into_one()
    {
        // BEH + LAM + ALEF -> beh, ligature. Output is 2 chars from 3 inputs.
        string shaped = ArabicShaper.Shape("بلا", out int[] map);
        Assert.Equal(2, shaped.Length);
        Assert.Equal(shaped.Length, map.Length);
        Assert.Equal(0, map[0]);   // the beh
        Assert.Equal(1, map[1]);   // the ligature reports the LAM, its first constituent
    }

    [Fact]
    public void The_index_map_is_the_identity_when_nothing_is_shaped()
    {
        ArabicShaper.Shape("abc", out int[] map);
        Assert.Equal(new[] { 0, 1, 2 }, map);
    }
}
