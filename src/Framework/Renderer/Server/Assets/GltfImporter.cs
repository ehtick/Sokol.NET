// GltfImporter.cs — loads .glb/.gltf via the native cgltf bindings (Sokol.CGltf)
// and translates the result into RenderingServer resources + ECS entities:
//   • each glTF primitive  → a single-sub-mesh MeshResource in MeshRegistry
//   • each glTF material   → a PbrMaterial in MaterialRegistry (RegisterPbr)
//   • each glTF image      → an sg texture in TextureCache
//   • each glTF node       → an ECS entity (Transform + optional MeshRenderer),
//                            parented to mirror the glTF node hierarchy.
//
// Modelled on examples/CGltfViewer/Source/CGltfModel.cs (the proven reference) but
// targets the Framework's 48 B ObjVertex layout so imported meshes render through the
// existing PBR/blinn pipelines unchanged.
//
// CROSS-PLATFORM: all file access goes through SFilesystem (sokol-fetch) — never
// System.IO.File — so it works on Desktop, Mobile, and Web. Paths are Assets-relative
// (resolved by SFilesystem against the current project's Assets folder). Because Web
// cannot block on I/O, the import is ASYNC: the main file, external .bin buffers, and
// external texture images are fetched via callbacks; the scene is built once all bytes
// have arrived, then onComplete fires on the main thread.
//
// Scope (M4): STATIC meshes + PBR metallic-roughness materials only. Skinning/morph
// extraction is deferred — skinned models import in bind pose (joints/weights dropped
// from the 48 B vertex). Not thread-safe (Sokol main-thread constraint; sfetch callbacks
// fire on the main thread during SFilesystem.Update()).

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server.Materials;
using GameEditor.Framework.Renderer.Server.Resources;
using GameEditor.Framework.Renderer.Server.Animation;
using Frent;
using Sokol;
using static Sokol.CGltf;

namespace GameEditor.Framework.Renderer.Server.Assets
{
    public static class GltfImporter
    {
        /// <summary>
        /// Imports the glTF/GLB at <paramref name="assetsRelativePath"/> (e.g. "Models/Helmet.glb",
        /// resolved against the current project's Assets folder via SFilesystem). Registers all
        /// meshes/materials/textures and builds the node hierarchy under one container entity, then
        /// invokes <paramref name="onComplete"/> with it (or null on failure) on the main thread.
        /// Asynchronous: external .bin/texture files are fetched before the scene is built.
        /// </summary>
        public static void ImportAsync(
            string assetsRelativePath,
            MeshRegistry meshReg, MaterialRegistry matReg, TextureCache texCache, ECSWorld world,
            Action<Entity?>? onComplete = null)
            => Start(assetsRelativePath, meshReg, matReg, texCache, world, buildEntities: true, onComplete);

        /// <summary>
        /// Re-registers a glTF/GLB's meshes, materials, and textures WITHOUT creating entities.
        /// Used on scene reload: the entities are already deserialized, only their resources —
        /// keyed <c>"&lt;file&gt;#m{i}p{j}"</c> / <c>"&lt;file&gt;#mat{i}"</c> — need re-populating.
        /// Cross-platform + async, same as <see cref="ImportAsync"/>.
        /// </summary>
        public static void PreloadAsync(
            string assetsRelativePath,
            MeshRegistry meshReg, MaterialRegistry matReg, TextureCache texCache, ECSWorld world,
            Action? onComplete = null)
            => Start(assetsRelativePath, meshReg, matReg, texCache, world, buildEntities: false,
                     _ => onComplete?.Invoke());

        private static void Start(
            string assetsRelativePath,
            MeshRegistry meshReg, MaterialRegistry matReg, TextureCache texCache, ECSWorld world,
            bool buildEntities, Action<Entity?>? onComplete)
        {
            Logger.Info($"[glTF] {(buildEntities ? "importing" : "preloading")} '{assetsRelativePath}' (project: {ConfigManager.ProjectFolder ?? "<none>"})");
            var job = new ImportJob(assetsRelativePath, meshReg, matReg, texCache, world, buildEntities, onComplete);
            LoadFile(assetsRelativePath, job.OnMainFileLoaded);
        }

        // Loads an Assets-relative file cross-platform. On DESKTOP the editor targets an external
        // project folder that sokol-fetch's fileutil_get_path does NOT resolve (it returns the path
        // verbatim, relative to the editor's cwd), so read straight from <project>/Assets — the same
        // resolution the OBJ loader uses. On WEB/MOBILE assets are bundled flat next to the app, where
        // SFilesystem resolves correctly. The desktop branch invokes the callback synchronously; the
        // async ImportJob handles either timing.
        private static void LoadFile(string relPath, SFileLoadCallback callback)
        {
            if (ConfigManager.HasProject)
            {
                string abs = Path.Combine(ConfigManager.ProjectFolder!, "Assets",
                    relPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(abs))
                {
                    byte[]? bytes = null;
                    try { bytes = File.ReadAllBytes(abs); }
                    catch (Exception ex) { Logger.Error($"[glTF] read failed '{abs}': {ex.Message}"); }
                    callback(relPath, bytes, bytes != null ? SFileLoadStatus.Success : SFileLoadStatus.Failed);
                    return;
                }
                Logger.Info($"[glTF] '{abs}' not on disk → falling back to SFilesystem");
            }
            SFilesystem.LoadFileAsync(relPath, callback);
        }

