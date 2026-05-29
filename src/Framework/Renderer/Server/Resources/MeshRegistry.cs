// MeshRegistry.cs — path → MeshResource mapping with 16-bit id allocation.
// Loads OBJ files, uploads sub-mesh VB+IB pairs to Sokol, and assigns a
// stable ushort MeshId for use in DrawCommand.MeshId.
// Not thread-safe (Sokol main-thread constraint).

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GameEditor.Framework.Renderer;
using static Sokol.SG;
using static Sokol.Utils;

namespace GameEditor.Framework.Renderer.Server.Resources
{
    public sealed unsafe class MeshRegistry : IDisposable
    {
        private readonly Dictionary<string, MeshResource> _byPath = new(StringComparer.Ordinal);
        private readonly MeshResource?[] _byId;    // sparse id → resource
        private ushort   _nextId = 1;              // 0 = invalid
        private bool     _disposed;

        public MeshRegistry(int capacity = 4096)
        {
            _byId = new MeshResource?[capacity];
        }

        // ── public API ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Load an OBJ file from raw bytes and upload to GPU.
        /// Returns the 16-bit MeshId. Ref-counted: safe to call multiple times for the same path.
        /// Returns 0 on failure.
        /// </summary>
        public ushort Load(string path, ReadOnlySpan<byte> data)
        {
            if (_byPath.TryGetValue(path, out var existing))
            {
                existing.RefCount++;
                return existing.Id;
            }

            string text = System.Text.Encoding.UTF8.GetString(data);
            ObjMesh obj = ObjLoader.Load(text.AsSpan(), path);

            if (obj.SubMeshes.Length == 0) return 0;

            if (_nextId >= _byId.Length) return 0; // registry full

            var subResources = new MeshSubResource[obj.SubMeshes.Length];
            for (int i = 0; i < obj.SubMeshes.Length; i++)
            {
                var sm    = obj.SubMeshes[i];
                var vbRes = UploadVertexBuffer(sm, path, i);
                var ibRes = UploadIndexBuffer(sm, path, i);

                subResources[i] = new MeshSubResource
                {
                    VertexBuffer = vbRes,
                    IndexBuffer  = ibRes,
                    IndexCount   = sm.Indices.Length,
                    MaterialName = sm.MaterialName,
                    MaterialKey  = (!string.IsNullOrEmpty(obj.MtlLib) && !string.IsNullOrEmpty(sm.MaterialName))
                                       ? string.Concat(obj.MtlLib, "#", sm.MaterialName)
                                       : ""
                };
            }

            System.Console.WriteLine($"[MeshRegistry] Loaded '{path}': {obj.SubMeshes.Length} sub-mesh(es), bounds={obj.Bounds}");
            ushort id = _nextId++;
            var res = new MeshResource
            {
                Id          = id,
                SourcePath  = path,
                MtlLib      = obj.MtlLib,
                SubMeshes   = subResources,
                LocalBounds = obj.Bounds,
                RefCount    = 1
            };

            _byPath[path] = res;
            _byId[id]     = res;
            return id;
        }

        /// <summary>Decrement ref-count; destroys GPU buffers when count reaches zero.</summary>
        public void Release(string path)
        {
            if (!_byPath.TryGetValue(path, out var res)) return;
            res.RefCount--;
            if (res.RefCount > 0) return;

            _byId[res.Id] = null;
            _byPath.Remove(path);
            res.Destroy();
        }

        /// <summary>Look up a mesh by 16-bit id. Returns null if not loaded.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MeshResource? GetById(ushort id)
            => (id > 0 && id < _byId.Length) ? _byId[id] : null;

        /// <summary>Look up a mesh by file path. Returns null if not loaded.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MeshResource? GetByPath(string path)
        {
            if (_byPath.TryGetValue(path, out var r))
                return r;

            if (!path.StartsWith("prim:", StringComparison.Ordinal))
                return null;

            return TryCreatePrimitive(path, out var primRes) ? primRes : null;
        }

