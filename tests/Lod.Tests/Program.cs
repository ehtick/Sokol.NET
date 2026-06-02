// LodSelector unit tests — RENDERING_SERVER_M3_REVIEW.md §3.1 (no oscillation) and
// §3.10 / T8 (a receding object must reach Skip, not get pinned at a level).
//
// Pure-logic test: no Sokol, no native libs, no NuGet. Run with:
//     dotnet run --project tests/Lod.Tests
// Exit code 0 = all pass, 1 = at least one failure.

using System;
using GameEditor.Framework.Renderer.Server.Lod;

namespace Lod.Tests
{
    internal static class Program
    {
        private static int _failures;

        private static void Check(string name, bool ok, string detail = "")
        {
            Console.WriteLine(ok ? $"  PASS  {name}"
                                 : $"  FAIL  {name}{(detail.Length > 0 ? "   " + detail : "")}");
            if (!ok) _failures++;
        }

        // A non-degenerate 3-level ladder: every margin is < the gap to the next coarser
        // threshold (gaps: 0.08, 0.015, 0.005), so LodGroup's constructor assert is happy.
        private static LodGroup MakeLadder() => new LodGroup(
            new LodLevel(0.10f,  0.01f),   // LOD 0
            new LodLevel(0.02f,  0.004f),  // LOD 1
            new LodLevel(0.005f, 0.001f)); // LOD 2 ; below 0.005 -> Skip (cull)

        // coverage = worldRadius / distToCamera; fix distToCamera = 1 so coverage == worldRadius.
        private static int PickAt(float coverage, int prevLevel, LodGroup g)
            => LodSelector.Pick(coverage, 1.0f, prevLevel, g);

        // Feed the selected level back in as "previous" for many frames at a constant
        // coverage, then assert the final state is a fixed point. A period-2 oscillation
        // (A,B,A,B,...) never settles, so Pick(cov, finalPrev) != finalPrev and this fails.
        private static bool ReachesFixedPoint(float coverage, int startPrev, LodGroup g)
        {
            int prev = startPrev;
            for (int frame = 0; frame < 32; frame++)
                prev = PickAt(coverage, prev, g);
            return PickAt(coverage, prev, g) == prev;
        }

        private static int Main()
        {
            var g = MakeLadder();

            Console.WriteLine("LodSelector tests");

            // ── No LodGroup -> always LOD 0, never culled ───────────────────────────
            Check("null group -> LOD 0 (no distance cull)",
                LodSelector.Pick(0.001f, 1000f, 0, null) == 0);

            // ── Nominal selection from a cold start (prev = 0) ──────────────────────
            Check("cov 0.5   -> LOD 0", PickAt(0.5f,   0, g) == 0);
            Check("cov 0.05  -> LOD 1", PickAt(0.05f,  0, g) == 1);
            Check("cov 0.01  -> LOD 2", PickAt(0.01f,  0, g) == 2);
            Check("cov 0.001 -> Skip ", PickAt(0.001f, 0, g) == LodSelector.Skip);

            // ── §3.10 / T8: receding object must reach Skip, never pinned ───────────
            Check("§3.10 prev=1, cov 0.001 -> Skip (not pinned at LOD 1)",
                PickAt(0.001f, 1, g) == LodSelector.Skip,
                $"got {PickAt(0.001f, 1, g)}");
            Check("§3.10 prev=2, cov 0.001 -> Skip (stay-bar did not collapse)",
                PickAt(0.001f, 2, g) == LodSelector.Skip,
                $"got {PickAt(0.001f, 2, g)}");

            // These two land in a coarser level's [thr-margin, thr) band — the ONLY place
            // the old "relax every coarser level by -margin" logic diverges from the fixed
            // "coarser levels use nominal thr" logic. They FAIL under the pre-§3.10 code
            // (which would return LOD 1 and LOD 2 respectively) and PASS after the fix.
            Check("§3.10 prev=0, cov 0.018 -> LOD 2 (coarser LOD 1 not over-relaxed to fit)",
                PickAt(0.018f, 0, g) == 2,
                $"got {PickAt(0.018f, 0, g)} (pre-fix returns 1)");
            Check("§3.10 prev=0, cov 0.0045 -> Skip (coarser LOD 2 not over-relaxed to fit)",
                PickAt(0.0045f, 0, g) == LodSelector.Skip,
                $"got {PickAt(0.0045f, 0, g)} (pre-fix returns 2)");

            // ── §3.1: no frame-to-frame oscillation at constant coverage ────────────
            // Sweep coverages straddling both boundaries; from BOTH a fine and a coarse
            // starting level. Each trajectory must settle to a fixed point.
            float[] covs = { 0.105f, 0.101f, 0.10f, 0.099f, 0.05f, 0.021f, 0.02f, 0.019f, 0.006f };
            foreach (float cov in covs)
            {
                Check($"§3.1 settles (no oscillation) at cov {cov}, start LOD 0",
                    ReachesFixedPoint(cov, 0, g));
                Check($"§3.1 settles (no oscillation) at cov {cov}, start LOD 2",
                    ReachesFixedPoint(cov, 2, g));
            }

            // ── Hysteresis directionality (dead zone around the LOD0/LOD1 boundary) ──
            Check("hysteresis: prev=1, cov 0.101 stays LOD 1 (inside dead zone)",
                PickAt(0.101f, 1, g) == 1);
            Check("hysteresis: prev=1, cov 0.111 upgrades to LOD 0 (> thr+margin)",
                PickAt(0.111f, 1, g) == 0);
            Check("hysteresis: prev=0, cov 0.099 stays LOD 0 (inside dead zone)",
                PickAt(0.099f, 0, g) == 0);
            Check("hysteresis: prev=0, cov 0.089 drops to LOD 1 (< thr-margin)",
                PickAt(0.089f, 0, g) == 1);

            Console.WriteLine();
            if (_failures == 0)
            {
                Console.WriteLine("ALL LodSelector TESTS PASSED");
                return 0;
            }
            Console.WriteLine($"{_failures} LodSelector TEST(S) FAILED");
            return 1;
        }
    }
}
