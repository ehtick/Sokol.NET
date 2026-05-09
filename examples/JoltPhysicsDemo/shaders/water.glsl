@ctype mat4 mat44_t
@ctype vec3 vec3_t
@ctype vec4 vec4_t

// Water surface shader — Fresnel, Blinn-Phong specular, animated shimmer.
// Vertex layout matches ConeVertex: position (float3) + normal (float3), stride=24.
// Per-vertex normals are pre-computed in DemoBase.AddDebugTri (reliable cross-product);
// using them here avoids Metal screen-space derivative axis-convention issues.

@vs vs_water
layout(binding=0) uniform water_vs_params {
    mat4 vp;
};

layout(location=0) in vec3 position;
layout(location=1) in vec3 normal;

out vec3 world_pos;
out vec3 v_normal;

void main() {
    gl_Position = vp * vec4(position, 1.0);
    world_pos   = position;
    v_normal    = normal;
}
@end

@fs fs_water
in vec3 world_pos;
in vec3 v_normal;
out vec4 frag_color;

layout(binding=1) uniform water_fs_params {
    vec4 view_pos_time;   // xyz = camera eye position, w = elapsed time
    vec4 light_dir;       // xyz = light direction (world space), w = unused
    vec4 water_color;     // xyz = deep water color, w = unused
};

void main() {
    vec3  N         = normalize(v_normal);
    vec3  view_pos  = view_pos_time.xyz;
    float time      = view_pos_time.w;
    vec3  light     = normalize(light_dir.xyz);
    vec3  water_col = water_color.xyz;

    vec3 V = normalize(view_pos - world_pos);
    vec3 H = normalize(light + V);

    // Schlick Fresnel: overhead = deep water color; grazing = sky/horizon tint
    float cosTheta = max(dot(V, N), 0.0);
    float fresnel  = pow(1.0 - cosTheta, 3.0);

    // Mix deep water toward a horizon sky color at grazing angles
    vec3 sky  = vec3(0.55, 0.75, 0.92);
    vec3 base = mix(water_col, sky, fresnel * 0.55);

    // Hemisphere ambient (sky top vs ground bottom gradient)
    float hemi   = N.y * 0.5 + 0.5;
    vec3 ambient = base * mix(vec3(0.08, 0.14, 0.30), vec3(0.35, 0.52, 0.72), hemi);

    // Diffuse (key light)
    float diff   = max(dot(N, light), 0.0);
    vec3 diffuse = diff * base * 0.60;

    // Fill light (low-angle bounce from opposite side)
    vec3 Lf   = normalize(vec3(-light.x, -0.3, -light.z));
    vec3 fill = max(dot(N, Lf), 0.0) * 0.12 * base;

    // Sharp sun specular sparkle (high exponent — water is highly reflective)
    float spec    = pow(max(dot(H, N), 0.0), 256.0);
    vec3 specular = vec3(1.0, 0.97, 0.88) * spec * 1.8;

    // Animated shimmer: two overlapping sine-ripple waves modulate brightness
    float sa      = sin(world_pos.x * 3.1 + time * 5.2) * sin(world_pos.z * 2.7 + time * 4.3);
    float sb      = sin(world_pos.x * 1.7 - time * 3.8) * sin(world_pos.z * 4.1 - time * 6.1);
    float shimmer = (sa + sb) * 0.025 + 1.0;

    vec3 final_color = (ambient + diffuse + fill + specular) * shimmer;
    frag_color = vec4(final_color, 0.82);
}
@end

@program water vs_water fs_water
