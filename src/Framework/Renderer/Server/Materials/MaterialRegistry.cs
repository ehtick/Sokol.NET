// MaterialRegistry.cs — path → Material mapping with 16-bit id allocation.
// Loads .mtl files via MtlLoader, resolves texture views from TextureCache,
// and builds BlinnPhongMaterial records ready for GPU binding.
// Not thread-safe (Sokol main-thread constraint).

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using static Sokol.SG;

namespace GameEditor.Framework.Renderer.Server.Materials
{
    using Resources;

    public sealed class MaterialRegistry : IDisposable
    {
        private readonly Dictionary<string, Material> _byPath = new(StringComparer.Ordinal);
        private readonly Material?[] _byId;     // sparse id → material
        private ushort   _nextId = 1;           // 0 = invalid
        private readonly TextureCache _textures;
        private bool _disposed;

        public MaterialRegistry(TextureCache textures, int capacity = 4096)
        {
            _textures = textures;
            _byId     = new Material?[capacity];
        }

        // ── public API ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Parse an MTL file and register all materials it defines.
        /// Returns the id of the first material in the file (or 0 on failure).
        /// basePath is the directory of the .obj file — texture paths are resolved relative to it.
        /// </summary>
        public ushort LoadMtl(string mtlFilePath, byte[]? mtlData, string basePath,
                              Func<string, byte[]?>? fileLoader = null)
        {
            var parsed = MtlLoader.Load(mtlData);
            ushort firstId = 0;

            foreach (var (name, mtl) in parsed)
            {
                string key = mtlFilePath + "#" + name;
                if (_byPath.TryGetValue(key, out var existing))
                {
                    if (firstId == 0) firstId = existing.Id;
                    continue;
                }

                if (_nextId >= _byId.Length) break; // registry full

                var mat = BuildBlinnPhong(mtl, basePath, fileLoader);
                ushort id = _nextId++;
                mat.Id   = id;
                mat.Name = name;

                _byPath[key] = mat;
                _byId[id]    = mat;
                if (firstId == 0) firstId = id;
            }

            return firstId;
        }

        /// <summary>Look up a material by 16-bit id. Returns null if not registered.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Material? GetById(ushort id)
            => (id > 0 && id < _byId.Length) ? _byId[id] : null;

        /// <summary>Look up a material by "mtlFile#materialName" key. Returns null if not registered.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Material? GetByKey(string key)
            => _byPath.TryGetValue(key, out var m) ? m : null;

        /// <summary>Returns the Id of the material for <paramref name="key"/>, or 0 if not registered.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort GetIdByKey(string key)
            => _byPath.TryGetValue(key, out var m) ? m.Id : (ushort)0;

        // ── builder ──────────────────────────────────────────────────────────────────

        private BlinnPhongMaterial BuildBlinnPhong(
            MtlMaterial mtl, string basePath, Func<string, byte[]?>? fileLoader)
        {
            var mat = new BlinnPhongMaterial
            {
                DiffuseColor  = mtl.Kd,
                SpecularColor = mtl.Ks,
                Shininess     = MathF.Max(mtl.Ns, 1f),
                Opacity       = mtl.D,
                IsTransparent = mtl.D < 1f || !string.IsNullOrEmpty(mtl.MapD),

                DiffuseMap  = _textures.PlaceholderWhite,
                SpecularMap = _textures.PlaceholderWhite,
                NormalMap   = _textures.PlaceholderNormal,
                OpacityMap  = _textures.PlaceholderWhite,
            };

            mat.DiffuseMap  = LoadTexture(mtl.MapKd,   basePath, fileLoader, ref mat.DiffuseMapPath);
            mat.SpecularMap = LoadTexture(mtl.MapKs,   basePath, fileLoader, ref mat.SpecularMapPath);
            mat.NormalMap   = LoadTexture(mtl.MapBump, basePath, fileLoader, ref mat.NormalMapPath,
                                          fallback: _textures.PlaceholderNormal);
            mat.OpacityMap  = LoadTexture(mtl.MapD,    basePath, fileLoader, ref mat.OpacityMapPath);
            mat.Sampler     = _textures.DefaultSampler;

            return mat;
        }

        private sg_view LoadTexture(
            string relPath, string basePath,
            Func<string, byte[]?>? loader,
            ref string outPath,
            sg_view fallback = default)
        {
            if (string.IsNullOrEmpty(relPath))
                return fallback.id != 0 ? fallback : _textures.PlaceholderWhite;

            string absPath = Path.IsPathRooted(relPath)
                ? relPath
                : Path.Combine(basePath, relPath);

            outPath = absPath;

            byte[]? data = loader?.Invoke(absPath);
            if (data == null || data.Length == 0)
            {
                Console.Error.WriteLine($"[MaterialRegistry] texture not found: {absPath}");
                return fallback.id != 0 ? fallback : _textures.PlaceholderWhite;
            }

            return _textures.LoadFromBytes(absPath, data);
        }

        // ── lifecycle ────────────────────────────────────────────────────────────────

        public void Shutdown()
        {
            // TextureCache.Release() handles GPU texture teardown.
            foreach (var mat in _byPath.Values)
            {
                if (mat is BlinnPhongMaterial bp)
                {
                    if (!string.IsNullOrEmpty(bp.DiffuseMapPath))  _textures.Release(bp.DiffuseMapPath);
                    if (!string.IsNullOrEmpty(bp.SpecularMapPath)) _textures.Release(bp.SpecularMapPath);
                    if (!string.IsNullOrEmpty(bp.NormalMapPath))   _textures.Release(bp.NormalMapPath);
                    if (!string.IsNullOrEmpty(bp.OpacityMapPath))  _textures.Release(bp.OpacityMapPath);
                }
            }
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
