/*
    BidiSharp: Bidirectional algorithm C# implementation

    Copyright (c) 2019 Fayyad Sufyan
    
    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
 */

using System;
using System.Text;
using System.Collections.Generic;


namespace BidiSharp
{
    public static class Bidi
    {
        // Max explicity depth (embedding level)
        private const int MAX_DEPTH = 125;

        private struct DirectionalStatus
        {
            internal byte    paragraphEmbeddingLevel;        // 0 >= value <= MAX_DEPTH
            internal byte    directionalOverrideStatus;      // N, R or L
            internal bool    directionalIsolateStatus;
        }

        private class IsolatingRunSequence
        {
            public byte         level;
            public BidiClass    sos;
            public BidiClass    eos;
            public int          length;
            public int[]        indexes;
            public byte[]       types;
            public byte[]       resolvedLevels;

            public IsolatingRunSequence(byte paragraphEmbeddingLevel, List<int> runIndexList, byte[] types, byte[] levels)
            {
                ComputeIsolatingRunSequence(this, paragraphEmbeddingLevel, runIndexList, types, levels);
            }
        }

        // Core: compute the visual→logical reorder map (newIndexes[visualPos] = logicalPos).
        // resolvedLevels is the per-logical-character embedding level AFTER rule L1, which rule L4
        // (mirroring, applied in GetOrderedString) needs to know which characters resolved to RTL.
        private static int[] ComputeReorderMap(string input, int[] lineBreaks, out byte[] resolvedLevels)
        {
            int     inputLength               = input.Length;
            byte[]  typesList                 = new byte[input.Length];
            byte[]  levelsList                = new byte[input.Length];
            int[]   matchingPDI;
            int[]   matchingIsolateInitiator;

            ClassifyCharacters(input, ref typesList);
            GetMatchingPDI(typesList, out matchingPDI, out matchingIsolateInitiator);
            byte baseLevel = GetParagraphEmbeddingLevel(typesList, matchingPDI);
            SetLevels(ref levelsList, baseLevel);
            GetExplicitEmbeddingLevels(baseLevel, typesList, ref levelsList, matchingPDI);
            RemoveX9Characters(ref typesList);

            var levelRuns = GetLevelRuns(levelsList);
            int nRuns = levelRuns.Count;
            int[] runCharsArray = GetRunForCharacter(levelRuns, inputLength);

            var sequences = GetIsolatingRunSequences(baseLevel, typesList, levelsList, levelRuns, matchingIsolateInitiator,
                                                     matchingPDI, runCharsArray);

            foreach (var sequence in sequences)
            {
                sequence.ResolveWeaks();
                sequence.ResolveNeutrals(input);
                sequence.ResolveImplicit();
                sequence.ApplyTypesAndLevels(ref typesList, ref levelsList);
            }

            var lines = lineBreaks == null ? new int[] { typesList.Length } : lineBreaks;
            // GetReorderedIndexes runs rule L1 over levelsList IN PLACE (GetTextLevels aliases it), so
            // after this call levelsList holds the final post-L1 levels that rule L4 must consult.
            var reordered = GetReorderedIndexes(baseLevel, typesList, levelsList, lines);
            resolvedLevels = levelsList;
            ApplyCombiningMarkOrder(input, reordered, levelsList);
            return reordered;
        }

        private static bool IsCombiningMark(char c)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            return cat == System.Globalization.UnicodeCategory.NonSpacingMark
                || cat == System.Globalization.UnicodeCategory.EnclosingMark;
        }

        /// <summary>Rule L3 — a combining mark on a right-to-left base ends up BEFORE that base once L2
        /// has reversed the run, because it followed the base in logical order. NanoVG/fontstash draws
        /// glyphs in buffer order and positions a zero-width mark on whatever it follows, so each
        /// [marks…][base] group that came out of an RTL run is flipped back to [base][marks…].
        /// Without this an Arabic shadda or a Hebrew niqqud lands on the neighbouring letter.
        /// <para>Applied to the reorder MAP rather than to the finished string, so the visual→logical
        /// mapping used for caret placement stays in step with the glyphs.</para></summary>
        private static void ApplyCombiningMarkOrder(string input, int[] order, byte[] levels)
        {
            int i = 0;
            while (i < order.Length)
            {
                if (!IsCombiningMark(input[order[i]])) { i++; continue; }

                int j = i;
                while (j < order.Length && IsCombiningMark(input[order[j]])) j++;

                // j indexes the base the marks decorate. They only precede it when the run is RTL;
                // in an LTR run the marks already follow their base and must be left alone.
                if (j < order.Length && (levels[order[j]] & 1) != 0)
                    Array.Reverse(order, i, j - i + 1);

                i = j + 1;
            }
        }

        // Entry point for algorithm to return at final correct display order
        public static string LogicalToVisual(string input, int[] lineBreaks = null)
        {
            int[] newIndexes = ComputeReorderMap(input, lineBreaks, out byte[] levels);
            return GetOrderedString(input, newIndexes, levels);
        }

        // Returns (visualString, visualToLogicalMap) where map[visualPos] = logicalPos
        public static (string visual, int[] visualToLogical) LogicalToVisualWithMap(string input, int[] lineBreaks = null)
        {
            int[] map = ComputeReorderMap(input, lineBreaks, out byte[] levels);
            return (GetOrderedString(input, map, levels), map);
        }

        // 3.2 Determine Bidi_class of each input character
        private static void ClassifyCharacters(string text, ref byte[] typesList)
        {
            typesList = new byte[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                int chIndex     = Convert.ToInt32(text[i]);
                typesList[i]    = Bidi_Types.BidiCharTypes[chIndex];
            }
        }

        // Rules P2, P3 Determine paragraph embedding level given types array and optional 
        // start and end index to treat types as a scoped paragraph (useful for rule X5c)
        private static byte GetParagraphEmbeddingLevel(byte[] types, int[] matchingPDI, int si = -1, int ei = -1)
        {
            int start   = si != -1 ? si : 0;
            int end     = ei != -1 ? ei : types.Length;

            // Find first L, AL or R character
            for (int i = start; i < end; i++)
            {
                var cct = (BidiClass)types[i];
                if(cct == BidiClass.L  || 
                   cct == BidiClass.AL || 
                   cct == BidiClass.R)
                {
                    if(cct == BidiClass.L) return 0;
                    else return 1;
                }
                else if(cct == BidiClass.LRI || 
                        cct == BidiClass.RLI || 
                        cct == BidiClass.FSI)
                {
                    // Skip characters between isolate initiator and matching PDI (if found)
                    i = matchingPDI[i];
                }
            }

            return 0;   // default, no strong character type found
        }

        // 3.3.2 Determine Explicit Embedding Levels and directions
        private static void GetExplicitEmbeddingLevels(byte level, byte[] types, ref byte[] levels, int[] matchingPDI)
        {
            // X1.
            // Directional Status Stack and entry
            Stack<DirectionalStatus> dirStatusStack = new Stack<DirectionalStatus>(MAX_DEPTH + 2);
            DirectionalStatus dirEntry = new DirectionalStatus
            {
                paragraphEmbeddingLevel = level,
                directionalOverrideStatus = (int)BidiClass.ON,
                directionalIsolateStatus = false
            };
            dirStatusStack.Push(dirEntry);
            
            int overflowIsolateCount    = 0;
            int overflowEmbeddingCount  = 0;
            int validIsolateCount       = 0;

            // X2-X8
            for (int i = 0; i < types.Length; i++)
            {
                BidiClass cct = (BidiClass)types[i];
                switch (cct)
                {
                    case BidiClass.RLE:
                    case BidiClass.RLO:
                    case BidiClass.LRE:
                    case BidiClass.LRO:
                    case BidiClass.LRI:
                    case BidiClass.RLI:
                    case BidiClass.FSI:
                    {
                        byte newLevel;      // New calculated embedding level

                        bool isIsolate = (cct == BidiClass.RLI || cct == BidiClass.LRI);

                        // X5a, X5b .1 isolate embedding level
                        if(isIsolate)
                        {
                            levels[i] = dirStatusStack.Peek().paragraphEmbeddingLevel;
                        }

                        // X5c. Get embedding level of characters between FSI and its matching PDI
                        // FSI = RLI if embedding level is 1, otherwise LRI

                        if(cct == BidiClass.FSI)
                        {
                            byte el = GetParagraphEmbeddingLevel(types, matchingPDI, i + 1, matchingPDI[i]);
                            cct = el == 1 ? BidiClass.RLI : BidiClass.LRI;
                        }

                        // 1 (RLE RLO RLI, LRE LRO LRI) Compute least odd/even embedding level greater than embedding level
                        //  of last entry on directional status stack
                        if(cct == BidiClass.RLE || cct == BidiClass.RLO || cct == BidiClass.RLI)
                        {
                            newLevel = (byte)LeastGreaterOdd(dirStatusStack.Peek().paragraphEmbeddingLevel);
                        }
                        else
                        {
                            newLevel = (byte)LeastGreaterEven(dirStatusStack.Peek().paragraphEmbeddingLevel);
                        }

                        // 2 New level would be valid(level <= max_depth) and overflow isolate count and
                        // overflow embedding count are both zero => this RLE is valid, increment isolate counter.
                        if(newLevel <= MAX_DEPTH &&  overflowIsolateCount == 0 && overflowEmbeddingCount == 0)
                        {
                            // X5b .3
                            if(isIsolate)
                            {
                                validIsolateCount++;
                            }

                            // Push new entry to stack
                            byte dos = cct == BidiClass.RLO ? (byte)BidiClass.R  // RLO = R directional override status
                                    : cct == BidiClass.LRO ? (byte)BidiClass.L   // LRO = L directional override status
                                    : (byte)BidiClass.ON;                        // All rest are neutrals
                            dirStatusStack.Push(new DirectionalStatus()
                            {
                                paragraphEmbeddingLevel = newLevel,
                                directionalOverrideStatus = dos,
                                directionalIsolateStatus = isIsolate
                            });
                        }
                        // 3 Otherwise, this is an overflow RLE. If the overflow isolate count is zero, 
                        // increment the overflow embedding count by one. Leave all other variables unchanged.
                        else
                        {
                            if(overflowIsolateCount == 0)
                            {
                                overflowEmbeddingCount++;
                            }
                        }
                    }
                    break;

                    // X6a Terminating Isolates
                    case BidiClass.PDI:
                    {
                        if (overflowIsolateCount > 0)   // This PDI matches an overflow isolate initiator
                        {
                            overflowIsolateCount--;
                        }
                        else if (validIsolateCount == 0)
                        {
                            // No matching isolator (valid or overflow), do nothing
                        }
                        else // This PDI matches a valid isolate initiator
                        {
                            overflowEmbeddingCount = 0;

                            while (dirStatusStack.Peek().directionalIsolateStatus == false)
                            {
                                dirStatusStack.Pop();
                            }

                            dirStatusStack.Pop();
                            validIsolateCount--;
                        }

                        levels[i] = dirStatusStack.Peek().paragraphEmbeddingLevel;
                    }
                    break;

                    // X7
                    case BidiClass.PDF:
                    {
                        if(overflowIsolateCount > 0) // X7 .1
                        {
                            // Do nothing
                        }
                        else if(overflowEmbeddingCount > 0) // X7 .2
                        {
                            overflowEmbeddingCount--;
                        }
                        else if(!dirStatusStack.Peek().directionalIsolateStatus && dirStatusStack.Count > 1) // X7 .3
                        {
                            dirStatusStack.Pop();
                        }
                        else
                        {
                            // Do nothing
                        }
                    }
                    break;

                    // X8
                    case BidiClass.B:
                    {
                        // Paragraph separators.
                        // Applied at the end of paragraph (last character in array).

                        // 1 Terminate(reset) all directional embeddings, overrides and isolates 
                        overflowEmbeddingCount = 0;
                        overflowIsolateCount = 0;
                        validIsolateCount = 0;
                        dirStatusStack.Clear();     // Also pop off initialization entry

                        // Re-push the initial entry so subsequent characters have a valid stack
                        dirStatusStack.Push(new DirectionalStatus
                        {
                            paragraphEmbeddingLevel = level,
                            directionalOverrideStatus = (int)BidiClass.ON,
                            directionalIsolateStatus = false
                        });

                        // 2 Assign separator character an embedding level equal to paragraph embedding level
                        levels[i] = level;
                    }
                    break;

                    // X6 Non-formatting characters
                    default:
                    {
                        levels[i] = dirStatusStack.Peek().paragraphEmbeddingLevel;
                        if(dirStatusStack.Peek().directionalOverrideStatus != (int)BidiClass.ON) // X6.b (6.2.0 naming)
                        {
                            types[i] = dirStatusStack.Peek().directionalOverrideStatus; // reset type to last element status
                        }
                    }
                    break;
                }
            }
        }

