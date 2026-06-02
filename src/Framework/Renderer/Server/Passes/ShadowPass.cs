using System;
using System.Numerics;
using System.Collections.Generic;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server.Lighting;
using GameEditor.Framework.Renderer.Server.Resources;
using static Sokol.SG;
using static Sokol.Utils;
using static shadow_caster_shader_cs.Shaders;

namespace GameEditor.Framework.Renderer.Server.Passes
{
    /// <summary>
    /// Renders shadow casters into a depth-only atlas slice.
    /// </summary>
    public sealed class ShadowPass
    {
        private struct PrimitiveShadowMesh
        {
            public sg_buffer VertexBuffer;
            public sg_buffer IndexBuffer;
            public int IndexCount;
        }

        private sg_shader _shader;
        private sg_pipeline _pipeline;
        private sg_pass_action _passAction;
        private sg_image _atlasDummyColorImage;
        private sg_view _atlasDummyColorView;
        private sg_image _cubeDummyColorImage;
        private sg_view _cubeDummyColorView;
        private readonly Dictionary<string, PrimitiveShadowMesh> _primitiveMeshes = new();
        private bool _initialized;

        public void Init()
        {
            if (_initialized) return;

            _shader = sg_make_shader(shadow_caster_shader_desc(sg_query_backend()));

            var desc = new sg_pipeline_desc
            {
                shader = _shader,
                index_type = sg_index_type.SG_INDEXTYPE_UINT32,
                cull_mode = sg_cull_mode.SG_CULLMODE_FRONT,
                color_count = 1,
                sample_count = 1,
                depth = new sg_depth_state
                {
                    pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
                    compare = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                    write_enabled = true
                },
                colors =
                {
                    [0] = new sg_color_target_state
                    {
                        pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                        write_mask = sg_color_mask.SG_COLORMASK_NONE
                    }
                },
                label = "shadow-caster-pipeline"
            };

            desc.layout.buffers[0].stride = 48;
            desc.layout.attrs[ATTR_shadow_caster_in_pos] = new sg_vertex_attr_state
            {
                buffer_index = 0,
                offset = 0,
                format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3,
            };

            _pipeline = sg_make_pipeline(desc);

            _passAction = default;
            _passAction.colors[0].load_action = sg_load_action.SG_LOADACTION_DONTCARE;
            _passAction.colors[0].store_action = sg_store_action.SG_STOREACTION_DONTCARE;
            _passAction.depth.load_action = sg_load_action.SG_LOADACTION_CLEAR;
            _passAction.depth.store_action = sg_store_action.SG_STOREACTION_STORE;
            _passAction.depth.clear_value = 1.0f;

            _atlasDummyColorImage = sg_make_image(new sg_image_desc
            {
                type = sg_image_type.SG_IMAGETYPE_2D,
                width = ShadowAtlas.SliceSize,
                height = ShadowAtlas.SliceSize,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                sample_count = 1,
                usage = { color_attachment = true },
                label = "shadow-pass-dummy-color-atlas"
            });
            _atlasDummyColorView = sg_make_view(new sg_view_desc
            {
                color_attachment = new sg_image_view_desc { image = _atlasDummyColorImage },
                label = "shadow-pass-dummy-color-atlas-view"
            });

            _cubeDummyColorImage = sg_make_image(new sg_image_desc
            {
                type = sg_image_type.SG_IMAGETYPE_2D,
                width = CubeShadowArray.FaceSize,
                height = CubeShadowArray.FaceSize,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                sample_count = 1,
                usage = { color_attachment = true },
                label = "shadow-pass-dummy-color-cube"
            });
            _cubeDummyColorView = sg_make_view(new sg_view_desc
            {
                color_attachment = new sg_image_view_desc { image = _cubeDummyColorImage },
                label = "shadow-pass-dummy-color-cube-view"
            });

            _initialized = true;
        }