        // One in-flight import. Owns the pinned glTF bytes + cgltf_data* across the async fetches
        // and frees them once the scene is built (or the import fails).
        private sealed unsafe class ImportJob
        {
            private readonly string _path;     // assets-relative path of the .gltf/.glb
            private readonly string _baseDir;  // assets-relative directory ("" when at Assets root)
            private readonly MeshRegistry     _meshReg;
            private readonly MaterialRegistry _matReg;
            private readonly TextureCache     _texCache;
            private readonly ECSWorld         _world;
            private readonly bool             _buildEntities;
            private readonly Action<Entity?>? _onComplete;

            private GCHandle _mainPin;
            private readonly List<GCHandle> _extPins = new();
            private cgltf_data* _data;
            private readonly Dictionary<int, byte[]> _images = new(); // image index → encoded bytes
            private int  _pending;
            private bool _failed;
            private bool _done;

            public ImportJob(string path, MeshRegistry meshReg, MaterialRegistry matReg,
                             TextureCache texCache, ECSWorld world, bool buildEntities, Action<Entity?>? onComplete)
            {
                _path          = path;
                _baseDir       = DirOf(path);
                _meshReg       = meshReg;
                _matReg        = matReg;
                _texCache      = texCache;
                _world         = world;
                _buildEntities = buildEntities;
                _onComplete    = onComplete;
            }

            // ── Stage 1: main file loaded → parse + discover external resources ──────────────
            public void OnMainFileLoaded(string _, byte[]? buffer, SFileLoadStatus status)
            {
                if (status != SFileLoadStatus.Success || buffer == null) { Fail($"could not load file (status={status})"); return; }

                _mainPin = GCHandle.Alloc(buffer, GCHandleType.Pinned);

                cgltf_options options = default;
                cgltf_data* data = null;
                cgltf_result r = cgltf_parse(options, (void*)_mainPin.AddrOfPinnedObject(),
                                             (nuint)buffer.Length, out data);
                if (r != cgltf_result.cgltf_result_success || data == null) { Fail($"cgltf_parse failed ({r})"); return; }
                _data = data;
                Logger.Info($"[glTF] parsed '{_path}': {(int)data->meshes_count} mesh(es), "
                    + $"{(int)data->materials_count} material(s), {(int)data->images_count} image(s)");

                // GLB embedded binary chunk → buffers[0].
                if (data->file_type == cgltf_file_type.cgltf_file_type_glb &&
                    data->buffers_count > 0 && data->buffers[0].data == null && data->bin != null)
                {
                    data->buffers[0].data             = data->bin;
                    data->buffers[0].data_free_method = cgltf_data_free_method.cgltf_data_free_method_none;
                }

                var bufLoads = new List<(int idx, string rel)>();
                for (int i = 0; i < (int)data->buffers_count; i++)
                {
                    cgltf_buffer* buf = &data->buffers[i];
                    if (buf->data != null) continue;
                    string? uri = PtrToStr(buf->uri);
                    if (uri == null || uri.StartsWith("data:", StringComparison.Ordinal)) continue;
                    bufLoads.Add((i, Combine(_baseDir, uri)));
                }

                var imgLoads = new List<(int idx, string rel)>();
                for (int i = 0; i < (int)data->images_count; i++)
                {
                    cgltf_image* img = &data->images[i];
                    if (img->buffer_view != null) continue;   // embedded — read from the buffer view
                    string? uri = PtrToStr(img->uri);
                    if (uri == null || uri.StartsWith("data:", StringComparison.Ordinal)) continue;
                    imgLoads.Add((i, Combine(_baseDir, uri)));
                }

                _pending = bufLoads.Count + imgLoads.Count;
                if (_pending == 0) { Finish(); return; }
                Logger.Info($"[glTF] fetching {bufLoads.Count} external buffer(s) + {imgLoads.Count} image(s)");

                foreach (var (idx, rel) in bufLoads)
                {
                    int captured = idx;
                    LoadFile(rel, (_, b, s) => OnBufferLoaded(captured, b, s));
                }
                foreach (var (idx, rel) in imgLoads)
                {
                    int captured = idx;
                    LoadFile(rel, (_, b, s) => OnImageLoaded(captured, b, s));
                }
            }

