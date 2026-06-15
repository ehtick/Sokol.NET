// bloom_extract.glsl — Sokol.Render2D bloom bright-pass (docs/RENDER2D.md §6.4).
// Reads the rendered scene+particles target and keeps only the bright energy (soft knee above a
// threshold), pre-multiplied by the bloom intensity, into a bloom buffer to be blurred. Full-res +
// gl_FragCoord/textureSize so it's backend-orientation-safe (mirrors blit.glsl).

@vs bloom_extract_vs
void main()
{
    vec2 uv = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
    gl_Position = vec4(uv * 2.0 - 1.0, 0.0, 1.0);
}
@end

@fs bloom_extract_fs
layout(binding=0) uniform texture2D bloom_src;
layout(binding=0) uniform sampler   bloom_smp;
layout(binding=0) uniform bloom_extract_params { vec4 ex; };   // ex.x = threshold, ex.y = intensity

out vec4 frag_color;

void main()
{
    vec2 uv = gl_FragCoord.xy / vec2(textureSize(sampler2D(bloom_src, bloom_smp), 0));
    vec3 c  = texture(sampler2D(bloom_src, bloom_smp), uv).rgb;
    float luma = max(max(c.r, c.g), c.b);
    float knee = max(luma - ex.x, 0.0) / max(luma, 1e-4);      // soft knee above the threshold
    frag_color = vec4(c * knee * ex.y, 1.0);
}
@end

@program bloom_extract bloom_extract_vs bloom_extract_fs