        public void Shutdown()
        {
            if (!_initialized) return;
            _initialized = false;

            if (_pipeline.id != 0)
            {
                sg_destroy_pipeline(_pipeline);
                _pipeline = default;
            }
            if (_shader.id != 0)
            {
                sg_destroy_shader(_shader);
                _shader = default;
            }

            if (_atlasDummyColorView.id != 0)
            {
                sg_destroy_view(_atlasDummyColorView);
                _atlasDummyColorView = default;
            }
            if (_atlasDummyColorImage.id != 0)
            {
                sg_destroy_image(_atlasDummyColorImage);
                _atlasDummyColorImage = default;
            }
            if (_cubeDummyColorView.id != 0)
            {
                sg_destroy_view(_cubeDummyColorView);
                _cubeDummyColorView = default;
            }
            if (_cubeDummyColorImage.id != 0)
            {
                sg_destroy_image(_cubeDummyColorImage);
                _cubeDummyColorImage = default;
            }

            foreach (var kv in _primitiveMeshes)
            {
                if (kv.Value.VertexBuffer.id != 0)
                    sg_destroy_buffer(kv.Value.VertexBuffer);
                if (kv.Value.IndexBuffer.id != 0)
                    sg_destroy_buffer(kv.Value.IndexBuffer);
            }
            _primitiveMeshes.Clear();
        }

        public void RenderDirectional(
            ECSWorld world,
            MeshRegistry meshRegistry,
            ShadowAtlas atlas,
            int slice,
            in Matrix4x4 lightViewProj)
        {
            if (!_initialized || !atlas.IsValid) return;

            RenderSlice(world, meshRegistry, _atlasDummyColorView, atlas.GetDepthSliceView(slice), in lightViewProj, out _);
        }

        public int RenderDirectionalCounted(
            ECSWorld world,
            MeshRegistry meshRegistry,
            ShadowAtlas atlas,
            int slice,
            in Matrix4x4 lightViewProj)
        {
            if (!_initialized || !atlas.IsValid) return 0;

            RenderSlice(world, meshRegistry, _atlasDummyColorView, atlas.GetDepthSliceView(slice), in lightViewProj, out int draws);
            return draws;
        }

        /// <summary>
        /// Renders all CSM cascades into consecutive atlas slices starting at <paramref name="baseSlice"/>.
        /// Each element of <paramref name="cascadeVPs"/> corresponds to one cascade, rendered into
        /// slice <c>baseSlice + i</c>.
        /// </summary>
        /// <returns>Total shadow draw calls across all cascades.</returns>
        public int RenderDirectionalCsmCounted(
            ECSWorld world,
            MeshRegistry meshRegistry,
            ShadowAtlas atlas,
            int baseSlice,
            ReadOnlySpan<Matrix4x4> cascadeVPs)
        {
            if (!_initialized || !atlas.IsValid) return 0;

            int totalDraws = 0;
            for (int i = 0; i < cascadeVPs.Length; i++)
            {
                Matrix4x4 vp = cascadeVPs[i];
                RenderSlice(world, meshRegistry, _atlasDummyColorView,
                    atlas.GetDepthSliceView(baseSlice + i), in vp, out int draws);
                totalDraws += draws;
            }
            return totalDraws;
        }

        public void RenderSpot(
            ECSWorld world,
            MeshRegistry meshRegistry,
            ShadowAtlas atlas,
            int slice,
            in Matrix4x4 lightViewProj)
        {
            if (!_initialized || !atlas.IsValid) return;

            RenderSlice(world, meshRegistry, _atlasDummyColorView, atlas.GetDepthSliceView(slice), in lightViewProj, out _);
        }

        public int RenderSpotCounted(
            ECSWorld world,
            MeshRegistry meshRegistry,
            ShadowAtlas atlas,
            int slice,
            in Matrix4x4 lightViewProj)
        {
            if (!_initialized || !atlas.IsValid) return 0;

            RenderSlice(world, meshRegistry, _atlasDummyColorView, atlas.GetDepthSliceView(slice), in lightViewProj, out int draws);
            return draws;
        }

        public void RenderPointFace(
            ECSWorld world,
            MeshRegistry meshRegistry,
            CubeShadowArray cubeArray,
            int pointLightIndex,
            int faceIndex,
            in Matrix4x4 lightViewProj)
        {
            if (!_initialized || !cubeArray.IsValid) return;

            RenderSlice(world, meshRegistry, _cubeDummyColorView, cubeArray.GetDepthFaceView(pointLightIndex, faceIndex), in lightViewProj, out _);
        }

        public int RenderPointFaceCounted(
            ECSWorld world,
            MeshRegistry meshRegistry,
            CubeShadowArray cubeArray,
            int pointLightIndex,
            int faceIndex,
            in Matrix4x4 lightViewProj)
        {
            if (!_initialized || !cubeArray.IsValid) return 0;

            RenderSlice(world, meshRegistry, _cubeDummyColorView, cubeArray.GetDepthFaceView(pointLightIndex, faceIndex), in lightViewProj, out int draws);
            return draws;
        }