            // ── Stage 2: external resources arrive ───────────────────────────────────────────
            private void OnBufferLoaded(int idx, byte[]? buffer, SFileLoadStatus status)
            {
                if (status == SFileLoadStatus.Success && buffer != null)
                {
                    var pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                    _extPins.Add(pin);
                    cgltf_buffer* buf = &_data->buffers[idx];
                    buf->data             = (void*)pin.AddrOfPinnedObject();
                    buf->data_free_method = cgltf_data_free_method.cgltf_data_free_method_none;
                }
                else
                {
                    _failed = true;   // a missing geometry buffer makes the model unusable
                    Logger.Error($"[glTF] missing buffer in '{_path}'");
                }
                if (--_pending == 0) FinishOrFail();
            }

            private void OnImageLoaded(int idx, byte[]? buffer, SFileLoadStatus status)
            {
                if (status == SFileLoadStatus.Success && buffer != null)
                    _images[idx] = buffer;
                else
                    Logger.Warning($"[glTF] missing texture image {idx} in '{_path}' (using placeholder)");
                if (--_pending == 0) FinishOrFail();
            }

            private void FinishOrFail()
            {
                if (_failed) { Fail("missing external buffer"); return; }
                Finish();
            }

            // ── Stage 3: all bytes present → build the scene ─────────────────────────────────
            private void Finish()
            {
                bool ok = false;
                Entity? result = null;
                try { result = BuildScene(); ok = true; }
                catch (Exception ex) { Logger.Error($"[glTF] {(_buildEntities ? "import" : "preload")} of '{_path}' failed: {ex}"); }
                Cleanup();
                if (ok) Logger.Info($"[glTF] {(_buildEntities ? "imported" : "preloaded resources for")} '{_path}'");
                if (!_done) { _done = true; _onComplete?.Invoke(result); }
            }

            private void Fail(string why)
            {
                Logger.Warning($"[glTF] {why} for '{_path}'");
                Cleanup();
                if (!_done) { _done = true; _onComplete?.Invoke(null); }
            }

            private void Cleanup()
            {
                if (_data != null) { cgltf_free(_data); _data = null; }
                foreach (var p in _extPins) if (p.IsAllocated) p.Free();
                _extPins.Clear();
                if (_mainPin.IsAllocated) _mainPin.Free();
            }

            // ── Scene construction (synchronous, once all bytes are present) ─────────────────
            // Always registers the resources; only builds entities for a full import (drag-drop).
            // Preload (scene reload) skips node building — the entities already exist.
            private Entity? BuildScene()
            {
                // A glTF mesh is "skinned" when some node references it together with a skin.
                // Those primitives take the GPU-skinning path (80 B verts + bone UBO); their static
                // 48 B registration is skipped so we don't create unused mesh GPU buffers.
                var skinnedMeshPtrs = new HashSet<IntPtr>();
                for (int ni = 0; ni < (int)_data->nodes_count; ni++)
                {
                    cgltf_node* n = &_data->nodes[ni];
                    if (n->mesh != null && n->skin != null) skinnedMeshPtrs.Add((IntPtr)n->mesh);
                }

                RegisterResources(skinnedMeshPtrs, out var primKeysByMesh, out var primMatsByMesh);

                // Skinned characters register their GPU meshes + shared animator into
                // SkinnedCharacterRegistry ALWAYS — even in preload (_buildEntities == false) — so a
                // cold scene reload repopulates the registry that the already-deserialized
                // SkinnedMeshRenderer entities resolve against. Entity creation stays gated.
                if (skinnedMeshPtrs.Count > 0)
                    return BuildSkinnedNodes(_buildEntities);

                return _buildEntities ? BuildNodes(primKeysByMesh, primMatsByMesh) : (Entity?)null;
            }

            // Registers every primitive as a MeshResource ("<file>#m{i}p{j}") and every material as a
            // PbrMaterial ("<file>#mat{i}") + its textures. Same keys the serialized scene references,
            // so a reload only needs this (no entity creation).
            private void RegisterResources(
                HashSet<IntPtr> skinnedMeshPtrs,
                out Dictionary<IntPtr, string[]> primKeysByMesh,
                out Dictionary<IntPtr, string[]> primMatsByMesh)
            {
                cgltf_data* data = _data;

                var matKeyByPtr = new Dictionary<IntPtr, string>();
                for (int i = 0; i < (int)data->materials_count; i++)
                {
                    cgltf_material* mat = &data->materials[i];
                    string key = $"{_path}#mat{i}";
                    _matReg.RegisterPbr(key, BuildMaterial(mat, i));
                    matKeyByPtr[(IntPtr)mat] = key;
                }

                primKeysByMesh = new Dictionary<IntPtr, string[]>();
                primMatsByMesh = new Dictionary<IntPtr, string[]>();
                for (int mi = 0; mi < (int)data->meshes_count; mi++)
                {
                    cgltf_mesh* mesh = &data->meshes[mi];
                    if (skinnedMeshPtrs.Contains((IntPtr)mesh))
                    {
                        // Skinned mesh — registered via the GPU-skinning path in BuildSkinnedNodes.
                        primKeysByMesh[(IntPtr)mesh] = Array.Empty<string>();
                        primMatsByMesh[(IntPtr)mesh] = Array.Empty<string>();
                        continue;
                    }
                    int primCount = (int)mesh->primitives_count;
                    var keys = new List<string>(primCount);
                    var mats = new List<string>(primCount);

                    for (int pi = 0; pi < primCount; pi++)
                    {
                        cgltf_primitive* prim = &mesh->primitives[pi];
                        if (prim->type != cgltf_primitive_type.cgltf_primitive_type_triangles) continue;
                        if (!BuildPrimitive(prim, out ObjVertex[] verts, out uint[] indices, out Aabb bounds)) continue;

                        string key = $"{_path}#m{mi}p{pi}";
                        var sub = new ObjSubMesh { MaterialName = "", Vertices = verts, Indices = indices };
                        if (_meshReg.RegisterMesh(key, new[] { sub }, in bounds) == 0) continue;

                        keys.Add(key);
                        mats.Add(prim->material != null && matKeyByPtr.TryGetValue((IntPtr)prim->material, out var mk)
                            ? mk : "");
                    }

                    primKeysByMesh[(IntPtr)mesh] = keys.ToArray();
                    primMatsByMesh[(IntPtr)mesh] = mats.ToArray();
                }
            }

