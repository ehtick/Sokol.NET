// bloom_blur.glsl — Sokol.Render2D separable Gaussian blur for bloom (docs/RENDER2D.md §6.4).
// One 9-tap pass along the axis given by `dir` (a UV-space texel step × direction). Run twice per
// iteration (horizontal then vertical). Full-res + gl_FragCoord/textureSize (orientation-safe).

@vs bloom_blur_vs
void main()
{
    vec2 uv = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
    gl_Position = vec4(uv * 2.0 - 1.0, 0.0, 1.0);
}
@end

@fs bloom_blur_fs
layout(binding=0) uniform texture2D bloom_src;
layout(binding=0) uniform sampler   bloom_smp;
layout(binding=0) uniform bloom_blur_params { vec4 dir; };   // dir.xy = UV step per tap × axis

out vec4 frag_color;

void main()
{
    vec2 uv = gl_FragCoord.xy / vec2(textureSize(sampler2D(bloom_src, bloom_smp), 0));
    vec2 d  = dir.xy;
    vec3 s  = texture(sampler2D(bloom_src, bloom_smp), uv).rgb * 0.227027;
    s += texture(sampler2D(bloom_src, bloom_smp), uv + d * 1.0).rgb * 0.1945946;
    s += texture(sampler2D(bloom_src, bloom_smp), uv - d * 1.0).rgb * 0.1945946;
    s += texture(sampler2D(bloom_src, bloom_smp), uv + d * 2.0).rgb * 0.1216216;
    s += texture(sampler2D(bloom_src, bloom_smp), uv - d * 2.0).rgb * 0.1216216;
    s += texture(sampler2D(bloom_src, bloom_smp), uv + d * 3.0).rgb * 0.054054;
    s += texture(sampler2D(bloom_src, bloom_smp), uv - d * 3.0).rgb * 0.054054;
    s += texture(sampler2D(bloom_src, bloom_smp), uv + d * 4.0).rgb * 0.016216;
    s += texture(sampler2D(bloom_src, bloom_smp), uv - d * 4.0).rgb * 0.016216;
    frag_color = vec4(s, 1.0);
}
@end

@program bloom_blur bloom_blur_vs bloom_blur_fs
