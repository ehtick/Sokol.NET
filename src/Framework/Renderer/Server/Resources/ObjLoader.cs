// ObjLoader.cs — Extended Wavefront OBJ parser for RenderingServer.
// Parses position + normal + UV, fan-triangulates polygons, computes tangents
// (MikkTSpace-style approximation), and builds an AABB during load.
// All hot-path parsing avoids string.Split to prevent heap allocation per line.
// See §4.4 (GC rules) and §3 (reference: examples/Checkers/Source/ObjLoader.cs).

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using GameEditor.Framework.Core;

namespace GameEditor.Framework.Renderer.Server.Resources
{
    /// <summary>Interleaved vertex: position + normal + UV + tangent (32 B).</summary>
    public struct ObjVertex
    {
        public Vector3 Position;  // 12 B
        public Vector3 Normal;    // 12 B
        public Vector2 Uv;        //  8 B
        public Vector4 Tangent;   // 16 B — xyz = tangent direction; w = bitangent sign (+1/-1)

        public const int SizeBytes = 12 + 12 + 8 + 16; // 48 B
    }

    /// <summary>Sub-mesh for one material section of an OBJ file.</summary>
    public sealed class ObjSubMesh
    {
        public string MaterialName = "";
        public ObjVertex[] Vertices = Array.Empty<ObjVertex>();
        public uint[]      Indices  = Array.Empty<uint>(); // uint32 for large meshes
    }

    /// <summary>Loaded result of ObjLoader.Load().</summary>
    public sealed class ObjMesh
    {
        public ObjSubMesh[] SubMeshes = Array.Empty<ObjSubMesh>();

        /// <summary>Axis-aligned bounding box encompassing all sub-meshes.</summary>
        public Aabb Bounds;

        /// <summary>Path this mesh was loaded from (for registry keying).</summary>
        public string SourcePath = "";

        /// <summary>MTL library filename referenced by this OBJ (may be empty).</summary>
        public string MtlLib = "";
    }

    /// <summary>
    /// Streaming Wavefront OBJ parser.
    /// Uses MemoryExtensions.Split over ReadOnlySpan&lt;char&gt; to avoid allocating
    /// a string array per line on the hot path.
    /// </summary>
    public static class ObjLoader
    {
        /// <summary>Parse an OBJ file from UTF-8 bytes.</summary>
        public static ObjMesh Load(byte[] data, string sourcePath = "")
        {
            string text = System.Text.Encoding.UTF8.GetString(data);
            return ParseObjText(text.AsSpan(), sourcePath);
        }

        /// <summary>Parse an OBJ file directly from a span (zero-copy if already in memory).</summary>
        public static ObjMesh Load(ReadOnlySpan<char> text, string sourcePath = "")
            => ParseObjText(text, sourcePath);

        // ─── private ─────────────────────────────────────────────────────────────────

        private static ObjMesh ParseObjText(ReadOnlySpan<char> text, string sourcePath)
        {
            var positions = new List<Vector3>(512);
            var normals   = new List<Vector3>(512);
            var uvs       = new List<Vector2>(512);

            // Groups keyed by material name.
            var groups  = new List<(string mat, List<int[]> faces)>();
            string      currentMat   = "default";
            List<int[]>? currentFaces = null;
            string mtlLib = "";

            EnsureGroup(ref currentMat, ref currentFaces, groups, currentMat);

            // Line-by-line parse over ReadOnlySpan<char> — no allocations per line.
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

                if (line.StartsWith("vt ".AsSpan(), StringComparison.Ordinal))
                {
                    uvs.Add(ParseVec2(line[3..]));
                }
                else if (line.StartsWith("vn ".AsSpan(), StringComparison.Ordinal))
                {
                    normals.Add(ParseVec3(line[3..]));
                }
                else if (line.StartsWith("v ".AsSpan(), StringComparison.Ordinal))
                {
                    positions.Add(ParseVec3(line[2..]));
                }
                else if (line.StartsWith("usemtl ".AsSpan(), StringComparison.Ordinal))
                {
                    string matName = line[7..].Trim().ToString();
                    EnsureGroup(ref currentMat, ref currentFaces, groups, matName);
                }
                else if (line.StartsWith("mtllib ".AsSpan(), StringComparison.Ordinal))
                {
                    mtlLib = line[7..].Trim().ToString();
                }
                else if (line.StartsWith("f ".AsSpan(), StringComparison.Ordinal))
                {
                    if (currentFaces == null) EnsureGroup(ref currentMat, ref currentFaces, groups, currentMat);
                    int[]? face = ParseFace(line[2..]);
                    if (face != null) currentFaces!.Add(face);
                }
            }