            // Builds the ECS node hierarchy under one container entity (full import only).
            private Entity BuildNodes(
                Dictionary<IntPtr, string[]> primKeysByMesh, Dictionary<IntPtr, string[]> primMatsByMesh)
            {
                cgltf_data* data = _data;
                string rootName = Path.GetFileNameWithoutExtension(_path);
                Entity container = _world.CreateEntity(string.IsNullOrEmpty(rootName) ? "GltfModel" : rootName);

                if (data->scene != null)
                {
                    cgltf_scene* scene = data->scene;
                    for (int i = 0; i < (int)scene->nodes_count; i++)
                        ImportNode(scene->nodes[i], container, primKeysByMesh, primMatsByMesh);
                }
                else
                {
                    for (int i = 0; i < (int)data->nodes_count; i++)
                    {
                        cgltf_node* n = &data->nodes[i];
                        if (n->parent == null)
                            ImportNode(n, container, primKeysByMesh, primMatsByMesh);
                    }
                }

                return container;
            }

            // Skinned glTF: extract the skeleton + clips once, build one shared CGltfAnimator, and
            // create one entity per skinned primitive carrying a SkinnedMeshRenderer. The mesh node's
            // own transform is ignored (per the glTF skinning spec — the joints place the mesh); the
            // entity sits at identity under the container and the bone matrices (joint globals from
            // the scene root) do the placement.
            private Entity? BuildSkinnedNodes(bool buildEntities)
            {
                cgltf_data* data = _data;
                Entity container = default;
                if (buildEntities)
                {
                    string rootName = Path.GetFileNameWithoutExtension(_path);
                    container = _world.CreateEntity(string.IsNullOrEmpty(rootName) ? "GltfModel" : rootName);
                }

                // Live GPU meshes + the shared animator persist in the registry (keyed by path) so
                // the character survives scene save/load + the Play→Stop snapshot; the component
                // stores only serializable keys.
                var extractor = CGltfSkinExtractor.Extract(data);
                var entry = SkinnedCharacterRegistry.GetOrCreateFresh(_path);
                entry.BoneCount = extractor.BoneCount;
                entry.Animator  = extractor.HasAnimations
                    ? new CGltfAnimator(extractor.Animations[0], extractor.Nodes, extractor.BoneCount, extractor.BoneInfoMap)
                    : null;

                int runningPrim = 0;
                for (int ni = 0; ni < (int)data->nodes_count; ni++)
                {
                    cgltf_node* node = &data->nodes[ni];
                    if (node->mesh == null || node->skin == null) continue;
                    string nodeName = PtrToStr(node->name) ?? "SkinnedMesh";

                    int primCount = (int)node->mesh->primitives_count;
                    for (int pi = 0; pi < primCount; pi++)
                    {
                        cgltf_primitive* prim = &node->mesh->primitives[pi];
                        if (prim->type != cgltf_primitive_type.cgltf_primitive_type_triangles) continue;
                        if (!BuildSkinnedPrimitive(prim, out SkinnedVertex[] sverts, out uint[] sidx, out Aabb sbounds)) continue;

                        int primIndex = runningPrim++;
                        entry.Meshes[primIndex] = SkinnedMesh.Create(sverts, sidx, in sbounds, $"{nodeName}_p{pi}");

                        if (!buildEntities) continue;   // preload: registry only, no entities

                        string matKey = prim->material != null
                            ? $"{_path}#mat{(int)(prim->material - data->materials)}" : "";

                        Entity e = _world.CreateEntity($"{nodeName}_skin{pi}");
                        _world.AddComponent(e, new Transform { Position = Vector3.Zero, Rotation = Quaternion.Identity, Scale = Vector3.One, Parent = container });
                        _world.AddComponent(e, new SkinnedMeshRenderer
                        {
                            CharacterKey    = _path,
                            PrimIndex       = primIndex,
                            MaterialKey     = matKey,
                            Visible         = true,
                            ReceivesShadows = true,
                            CastsShadows    = true,
                        });
                    }
                }

                Logger.Info($"[glTF] skinned {(buildEntities ? "import" : "preload")} '{_path}': {extractor.BoneCount} bones, {extractor.Animations.Count} anim(s), {runningPrim} prim(s)");
                return buildEntities ? container : (Entity?)null;
            }

