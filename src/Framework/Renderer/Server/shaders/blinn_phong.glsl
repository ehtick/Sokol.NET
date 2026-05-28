// Blinn-Phong shader for RenderingServer — M1.
//
// Vertex buffer 0 (ObjVertex, 48 B, per-vertex):
//   loc 0  in_pos      vec3  (position)
//   loc 1  in_normal   vec3
//   loc 2  in_uv       vec2
//   loc 3  in_tangent  vec4  (xyz = tangent, w = bitangent sign)
//
// Vertex buffer 1 (InstanceData, 80 B, per-instance):
//   loc 4-7  in_model_{0..3}  vec4  (row-major mat4)
//   loc 8    in_custom        vec4  (tint.rgb, lod_fade)
//
// Uniform blocks:
//   binding 0 (VS): blinn_phong_vs_params { mat4 view_proj; vec4 camera_pos; }
//   binding 1 (FS): blinn_phong_fs_params { material + light data }
//
// Textures (FS):
//   binding 0: diffuse_map   + diffuse_smp
//   binding 1: specular_map  + specular_smp
//   binding 2: normal_map    + normal_smp
//   binding 3: opacity_map   + opacity_smp

@ctype mat4 System.Numerics.Matrix4x4
@ctype vec4 System.Numerics.Vector4
@ctype vec3 System.Numerics.Vector3
@ctype vec2 System.Numerics.Vector2

// ─── Vertex Shader ────────────────────────────────────────────────────────────

@vs blinn_phong_vs

layout(binding=0) uniform blinn_phong_vs_params {
    mat4 view_proj;
    vec4 camera_pos;   // xyz = world-space camera position
};

// Per-vertex (buffer slot 0)
layout(location=0) in vec3 in_pos;
layout(location=1) in vec3 in_normal;
layout(location=2) in vec2 in_uv;
layout(location=3) in vec4 in_tangent;   // xyz = tangent direction, w = bitangent sign

// Per-instance (buffer slot 1)
layout(location=4) in vec4 in_model_0;
layout(location=5) in vec4 in_model_1;
layout(location=6) in vec4 in_model_2;
layout(location=7) in vec4 in_model_3;
layout(location=8) in vec4 in_custom;    // rgb = tint, a = lod_fade

out vec3 fs_world_pos;
out vec3 fs_world_normal;
out vec3 fs_world_tangent;
out vec3 fs_world_bitangent;
out vec2 fs_uv;
out vec4 fs_custom;

void main() {
    mat4 model = mat4(in_model_0, in_model_1, in_model_2, in_model_3);

    vec4 wpos    = model * vec4(in_pos, 1.0);
    gl_Position  = view_proj * wpos;

    // Normal matrix = transpose(inverse(mat3(model))).
    // For uniform-scale objects this equals mat3(model), but we compute it fully.
    mat3 nm = transpose(inverse(mat3(model)));

    fs_world_pos       = wpos.xyz;
    fs_world_normal    = normalize(nm * in_normal);
    vec3 wt            = normalize(nm * in_tangent.xyz);
    fs_world_tangent   = wt;
    fs_world_bitangent = normalize(cross(fs_world_normal, wt) * in_tangent.w);
    fs_uv              = in_uv;
    fs_custom          = in_custom;
}
@end

// ─── Fragment Shader ──────────────────────────────────────────────────────────

@fs blinn_phong_fs

// Layout: material params + packed light array.
// GpuLight = 4 vec4s (64 B) → MAX_LIGHTS(32) * 4 = 128 vec4s.
layout(binding=1) uniform blinn_phong_fs_params {
    vec4 material_kd_alpha;   // xyz = Kd (diffuse colour), w = opacity
    vec4 material_ks_shin;    // xyz = Ks (specular colour), w = Ns (shininess)
    vec4 ambient_num;         // xyz = ambient colour, w = float(numLights)
    vec4 cam_pos_pad;         // xyz = camera world position (for spec), w = pad
    vec4 lights_data[128];    // 32 lights × 4 vec4s
};

// Textures + separate samplers (sokol-shdc's cross-compilation model).
layout(binding=0) uniform texture2D diffuse_map;
layout(binding=1) uniform texture2D specular_map;
layout(binding=2) uniform texture2D normal_map;
layout(binding=3) uniform texture2D opacity_map;