            // Build sub-meshes and overall AABB.
            var aabbMin = new Vector3(float.MaxValue);
            var aabbMax = new Vector3(float.MinValue);

            var subMeshes = new List<ObjSubMesh>(groups.Count);
            foreach (var (mat, faces) in groups)
            {
                if (faces.Count == 0) continue;
                var sm = BuildSubMesh(mat, faces, positions, normals, uvs);
                ExpandAabb(ref aabbMin, ref aabbMax, sm);
                subMeshes.Add(sm);
            }

            // Guard against empty file.
            if (aabbMin.X > aabbMax.X) { aabbMin = Vector3.Zero; aabbMax = Vector3.Zero; }

            return new ObjMesh
            {
                SubMeshes  = subMeshes.ToArray(),
                Bounds     = new Aabb(aabbMin, aabbMax),
                SourcePath = sourcePath,
                MtlLib     = mtlLib
            };
        }

        // ── group management ────────────────────────────────────────────────────────

        private static void EnsureGroup(
            ref string currentMat, ref List<int[]>? currentFaces,
            List<(string, List<int[]>)> groups, string matName)
        {
            if (currentFaces != null && currentMat == matName) return;
            currentMat   = matName;
            currentFaces = new List<int[]>(64);
            groups.Add((matName, currentFaces));
        }

        // ── sub-mesh builder ────────────────────────────────────────────────────────

        private static ObjSubMesh BuildSubMesh(
            string mat,
            List<int[]> faces,
            List<Vector3> positions,
            List<Vector3> normals,
            List<Vector2> uvs)
        {
            // Each face token encodes (posIdx, uvIdx, normalIdx) as three packed ints.
            var vertMap = new Dictionary<(int, int, int), uint>(faces.Count * 3);
            var verts   = new List<ObjVertex>(faces.Count * 3);
            var indices = new List<uint>(faces.Count * 3);

            foreach (int[] face in faces)
            {
                // Fan triangulation for n-gons.
                int triCount = face.Length / 3 - 2;
                for (int t = 0; t < triCount; t++)
                {
                    int i0 = 0;
                    int i1 = t + 1;
                    int i2 = t + 2;
                    indices.Add(GetOrAdd((face[i0 * 3], face[i0 * 3 + 1], face[i0 * 3 + 2]),
                                         vertMap, verts, positions, normals, uvs));
                    indices.Add(GetOrAdd((face[i1 * 3], face[i1 * 3 + 1], face[i1 * 3 + 2]),
                                         vertMap, verts, positions, normals, uvs));
                    indices.Add(GetOrAdd((face[i2 * 3], face[i2 * 3 + 1], face[i2 * 3 + 2]),
                                         vertMap, verts, positions, normals, uvs));
                }
            }

            var vertsArray   = verts.ToArray();
            var indicesArray = indices.ToArray();

            // --- DIAGNOSTIC ---
            Logger.Info($"[ObjLoader] SubMesh '{mat}': {vertsArray.Length} verts, {indicesArray.Length / 3} tris, hasVn={normals.Count > 0}");
            if (indicesArray.Length >= 3)
            {
                uint ia = indicesArray[0], ib = indicesArray[1], ic = indicesArray[2];
                var va = vertsArray[ia]; var vb = vertsArray[ib]; var vc = vertsArray[ic];
                Logger.Info($"[ObjLoader]   tri0 idx=({ia},{ib},{ic})");
                Logger.Info($"[ObjLoader]   tri0 pos: A={va.Position} B={vb.Position} C={vc.Position}");
                Logger.Info($"[ObjLoader]   tri0 nor: A={va.Normal} B={vb.Normal} C={vc.Normal}");
                // Cross product A→B × A→C should match face normal direction
                var ab = vb.Position - va.Position;
                var ac = vc.Position - va.Position;
                var cross = Vector3.Normalize(Vector3.Cross(ab, ac));
                Logger.Info($"[ObjLoader]   tri0 cross(AB,AC)={cross}  (should match face normal)");
            }
            // --- END DIAGNOSTIC ---

            // If the OBJ file has no vn normals, compute smooth normals from geometry.
            if (normals.Count == 0)
                ComputeSmoothNormals(vertsArray, indicesArray);

            // Compute tangents per triangle then accumulate + renormalise.
            ComputeTangents(vertsArray, indicesArray);

            return new ObjSubMesh
            {
                MaterialName = mat,
                Vertices     = vertsArray,
                Indices      = indicesArray
            };
        }

