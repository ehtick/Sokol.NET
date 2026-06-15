// blit.glsl — Sokol.Render2D fullscreen composite (Strategy C, see docs/RENDER2D.md §5).
// Copies the single-sampled offscreen scene target into the active swapchain pass, unchanged,
// BEFORE the NanoVG HUD draws on top. Mirrors src/Framework .../screen_copy.glsl — the same
// offscreen→swapchain blit the (GPU-verified on all 6 backends) transmission path uses, so
// backend Y-orientation is already solved. Vertex stage is one fullscreen triangle (no vbuf).

// ── Vertex: clip-space fullscreen triangle from gl_VertexIndex ─────────────────
@vs blit_vs

void main()
{
    vec2 uv = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
    gl_Position = vec4(uv * 2.0 - 1.0, 0.0, 1.0);
}

@end

// ── Fragment: straight copy (uv reconstructed from window position = orientation-safe) ─────
@fs blit_fs

layout(binding=0) uniform texture2D src_tex;
layout(binding=0) uniform sampler   src_smp;

out vec4 frag_color;

void main()
{
    vec2 uv = gl_FragCoord.xy / vec2(textureSize(sampler2D(src_tex, src_smp), 0));
    frag_color = texture(sampler2D(src_tex, src_smp), uv);
}

@end

@program blit blit_vs blit_fs