            // ── Nodes ────────────────────────────────────────────────────────────────────────
            private void ImportNode(
                cgltf_node* node, Entity parent,
                Dictionary<IntPtr, string[]> primKeysByMesh, Dictionary<IntPtr, string[]> primMatsByMesh)
            {
                Vector3    pos   = Vector3.Zero;
                Quaternion rot   = Quaternion.Identity;
                Vector3    scale = Vector3.One;

                if (node->has_matrix != 0)
                {
                    var m = new Matrix4x4(
                        node->matrix[0],  node->matrix[1],  node->matrix[2],  node->matrix[3],
                        node->matrix[4],  node->matrix[5],  node->matrix[6],  node->matrix[7],
                        node->matrix[8],  node->matrix[9],  node->matrix[10], node->matrix[11],
                        node->matrix[12], node->matrix[13], node->matrix[14], node->matrix[15]);
                    Matrix4x4.Decompose(m, out scale, out rot, out pos);
                }
                else
                {
                    if (node->has_translation != 0) pos   = new Vector3(node->translation[0], node->translation[1], node->translation[2]);
                    if (node->has_rotation    != 0) rot   = new Quaternion(node->rotation[0], node->rotation[1], node->rotation[2], node->rotation[3]);
                    if (node->has_scale       != 0) scale = new Vector3(node->scale[0], node->scale[1], node->scale[2]);
                }

                string name = PtrToStr(node->name) ?? "Node";
                Entity entity = _world.CreateEntity(name);
                // Transform already exists (CreateEntity) → AddComponent overwrites with no structural
                // change, so adding MeshRenderer afterwards is safe.
                _world.AddComponent(entity, new Transform { Position = pos, Rotation = rot, Scale = scale, Parent = parent });

                if (node->mesh != null && primKeysByMesh.TryGetValue((IntPtr)node->mesh, out var primKeys))
                {
                    var matKeys = primMatsByMesh[(IntPtr)node->mesh];
                    if (primKeys.Length == 1)
                    {
                        AddMeshRenderer(entity, primKeys[0], matKeys[0]);
                    }
                    else
                    {
                        // Multi-primitive mesh: one child entity per primitive so each carries its
                        // own material (the draw loop keys a group by a single material).
                        for (int p = 0; p < primKeys.Length; p++)
                        {
                            Entity primE = _world.CreateEntity($"{name}_prim{p}");
                            _world.AddComponent(primE, new Transform { Position = Vector3.Zero, Rotation = Quaternion.Identity, Scale = Vector3.One, Parent = entity });
                            AddMeshRenderer(primE, primKeys[p], matKeys[p]);
                        }
                    }
                }

                for (int c = 0; c < (int)node->children_count; c++)
                    ImportNode(node->children[c], entity, primKeysByMesh, primMatsByMesh);
            }

            private void AddMeshRenderer(Entity e, string meshKey, string matKey)
            {
                _world.AddComponent(e, new MeshRenderer
                {
                    MeshPath        = meshKey,
                    MaterialPath    = matKey,
                    Visible         = true,
                    ReceivesShadows = true,
                    CastsShadows    = true,
                });
            }