        private static uint GetOrAdd(
            (int p, int t, int n) key,
            Dictionary<(int, int, int), uint> map,
            List<ObjVertex> verts,
            List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs)
        {
            if (map.TryGetValue(key, out uint idx)) return idx;

            idx = (uint)verts.Count;
            map[key] = idx;

            var v = new ObjVertex
            {
                Position = key.p >= 0 && key.p < positions.Count ? positions[key.p] : Vector3.Zero,
                Normal   = key.n >= 0 && key.n < normals.Count   ? normals[key.n]   : Vector3.UnitY,
                Uv       = key.t >= 0 && key.t < uvs.Count       ? uvs[key.t]       : Vector2.Zero,
                Tangent  = new Vector4(1, 0, 0, 1)
            };
            verts.Add(v);
            return idx;
        }

        // ── smooth normal generation (used when OBJ has no vn normals) ──────────────

        private static void ComputeSmoothNormals(ObjVertex[] verts, uint[] indices)
        {
            if (verts.Length == 0 || indices.Length == 0) return;

            var accum = new Vector3[verts.Length];
            for (int i = 0; i < indices.Length; i += 3)
            {
                uint i0 = indices[i], i1 = indices[i + 1], i2 = indices[i + 2];
                // Area-weighted face normal: larger triangles contribute more.
                Vector3 e1       = verts[i1].Position - verts[i0].Position;
                Vector3 e2       = verts[i2].Position - verts[i0].Position;
                Vector3 faceNorm = Vector3.Cross(e1, e2);
                accum[i0] += faceNorm;
                accum[i1] += faceNorm;
                accum[i2] += faceNorm;
            }
            for (int i = 0; i < verts.Length; i++)
            {
                float len = accum[i].Length();
                verts[i].Normal = len > 1e-6f ? accum[i] / len : Vector3.UnitY;
            }
        }

        // ── tangent computation (MikkTSpace-style approximation) ─────────────────────