layout(binding=0) uniform sampler diffuse_smp;
layout(binding=1) uniform sampler specular_smp;
layout(binding=2) uniform sampler normal_smp;
layout(binding=3) uniform sampler opacity_smp;

in vec3 fs_world_pos;
in vec3 fs_world_normal;
in vec3 fs_world_tangent;
in vec3 fs_world_bitangent;
in vec2 fs_uv;
in vec4 fs_custom;

out vec4 frag_color;

// Unpack normal from tangent-space normal map (RGB → [-1,1]).
vec3 sample_normal(vec2 uv, vec3 N, vec3 T, vec3 B) {
    vec3 n_ts = texture(sampler2D(normal_map, normal_smp), uv).xyz * 2.0 - 1.0;
    // Transform from tangent space to world space.
    return normalize(T * n_ts.x + B * n_ts.y + N * n_ts.z);
}

void main() {
    // --- material fetch ---
    vec4  diff_tex  = texture(sampler2D(diffuse_map,  diffuse_smp),  fs_uv);
    float spec_tex  = texture(sampler2D(specular_map, specular_smp), fs_uv).r;
    float opac_tex  = texture(sampler2D(opacity_map,  opacity_smp),  fs_uv).r;

    vec3  Kd        = material_kd_alpha.xyz * diff_tex.rgb * fs_custom.rgb;
    vec3  Ks        = material_ks_shin.xyz  * spec_tex;
    float Ns        = max(material_ks_shin.w, 1.0);
    float opacity   = material_kd_alpha.w   * opac_tex;

    // Alpha test (discard nearly transparent fragments).
    if (opacity < 0.05) discard;

    // --- normal ---
    vec3 N = sample_normal(fs_uv, fs_world_normal, fs_world_tangent, fs_world_bitangent);
    vec3 V = normalize(cam_pos_pad.xyz - fs_world_pos);

    // --- lighting accumulation ---
    vec3 ambient = ambient_num.xyz * Kd;
    vec3 accum   = ambient;
    int  count   = min(int(ambient_num.w), 32);

    for (int i = 0; i < count; i++) {
        vec4 d0 = lights_data[i * 4 + 0];  // xyz=pos,      w=type
        vec4 d1 = lights_data[i * 4 + 1];  // xyz=dir,      w=range
        vec4 d2 = lights_data[i * 4 + 2];  // xyz=color,    w=intensity
        vec4 d3 = lights_data[i * 4 + 3];  // x=innerCos, y=outerCos

        vec3  lc  = d2.xyz * d2.w;
        int   lt  = int(d0.w);

        vec3 L = vec3(0.0);
        float atten = 1.0;

        if (lt == 0) {
            // Directional — d1.xyz = travel direction (away from source)
            L = normalize(-d1.xyz);

        } else if (lt == 1) {
            // Point
            vec3  toL = d0.xyz - fs_world_pos;
            float dist = length(toL);
            float rng  = d1.w;
            if (dist >= rng || rng <= 0.0) continue;
            L     = normalize(toL);
            float t = dist / rng;
            atten = max(1.0 - t * t, 0.0);

        } else {
            // Spot
            vec3  toL = d0.xyz - fs_world_pos;
            float dist = length(toL);
            float rng  = d1.w;
            if (dist >= rng || rng <= 0.0) continue;
            L     = normalize(toL);
            float t    = dist / rng;
            atten      = max(1.0 - t * t, 0.0);
            vec3 sd    = normalize(d1.xyz);
            float cosA = dot(-L, sd);
            float sf   = clamp((cosA - d3.y) / max(d3.x - d3.y, 0.0001), 0.0, 1.0);
            atten *= sf;
        }

        // Diffuse (Lambert)
        float NdotL = max(dot(N, L), 0.0);

        // Specular (Blinn-Phong)
        vec3  H     = normalize(L + V);
        float NdotH = max(dot(N, H), 0.0);
        float spec  = pow(NdotH, Ns);

        accum += lc * atten * (Kd * NdotL + Ks * spec);
    }

    frag_color = vec4(accum, opacity);
}
@end

@program blinn_phong blinn_phong_vs blinn_phong_fs