        // 3.3.3 Resolve Weak Types
        private static void ResolveWeaks(this IsolatingRunSequence sequence)
        {
            // W1 NSM
            for (int i = 0; i < sequence.length; i++)
            {
                var ct = (BidiClass)sequence.types[i];
                var prevType = i == 0 ? sequence.sos : (BidiClass)sequence.types[i - 1];
                if(ct == BidiClass.NSM)
                {
                    // if NSM is at start of sequence resolved to sos type
                    // assign ON if previous is isolate initiator or PDI, otherwise type of previous
                    bool isIsolateOrPDI = prevType == BidiClass.LRI || 
                                          prevType == BidiClass.RLI || 
                                          prevType == BidiClass.FSI || 
                                          prevType == BidiClass.PDI;

                    sequence.types[i] = isIsolateOrPDI ? (byte)BidiClass.ON : (byte)prevType;
                }
            }

            // W2 EN
            // At each EN search in backward until first strong type is found, if AL is found then resolve to AN
            for (int i = 0; i < sequence.length; i++)
            {
                var chType = (BidiClass)sequence.types[i];
                if (chType == BidiClass.EN)
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var type = (BidiClass)sequence.types[j];
                        if (type == BidiClass.R  || type == BidiClass.AL || type == BidiClass.L)
                        {
                            // Stop at the FIRST strong type whatever it is. Breaking only on AL let the
                            // scan run past an intervening R or L and pick up an AL further back, which
                            // turned a European number into an Arabic one that W2 never licensed.
                            if (type == BidiClass.AL) sequence.types[i] = (byte)BidiClass.AN;
                            break;
                        }
                    }
                }
            }

            // W3 AL
            // Resolve all ALs to R
            for (int i = 0; i < sequence.length; i++)
            {
                if ((BidiClass)sequence.types[i] == BidiClass.AL)
                {
                    sequence.types[i] = (byte)BidiClass.R;
                }
            }

            // W4 ES, CS (Number Separators)
            // ES between EN is resolved to EN
            // Single CS between same numbers type is resolve to that number type
            for (int i = 1; i < sequence.length - 1; i++)
            {
                var cct         = (BidiClass)sequence.types[i];
                var prevType    = (BidiClass)sequence.types[i - 1];
                var nextType    = (BidiClass)sequence.types[i + 1];

                if (cct == BidiClass.ES && prevType == BidiClass.EN && nextType == BidiClass.EN) // EN ES EN -> EN EN EN
                {
                    sequence.types[i] = (byte)BidiClass.EN;
                }
                else if (cct == BidiClass.CS && (
                prevType == BidiClass.EN && nextType == BidiClass.EN ||
                prevType == BidiClass.AN && nextType == BidiClass.AN))      // EN CS EN -> EN EN EN, AN CS AN -> AN AN AN
                {
                    sequence.types[i] = (byte)prevType;
                }
            }

            // W5 ET(s) adjacent to EN resolve to EN(s)
            var typesSet = new BidiClass[] { BidiClass.ET };
            for (int i = 0; i < sequence.length; i++)
            {
                if ((BidiClass)sequence.types[i] == BidiClass.ET)
                {
                    int runStart = i;
                    // int runEnd = runStart;
                    // runEnd = Array.FindIndex(sequence.types, runStart, t1 => typesSet.Any(t2 => t2 == (BidiClass)t1));
                    int runEnd = sequence.GetRunLimit(runStart, sequence.length, typesSet);

                    var type = runStart > 0 ? (BidiClass)sequence.types[runStart - 1] : sequence.sos;

                    if (type != BidiClass.EN)
                    {
                        type = runEnd < sequence.length ? (BidiClass)sequence.types[runEnd] : sequence.eos; // End type
                    }

                    if (type == BidiClass.EN)
                    {
                        sequence.SetRunTypes(runStart, runEnd, BidiClass.EN); // Resolve to EN
                    }

                    i = runEnd; // advance to end of sequence
                }
            }

            // W6 Separators and Terminators -> ON
            for (int i = 0; i < sequence.length; i++)
            {
                var t = (BidiClass)sequence.types[i];
                if (t == BidiClass.ET || t == BidiClass.ES || t == BidiClass.CS)
                {
                    sequence.types[i] = (byte)BidiClass.ON;
                }
            }

            // W7 same as W2 but EN -> L
            for (int i = 0; i < sequence.length; i++)
            {
                if((BidiClass)sequence.types[i] == BidiClass.EN)
                {
                    var prevStrong = sequence.sos;  // Default to sos if reached start
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var t = (BidiClass)sequence.types[j];
                        if (t == BidiClass.R || t == BidiClass.L || t == BidiClass.AL)
                        {
                            prevStrong = t;
                            break;
                        }
                    }

                    // ⛔ This test used to sit INSIDE the backward scan, so it ran against the initial
                    // value (sos) on every non-strong character and never against the strong type the
                    // scan actually found — the loop breaks the moment it finds one. Any EN preceded by
                    // a neutral in an LTR paragraph was therefore forced to L regardless of the Hebrew
                    // or Arabic run in front of it, and "bbdge סז 07" reordered as "bbdge זס 07"
                    // instead of "bbdge 07 זס".
                    if (prevStrong == BidiClass.L)
                    {
                        sequence.types[i] = (byte)BidiClass.L;
                    }
                }
            }

        }

        // 3.3.4 Resolve Neutral Types
        // In final results all NIs are resolved to R or L
        // BD16: the maximum bracket-pair stack depth. An opening bracket that would overflow it stops
        // BD16 for the rest of the sequence, keeping the pairs already identified.
        private const int MAX_PAIRING_DEPTH = 63;

        /// <summary>Bidi_Paired_Bracket: the other half of the pair, and whether THIS character opens it.
        /// The 61 pairs are the Bidi_Mirrored characters whose General_Category is Ps or Pe.
        /// ⛔ Openness is stored explicitly rather than inferred from "the opener is the lower code
        /// point" — that holds for 60 of the 61 pairs and fails for U+298F/U+298E.</summary>
        private static (char partner, bool open) PairedBracket(char c) => c switch
        {
            '\u0028' => ('\u0029', true ),   // LEFT PARENTHESIS
            '\u0029' => ('\u0028', false),   // RIGHT PARENTHESIS
            '\u005B' => ('\u005D', true ),   // LEFT SQUARE BRACKET
            '\u005D' => ('\u005B', false),   // RIGHT SQUARE BRACKET
            '\u007B' => ('\u007D', true ),   // LEFT CURLY BRACKET
            '\u007D' => ('\u007B', false),   // RIGHT CURLY BRACKET
            '\u2045' => ('\u2046', true ),   // LEFT SQUARE BRACKET WITH QUILL
            '\u2046' => ('\u2045', false),   // RIGHT SQUARE BRACKET WITH QUILL
            '\u207D' => ('\u207E', true ),   // SUPERSCRIPT LEFT PARENTHESIS
            '\u207E' => ('\u207D', false),   // SUPERSCRIPT RIGHT PARENTHESIS
            '\u208D' => ('\u208E', true ),   // SUBSCRIPT LEFT PARENTHESIS
            '\u208E' => ('\u208D', false),   // SUBSCRIPT RIGHT PARENTHESIS
            '\u2308' => ('\u2309', true ),   // LEFT CEILING
            '\u2309' => ('\u2308', false),   // RIGHT CEILING
            '\u230A' => ('\u230B', true ),   // LEFT FLOOR
            '\u230B' => ('\u230A', false),   // RIGHT FLOOR
            '\u2329' => ('\u232A', true ),   // LEFT-POINTING ANGLE BRACKET
            '\u232A' => ('\u2329', false),   // RIGHT-POINTING ANGLE BRACKET
            '\u2768' => ('\u2769', true ),   // MEDIUM LEFT PARENTHESIS ORNAMENT
            '\u2769' => ('\u2768', false),   // MEDIUM RIGHT PARENTHESIS ORNAMENT
            '\u276A' => ('\u276B', true ),   // MEDIUM FLATTENED LEFT PARENTHESIS ORNAMENT
            '\u276B' => ('\u276A', false),   // MEDIUM FLATTENED RIGHT PARENTHESIS ORNAMENT
            '\u276C' => ('\u276D', true ),   // MEDIUM LEFT-POINTING ANGLE BRACKET ORNAMENT
            '\u276D' => ('\u276C', false),   // MEDIUM RIGHT-POINTING ANGLE BRACKET ORNAMENT
            '\u276E' => ('\u276F', true ),   // HEAVY LEFT-POINTING ANGLE QUOTATION MARK ORNAMENT
            '\u276F' => ('\u276E', false),   // HEAVY RIGHT-POINTING ANGLE QUOTATION MARK ORNAMENT
            '\u2770' => ('\u2771', true ),   // HEAVY LEFT-POINTING ANGLE BRACKET ORNAMENT
            '\u2771' => ('\u2770', false),   // HEAVY RIGHT-POINTING ANGLE BRACKET ORNAMENT
            '\u2772' => ('\u2773', true ),   // LIGHT LEFT TORTOISE SHELL BRACKET ORNAMENT
            '\u2773' => ('\u2772', false),   // LIGHT RIGHT TORTOISE SHELL BRACKET ORNAMENT
            '\u2774' => ('\u2775', true ),   // MEDIUM LEFT CURLY BRACKET ORNAMENT
            '\u2775' => ('\u2774', false),   // MEDIUM RIGHT CURLY BRACKET ORNAMENT
            '\u27C5' => ('\u27C6', true ),   // LEFT S-SHAPED BAG DELIMITER
            '\u27C6' => ('\u27C5', false),   // RIGHT S-SHAPED BAG DELIMITER
            '\u27E6' => ('\u27E7', true ),   // MATHEMATICAL LEFT WHITE SQUARE BRACKET
            '\u27E7' => ('\u27E6', false),   // MATHEMATICAL RIGHT WHITE SQUARE BRACKET
            '\u27E8' => ('\u27E9', true ),   // MATHEMATICAL LEFT ANGLE BRACKET
            '\u27E9' => ('\u27E8', false),   // MATHEMATICAL RIGHT ANGLE BRACKET
            '\u27EA' => ('\u27EB', true ),   // MATHEMATICAL LEFT DOUBLE ANGLE BRACKET
            '\u27EB' => ('\u27EA', false),   // MATHEMATICAL RIGHT DOUBLE ANGLE BRACKET
            '\u27EC' => ('\u27ED', true ),   // MATHEMATICAL LEFT WHITE TORTOISE SHELL BRACKET
            '\u27ED' => ('\u27EC', false),   // MATHEMATICAL RIGHT WHITE TORTOISE SHELL BRACKET
            '\u27EE' => ('\u27EF', true ),   // MATHEMATICAL LEFT FLATTENED PARENTHESIS
            '\u27EF' => ('\u27EE', false),   // MATHEMATICAL RIGHT FLATTENED PARENTHESIS
            '\u2983' => ('\u2984', true ),   // LEFT WHITE CURLY BRACKET
            '\u2984' => ('\u2983', false),   // RIGHT WHITE CURLY BRACKET
            '\u2985' => ('\u2986', true ),   // LEFT WHITE PARENTHESIS
            '\u2986' => ('\u2985', false),   // RIGHT WHITE PARENTHESIS
            '\u2987' => ('\u2988', true ),   // Z NOTATION LEFT IMAGE BRACKET
            '\u2988' => ('\u2987', false),   // Z NOTATION RIGHT IMAGE BRACKET
            '\u2989' => ('\u298A', true ),   // Z NOTATION LEFT BINDING BRACKET
            '\u298A' => ('\u2989', false),   // Z NOTATION RIGHT BINDING BRACKET
            '\u298B' => ('\u298C', true ),   // LEFT SQUARE BRACKET WITH UNDERBAR
            '\u298C' => ('\u298B', false),   // RIGHT SQUARE BRACKET WITH UNDERBAR
            '\u298D' => ('\u2990', true ),   // LEFT SQUARE BRACKET WITH TICK IN TOP CORNER
            '\u298E' => ('\u298F', false),   // RIGHT SQUARE BRACKET WITH TICK IN BOTTOM CORNER
            '\u298F' => ('\u298E', true ),   // LEFT SQUARE BRACKET WITH TICK IN BOTTOM CORNER
            '\u2990' => ('\u298D', false),   // RIGHT SQUARE BRACKET WITH TICK IN TOP CORNER
            '\u2991' => ('\u2992', true ),   // LEFT ANGLE BRACKET WITH DOT
            '\u2992' => ('\u2991', false),   // RIGHT ANGLE BRACKET WITH DOT
            '\u2993' => ('\u2994', true ),   // LEFT ARC LESS-THAN BRACKET
            '\u2994' => ('\u2993', false),   // RIGHT ARC GREATER-THAN BRACKET
            '\u2995' => ('\u2996', true ),   // DOUBLE LEFT ARC GREATER-THAN BRACKET
            '\u2996' => ('\u2995', false),   // DOUBLE RIGHT ARC LESS-THAN BRACKET
            '\u2997' => ('\u2998', true ),   // LEFT BLACK TORTOISE SHELL BRACKET
            '\u2998' => ('\u2997', false),   // RIGHT BLACK TORTOISE SHELL BRACKET
            '\u29D8' => ('\u29D9', true ),   // LEFT WIGGLY FENCE
            '\u29D9' => ('\u29D8', false),   // RIGHT WIGGLY FENCE
            '\u29DA' => ('\u29DB', true ),   // LEFT DOUBLE WIGGLY FENCE
            '\u29DB' => ('\u29DA', false),   // RIGHT DOUBLE WIGGLY FENCE
            '\u29FC' => ('\u29FD', true ),   // LEFT-POINTING CURVED ANGLE BRACKET
            '\u29FD' => ('\u29FC', false),   // RIGHT-POINTING CURVED ANGLE BRACKET
            '\u2E22' => ('\u2E23', true ),   // TOP LEFT HALF BRACKET
            '\u2E23' => ('\u2E22', false),   // TOP RIGHT HALF BRACKET
            '\u2E24' => ('\u2E25', true ),   // BOTTOM LEFT HALF BRACKET
            '\u2E25' => ('\u2E24', false),   // BOTTOM RIGHT HALF BRACKET
            '\u2E26' => ('\u2E27', true ),   // LEFT SIDEWAYS U BRACKET
            '\u2E27' => ('\u2E26', false),   // RIGHT SIDEWAYS U BRACKET
            '\u2E28' => ('\u2E29', true ),   // LEFT DOUBLE PARENTHESIS
            '\u2E29' => ('\u2E28', false),   // RIGHT DOUBLE PARENTHESIS
            '\u2E55' => ('\u2E56', true ),   // LEFT SQUARE BRACKET WITH STROKE
            '\u2E56' => ('\u2E55', false),   // RIGHT SQUARE BRACKET WITH STROKE
            '\u2E57' => ('\u2E58', true ),   // LEFT SQUARE BRACKET WITH DOUBLE STROKE
            '\u2E58' => ('\u2E57', false),   // RIGHT SQUARE BRACKET WITH DOUBLE STROKE
            '\u2E59' => ('\u2E5A', true ),   // TOP HALF LEFT PARENTHESIS
            '\u2E5A' => ('\u2E59', false),   // TOP HALF RIGHT PARENTHESIS
            '\u2E5B' => ('\u2E5C', true ),   // BOTTOM HALF LEFT PARENTHESIS
            '\u2E5C' => ('\u2E5B', false),   // BOTTOM HALF RIGHT PARENTHESIS
            '\u3008' => ('\u3009', true ),   // LEFT ANGLE BRACKET
            '\u3009' => ('\u3008', false),   // RIGHT ANGLE BRACKET
            '\u300A' => ('\u300B', true ),   // LEFT DOUBLE ANGLE BRACKET
            '\u300B' => ('\u300A', false),   // RIGHT DOUBLE ANGLE BRACKET
            '\u300C' => ('\u300D', true ),   // LEFT CORNER BRACKET
            '\u300D' => ('\u300C', false),   // RIGHT CORNER BRACKET
            '\u300E' => ('\u300F', true ),   // LEFT WHITE CORNER BRACKET
            '\u300F' => ('\u300E', false),   // RIGHT WHITE CORNER BRACKET
            '\u3010' => ('\u3011', true ),   // LEFT BLACK LENTICULAR BRACKET
            '\u3011' => ('\u3010', false),   // RIGHT BLACK LENTICULAR BRACKET
            '\u3014' => ('\u3015', true ),   // LEFT TORTOISE SHELL BRACKET
            '\u3015' => ('\u3014', false),   // RIGHT TORTOISE SHELL BRACKET
            '\u3016' => ('\u3017', true ),   // LEFT WHITE LENTICULAR BRACKET
            '\u3017' => ('\u3016', false),   // RIGHT WHITE LENTICULAR BRACKET
            '\u3018' => ('\u3019', true ),   // LEFT WHITE TORTOISE SHELL BRACKET
            '\u3019' => ('\u3018', false),   // RIGHT WHITE TORTOISE SHELL BRACKET
            '\u301A' => ('\u301B', true ),   // LEFT WHITE SQUARE BRACKET
            '\u301B' => ('\u301A', false),   // RIGHT WHITE SQUARE BRACKET
            '\uFE59' => ('\uFE5A', true ),   // SMALL LEFT PARENTHESIS
            '\uFE5A' => ('\uFE59', false),   // SMALL RIGHT PARENTHESIS
            '\uFE5B' => ('\uFE5C', true ),   // SMALL LEFT CURLY BRACKET
            '\uFE5C' => ('\uFE5B', false),   // SMALL RIGHT CURLY BRACKET
            '\uFE5D' => ('\uFE5E', true ),   // SMALL LEFT TORTOISE SHELL BRACKET
            '\uFE5E' => ('\uFE5D', false),   // SMALL RIGHT TORTOISE SHELL BRACKET
            '\uFF08' => ('\uFF09', true ),   // FULLWIDTH LEFT PARENTHESIS
            '\uFF09' => ('\uFF08', false),   // FULLWIDTH RIGHT PARENTHESIS
            '\uFF3B' => ('\uFF3D', true ),   // FULLWIDTH LEFT SQUARE BRACKET
            '\uFF3D' => ('\uFF3B', false),   // FULLWIDTH RIGHT SQUARE BRACKET
            '\uFF5B' => ('\uFF5D', true ),   // FULLWIDTH LEFT CURLY BRACKET
            '\uFF5D' => ('\uFF5B', false),   // FULLWIDTH RIGHT CURLY BRACKET
            '\uFF5F' => ('\uFF60', true ),   // FULLWIDTH LEFT WHITE PARENTHESIS
            '\uFF60' => ('\uFF5F', false),   // FULLWIDTH RIGHT WHITE PARENTHESIS
            '\uFF62' => ('\uFF63', true ),   // HALFWIDTH LEFT CORNER BRACKET
            '\uFF63' => ('\uFF62', false),   // HALFWIDTH RIGHT CORNER BRACKET
            _ => ('\0', false),
        };

        // BD16 matches brackets under CANONICAL equivalence, so U+2329/U+232A (which decompose to
        // U+3008/U+3009) pair with either spelling of the angle bracket.
        private static char CanonicalBracket(char c) => c switch
        {
            '\u2329' => '\u3008',
            '\u232A' => '\u3009',
            _         => c,
        };

        // For N0 only, EN and AN count as R; everything else that is not L or R is "no strong type",
        // which we spell as ON here.
        private static BidiClass StrongDirectionOf(byte t) => (BidiClass)t switch
        {
            BidiClass.L                                   => BidiClass.L,
            BidiClass.R or BidiClass.EN or BidiClass.AN   => BidiClass.R,
            _                                             => BidiClass.ON,
        };

        // BD16: identify the bracket pairs in this isolating run sequence, sorted by opening position.
        private static List<(int open, int close)> LocateBracketPairs(this IsolatingRunSequence sequence, string text)
        {
            var pairs    = new List<(int, int)>();
            var expected = new char[MAX_PAIRING_DEPTH];   // the closing bracket each open is waiting for
            var openPos  = new int[MAX_PAIRING_DEPTH];
            int depth    = 0;

            for (int i = 0; i < sequence.length; i++)
            {
                // BD14/BD15: only a bracket whose CURRENT type is ON can open or close a pair.
                if ((BidiClass)sequence.types[i] != BidiClass.ON) continue;

                char c = text[sequence.indexes[i]];
                var (partner, isOpen) = PairedBracket(c);
                if (partner == '\0') continue;

                if (isOpen)
                {
                    if (depth == MAX_PAIRING_DEPTH) break;   // stack overflow: keep what we have, stop
                    expected[depth] = CanonicalBracket(partner);
                    openPos[depth]  = i;
                    depth++;
                }
                else
                {
                    char closing = CanonicalBracket(c);
                    for (int s = depth - 1; s >= 0; s--)
                    {
                        if (expected[s] != closing) continue;
                        pairs.Add((openPos[s], i));
                        depth = s;          // pop the match AND everything stacked above it
                        break;
                    }
                }
            }

            pairs.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            return pairs;
        }

        /// <summary>Rule N0 — resolve paired brackets to the same direction so that "(bloom)" inside a
        /// Hebrew sentence keeps both of its brackets on the same side of the text they enclose.
        /// Without it the two halves of a pair can resolve independently under N1/N2 and end up
        /// straddling the wrong runs.</summary>
        private static void ResolveBrackets(this IsolatingRunSequence sequence, string text)
        {
            var pairs = sequence.LocateBracketPairs(text);
            if (pairs.Count == 0) return;

            BidiClass e = GetTypeForLevel(sequence.level);                    // embedding direction
            BidiClass o = e == BidiClass.L ? BidiClass.R : BidiClass.L;       // opposite direction

            foreach (var (op, cl) in pairs)
            {
                // N0 b/c: which strong direction does the bracket pair enclose?
                bool foundE = false, foundO = false;
                for (int k = op + 1; k < cl; k++)
                {
                    var s = StrongDirectionOf(sequence.types[k]);
                    if (s == BidiClass.ON) continue;
                    if (s == e) { foundE = true; break; }
                    foundO = true;
                }

                BidiClass resolved;
                if (foundE)
                {
                    resolved = e;                                             // N0 b
                }
                else if (foundO)
                {
                    // N0 c: the opposite direction is enclosed — follow the established context
                    // BEFORE the opening bracket, falling back to sos at the start of the sequence.
                    BidiClass prior = BidiClass.ON;
                    for (int k = op - 1; k >= 0; k--)
                    {
                        var s = StrongDirectionOf(sequence.types[k]);
                        if (s != BidiClass.ON) { prior = s; break; }
                    }
                    if (prior == BidiClass.ON) prior = sequence.sos;
                    resolved = prior == o ? o : e;                            // N0 c.1 / c.2
                }
                else
                {
                    continue;                                                 // N0 d: leave to N1/N2
                }

                sequence.types[op] = (byte)resolved;
                sequence.types[cl] = (byte)resolved;
                sequence.FollowingNsmTakeBracketType(text, op, resolved);
                sequence.FollowingNsmTakeBracketType(text, cl, resolved);
            }
        }

        // N0 tail: characters whose ORIGINAL type (before W1 rewrote them) was NSM and which directly
        // follow a bracket that N0 just changed must take the bracket's new type, so a combining mark
        // cannot be left behind in the other direction.
        private static void FollowingNsmTakeBracketType(this IsolatingRunSequence sequence, string text, int at, BidiClass t)
        {
            for (int k = at + 1; k < sequence.length; k++)
            {
                if ((BidiClass)Bidi_Types.BidiCharTypes[text[sequence.indexes[k]]] != BidiClass.NSM) break;
                sequence.types[k] = (byte)t;
            }
        }

        private static void ResolveNeutrals(this IsolatingRunSequence sequence, string text)
        {
            // N0 runs before N1/N2 and can hand them already-resolved brackets.
            sequence.ResolveBrackets(text);

            // N1
            // Sequence of NIs will resolve to surrounding "strong" type if text on both sides was of same direction.
            // sos and eos are used at run sequence boundaries. AN and EN will resolve type to R.
            var typesSet = new BidiClass[] { BidiClass.B, BidiClass.S, BidiClass.WS, BidiClass.ON, BidiClass.LRI, BidiClass.RLI, BidiClass.FSI, BidiClass.PDI };
            for (int i = 0; i < sequence.length; i++)
            {
                var ct = (BidiClass)sequence.types[i];
                bool isNI = ct == BidiClass.B   ||
                            ct == BidiClass.S   ||
                            ct == BidiClass.WS  ||
                            ct == BidiClass.ON  ||
                            ct == BidiClass.LRI ||
                            ct == BidiClass.RLI ||
                            ct == BidiClass.FSI ||
                            ct == BidiClass.PDI;

                if (isNI)
                {
                    BidiClass   leadType  = 0;
                    BidiClass   trailType = 0;
                    int         start     = i;
                    int         runEnd    = sequence.GetRunLimit(start, sequence.length, typesSet);

                    // Start of matching NI
                    if (start == 0) // Start boundary, lead type = sos
                    {
                        leadType = sequence.sos;
                    }
                    else
                    {
                        leadType = (BidiClass)sequence.types[start - 1];
                        if (leadType == BidiClass.AN || leadType == BidiClass.EN)   // Leading AN, EN resolve type to R
                        {
                            leadType = BidiClass.R;
                        }
                    }

                    // End of Matching NI
                    if (runEnd == sequence.length) // End boundary. trail type = eos
                    {
                        trailType = sequence.eos;
                    }
                    else
                    {
                        trailType = (BidiClass)sequence.types[runEnd];
                        if (trailType == BidiClass.AN || trailType == BidiClass.EN)
                        {
                            trailType = BidiClass.R;
                        }
                    }

                    if (leadType == trailType)
                    {
                        sequence.SetRunTypes(start, runEnd, leadType);
                    }
                    else    // N2
                    {
                        // Remaining NIs take current run embedding level
                        var runDirection = GetTypeForLevel(sequence.level);
                        sequence.SetRunTypes(start, runEnd, runDirection);
                    }

                    i = runEnd;
                }
            }
        }

        // 3.3.5 Resolve Implicit Embedding Levels
        private static void ResolveImplicit(this IsolatingRunSequence sequence)
        {
            byte level = sequence.level;

            // Initialize the sequence resolved levels with sequence embedding level
            sequence.resolvedLevels = new byte[sequence.length];
            SetLevels(ref sequence.resolvedLevels, sequence.level);

            for (int i = 0; i < sequence.length; i++)
            {
                var ct = (BidiClass)sequence.types[i];

                // I1
                // Sequence level is even (Left-to-right) then R types go up one level, AN and EN go up two levels
                if (!IsOdd(level))
                {
                    if (ct == BidiClass.R)
                    {
                        sequence.resolvedLevels[i] += 1;
                    }
                    else if(ct == BidiClass.AN || ct == BidiClass.EN)
                    {
                        sequence.resolvedLevels[i] += 2;
                    }
                }
                // N2
                // Sequence level is odd (Right-to-left) then L, AN, EN go up one level
                else
                {
                    if (ct == BidiClass.L || ct == BidiClass.AN || ct == BidiClass.EN)
                    {
                        sequence.resolvedLevels[i] += 1;
                    }
                }
            }
        }

        private static void ApplyTypesAndLevels(this IsolatingRunSequence sequence, ref byte[] typesList, ref byte[] levelsList)
        {
            for (int i = 0; i < sequence.length; i++)
            {
                int idx         = sequence.indexes[i];
                typesList[idx]  = sequence.types[i];
                levelsList[idx] = sequence.resolvedLevels[i];
            }
        }

        // Entry for Rules L1-L2
        // Return the final ordered levels array including the line breaks
        private static int[] GetReorderedIndexes(byte level, byte[] typesList, byte[] levelsList, int[] lineBreaks)
        {
            var levels = GetTextLevels(level, typesList, levelsList, lineBreaks);
            
            var multilineLevels = GetMultiLineReordered(levels, lineBreaks);

            return multilineLevels;
        }

        private static void GetMatchingPDI(byte[] types, out int[] outMatchingPDI, out int[] outMatchingIsolateInitiator)
        {
            int[] matchingPDI = new int[types.Length];
            int[] matchingIsolateInitiator = new int[types.Length];
            
            // Scan for isolate initiator
            for (int i = 0; i < types.Length; i++)
            {
                var cct = (BidiClass)types[i];
                if(cct == BidiClass.LRI || 
                   cct == BidiClass.RLI || 
                   cct == BidiClass.FSI)
                {
                    int  counter         = 1;
                    bool hasMatchingPDI  = false;

                    // Scan the text following isolate initiator till end of paragraph
                    for (int j = i + 1; j < types.Length; j++)
                    {
                        BidiClass nct = (BidiClass)types[j];
                        if(nct == BidiClass.LRI || 
                           nct == BidiClass.RLI || 
                           nct == BidiClass.FSI)        // Increment counter at every isolate initiator
                        {
                            counter++;
                        }
                        else if(nct == BidiClass.PDI)   // Decrement counter at every PDI
                        {
                            counter--;
                            
                            if(counter == 0)            // BD9 bullet 3. Stop when counter is 0
                            {
                                hasMatchingPDI              = true;
                                matchingPDI[i]              = j;      // Matching PDI found
                                matchingIsolateInitiator[j] = i;
                                break;
                            }
                            
                        }
                    }

                    if (!hasMatchingPDI)
                    {
                        matchingPDI[i] = types.Length;
                    }
                }
                else        // Other characters matchingPDI are set to -1
                {
                    matchingPDI[i]              = -1;
                    matchingIsolateInitiator[i] = -1;
                }
            }

            outMatchingPDI              = matchingPDI;
            outMatchingIsolateInitiator = matchingIsolateInitiator;
        }

        private static void RemoveX9Characters(ref byte[] buffer)
        {
            // Todo: ZWJ and ZWNJ characters exception from BN overriding

            // Replace Embedding and override type with BN
            for (int i = 0; i < buffer.Length; i++)
            {
                var ct = (BidiClass)buffer[i];
                if(ct == BidiClass.LRE || ct == BidiClass.RLE ||
                   ct == BidiClass.LRO || ct == BidiClass.RLO)
                {
                    buffer[i] = (byte)BidiClass.BN;
                }
            }
        }

        private static List<List<int>> GetLevelRuns(byte[] levels)
        {
            List<int>       runList         = new List<int>();
            List<List<int>> allRunsList     = new List<List<int>>();

            sbyte currentLevel = -1;
            for (int i = 0; i < levels.Length; i++)
            {
                if(levels[i] != currentLevel)        // New run
                {
                    if(currentLevel >= 0)           // Assign last run
                    {
                        allRunsList.Add(runList);
                        runList.Clear();
                    }

                    currentLevel = (sbyte)levels[i];       // New run level
                }

                runList.Add(i);
            }

            // Append last run
            if (runList.Count > 0)
            {
                allRunsList.Add(runList);
            }

            return allRunsList;
        }

        // Map each character to its belonging run
        private static int[] GetRunForCharacter(List<List<int>> levelRuns, int length)
        {
            int[] runCharsArray = new int[length];
            for (int i = 0; i < levelRuns.Count; i++)
            {
                for (int j = 0; j < levelRuns[i].Count; j++)
                {
                    int chPos = levelRuns[i][j];
                    runCharsArray[chPos] = chPos;
                }
            }

            return runCharsArray;
        }

        private static List<IsolatingRunSequence> GetIsolatingRunSequences(byte pLevel, byte[] types, byte[] levels, 
        List<List<int>> levelRuns, int[] matchingIsolateInitiator, int[] matchingPDI, int[] runCharsArray)
        {
            List<IsolatingRunSequence> allRunSequences = new List<IsolatingRunSequence>(levelRuns.Count);

            foreach (var run in levelRuns)
            {
                List<int> currRunSequence;
                var first = run[0];

                if((BidiClass)types[first] != BidiClass.PDI || matchingIsolateInitiator[first] == -1) // BD13 bullet 2
                {
                    currRunSequence = new List<int>(run);           // initialize a new level run sequence with current run
                    
                    int  lastCh              = currRunSequence[currRunSequence.Count - 1];
                    var  lastType            = (BidiClass)types[lastCh];
                    bool isIsolateInitiator  = lastType == BidiClass.RLI || 
                                               lastType == BidiClass.LRI || 
                                               lastType == BidiClass.FSI;

                    int lastChMatchingPDI = matchingPDI[lastCh];
                    while (isIsolateInitiator && lastChMatchingPDI != types.Length)
                    {
                        var lChRunIndex = runCharsArray[lastChMatchingPDI]; // Get run index for last character that has matchingPDI
                        var newRun = levelRuns[lChRunIndex];
                        currRunSequence.AddRange(newRun);
                    }

                    allRunSequences.Add(new IsolatingRunSequence(pLevel, currRunSequence, types, levels));
                }
            }

            return allRunSequences;
        }

        // X10 bullet 2 Determine start and end of sequence types (R or L) for an isolating run sequence
        // using run sequence indexes
        private static void ComputeIsolatingRunSequence(this IsolatingRunSequence sequence, byte pLevel, List<int> indexList, 
        byte[] typesList, byte[] levels)
        {
            sequence.length = indexList.Count;
            sequence.indexes = indexList.ToArray();                     // Indexes of run in original text
            
            // Character types of run sequence
            sequence.types = new byte[indexList.Count];
            for (int i = 0; i < sequence.length; i++)
            {
                sequence.types[i] = typesList[indexList[i]];
            }

            // sos
            var firstLevel = levels[indexList[0]];      // level of first character
            sequence.level = firstLevel;
            var previous = indexList[0] - 1;
            var prevLevel = previous >= 0 ? levels[previous] : pLevel;
            sequence.sos = GetTypeForLevel(Math.Max(firstLevel, prevLevel));

            // eos
            var lastType     = (BidiClass)sequence.types[sequence.length - 1];
            var last         = indexList[sequence.length - 1];       // last character in the sequence
            var lastLevel    = levels[last];
            var next         = indexList[sequence.length - 1] + 1;   // next character after sequence (in paragraph)
            var nextLevel    = next < typesList.Length && lastType != BidiClass.PDI ? levels[last] : pLevel;
            sequence.eos     = GetTypeForLevel(Math.Max(lastLevel, nextLevel));
        }

        // Override levels list with new level value
        private static void SetLevels(ref byte[] levels, byte newLevel)
        {
            for (int i = 0; i < levels.Length; i++)
            {
                levels[i] = newLevel;
            }
        }

        // Return end index of run consisting of types in typesSet
        // Start from index and check the value, if value not present in set then return index.
        private static int GetRunLimit(this IsolatingRunSequence sequence, int index, int limit, BidiClass[] typesSet)
        {
            loop: for (; index < limit;)
            {
                var type = (BidiClass)sequence.types[index];
                for (int i = 0; i < typesSet.Length; i++)
                {
                    if (type == typesSet[i])
                    {
                        index++;
                        goto loop;
                    }
                }

                // No match in typesSet
                return index;
            }

            return limit;
        }

        // Override types list from start up to (not including) limit to newType
        private static void SetRunTypes(this IsolatingRunSequence sequence, int start, int limit, BidiClass newType)
        {
            for (int i = start; i < limit; i++)
            {
                sequence.types[i] = (byte)newType;
            }
        }

        // Compute least odd level greater than l
        private static int LeastGreaterOdd(int l)
        {
            return IsOdd(l) ? l + 2 : l + 1;
        }
        
        // Compute least even level greater than l
        private static int LeastGreaterEven(int l)
        {
            return !IsOdd(l) ? l + 2: l + 1;
        }

        private static bool IsOdd(int n)
        {
            return (n & 1) != 0;
        }

        // Return L if level is even and R if Odd
        private static BidiClass GetTypeForLevel(byte level)
        {
            return (level & 1) == 0 ? BidiClass.L : BidiClass.R;
        }

        private static byte[] GetTextLevels(byte paragraphEmbeddingLevel, byte[] typesList, byte[] levelsList, int[] lineBreaks)
        {
            byte[] finalLevels = levelsList;

            // Rule L1
            // Level of S and B is changed to the paragraph embedding level.
            // Any sequence of whitespace and/or isolate formatting characters preceding S, B are changed to paragraph level
            for (int i = 0; i < finalLevels.Length; i++)
            {
                var t = (BidiClass)typesList[i];    // Types here are original ones not the output of previous stages

                if (t == BidiClass.S || t == BidiClass.B)
                {
                    finalLevels[i] = paragraphEmbeddingLevel;
                }

                // Search backward for whitespace or isolates (LRI, RLI, FSI, PDI)
                for (int j = i - 1; j >= 0; j--)
                {
                    t = (BidiClass)typesList[j];
                    if (t == BidiClass.LRI ||
                        t == BidiClass.RLI ||
                        t == BidiClass.FSI ||
                        t == BidiClass.FSI)
                    {
                        finalLevels[j] = paragraphEmbeddingLevel;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // Search backward for any sequence of whitespace or isolates at ach line breaks (ends)
            int start = 0;
            for (int i = 0; i < lineBreaks.Length; i++)
            {
                int end = lineBreaks[i];    // Line limit (new line start)
                for (int j = end - 1; j >= start; j--)
                {
                    var t = (BidiClass)typesList[j];
                    if (t == BidiClass.LRI ||
                        t == BidiClass.RLI ||
                        t == BidiClass.FSI ||
                        t == BidiClass.FSI)
                    {
                        finalLevels[j] = paragraphEmbeddingLevel;
                    }
                    else
                    {
                        break;
                    }
                }

                start = end; // Reset start to new line start
            }

            return finalLevels;
        }
        
        // Compute correct text indexes using levels array and line breaks positions.
        // Line breaks should be calculated and supplied by the rendering system after shaping and bounds calculations
        private static int[] GetMultiLineReordered(byte[] levels, int[] lineBreaks)
        {
            int[] resultIndexes = new int[levels.Length];

            // Calculate lines levels separately and append them at their final offsets in levels array
            int start = 0;
            for (int i = 0; i < lineBreaks.Length; i++)
            {
                int end = lineBreaks[i];

                var tempLevels = new byte[end - start];  // Line levels
                Array.Copy(levels, start, tempLevels, 0, tempLevels.Length); // Copy line levels to work on it

                var tempReorderedIndexes = ComputeReorderingIndexes(tempLevels); // Rule L2 (reversing)
                for (int j = 0; j < tempReorderedIndexes.Length; j++)
                {
                    resultIndexes[start + j] = tempReorderedIndexes[j] + start;
                }

                start = end; // Next line start
            }

            return resultIndexes;
        }

        // Rule L2
        private static int[] ComputeReorderingIndexes(byte[] levels)
        {
            int lineLength = levels.Length;

            // Initialize line indexes to logical order 0,1,2, etc..
            int[] resultIndexes = new int[lineLength];
            for (int i = 0; i < lineLength; i++)
            {
                resultIndexes[i] = i;
            }

            // Determine highest level on the text
            // scan for highest level and lowest odd level
            byte highestLevel    = 0;
            byte lowestOddLevel  = MAX_DEPTH + 2; // max value for odd levels
            foreach (var level in levels)
            {
                if (level > highestLevel) // highest level
                {
                    highestLevel = level;
                }
                
                // lowest odd level (start from max possible odd levels down to lowest level found)
                if (IsOdd(level) && level < lowestOddLevel)
                {
                    lowestOddLevel = level;
                }
            }

            for (int l = highestLevel; l >= lowestOddLevel; l--)    // Reverse from highest level down to lowest odd level
            {
                for (int i = 0; i < lineLength; i++)
                {
                    if (levels[i] >= l)
                    {
                        int start   = i;
                        int end     = i + 1;

                        while (end < lineLength && levels[end] >= l)    // Text range at this level or above
                        {
                            end++;
                        }

                        for (int j = start, k = end - 1; j < k; j++, k--) // Reverse
                        {
                            int tmp             = resultIndexes[j];
                            resultIndexes[j]    = resultIndexes[k];
                            resultIndexes[k]    = tmp;
                        }

                        i = end; // Skip to end
                    }
                }
            }

            return resultIndexes;
        }

        // Return final correctly reversed string order, applying rule L4 on the way out.
        private static string GetOrderedString(string input, int[] newIndexes, byte[] resolvedLevels)
        {
            var sb = new StringBuilder(input.Length);
            for (int i = 0; i < newIndexes.Length; i++)
            {
                int  logical = newIndexes[i];
                char c       = input[logical];

                // Rule L4: a character is depicted by a MIRRORED glyph if and only if its resolved
                // direction is R (odd embedding level) and its Bidi_Mirrored property is Yes. Without
                // this, "\u05D2\u05D1\u05D5\u05D4\u05D4 (\u05D6\u05D5\u05D4\u05E8)" renders with the
                // parentheses pointing the wrong way once L2 has reversed the run. The level is the
                // per-character one, not the paragraph's, so a Latin phrase embedded in Hebrew keeps
                // its own unmirrored brackets.
                if ((resolvedLevels[logical] & 1) != 0) c = MirrorGlyph(c);

                sb.Append(c);
            }

            return sb.ToString();
        }

        // Bidi_Mirroring_Glyph. Generated from Unicode 16 and validated: every entry has
        // Bidi_Mirrored=Yes, and the map is an involution (Mirror(Mirror(c)) == c). Covers all 167 BMP
        // pairs that have a mirror CHARACTER — which is every parenthesis, bracket, brace and angle
        // quotation mark in the BMP, plus the relational and set operators.
        //
        // The ~215 remaining Bidi_Mirrored code points are symbols such as U+221A SQUARE ROOT whose
        // mirror image is not itself an encoded character; Unicode expects those to be mirrored
        // geometrically by the font/shaper, which is out of this function's reach. They are returned
        // unchanged, i.e. exactly the pre-L4 behaviour, so an unlisted glyph can never regress.
        private static char MirrorGlyph(char c) => c switch
        {
            '\u0028' => '\u0029',   // LEFT PARENTHESIS
            '\u0029' => '\u0028',   // RIGHT PARENTHESIS
            '\u003C' => '\u003E',   // LESS-THAN SIGN
            '\u003E' => '\u003C',   // GREATER-THAN SIGN
            '\u005B' => '\u005D',   // LEFT SQUARE BRACKET
            '\u005D' => '\u005B',   // RIGHT SQUARE BRACKET
            '\u007B' => '\u007D',   // LEFT CURLY BRACKET
            '\u007D' => '\u007B',   // RIGHT CURLY BRACKET
            '\u00AB' => '\u00BB',   // LEFT-POINTING DOUBLE ANGLE QUOTATION MARK
            '\u00BB' => '\u00AB',   // RIGHT-POINTING DOUBLE ANGLE QUOTATION MARK
            '\u2039' => '\u203A',   // SINGLE LEFT-POINTING ANGLE QUOTATION MARK
            '\u203A' => '\u2039',   // SINGLE RIGHT-POINTING ANGLE QUOTATION MARK
            '\u2045' => '\u2046',   // LEFT SQUARE BRACKET WITH QUILL
            '\u2046' => '\u2045',   // RIGHT SQUARE BRACKET WITH QUILL
            '\u207D' => '\u207E',   // SUPERSCRIPT LEFT PARENTHESIS
            '\u207E' => '\u207D',   // SUPERSCRIPT RIGHT PARENTHESIS
            '\u208D' => '\u208E',   // SUBSCRIPT LEFT PARENTHESIS
            '\u208E' => '\u208D',   // SUBSCRIPT RIGHT PARENTHESIS
            '\u2208' => '\u220B',   // ELEMENT OF
            '\u220A' => '\u220D',   // SMALL ELEMENT OF
            '\u220B' => '\u2208',   // CONTAINS AS MEMBER
            '\u220D' => '\u220A',   // SMALL CONTAINS AS MEMBER
            '\u2264' => '\u2265',   // LESS-THAN OR EQUAL TO
            '\u2265' => '\u2264',   // GREATER-THAN OR EQUAL TO
            '\u2266' => '\u2267',   // LESS-THAN OVER EQUAL TO
            '\u2267' => '\u2266',   // GREATER-THAN OVER EQUAL TO
            '\u2268' => '\u2269',   // LESS-THAN BUT NOT EQUAL TO
            '\u2269' => '\u2268',   // GREATER-THAN BUT NOT EQUAL TO
            '\u226A' => '\u226B',   // MUCH LESS-THAN
            '\u226B' => '\u226A',   // MUCH GREATER-THAN
            '\u226E' => '\u226F',   // NOT LESS-THAN
            '\u226F' => '\u226E',   // NOT GREATER-THAN
            '\u2270' => '\u2271',   // NEITHER LESS-THAN NOR EQUAL TO
            '\u2271' => '\u2270',   // NEITHER GREATER-THAN NOR EQUAL TO
            '\u2272' => '\u2273',   // LESS-THAN OR EQUIVALENT TO
            '\u2273' => '\u2272',   // GREATER-THAN OR EQUIVALENT TO
            '\u2274' => '\u2275',   // NEITHER LESS-THAN NOR EQUIVALENT TO
            '\u2275' => '\u2274',   // NEITHER GREATER-THAN NOR EQUIVALENT TO
            '\u2276' => '\u2277',   // LESS-THAN OR GREATER-THAN
            '\u2277' => '\u2276',   // GREATER-THAN OR LESS-THAN
            '\u2278' => '\u2279',   // NEITHER LESS-THAN NOR GREATER-THAN
            '\u2279' => '\u2278',   // NEITHER GREATER-THAN NOR LESS-THAN
            '\u227A' => '\u227B',   // PRECEDES
            '\u227B' => '\u227A',   // SUCCEEDS
            '\u227C' => '\u227D',   // PRECEDES OR EQUAL TO
            '\u227D' => '\u227C',   // SUCCEEDS OR EQUAL TO
            '\u227E' => '\u227F',   // PRECEDES OR EQUIVALENT TO
            '\u227F' => '\u227E',   // SUCCEEDS OR EQUIVALENT TO
            '\u2282' => '\u2283',   // SUBSET OF
            '\u2283' => '\u2282',   // SUPERSET OF
            '\u2284' => '\u2285',   // NOT A SUBSET OF
            '\u2285' => '\u2284',   // NOT A SUPERSET OF
            '\u2286' => '\u2287',   // SUBSET OF OR EQUAL TO
            '\u2287' => '\u2286',   // SUPERSET OF OR EQUAL TO
            '\u2288' => '\u2289',   // NEITHER A SUBSET OF NOR EQUAL TO
            '\u2289' => '\u2288',   // NEITHER A SUPERSET OF NOR EQUAL TO
            '\u228A' => '\u228B',   // SUBSET OF WITH NOT EQUAL TO
            '\u228B' => '\u228A',   // SUPERSET OF WITH NOT EQUAL TO
            '\u22A2' => '\u22A3',   // RIGHT TACK
            '\u22A3' => '\u22A2',   // LEFT TACK
            '\u22AB' => '\u2AE5',   // DOUBLE VERTICAL BAR DOUBLE RIGHT TURNSTILE
            '\u22B0' => '\u22B1',   // PRECEDES UNDER RELATION
            '\u22B1' => '\u22B0',   // SUCCEEDS UNDER RELATION
            '\u22C9' => '\u22CA',   // LEFT NORMAL FACTOR SEMIDIRECT PRODUCT
            '\u22CA' => '\u22C9',   // RIGHT NORMAL FACTOR SEMIDIRECT PRODUCT
            '\u22CB' => '\u22CC',   // LEFT SEMIDIRECT PRODUCT
            '\u22CC' => '\u22CB',   // RIGHT SEMIDIRECT PRODUCT
            '\u22D0' => '\u22D1',   // DOUBLE SUBSET
            '\u22D1' => '\u22D0',   // DOUBLE SUPERSET
            '\u22D6' => '\u22D7',   // LESS-THAN WITH DOT
            '\u22D7' => '\u22D6',   // GREATER-THAN WITH DOT
            '\u22D8' => '\u22D9',   // VERY MUCH LESS-THAN
            '\u22D9' => '\u22D8',   // VERY MUCH GREATER-THAN
            '\u22DA' => '\u22DB',   // LESS-THAN EQUAL TO OR GREATER-THAN
            '\u22DB' => '\u22DA',   // GREATER-THAN EQUAL TO OR LESS-THAN
            '\u22DC' => '\u22DD',   // EQUAL TO OR LESS-THAN
            '\u22DD' => '\u22DC',   // EQUAL TO OR GREATER-THAN
            '\u22DE' => '\u22DF',   // EQUAL TO OR PRECEDES
            '\u22DF' => '\u22DE',   // EQUAL TO OR SUCCEEDS
            '\u22E6' => '\u22E7',   // LESS-THAN BUT NOT EQUIVALENT TO
            '\u22E7' => '\u22E6',   // GREATER-THAN BUT NOT EQUIVALENT TO
            '\u22E8' => '\u22E9',   // PRECEDES BUT NOT EQUIVALENT TO
            '\u22E9' => '\u22E8',   // SUCCEEDS BUT NOT EQUIVALENT TO
            '\u2308' => '\u2309',   // LEFT CEILING
            '\u2309' => '\u2308',   // RIGHT CEILING
            '\u230A' => '\u230B',   // LEFT FLOOR
            '\u230B' => '\u230A',   // RIGHT FLOOR
            '\u2329' => '\u232A',   // LEFT-POINTING ANGLE BRACKET
            '\u232A' => '\u2329',   // RIGHT-POINTING ANGLE BRACKET
            '\u2768' => '\u2769',   // MEDIUM LEFT PARENTHESIS ORNAMENT
            '\u2769' => '\u2768',   // MEDIUM RIGHT PARENTHESIS ORNAMENT
            '\u276A' => '\u276B',   // MEDIUM FLATTENED LEFT PARENTHESIS ORNAMENT
            '\u276B' => '\u276A',   // MEDIUM FLATTENED RIGHT PARENTHESIS ORNAMENT
            '\u276C' => '\u276D',   // MEDIUM LEFT-POINTING ANGLE BRACKET ORNAMENT
            '\u276D' => '\u276C',   // MEDIUM RIGHT-POINTING ANGLE BRACKET ORNAMENT
            '\u276E' => '\u276F',   // HEAVY LEFT-POINTING ANGLE QUOTATION MARK ORNAMENT
            '\u276F' => '\u276E',   // HEAVY RIGHT-POINTING ANGLE QUOTATION MARK ORNAMENT
            '\u2770' => '\u2771',   // HEAVY LEFT-POINTING ANGLE BRACKET ORNAMENT
            '\u2771' => '\u2770',   // HEAVY RIGHT-POINTING ANGLE BRACKET ORNAMENT
            '\u2772' => '\u2773',   // LIGHT LEFT TORTOISE SHELL BRACKET ORNAMENT
            '\u2773' => '\u2772',   // LIGHT RIGHT TORTOISE SHELL BRACKET ORNAMENT
            '\u2774' => '\u2775',   // MEDIUM LEFT CURLY BRACKET ORNAMENT
            '\u2775' => '\u2774',   // MEDIUM RIGHT CURLY BRACKET ORNAMENT
            '\u27C3' => '\u27C4',   // OPEN SUBSET
            '\u27C4' => '\u27C3',   // OPEN SUPERSET
            '\u27C5' => '\u27C6',   // LEFT S-SHAPED BAG DELIMITER
            '\u27C6' => '\u27C5',   // RIGHT S-SHAPED BAG DELIMITER
            '\u27D5' => '\u27D6',   // LEFT OUTER JOIN
            '\u27D6' => '\u27D5',   // RIGHT OUTER JOIN
            '\u27DD' => '\u27DE',   // LONG RIGHT TACK
            '\u27DE' => '\u27DD',   // LONG LEFT TACK
            '\u27E2' => '\u27E3',   // WHITE CONCAVE-SIDED DIAMOND WITH LEFTWARDS TICK
            '\u27E3' => '\u27E2',   // WHITE CONCAVE-SIDED DIAMOND WITH RIGHTWARDS TICK
            '\u27E4' => '\u27E5',   // WHITE SQUARE WITH LEFTWARDS TICK
            '\u27E5' => '\u27E4',   // WHITE SQUARE WITH RIGHTWARDS TICK
            '\u27E6' => '\u27E7',   // MATHEMATICAL LEFT WHITE SQUARE BRACKET
            '\u27E7' => '\u27E6',   // MATHEMATICAL RIGHT WHITE SQUARE BRACKET
            '\u27E8' => '\u27E9',   // MATHEMATICAL LEFT ANGLE BRACKET
            '\u27E9' => '\u27E8',   // MATHEMATICAL RIGHT ANGLE BRACKET
            '\u27EA' => '\u27EB',   // MATHEMATICAL LEFT DOUBLE ANGLE BRACKET
            '\u27EB' => '\u27EA',   // MATHEMATICAL RIGHT DOUBLE ANGLE BRACKET
            '\u27EC' => '\u27ED',   // MATHEMATICAL LEFT WHITE TORTOISE SHELL BRACKET
            '\u27ED' => '\u27EC',   // MATHEMATICAL RIGHT WHITE TORTOISE SHELL BRACKET
            '\u27EE' => '\u27EF',   // MATHEMATICAL LEFT FLATTENED PARENTHESIS
            '\u27EF' => '\u27EE',   // MATHEMATICAL RIGHT FLATTENED PARENTHESIS
            '\u2983' => '\u2984',   // LEFT WHITE CURLY BRACKET
            '\u2984' => '\u2983',   // RIGHT WHITE CURLY BRACKET
            '\u2985' => '\u2986',   // LEFT WHITE PARENTHESIS
            '\u2986' => '\u2985',   // RIGHT WHITE PARENTHESIS
            '\u2987' => '\u2988',   // Z NOTATION LEFT IMAGE BRACKET
            '\u2988' => '\u2987',   // Z NOTATION RIGHT IMAGE BRACKET
            '\u2989' => '\u298A',   // Z NOTATION LEFT BINDING BRACKET
            '\u298A' => '\u2989',   // Z NOTATION RIGHT BINDING BRACKET
            '\u298B' => '\u298C',   // LEFT SQUARE BRACKET WITH UNDERBAR
            '\u298C' => '\u298B',   // RIGHT SQUARE BRACKET WITH UNDERBAR
            '\u298D' => '\u2990',   // LEFT SQUARE BRACKET WITH TICK IN TOP CORNER
            '\u298E' => '\u298F',   // RIGHT SQUARE BRACKET WITH TICK IN BOTTOM CORNER
            '\u298F' => '\u298E',   // LEFT SQUARE BRACKET WITH TICK IN BOTTOM CORNER
            '\u2990' => '\u298D',   // RIGHT SQUARE BRACKET WITH TICK IN TOP CORNER
            '\u2991' => '\u2992',   // LEFT ANGLE BRACKET WITH DOT
            '\u2992' => '\u2991',   // RIGHT ANGLE BRACKET WITH DOT
            '\u2993' => '\u2994',   // LEFT ARC LESS-THAN BRACKET
            '\u2994' => '\u2993',   // RIGHT ARC GREATER-THAN BRACKET
            '\u2995' => '\u2996',   // DOUBLE LEFT ARC GREATER-THAN BRACKET
            '\u2996' => '\u2995',   // DOUBLE RIGHT ARC LESS-THAN BRACKET
            '\u2997' => '\u2998',   // LEFT BLACK TORTOISE SHELL BRACKET
            '\u2998' => '\u2997',   // RIGHT BLACK TORTOISE SHELL BRACKET
            '\u29A8' => '\u29A9',   // MEASURED ANGLE WITH OPEN ARM ENDING IN ARROW POINTING UP AND RIGHT
            '\u29A9' => '\u29A8',   // MEASURED ANGLE WITH OPEN ARM ENDING IN ARROW POINTING UP AND LEFT
            '\u29AA' => '\u29AB',   // MEASURED ANGLE WITH OPEN ARM ENDING IN ARROW POINTING DOWN AND RIGHT
            '\u29AB' => '\u29AA',   // MEASURED ANGLE WITH OPEN ARM ENDING IN ARROW POINTING DOWN AND LEFT
            '\u29AC' => '\u29AD',   // MEASURED ANGLE WITH OPEN ARM ENDING IN ARROW POINTING RIGHT AND UP
            '\u29AD' => '\u29AC',   // MEASURED ANGLE WITH OPEN ARM ENDING IN ARROW POINTING LEFT AND UP
            '\u29AE' => '\u29AF',   // MEASURED ANGLE WITH OPEN ARM ENDING IN ARROW POINTING RIGHT AND DOWN
            '\u29AF' => '\u29AE',   // MEASURED ANGLE WITH OPEN ARM ENDING IN ARROW POINTING LEFT AND DOWN
            '\u29C0' => '\u29C1',   // CIRCLED LESS-THAN
            '\u29C1' => '\u29C0',   // CIRCLED GREATER-THAN
            '\u29D1' => '\u29D2',   // BOWTIE WITH LEFT HALF BLACK
            '\u29D2' => '\u29D1',   // BOWTIE WITH RIGHT HALF BLACK
            '\u29D4' => '\u29D5',   // TIMES WITH LEFT HALF BLACK
            '\u29D5' => '\u29D4',   // TIMES WITH RIGHT HALF BLACK
            '\u29D8' => '\u29D9',   // LEFT WIGGLY FENCE
            '\u29D9' => '\u29D8',   // RIGHT WIGGLY FENCE
            '\u29DA' => '\u29DB',   // LEFT DOUBLE WIGGLY FENCE
            '\u29DB' => '\u29DA',   // RIGHT DOUBLE WIGGLY FENCE
            '\u29E8' => '\u29E9',   // DOWN-POINTING TRIANGLE WITH LEFT HALF BLACK
            '\u29E9' => '\u29E8',   // DOWN-POINTING TRIANGLE WITH RIGHT HALF BLACK
            '\u29FC' => '\u29FD',   // LEFT-POINTING CURVED ANGLE BRACKET
            '\u29FD' => '\u29FC',   // RIGHT-POINTING CURVED ANGLE BRACKET
            '\u2A2D' => '\u2A2E',   // PLUS SIGN IN LEFT HALF CIRCLE
            '\u2A2E' => '\u2A2D',   // PLUS SIGN IN RIGHT HALF CIRCLE
            '\u2A34' => '\u2A35',   // MULTIPLICATION SIGN IN LEFT HALF CIRCLE
            '\u2A35' => '\u2A34',   // MULTIPLICATION SIGN IN RIGHT HALF CIRCLE
            '\u2A79' => '\u2A7A',   // LESS-THAN WITH CIRCLE INSIDE
            '\u2A7A' => '\u2A79',   // GREATER-THAN WITH CIRCLE INSIDE
            '\u2A7B' => '\u2A7C',   // LESS-THAN WITH QUESTION MARK ABOVE
            '\u2A7C' => '\u2A7B',   // GREATER-THAN WITH QUESTION MARK ABOVE
            '\u2A7D' => '\u2A7E',   // LESS-THAN OR SLANTED EQUAL TO
            '\u2A7E' => '\u2A7D',   // GREATER-THAN OR SLANTED EQUAL TO
            '\u2A7F' => '\u2A80',   // LESS-THAN OR SLANTED EQUAL TO WITH DOT INSIDE
            '\u2A80' => '\u2A7F',   // GREATER-THAN OR SLANTED EQUAL TO WITH DOT INSIDE
            '\u2A81' => '\u2A82',   // LESS-THAN OR SLANTED EQUAL TO WITH DOT ABOVE
            '\u2A82' => '\u2A81',   // GREATER-THAN OR SLANTED EQUAL TO WITH DOT ABOVE
            '\u2A83' => '\u2A84',   // LESS-THAN OR SLANTED EQUAL TO WITH DOT ABOVE RIGHT
            '\u2A84' => '\u2A83',   // GREATER-THAN OR SLANTED EQUAL TO WITH DOT ABOVE LEFT
            '\u2A85' => '\u2A86',   // LESS-THAN OR APPROXIMATE
            '\u2A86' => '\u2A85',   // GREATER-THAN OR APPROXIMATE
            '\u2A87' => '\u2A88',   // LESS-THAN AND SINGLE-LINE NOT EQUAL TO
            '\u2A88' => '\u2A87',   // GREATER-THAN AND SINGLE-LINE NOT EQUAL TO
            '\u2A89' => '\u2A8A',   // LESS-THAN AND NOT APPROXIMATE
            '\u2A8A' => '\u2A89',   // GREATER-THAN AND NOT APPROXIMATE
            '\u2A8B' => '\u2A8C',   // LESS-THAN ABOVE DOUBLE-LINE EQUAL ABOVE GREATER-THAN
            '\u2A8C' => '\u2A8B',   // GREATER-THAN ABOVE DOUBLE-LINE EQUAL ABOVE LESS-THAN
            '\u2A8D' => '\u2A8E',   // LESS-THAN ABOVE SIMILAR OR EQUAL
            '\u2A8E' => '\u2A8D',   // GREATER-THAN ABOVE SIMILAR OR EQUAL
            '\u2A8F' => '\u2A90',   // LESS-THAN ABOVE SIMILAR ABOVE GREATER-THAN
            '\u2A90' => '\u2A8F',   // GREATER-THAN ABOVE SIMILAR ABOVE LESS-THAN
            '\u2A91' => '\u2A92',   // LESS-THAN ABOVE GREATER-THAN ABOVE DOUBLE-LINE EQUAL
            '\u2A92' => '\u2A91',   // GREATER-THAN ABOVE LESS-THAN ABOVE DOUBLE-LINE EQUAL
            '\u2A93' => '\u2A94',   // LESS-THAN ABOVE SLANTED EQUAL ABOVE GREATER-THAN ABOVE SLANTED EQUAL
            '\u2A94' => '\u2A93',   // GREATER-THAN ABOVE SLANTED EQUAL ABOVE LESS-THAN ABOVE SLANTED EQUAL
            '\u2A95' => '\u2A96',   // SLANTED EQUAL TO OR LESS-THAN
            '\u2A96' => '\u2A95',   // SLANTED EQUAL TO OR GREATER-THAN
            '\u2A97' => '\u2A98',   // SLANTED EQUAL TO OR LESS-THAN WITH DOT INSIDE
            '\u2A98' => '\u2A97',   // SLANTED EQUAL TO OR GREATER-THAN WITH DOT INSIDE
            '\u2A99' => '\u2A9A',   // DOUBLE-LINE EQUAL TO OR LESS-THAN
            '\u2A9A' => '\u2A99',   // DOUBLE-LINE EQUAL TO OR GREATER-THAN
            '\u2A9B' => '\u2A9C',   // DOUBLE-LINE SLANTED EQUAL TO OR LESS-THAN
            '\u2A9C' => '\u2A9B',   // DOUBLE-LINE SLANTED EQUAL TO OR GREATER-THAN
            '\u2A9D' => '\u2A9E',   // SIMILAR OR LESS-THAN
            '\u2A9E' => '\u2A9D',   // SIMILAR OR GREATER-THAN
            '\u2A9F' => '\u2AA0',   // SIMILAR ABOVE LESS-THAN ABOVE EQUALS SIGN
            '\u2AA0' => '\u2A9F',   // SIMILAR ABOVE GREATER-THAN ABOVE EQUALS SIGN
            '\u2AA1' => '\u2AA2',   // DOUBLE NESTED LESS-THAN
            '\u2AA2' => '\u2AA1',   // DOUBLE NESTED GREATER-THAN
            '\u2AA6' => '\u2AA7',   // LESS-THAN CLOSED BY CURVE
            '\u2AA7' => '\u2AA6',   // GREATER-THAN CLOSED BY CURVE
            '\u2AA8' => '\u2AA9',   // LESS-THAN CLOSED BY CURVE ABOVE SLANTED EQUAL
            '\u2AA9' => '\u2AA8',   // GREATER-THAN CLOSED BY CURVE ABOVE SLANTED EQUAL
            '\u2AAA' => '\u2AAB',   // SMALLER THAN
            '\u2AAB' => '\u2AAA',   // LARGER THAN
            '\u2AAC' => '\u2AAD',   // SMALLER THAN OR EQUAL TO
            '\u2AAD' => '\u2AAC',   // LARGER THAN OR EQUAL TO
            '\u2AAF' => '\u2AB0',   // PRECEDES ABOVE SINGLE-LINE EQUALS SIGN
            '\u2AB0' => '\u2AAF',   // SUCCEEDS ABOVE SINGLE-LINE EQUALS SIGN
            '\u2AB1' => '\u2AB2',   // PRECEDES ABOVE SINGLE-LINE NOT EQUAL TO
            '\u2AB2' => '\u2AB1',   // SUCCEEDS ABOVE SINGLE-LINE NOT EQUAL TO
            '\u2AB3' => '\u2AB4',   // PRECEDES ABOVE EQUALS SIGN
            '\u2AB4' => '\u2AB3',   // SUCCEEDS ABOVE EQUALS SIGN
            '\u2AB5' => '\u2AB6',   // PRECEDES ABOVE NOT EQUAL TO
            '\u2AB6' => '\u2AB5',   // SUCCEEDS ABOVE NOT EQUAL TO
            '\u2AB7' => '\u2AB8',   // PRECEDES ABOVE ALMOST EQUAL TO
            '\u2AB8' => '\u2AB7',   // SUCCEEDS ABOVE ALMOST EQUAL TO
            '\u2AB9' => '\u2ABA',   // PRECEDES ABOVE NOT ALMOST EQUAL TO
            '\u2ABA' => '\u2AB9',   // SUCCEEDS ABOVE NOT ALMOST EQUAL TO
            '\u2ABB' => '\u2ABC',   // DOUBLE PRECEDES
            '\u2ABC' => '\u2ABB',   // DOUBLE SUCCEEDS
            '\u2ABD' => '\u2ABE',   // SUBSET WITH DOT
            '\u2ABE' => '\u2ABD',   // SUPERSET WITH DOT
            '\u2ABF' => '\u2AC0',   // SUBSET WITH PLUS SIGN BELOW
            '\u2AC0' => '\u2ABF',   // SUPERSET WITH PLUS SIGN BELOW
            '\u2AC1' => '\u2AC2',   // SUBSET WITH MULTIPLICATION SIGN BELOW
            '\u2AC2' => '\u2AC1',   // SUPERSET WITH MULTIPLICATION SIGN BELOW
            '\u2AC3' => '\u2AC4',   // SUBSET OF OR EQUAL TO WITH DOT ABOVE
            '\u2AC4' => '\u2AC3',   // SUPERSET OF OR EQUAL TO WITH DOT ABOVE
            '\u2AC5' => '\u2AC6',   // SUBSET OF ABOVE EQUALS SIGN
            '\u2AC6' => '\u2AC5',   // SUPERSET OF ABOVE EQUALS SIGN
            '\u2AC7' => '\u2AC8',   // SUBSET OF ABOVE TILDE OPERATOR
            '\u2AC8' => '\u2AC7',   // SUPERSET OF ABOVE TILDE OPERATOR
            '\u2AC9' => '\u2ACA',   // SUBSET OF ABOVE ALMOST EQUAL TO
            '\u2ACA' => '\u2AC9',   // SUPERSET OF ABOVE ALMOST EQUAL TO
            '\u2ACB' => '\u2ACC',   // SUBSET OF ABOVE NOT EQUAL TO
            '\u2ACC' => '\u2ACB',   // SUPERSET OF ABOVE NOT EQUAL TO
            '\u2ACD' => '\u2ACE',   // SQUARE LEFT OPEN BOX OPERATOR
            '\u2ACE' => '\u2ACD',   // SQUARE RIGHT OPEN BOX OPERATOR
            '\u2ACF' => '\u2AD0',   // CLOSED SUBSET
            '\u2AD0' => '\u2ACF',   // CLOSED SUPERSET
            '\u2AD1' => '\u2AD2',   // CLOSED SUBSET OR EQUAL TO
            '\u2AD2' => '\u2AD1',   // CLOSED SUPERSET OR EQUAL TO
            '\u2AD3' => '\u2AD4',   // SUBSET ABOVE SUPERSET
            '\u2AD4' => '\u2AD3',   // SUPERSET ABOVE SUBSET
            '\u2AD5' => '\u2AD6',   // SUBSET ABOVE SUBSET
            '\u2AD6' => '\u2AD5',   // SUPERSET ABOVE SUPERSET
            '\u2AE5' => '\u22AB',   // DOUBLE VERTICAL BAR DOUBLE LEFT TURNSTILE
            '\u2AF7' => '\u2AF8',   // TRIPLE NESTED LESS-THAN
            '\u2AF8' => '\u2AF7',   // TRIPLE NESTED GREATER-THAN
            '\u2AF9' => '\u2AFA',   // DOUBLE-LINE SLANTED LESS-THAN OR EQUAL TO
            '\u2AFA' => '\u2AF9',   // DOUBLE-LINE SLANTED GREATER-THAN OR EQUAL TO
            '\u2E02' => '\u2E03',   // LEFT SUBSTITUTION BRACKET
            '\u2E03' => '\u2E02',   // RIGHT SUBSTITUTION BRACKET
            '\u2E04' => '\u2E05',   // LEFT DOTTED SUBSTITUTION BRACKET
            '\u2E05' => '\u2E04',   // RIGHT DOTTED SUBSTITUTION BRACKET
            '\u2E09' => '\u2E0A',   // LEFT TRANSPOSITION BRACKET
            '\u2E0A' => '\u2E09',   // RIGHT TRANSPOSITION BRACKET
            '\u2E0C' => '\u2E0D',   // LEFT RAISED OMISSION BRACKET
            '\u2E0D' => '\u2E0C',   // RIGHT RAISED OMISSION BRACKET
            '\u2E1C' => '\u2E1D',   // LEFT LOW PARAPHRASE BRACKET
            '\u2E1D' => '\u2E1C',   // RIGHT LOW PARAPHRASE BRACKET
            '\u2E20' => '\u2E21',   // LEFT VERTICAL BAR WITH QUILL
            '\u2E21' => '\u2E20',   // RIGHT VERTICAL BAR WITH QUILL
            '\u2E22' => '\u2E23',   // TOP LEFT HALF BRACKET
            '\u2E23' => '\u2E22',   // TOP RIGHT HALF BRACKET
            '\u2E24' => '\u2E25',   // BOTTOM LEFT HALF BRACKET
            '\u2E25' => '\u2E24',   // BOTTOM RIGHT HALF BRACKET
            '\u2E26' => '\u2E27',   // LEFT SIDEWAYS U BRACKET
            '\u2E27' => '\u2E26',   // RIGHT SIDEWAYS U BRACKET
            '\u2E28' => '\u2E29',   // LEFT DOUBLE PARENTHESIS
            '\u2E29' => '\u2E28',   // RIGHT DOUBLE PARENTHESIS
            '\u2E55' => '\u2E56',   // LEFT SQUARE BRACKET WITH STROKE
            '\u2E56' => '\u2E55',   // RIGHT SQUARE BRACKET WITH STROKE
            '\u2E57' => '\u2E58',   // LEFT SQUARE BRACKET WITH DOUBLE STROKE
            '\u2E58' => '\u2E57',   // RIGHT SQUARE BRACKET WITH DOUBLE STROKE
            '\u2E59' => '\u2E5A',   // TOP HALF LEFT PARENTHESIS
            '\u2E5A' => '\u2E59',   // TOP HALF RIGHT PARENTHESIS
            '\u2E5B' => '\u2E5C',   // BOTTOM HALF LEFT PARENTHESIS
            '\u2E5C' => '\u2E5B',   // BOTTOM HALF RIGHT PARENTHESIS
            '\u3008' => '\u3009',   // LEFT ANGLE BRACKET
            '\u3009' => '\u3008',   // RIGHT ANGLE BRACKET
            '\u300A' => '\u300B',   // LEFT DOUBLE ANGLE BRACKET
            '\u300B' => '\u300A',   // RIGHT DOUBLE ANGLE BRACKET
            '\u300C' => '\u300D',   // LEFT CORNER BRACKET
            '\u300D' => '\u300C',   // RIGHT CORNER BRACKET
            '\u300E' => '\u300F',   // LEFT WHITE CORNER BRACKET
            '\u300F' => '\u300E',   // RIGHT WHITE CORNER BRACKET
            '\u3010' => '\u3011',   // LEFT BLACK LENTICULAR BRACKET
            '\u3011' => '\u3010',   // RIGHT BLACK LENTICULAR BRACKET
            '\u3014' => '\u3015',   // LEFT TORTOISE SHELL BRACKET
            '\u3015' => '\u3014',   // RIGHT TORTOISE SHELL BRACKET
            '\u3016' => '\u3017',   // LEFT WHITE LENTICULAR BRACKET
            '\u3017' => '\u3016',   // RIGHT WHITE LENTICULAR BRACKET
            '\u3018' => '\u3019',   // LEFT WHITE TORTOISE SHELL BRACKET
            '\u3019' => '\u3018',   // RIGHT WHITE TORTOISE SHELL BRACKET
            '\u301A' => '\u301B',   // LEFT WHITE SQUARE BRACKET
            '\u301B' => '\u301A',   // RIGHT WHITE SQUARE BRACKET
            '\uFE59' => '\uFE5A',   // SMALL LEFT PARENTHESIS
            '\uFE5A' => '\uFE59',   // SMALL RIGHT PARENTHESIS
            '\uFE5B' => '\uFE5C',   // SMALL LEFT CURLY BRACKET
            '\uFE5C' => '\uFE5B',   // SMALL RIGHT CURLY BRACKET
            '\uFE5D' => '\uFE5E',   // SMALL LEFT TORTOISE SHELL BRACKET
            '\uFE5E' => '\uFE5D',   // SMALL RIGHT TORTOISE SHELL BRACKET
            '\uFE64' => '\uFE65',   // SMALL LESS-THAN SIGN
            '\uFE65' => '\uFE64',   // SMALL GREATER-THAN SIGN
            '\uFF08' => '\uFF09',   // FULLWIDTH LEFT PARENTHESIS
            '\uFF09' => '\uFF08',   // FULLWIDTH RIGHT PARENTHESIS
            '\uFF1C' => '\uFF1E',   // FULLWIDTH LESS-THAN SIGN
            '\uFF1E' => '\uFF1C',   // FULLWIDTH GREATER-THAN SIGN
            '\uFF3B' => '\uFF3D',   // FULLWIDTH LEFT SQUARE BRACKET
            '\uFF3D' => '\uFF3B',   // FULLWIDTH RIGHT SQUARE BRACKET
            '\uFF5B' => '\uFF5D',   // FULLWIDTH LEFT CURLY BRACKET
            '\uFF5D' => '\uFF5B',   // FULLWIDTH RIGHT CURLY BRACKET
            '\uFF5F' => '\uFF60',   // FULLWIDTH LEFT WHITE PARENTHESIS
            '\uFF60' => '\uFF5F',   // FULLWIDTH RIGHT WHITE PARENTHESIS
            '\uFF62' => '\uFF63',   // HALFWIDTH LEFT CORNER BRACKET
            '\uFF63' => '\uFF62',   // HALFWIDTH RIGHT CORNER BRACKET
            _ => c,
        };
    }
}
