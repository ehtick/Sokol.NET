// tonemap_pass.glsl
// Fullscreen HDR-to-display pass: reads an RGBA16F offscreen colour attachment,
// applies exposure + tone map operator, and writes sRGB output.
// Vertex stage uses a single full-screen triangle (no vertex buffer needed).
// The UBO binding=0 must not conflict with any other resource in the same pass;
// this shader is always used in its own isolated pass, so binding=0 is safe.

@ctype mat4 System.Numerics.Matrix4x4

// ── Vertex: clip-space fullscreen triangle from gl_VertexIndex ─────────────
@vs tonemap_vs

void main()
{
    // Generates a triangle that covers the entire NDC clip volume.
    // Vertex 0: (-1,-1)  Vertex 1: (3,-1)  Vertex 2: (-1, 3)
    vec2 uv  = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
    gl_Position = vec4(uv * 2.0 - 1.0, 0.0, 1.0);
}

@end

// ── Fragment: tone map ──────────────────────────────────────────────────────
@fs tonemap_fs

layout(binding=0) uniform tonemap_params {
    float u_Exposure;
    int   u_Type;    // 0=linear, 1=ACES_Narkowicz, 2=ACES_Hill, 3=KHR_PBR_Neutral
    float _pad0;
    float _pad1;
};

layout(binding=0) uniform texture2D hdr_tex;
layout(binding=0) uniform sampler   hdr_smp;

out vec4 frag_color;

// ── Tone map operators ──────────────────────────────────────────────────────

const float GAMMA     = 2.2;
const float INV_GAMMA = 1.0 / GAMMA;

vec3 linearToSRGB(vec3 c)
{
    return pow(max(c, vec3(0.0)), vec3(INV_GAMMA));
}

vec3 toneMapACES_Narkowicz(vec3 c)
{
    const float A = 2.51, B = 0.03, C = 2.43, D = 0.59, E = 0.14;
    return clamp((c * (A * c + B)) / (c * (C * c + D) + E), 0.0, 1.0);
}

const mat3 ACESInputMat = mat3(
     0.59719,  0.07600,  0.02840,
     0.35458,  0.90834,  0.13383,
     0.04823,  0.01566,  0.83777);

const mat3 ACESOutputMat = mat3(
     1.60475, -0.10208, -0.00327,
    -0.53108,  1.10813, -0.07276,
    -0.07367, -0.00605,  1.07602);

vec3 RRTAndODTFit(vec3 c)
{
    vec3 a = c * (c + 0.0245786) - 0.000090537;
    vec3 b = c * (0.983729 * c + 0.4329510) + 0.238081;
    return a / b;
}

vec3 toneMapACES_Hill(vec3 c)
{
    c = ACESInputMat * c;
    c = RRTAndODTFit(c);
    c = ACESOutputMat * c;
    return clamp(c, 0.0, 1.0);
}

vec3 toneMapKhrPbrNeutral(vec3 c)
{
    const float startCompression = 0.8 - 0.04;
    const float desaturation     = 0.15;
    float x = min(c.r, min(c.g, c.b));
    float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
    c -= offset;
    float peak = max(c.r, max(c.g, c.b));
    if (peak < startCompression) return c;
    const float d = 1.0 - startCompression;
    float newPeak = 1.0 - d * d / (peak + d - startCompression);
    c *= newPeak / peak;
    float g = 1.0 - 1.0 / (desaturation * (peak - newPeak) + 1.0);
    return mix(c, newPeak * vec3(1.0), g);
}

void main()
{
    // Reconstruct UV from fragment position.
    vec2 uv = gl_FragCoord.xy / vec2(textureSize(sampler2D(hdr_tex, hdr_smp), 0));
    vec3 color = texture(sampler2D(hdr_tex, hdr_smp), uv).rgb;

    color *= u_Exposure;

    if (u_Type == 1)      color = toneMapACES_Narkowicz(color);
    else if (u_Type == 2) color = toneMapACES_Hill(color);
    else if (u_Type == 3) color = toneMapKhrPbrNeutral(color);
    // u_Type == 0: linear (exposure only, no curve)

    frag_color = vec4(linearToSRGB(color), 1.0);
}

@end

@program tonemap_pass tonemap_vs tonemap_fs
