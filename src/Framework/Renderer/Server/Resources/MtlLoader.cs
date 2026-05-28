// MtlLoader.cs — Wavefront MTL parser.
// Parses Kd, Ks, Ns, d (opacity), map_Kd, map_Ks, map_Bump, map_d.
// Returns a dictionary of material name → MtlMaterial records.
// All parsing uses ReadOnlySpan<char> to avoid per-line allocations.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace GameEditor.Framework.Renderer.Server.Resources
{
    /// <summary>Parsed MTL material record (Blinn-Phong channels).</summary>
    public sealed class MtlMaterial
    {
        public string Name      = "";

        // ── colour channels ──────────────────────────────────────────────────────────
        public Vector3 Kd       = Vector3.One;          // diffuse colour
        public Vector3 Ks       = new(0.5f);            // specular colour
        public float   Ns       = 32f;                  // specular exponent
        public float   D        = 1f;                   // opacity / dissolve (1 = opaque)

        // ── texture maps (file names relative to the .mtl directory) ────────────────
        public string  MapKd    = "";                   // diffuse albedo map
        public string  MapKs    = "";                   // specular map
        public string  MapBump  = "";                   // normal / bump map
        public string  MapD     = "";                   // opacity/alpha map
    }

    public static class MtlLoader
    {
        /// <summary>Parse MTL bytes (UTF-8). Returns empty dict on null/empty input.</summary>
        public static Dictionary<string, MtlMaterial> Load(byte[]? data)
        {
            if (data == null || data.Length == 0) return new();
            string text = System.Text.Encoding.UTF8.GetString(data);
            return ParseMtlText(text.AsSpan());
        }

        public static Dictionary<string, MtlMaterial> Load(ReadOnlySpan<char> text)
            => ParseMtlText(text);

        // ── private ──────────────────────────────────────────────────────────────────

        private static Dictionary<string, MtlMaterial> ParseMtlText(ReadOnlySpan<char> text)
        {
            var result  = new Dictionary<string, MtlMaterial>(StringComparer.Ordinal);
            MtlMaterial? current = null;

            var remaining = text;
            while (!remaining.IsEmpty)
            {
                int nl = remaining.IndexOfAny('\n', '\r');
                ReadOnlySpan<char> line;
                if (nl < 0)
                {
                    line      = remaining.Trim();
                    remaining = default;
                }
                else
                {
                    line      = remaining[..nl].Trim();
                    remaining = remaining[(nl + 1)..];
                    if (!remaining.IsEmpty && remaining[0] == '\n') remaining = remaining[1..];
                }

                if (line.IsEmpty || line[0] == '#') continue;

                if (line.StartsWith("newmtl ".AsSpan(), StringComparison.Ordinal))
                {
                    if (current != null) result[current.Name] = current;
                    current      = new MtlMaterial { Name = line[7..].Trim().ToString() };
                }
                else if (current == null) continue;
                else if (line.StartsWith("Kd ".AsSpan(), StringComparison.Ordinal))
                {
                    current.Kd = ParseVec3(line[3..]);
                }
                else if (line.StartsWith("Ks ".AsSpan(), StringComparison.Ordinal))
                {
                    current.Ks = ParseVec3(line[3..]);
                }
                else if (line.StartsWith("Ns ".AsSpan(), StringComparison.Ordinal))
                {
                    current.Ns = ParseFloat(SkipWhitespace(line[3..]));
                }
                else if (line.StartsWith("d ".AsSpan(), StringComparison.Ordinal))
                {
                    current.D = ParseFloat(SkipWhitespace(line[2..]));
                }
                else if (line.StartsWith("Tr ".AsSpan(), StringComparison.Ordinal))
                {
                    // Tr is transparency (1 - d).
                    current.D = 1f - ParseFloat(SkipWhitespace(line[3..]));
                }
                else if (line.StartsWith("map_Kd ".AsSpan(), StringComparison.Ordinal))
                {
                    current.MapKd = line[7..].Trim().ToString();
                }
                else if (line.StartsWith("map_Ks ".AsSpan(), StringComparison.Ordinal))
                {
                    current.MapKs = line[7..].Trim().ToString();
                }
                else if (line.StartsWith("map_Bump ".AsSpan(), StringComparison.Ordinal) ||
                         line.StartsWith("bump ".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    // 'map_Bump' or 'bump': skip options like '-bm 1.0' to get the filename.
                    current.MapBump = ExtractMapFilename(line[line.IndexOf(' ')..]);
                }
                else if (line.StartsWith("map_d ".AsSpan(), StringComparison.Ordinal))
                {
                    current.MapD = line[6..].Trim().ToString();
                }
            }

            if (current != null) result[current.Name] = current;
            return result;
        }

        // ── number / token helpers ───────────────────────────────────────────────────

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<char> SkipWhitespace(ReadOnlySpan<char> s)
            => s.TrimStart();

        private static Vector3 ParseVec3(ReadOnlySpan<char> s)
        {
            float x = 0, y = 0, z = 0;
            int i = 0;
            foreach (Range range in s.Split(' '))
            {
                var tok = s[range];
                if (tok.IsEmpty) continue;
                float v = ParseFloat(tok);
                switch (i++) { case 0: x = v; break; case 1: y = v; break; case 2: z = v; break; }
            }
            return new Vector3(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ParseFloat(ReadOnlySpan<char> s)
        {
            float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out float v);
            return v;
        }

        /// <summary>Skip optional flag tokens ('-flag value') and return the trailing filename.</summary>
        private static string ExtractMapFilename(ReadOnlySpan<char> s)
        {
            s = s.TrimStart();
            string last = "";
            bool skipNext = false;
            foreach (Range range in s.Split(' '))
            {
                var t = s[range].TrimStart();
                if (t.IsEmpty) continue;
                if (skipNext)  { skipNext = false; continue; }
                if (t[0] == '-') { skipNext = true; continue; }
                last = t.ToString();
            }
            return last;
        }
    }
}