            // ── Materials ──────────────────────────────────────────────────────────────────
            private PbrMaterial BuildMaterial(cgltf_material* mat, int matIndex)
            {
                var m = new PbrMaterial { Name = PtrToStr(mat->name) ?? $"mat{matIndex}" };

                if (mat->has_pbr_metallic_roughness != 0)
                {
                    ref var pbr = ref mat->pbr_metallic_roughness;
                    m.BaseColorFactor = new Vector4(pbr.base_color_factor[0], pbr.base_color_factor[1],
                                                    pbr.base_color_factor[2], pbr.base_color_factor[3]);
                    m.MetallicFactor  = pbr.metallic_factor;
                    m.RoughnessFactor = pbr.roughness_factor;
                    m.BaseColorMap         = LoadTexture(pbr.base_color_texture,         srgb: true,  out m.BaseColorMapPath);
                    m.MetallicRoughnessMap = LoadTexture(pbr.metallic_roughness_texture, srgb: false, out m.MetallicRoughnessMapPath);
                }
                else
                {
                    m.BaseColorFactor = Vector4.One;
                    m.MetallicFactor  = 0f;
                    m.RoughnessFactor = 0.5f;
                }

                m.NormalMap         = LoadTexture(mat->normal_texture,    srgb: false, out m.NormalMapPath);
                m.NormalMapScale    = mat->normal_texture.scale != 0f ? mat->normal_texture.scale : 1f;
                m.OcclusionMap      = LoadTexture(mat->occlusion_texture, srgb: false, out m.OcclusionMapPath);
                m.OcclusionStrength = mat->occlusion_texture.scale != 0f ? mat->occlusion_texture.scale : 1f;
                m.EmissiveFactor    = new Vector3(mat->emissive_factor[0], mat->emissive_factor[1], mat->emissive_factor[2]);
                m.EmissiveMap       = LoadTexture(mat->emissive_texture,  srgb: true,  out m.EmissiveMapPath);
                m.EmissiveStrength  = mat->has_emissive_strength != 0 ? mat->emissive_strength.emissive_strength : 1f;

                m.AlphaMode = mat->alpha_mode switch
                {
                    cgltf_alpha_mode.cgltf_alpha_mode_mask  => 1,
                    cgltf_alpha_mode.cgltf_alpha_mode_blend => 2,
                    _                                       => 0,
                };
                m.AlphaCutoff   = mat->alpha_cutoff > 0f ? mat->alpha_cutoff : 0.5f;
                m.IsTransparent = m.AlphaMode == 2;
                m.DoubleSided   = mat->double_sided != 0;
                m.Sampler       = _texCache.DefaultSampler;
                return m;
            }

            // Decodes the texture referenced by <paramref name="view"/> — from its embedded buffer
            // view (GLB) or the pre-fetched external image bytes — and uploads it. Zero view = absent.
            private Sokol.SG.sg_view LoadTexture(cgltf_texture_view view, bool srgb, out string outPath)
            {
                outPath = "";
                if (view.texture == null) return default;
                cgltf_image* img = view.texture->image;
                if (img == null) return default;

                int imgIndex = (int)(img - _data->images);
                string key = $"{_path}#img{imgIndex}";

                byte[]? imageData = null;
                if (img->buffer_view != null)
                {
                    byte* ptr = cgltf_buffer_view_data(*img->buffer_view);
                    if (ptr == null) return default;
                    int sz = (int)img->buffer_view->size;
                    imageData = new byte[sz];
                    Marshal.Copy((IntPtr)ptr, imageData, 0, sz);
                }
                else if (_images.TryGetValue(imgIndex, out var ext))
                {
                    imageData = ext;
                }
                if (imageData == null) return default;

                outPath = key;
                return _texCache.LoadFromBytes(key, imageData, srgb);
            }

            // ── Primitives ─────────────────────────────────────────────────────────────────
            private bool BuildPrimitive(cgltf_primitive* prim, out ObjVertex[] vertices, out uint[] indices, out Aabb bounds)
            {
                vertices = Array.Empty<ObjVertex>();
                indices  = Array.Empty<uint>();
                bounds   = default;

                cgltf_accessor* posAcc = null, normAcc = null, tanAcc = null, tc0Acc = null;
                for (int ai = 0; ai < (int)prim->attributes_count; ai++)
                {
                    cgltf_attribute* a = &prim->attributes[ai];
                    switch (a->type)
                    {
                        case cgltf_attribute_type.cgltf_attribute_type_position: posAcc  = a->data; break;
                        case cgltf_attribute_type.cgltf_attribute_type_normal:   normAcc = a->data; break;
                        case cgltf_attribute_type.cgltf_attribute_type_tangent:  tanAcc  = a->data; break;
                        case cgltf_attribute_type.cgltf_attribute_type_texcoord:
                            if (a->index == 0) tc0Acc = a->data;
                            break;
                    }
                }

                if (posAcc == null) return false;
                int vc = (int)posAcc->count;
                if (vc == 0) return false;

                float[] posData  = UnpackFloats(posAcc,  vc * 3);
                float[] normData = normAcc != null ? UnpackFloats(normAcc, vc * 3) : Array.Empty<float>();
                float[] tanData  = tanAcc  != null ? UnpackFloats(tanAcc,  vc * 4) : Array.Empty<float>();
                float[] tc0Data  = tc0Acc  != null ? UnpackFloats(tc0Acc,  vc * 2) : Array.Empty<float>();

                uint[] idx;
                if (prim->indices != null)
                {
                    int ic = (int)prim->indices->count;
                    idx = new uint[ic];
                    for (int i = 0; i < ic; i++)
                        idx[i] = (uint)cgltf_accessor_read_index(*prim->indices, (nuint)i);
                }
                else
                {
                    idx = new uint[vc];
                    for (int i = 0; i < vc; i++) idx[i] = (uint)i;
                }

                // glTF tangents are optional; synthesise them (Lengyel) when a UV set + normals exist.
                if (tanData.Length == 0 && tc0Data.Length > 0 && normData.Length > 0)
                    tanData = ComputeTangents(posData, normData, tc0Data, idx, vc);

                vertices = new ObjVertex[vc];
                var bmin = new Vector3(float.MaxValue);
                var bmax = new Vector3(float.MinValue);
                for (int i = 0; i < vc; i++)
                {
                    var p = new Vector3(posData[i * 3], posData[i * 3 + 1], posData[i * 3 + 2]);
                    vertices[i] = new ObjVertex
                    {
                        Position = p,
                        Normal   = normData.Length > 0 ? new Vector3(normData[i * 3], normData[i * 3 + 1], normData[i * 3 + 2]) : Vector3.UnitY,
                        Uv       = tc0Data.Length  > 0 ? new Vector2(tc0Data[i * 2], tc0Data[i * 2 + 1]) : Vector2.Zero,
                        Tangent  = tanData.Length  > 0 ? new Vector4(tanData[i * 4], tanData[i * 4 + 1], tanData[i * 4 + 2], tanData[i * 4 + 3]) : new Vector4(1f, 0f, 0f, 1f),
                    };
                    bmin = Vector3.Min(bmin, p);
                    bmax = Vector3.Max(bmax, p);
                }

                indices = idx;
                bounds  = new Aabb(bmin, bmax);
                return true;
            }