        private bool TryCreatePrimitive(string path, out MeshResource resource)
        {
            resource = default!;

            if (!PrimitiveMeshSpec.TryParse(path, out PrimitiveMeshSpec spec))
                return false;

            var (positions, indices) = PrimitiveMeshGeometry.GetMeshTriangles(spec);
            if (positions.Length == 0 || indices.Length == 0)
                return false;

            // PrimitiveMeshGeometry is shared with physics generation and uses opposite
            // winding for render-facing triangles. Flip winding here so raster culling,
            // lighting normals, and shadow casting are consistent with OBJ meshes.
            var renderIndices = new uint[indices.Length];
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                renderIndices[i + 0] = indices[i + 0];
                renderIndices[i + 1] = indices[i + 2];
                renderIndices[i + 2] = indices[i + 1];
            }

            var vertices = new ObjVertex[positions.Length];
            var normals = new Vector3[positions.Length];

            for (int i = 0; i + 2 < renderIndices.Length; i += 3)
            {
                int ia = (int)renderIndices[i + 0];
                int ib = (int)renderIndices[i + 1];
                int ic = (int)renderIndices[i + 2];
                if ((uint)ia >= (uint)positions.Length || (uint)ib >= (uint)positions.Length || (uint)ic >= (uint)positions.Length)
                    continue;

                Vector3 e1 = positions[ib] - positions[ia];
                Vector3 e2 = positions[ic] - positions[ia];
                Vector3 n = Vector3.Cross(e1, e2);
                if (n.LengthSquared() <= 1e-12f)
                    continue;
                n = Vector3.Normalize(n);
                normals[ia] += n;
                normals[ib] += n;
                normals[ic] += n;
            }

            Vector3 bmin = new(float.MaxValue);
            Vector3 bmax = new(float.MinValue);

            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i];
                Vector3 n = normals[i];
                if (n.LengthSquared() <= 1e-12f)
                    n = Vector3.UnitY;
                else
                    n = Vector3.Normalize(n);

                Vector3 tangentAxis = MathF.Abs(n.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
                Vector3 t = Vector3.Normalize(Vector3.Cross(tangentAxis, n));
                Vector2 uv = new(p.X, p.Z);

                vertices[i] = new ObjVertex
                {
                    Position = p,
                    Normal = n,
                    Uv = uv,
                    Tangent = new Vector4(t, 1f)
                };

                bmin = Vector3.Min(bmin, p);
                bmax = Vector3.Max(bmax, p);
            }

            var sub = new ObjSubMesh
            {
                MaterialName = "",
                Vertices = vertices,
                Indices = renderIndices
            };

            ushort id = _nextId++;
            if (id == 0 || id >= _byId.Length)
                return false;

            var subResources = new MeshSubResource[1];
            subResources[0] = new MeshSubResource
            {
                VertexBuffer = UploadVertexBuffer(sub, path, 0),
                IndexBuffer = UploadIndexBuffer(sub, path, 0),
                IndexCount = sub.Indices.Length,
                MaterialName = "",
                MaterialKey = ""
            };

            resource = new MeshResource
            {
                Id = id,
                SourcePath = path,
                MtlLib = "",
                SubMeshes = subResources,
                LocalBounds = new Aabb(bmin, bmax),
                RefCount = 1
            };

            _byPath[path] = resource;
            _byId[id] = resource;
            return true;
        }

        // ── upload helpers ───────────────────────────────────────────────────────────

        private static sg_buffer UploadVertexBuffer(ObjSubMesh sm, string path, int subIdx)
        {
            var span = sm.Vertices.AsSpan();
            return sg_make_buffer(new sg_buffer_desc
            {
                data  = SG_RANGE(span),
                usage = new sg_buffer_usage { vertex_buffer = true, immutable = true },
                label = $"{path}:vb[{subIdx}]"
            });
        }

        private static sg_buffer UploadIndexBuffer(ObjSubMesh sm, string path, int subIdx)
        {
            var span = sm.Indices.AsSpan();
            return sg_make_buffer(new sg_buffer_desc
            {
                usage = new sg_buffer_usage { index_buffer = true, immutable = true },
                data  = SG_RANGE(span),
                label = $"{path}:ib[{subIdx}]"
            });
        }

        // ── lifecycle ────────────────────────────────────────────────────────────────

        public void Shutdown()
        {
            foreach (var res in _byPath.Values) res.Destroy();
            _byPath.Clear();
            Array.Clear(_byId, 0, _byId.Length);
            _nextId = 1;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Shutdown();
        }
    }
}
