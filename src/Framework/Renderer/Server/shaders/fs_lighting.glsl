// Lighting functions for the Framework PBR shader.
// Uses the same packed GpuLight[32] format as blinn_phong.glsl so LightBuffer.cs
// can fill it unchanged.
//
// GpuLight packed layout (4 vec4s, indices i*4+0 … i*4+3):
//   [i*4+0] PositionType   : xyz=worldPos  w=type (0=dir,1=point,2=spot)
//   [i*4+1] DirectionRange : xyz=dir       w=range
//   [i*4+2] ColorIntensity : xyz=color     w=intensity
//   [i*4+3] SpotShadow     : x=innerCos  y=outerCos  z=shadowIdx  w=pad

float get_range_attenuation(float range, float distance)
{
    if (range < 0.0) return 1.0;
    return max(min(1.0 - pow(distance / range, 4.0), 1.0), 0.0) / max(distance * distance, 1e-4);
}

// Returns vec4(lightDir.xyz, attenuation).
vec4 getLightDirectionAndAttenuation(int i, vec3 frag_pos)
{
    vec3  pos       = lights_data[i*4+0].xyz;
    float ltype     = lights_data[i*4+0].w;
    vec3  dir       = lights_data[i*4+1].xyz;
    float range     = lights_data[i*4+1].w;
    float innerCos  = lights_data[i*4+3].x;
    float outerCos  = lights_data[i*4+3].y;

    vec3  l = vec3(0.0);
    float att = 1.0;

    int ltype_i = int(ltype + 0.5);
    if (ltype_i == LIGHT_TYPE_DIRECTIONAL) {
        l = normalize(-dir);
    } else if (ltype_i == LIGHT_TYPE_POINT) {
        vec3 toLight = pos - frag_pos;
        float dist = length(toLight);
        l   = toLight / max(dist, 1e-6);
        att = get_range_attenuation(range, dist);
    } else if (ltype_i == LIGHT_TYPE_SPOT) {
        vec3 toLight = pos - frag_pos;
        float dist = length(toLight);
        l   = toLight / max(dist, 1e-6);
        att = get_range_attenuation(range, dist);
        float theta   = dot(-l, normalize(dir));
        float epsilon = innerCos - outerCos;
        float cone    = clamp((theta - outerCos) / max(epsilon, 1e-4), 0.0, 1.0);
        att *= cone;
    }
    return vec4(l, att);
}

// ─── CSM shadow sampling ──────────────────────────────────────────────────────
#if CSM_CASCADES > 0

// Returns a 0…1 shadow term for the directional cascade that covers view_depth.
float sampleCsmShadow(vec3 worldPos, float view_depth)
{
    // Choose cascade.
    int cascade = CSM_CASCADES - 1;
    for (int c = 0; c < CSM_CASCADES; c++) {
        if (view_depth < csm_split_depths[c]) { cascade = c; break; }
    }

    // Project into shadow clip space using the VS-passed cascade VP.
    // The VS already computed v_ShadowPos[c] for all cascades.
    // We re-project here from the array output of the VS.
    vec4 shadowCoord;
#if defined(CSM4)
    if      (cascade == 0) shadowCoord = v_ShadowPos[0];
    else if (cascade == 1) shadowCoord = v_ShadowPos[1];
    else if (cascade == 2) shadowCoord = v_ShadowPos[2];
    else                   shadowCoord = v_ShadowPos[3];
#elif defined(CSM2)
    if      (cascade == 0) shadowCoord = v_ShadowPos[0];
    else                   shadowCoord = v_ShadowPos[1];
#else
    shadowCoord = v_ShadowPos[0];
#endif

    vec3 ndc = shadowCoord.xyz / max(shadowCoord.w, 1e-5);
#if !SOKOL_GLSL
    ndc.y = -ndc.y;
    if (ndc.x < -1.0 || ndc.x > 1.0 || ndc.y < -1.0 || ndc.y > 1.0 ||
        ndc.z <  0.0 || ndc.z >  1.0) return 1.0;
#else
    if (ndc.x < -1.0 || ndc.x > 1.0 || ndc.y < -1.0 || ndc.y > 1.0 ||
        ndc.z < -1.0 || ndc.z >  1.0) return 1.0;
#endif

    vec2 uv = ndc.xy * 0.5 + 0.5;
#if SOKOL_GLSL
    float receiver = ndc.z * 0.5 + 0.5;
#else
    float receiver = ndc.z;
#endif
    float bias     = csm_bias;
    float slice    = float(cascade);

    vec2 texel = 1.0 / vec2(textureSize(sampler2DArray(shadow_atlas, shadow_atlas_smp), 0).xy);

    if (csm_pcf_taps >= 25) {
        float vis = 0.0;
        for (int y = -2; y <= 2; y++)
        for (int x = -2; x <= 2; x++) {
            vec2 s = uv + vec2(float(x), float(y)) * texel * 1.5;
            vis += texture(sampler2DArrayShadow(shadow_atlas, shadow_atlas_smp),
                           vec4(s, slice, receiver - bias));
        }
        return vis / 25.0;
    } else if (csm_pcf_taps >= 9) {
        float vis = 0.0;
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++) {
            vec2 s = uv + vec2(float(x), float(y)) * texel;
            vis += texture(sampler2DArrayShadow(shadow_atlas, shadow_atlas_smp),
                           vec4(s, slice, receiver - bias));
        }
        return vis / 9.0;
    } else {
        return texture(sampler2DArrayShadow(shadow_atlas, shadow_atlas_smp),
                       vec4(uv, slice, receiver - bias));
    }
}
#endif  // CSM_CASCADES > 0
