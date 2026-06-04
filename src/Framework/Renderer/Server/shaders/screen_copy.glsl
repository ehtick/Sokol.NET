// screen_copy.glsl
// Exact-passthrough fullscreen blit: samples one color texture and writes it
// unchanged. Used by the transmission path to capture the opaque scene into a
// sampleable "screen color" texture (you cannot sample the attachment you are
// drawing into). Deliberately NOT the tonemap pass — that one always re-encodes
// sRGB, which would double-encode an already display-ready RGBA8 scene target.
// Vertex stage is a single full-screen triangle (no vertex buffer needed).

// ── Vertex: clip-space fullscreen triangle from gl_VertexIndex ─────────────
@vs screen_copy_vs

void main()
{
    vec2 uv  = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
    gl_Position = vec4(uv * 2.0 - 1.0, 0.0, 1.0);
}

@end

// ── Fragment: straight copy ─────────────────────────────────────────────────
@fs screen_copy_fs

layout(binding=0) uniform texture2D src_tex;
layout(binding=0) uniform sampler   src_smp;

out vec4 frag_color;

void main()
{
    // Reconstruct UV from window-space fragment position (backend-orientation safe,
    // mirrors tonemap_pass.glsl) so the copy preserves the source's texel layout.
    vec2 uv = gl_FragCoord.xy / vec2(textureSize(sampler2D(src_tex, src_smp), 0));
    frag_color = texture(sampler2D(src_tex, src_smp), uv);
}

@end

@program screen_copy screen_copy_vs screen_copy_fs