            // Skinned variant of BuildPrimitive: also reads JOINTS_0 + WEIGHTS_0 into an 80 B
            // SkinnedVertex (joint indices are unpacked as floats — the shader casts them back).
            private bool BuildSkinnedPrimitive(cgltf_primitive* prim, out SkinnedVertex[] vertices, out uint[] indices, out Aabb bounds)
            {
                vertices = Array.Empty<SkinnedVertex>();
                indices  = Array.Empty<uint>();
                bounds   = default;

                cgltf_accessor* posAcc = null, normAcc = null, tanAcc = null, tc0Acc = null, jntAcc = null, wgtAcc = null;
                for (int ai = 0; ai < (int)prim->attributes_count; ai++)
                {
                    cgltf_attribute* a = &prim->attributes[ai];
                    switch (a->type)
                    {
                        case cgltf_attribute_type.cgltf_attribute_type_position: posAcc  = a->data; break;
                        case cgltf_attribute_type.cgltf_attribute_type_normal:   normAcc = a->data; break;
                        case cgltf_attribute_type.cgltf_attribute_type_tangent:  tanAcc  = a->data; break;
                        case cgltf_attribute_type.cgltf_attribute_type_texcoord: if (a->index == 0) tc0Acc = a->data; break;
                        case cgltf_attribute_type.cgltf_attribute_type_joints:   if (a->index == 0) jntAcc = a->data; break;
                        case cgltf_attribute_type.cgltf_attribute_type_weights:  if (a->index == 0) wgtAcc = a->data; break;
                    }
                }

                if (posAcc == null) return false;
                int vc = (int)posAcc->count;
                if (vc == 0) return false;

                float[] posData  = UnpackFloats(posAcc,  vc * 3);
                float[] normData = normAcc != null ? UnpackFloats(normAcc, vc * 3) : Array.Empty<float>();
                float[] tanData  = tanAcc  != null ? UnpackFloats(tanAcc,  vc * 4) : Array.Empty<float>();
                float[] tc0Data  = tc0Acc  != null ? UnpackFloats(tc0Acc,  vc * 2) : Array.Empty<float>();
                float[] jntData  = jntAcc  != null ? UnpackFloats(jntAcc,  vc * 4) : Array.Empty<float>();
                float[] wgtData  = wgtAcc  != null ? UnpackFloats(wgtAcc,  vc * 4) : Array.Empty<float>();

                uint[] idx;
                if (prim->indices != null)
                {
                    int ic = (int)prim->indices->count;
                    idx = new uint[ic];
                    for (int i = 0; i < ic; i++)
                        idx[i] = (uint)cgltf_accessor_read_index(*prim->indices, (nuint)i);
                }
                else
                {
                    idx = new uint[vc];
                    for (int i = 0; i < vc; i++) idx[i] = (uint)i;
                }

                if (tanData.Length == 0 && tc0Data.Length > 0 && normData.Length > 0)
                    tanData = ComputeTangents(posData, normData, tc0Data, idx, vc);

                vertices = new SkinnedVertex[vc];
                var bmin = new Vector3(float.MaxValue);
                var bmax = new Vector3(float.MinValue);
                for (int i = 0; i < vc; i++)
                {
                    var p = new Vector3(posData[i * 3], posData[i * 3 + 1], posData[i * 3 + 2]);
                    vertices[i] = new SkinnedVertex
                    {
                        Position = p,
                        Normal   = normData.Length > 0 ? new Vector3(normData[i * 3], normData[i * 3 + 1], normData[i * 3 + 2]) : Vector3.UnitY,
                        Uv       = tc0Data.Length  > 0 ? new Vector2(tc0Data[i * 2], tc0Data[i * 2 + 1]) : Vector2.Zero,
                        Tangent  = tanData.Length  > 0 ? new Vector4(tanData[i * 4], tanData[i * 4 + 1], tanData[i * 4 + 2], tanData[i * 4 + 3]) : new Vector4(1f, 0f, 0f, 1f),
                        Joints   = jntData.Length  > 0 ? new Vector4(jntData[i * 4], jntData[i * 4 + 1], jntData[i * 4 + 2], jntData[i * 4 + 3]) : Vector4.Zero,
                        Weights  = wgtData.Length  > 0 ? new Vector4(wgtData[i * 4], wgtData[i * 4 + 1], wgtData[i * 4 + 2], wgtData[i * 4 + 3]) : new Vector4(1f, 0f, 0f, 0f),
                    };
                    bmin = Vector3.Min(bmin, p);
                    bmax = Vector3.Max(bmax, p);
                }

                indices = idx;
                bounds  = new Aabb(bmin, bmax);
                return true;
            }