        private void RenderSlice(
            ECSWorld world,
            MeshRegistry meshRegistry,
            sg_view colorView,
            sg_view depthSliceView,
            in Matrix4x4 lightViewProj,
            out int drawCallCount)
        {
            drawCallCount = 0;
            if (colorView.id == 0 || depthSliceView.id == 0) return;

            var pass = new sg_pass
            {
                action = _passAction,
                attachments =
                {
                    colors = { [0] = colorView },
                    depth_stencil = depthSliceView
                }
            };

            sg_begin_pass(pass);
            try
            {
                sg_apply_pipeline(_pipeline);

                foreach (var row in world.Query<ActiveFlag, MeshRenderer, Transform>()
                                         .Enumerate<ActiveFlag, MeshRenderer, Transform>())
                {
                    if (!row.Item1.Value.Active) continue;
                    ref readonly var mr = ref row.Item2.Value;
                    if (!mr.Visible || !mr.CastsShadows) continue;
                    ref readonly var tf = ref row.Item3.Value;

                    Matrix4x4 model = Transform.GetWorldMatrix(world, tf);
                    var vsParams = new shadow_caster_vs_params_t
                    {
                        mvp = model * lightViewProj
                    };
                    sg_apply_uniforms(UB_shadow_caster_vs_params, SG_RANGE(ref vsParams));

                    if (PrimitiveMeshSpec.TryParse(mr.MeshPath, out var primSpec))
                    {
                        PrimitiveShadowMesh prim = GetOrCreatePrimitiveShadowMesh(PrimitiveMeshSpec.ToMeshPath(primSpec));
                        if (prim.IndexCount == 0 || prim.VertexBuffer.id == 0 || prim.IndexBuffer.id == 0)
                            continue;

                        sg_apply_bindings(new sg_bindings
                        {
                            vertex_buffers = { [0] = prim.VertexBuffer },
                            index_buffer = prim.IndexBuffer,
                        });

                        sg_draw(0, (uint)prim.IndexCount, 1);
                        drawCallCount++;
                        continue;
                    }

                    MeshResource? mesh = meshRegistry.GetByPath(mr.MeshPath);
                    if (mesh == null || mesh.SubMeshes == null || mesh.SubMeshes.Length == 0) continue;

                    foreach (MeshSubResource sub in mesh.SubMeshes)
                    {
                        if (sub.IndexCount == 0 || sub.VertexBuffer.id == 0 || sub.IndexBuffer.id == 0)
                            continue;

                        sg_apply_bindings(new sg_bindings
                        {
                            vertex_buffers = { [0] = sub.VertexBuffer },
                            index_buffer = sub.IndexBuffer,
                        });

                        sg_draw(0, (uint)sub.IndexCount, 1);
                        drawCallCount++;
                    }
                }
            }
            finally
            {
                sg_end_pass();
            }
        }

        private PrimitiveShadowMesh GetOrCreatePrimitiveShadowMesh(string key)
        {
            if (_primitiveMeshes.TryGetValue(key, out var cached))
                return cached;

            PrimitiveShadowMesh created = default;
            if (!PrimitiveMeshSpec.TryParse(key, out var spec))
            {
                _primitiveMeshes[key] = created;
                return created;
            }

            var (verts, inds) = PrimitiveMeshGeometry.GetMeshTriangles(spec);
            if (verts.Length == 0 || inds.Length == 0)
            {
                _primitiveMeshes[key] = created;
                return created;
            }

            float[] packed = new float[verts.Length * 12];
            for (int i = 0; i < verts.Length; i++)
            {
                int o = i * 12;
                packed[o + 0] = verts[i].X;
                packed[o + 1] = verts[i].Y;
                packed[o + 2] = verts[i].Z;
            }

            created.VertexBuffer = sg_make_buffer(new sg_buffer_desc
            {
                data = SG_RANGE(packed),
                label = "shadow-prim-vbuf"
            });
            created.IndexBuffer = sg_make_buffer(new sg_buffer_desc
            {
                usage = new sg_buffer_usage { index_buffer = true },
                data = SG_RANGE(inds),
                label = "shadow-prim-ibuf"
            });
            created.IndexCount = inds.Length;

            _primitiveMeshes[key] = created;
            return created;
        }
    }
}