        private static void ComputeTangents(ObjVertex[] verts, uint[] indices)
        {
            if (verts.Length == 0 || indices.Length == 0) return;

            var tan1 = new Vector3[verts.Length];
            var tan2 = new Vector3[verts.Length];

            for (int i = 0; i < indices.Length; i += 3)
            {
                uint i0 = indices[i], i1 = indices[i + 1], i2 = indices[i + 2];
                ref ObjVertex v0 = ref verts[i0];
                ref ObjVertex v1 = ref verts[i1];
                ref ObjVertex v2 = ref verts[i2];

                Vector3 e1  = v1.Position - v0.Position;
                Vector3 e2  = v2.Position - v0.Position;
                float   duv1x = v1.Uv.X - v0.Uv.X, duv1y = v1.Uv.Y - v0.Uv.Y;
                float   duv2x = v2.Uv.X - v0.Uv.X, duv2y = v2.Uv.Y - v0.Uv.Y;

                float r = duv1x * duv2y - duv2x * duv1y;
                if (MathF.Abs(r) < 1e-8f) continue;
                float f = 1f / r;

                var sdir = new Vector3(
                    f * (duv2y * e1.X - duv1y * e2.X),
                    f * (duv2y * e1.Y - duv1y * e2.Y),
                    f * (duv2y * e1.Z - duv1y * e2.Z));

                var tdir = new Vector3(
                    f * (duv1x * e2.X - duv2x * e1.X),
                    f * (duv1x * e2.Y - duv2x * e1.Y),
                    f * (duv1x * e2.Z - duv2x * e1.Z));

                tan1[i0] += sdir; tan1[i1] += sdir; tan1[i2] += sdir;
                tan2[i0] += tdir; tan2[i1] += tdir; tan2[i2] += tdir;
            }

            for (int i = 0; i < verts.Length; i++)
            {
                var n = verts[i].Normal;
                var t = tan1[i];

                // Gram-Schmidt orthogonalise.
                // Guard: if tan1[i] is zero (all UVs degenerate / no UVs in OBJ),
                // Vector3.Normalize(Zero) = NaN which poisons the fragment shader.
                // Fall back to any vector perpendicular to the normal instead.
                Vector3 tangent;
                float tsq = t.LengthSquared();
                if (tsq > 1e-12f)
                {
                    tangent = Vector3.Normalize(t - n * Vector3.Dot(n, t));
                }
                else
                {
                    // Pick a stable perpendicular to n (Frisvad / Duff et al. approach).
                    var perp = MathF.Abs(n.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY;
                    tangent  = Vector3.Normalize(perp - n * Vector3.Dot(n, perp));
                }

                float sign = Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0f ? -1f : 1f;

                verts[i].Tangent = new Vector4(tangent, sign);
            }
        }

        // ── AABB helpers ─────────────────────────────────────────────────────────────

        private static void ExpandAabb(ref Vector3 min, ref Vector3 max, ObjSubMesh sm)
        {
            foreach (ref readonly ObjVertex v in sm.Vertices.AsSpan())
            {
                min = Vector3.Min(min, v.Position);
                max = Vector3.Max(max, v.Position);
            }
        }

        // ── face parsing ─────────────────────────────────────────────────────────────

        // Returns flat array of [p0,t0,n0, p1,t1,n1, ...] index triples per vertex.
        private static int[]? ParseFace(ReadOnlySpan<char> s)
        {
            // Count tokens first (avoids List allocation for typical tri/quad faces).
            int tokenCount = CountTokens(s);
            if (tokenCount < 3) return null;

            var result = new int[tokenCount * 3];
            int idx    = 0;

            // Re-iterate tokens.
            var rem = s;
            while (!rem.IsEmpty)
            {
                rem = rem.TrimStart();
                if (rem.IsEmpty) break;
                int sp = rem.IndexOf(' ');
                ReadOnlySpan<char> tok;
                if (sp < 0) { tok = rem; rem = default; }
                else         { tok = rem[..sp]; rem = rem[(sp + 1)..]; }

                ParseFaceToken(tok, result, idx * 3);
                idx++;
            }

            return idx >= 3 ? result : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountTokens(ReadOnlySpan<char> s)
        {
            int count = 0;
            bool inToken = false;
            foreach (char c in s)
            {
                if (c == ' ' || c == '\t') { inToken = false; }
                else if (!inToken)         { inToken = true; count++; }
            }
            return count;
        }

        private static void ParseFaceToken(ReadOnlySpan<char> tok, int[] result, int offset)
        {
            // Format variants: "1", "1/2", "1/2/3", "1//3"
            int slash1 = tok.IndexOf('/');
            if (slash1 < 0)
            {
                result[offset]     = ParseInt(tok) - 1;
                result[offset + 1] = -1;
                result[offset + 2] = -1;
                return;
            }

            result[offset] = ParseInt(tok[..slash1]) - 1;

            var after1 = tok[(slash1 + 1)..];
            int slash2 = after1.IndexOf('/');
            if (slash2 < 0)
            {
                result[offset + 1] = ParseInt(after1) - 1;
                result[offset + 2] = -1;
                return;
            }

            result[offset + 1] = slash2 == 0 ? -1 : ParseInt(after1[..slash2]) - 1;
            result[offset + 2] = ParseInt(after1[(slash2 + 1)..]) - 1;
        }

        // ── number parsing ───────────────────────────────────────────────────────────

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        private static Vector2 ParseVec2(ReadOnlySpan<char> s)
        {
            float u = 0, v = 0;
            int i = 0;
            foreach (Range range in s.Split(' '))
            {
                var tok = s[range];
                if (tok.IsEmpty) continue;
                float f = ParseFloat(tok);
                switch (i++) { case 0: u = f; break; case 1: v = f; break; }
            }
            return new Vector2(u, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ParseInt(ReadOnlySpan<char> s)
        {
            int.TryParse(s, out int v);
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ParseFloat(ReadOnlySpan<char> s)
        {
            float.TryParse(s, System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out float v);
            return v;
        }
    }
}