            private static float[] UnpackFloats(cgltf_accessor* acc, int totalFloats)
            {
                if (acc == null || totalFloats <= 0) return Array.Empty<float>();
                var buf = new float[totalFloats];
                fixed (float* p = buf)
                    cgltf_accessor_unpack_floats(*acc, p, (nuint)totalFloats);
                return buf;
            }
        }

        // ── Static helpers ───────────────────────────────────────────────────────────────────

        // Lengyel's method (ported from CGltfModel.CalculateTangents) → packed float[vc*4],
        // xyz = orthonormalised tangent, w = bitangent handedness.
        private static float[] ComputeTangents(float[] pos, float[] nrm, float[] uv, uint[] indices, int vc)
        {
            var tan1 = new Vector3[vc];
            var tan2 = new Vector3[vc];

            for (int ii = 0; ii + 2 < indices.Length; ii += 3)
            {
                int i0 = (int)indices[ii], i1 = (int)indices[ii + 1], i2 = (int)indices[ii + 2];
                Vector3 p0 = V3(pos, i0), p1 = V3(pos, i1), p2 = V3(pos, i2);
                Vector2 u0 = V2(uv, i0),  u1 = V2(uv, i1),  u2 = V2(uv, i2);

                float x1 = p1.X - p0.X, x2 = p2.X - p0.X;
                float y1 = p1.Y - p0.Y, y2 = p2.Y - p0.Y;
                float z1 = p1.Z - p0.Z, z2 = p2.Z - p0.Z;
                float s1 = u1.X - u0.X, s2 = u2.X - u0.X;
                float t1 = u1.Y - u0.Y, t2 = u2.Y - u0.Y;
                float denom = s1 * t2 - s2 * t1;
                if (MathF.Abs(denom) < 1e-7f) continue;
                float rcp = 1f / denom;
                var sdir = new Vector3((t2 * x1 - t1 * x2) * rcp, (t2 * y1 - t1 * y2) * rcp, (t2 * z1 - t1 * z2) * rcp);
                var tdir = new Vector3((s1 * x2 - s2 * x1) * rcp, (s1 * y2 - s2 * y1) * rcp, (s1 * z2 - s2 * z1) * rcp);

                tan1[i0] += sdir; tan1[i1] += sdir; tan1[i2] += sdir;
                tan2[i0] += tdir; tan2[i1] += tdir; tan2[i2] += tdir;
            }

            var outp = new float[vc * 4];
            for (int i = 0; i < vc; i++)
            {
                Vector3 n = V3(nrm, i);
                Vector3 t = tan1[i];
                Vector3 ortho = t - n * Vector3.Dot(n, t);
                ortho = ortho.LengthSquared() > 1e-12f ? Vector3.Normalize(ortho) : new Vector3(1f, 0f, 0f);
                float hand = Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0f ? -1f : 1f;
                outp[i * 4 + 0] = ortho.X;
                outp[i * 4 + 1] = ortho.Y;
                outp[i * 4 + 2] = ortho.Z;
                outp[i * 4 + 3] = hand;
            }
            return outp;
        }

        private static Vector3 V3(float[] a, int i) => new Vector3(a[i * 3], a[i * 3 + 1], a[i * 3 + 2]);
        private static Vector2 V2(float[] a, int i) => new Vector2(a[i * 2], a[i * 2 + 1]);

        private static unsafe string? PtrToStr(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);

        // Assets-relative directory of a file path (forward-slash; "" when at the Assets root).
        private static string DirOf(string rel)
        {
            rel = rel.Replace('\\', '/');
            int i = rel.LastIndexOf('/');
            return i < 0 ? "" : rel.Substring(0, i);
        }

        // Joins an Assets-relative directory with a (possibly URL-encoded) glTF-relative URI.
        private static string Combine(string dir, string uri)
        {
            uri = Uri.UnescapeDataString(uri).Replace('\\', '/');
            return string.IsNullOrEmpty(dir) ? uri : dir + "/" + uri;
        }
    }
}
