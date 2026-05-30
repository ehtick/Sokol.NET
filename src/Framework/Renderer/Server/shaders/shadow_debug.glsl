// shadow_debug.glsl
// Debug visualization: renders one slice of the shadow atlas (depth2d_array)
// as a greyscale quad. Uses the shadow comparison sampler so Metal emits
// depth2d_array<float>, which is required for SG_PIXELFORMAT_DEPTH textures.
// The comparison reference (ref_z) is set to show geometry as white on black.

@ctype vec2 System.Numerics.Vector2

@vs shadow_debug_vs
layout(location=0) in vec2 in_pos;  // quad positions in [0,1]×[0,1]
out vec2 fs_uv;

void main()
{
    fs_uv = in_pos;
    // Map [0,1] → NDC [-1,+1].
    gl_Position = vec4(in_pos * 2.0 - 1.0, 0.0, 1.0);
}
@end

@fs shadow_debug_fs
layout(binding=0) uniform shadow_debug_fs_params
{
    float slice;
    float ref_z;   // comparison reference; 0.9999 shows all geometry as white
    float _pad0;
    float _pad1;
};

layout(binding=0) uniform texture2DArray shadow_atlas;
layout(binding=0) uniform samplerShadow shadow_smp;

in vec2 fs_uv;
out vec4 frag_color;

void main()
{
    // Sampler uses LESS_EQUAL: passes (→ 1.0) when ref_z <= stored_depth.
    // At ref_z = 0.9999:
    //   background (stored = 1.0 clear)  → passes → 1.0
    //   geometry   (stored < 0.9999)     → fails  → 0.0
    // Invert so geometry = white (1.0), background = black (0.0).
    float result = texture(sampler2DArrayShadow(shadow_atlas, shadow_smp),
                           vec4(fs_uv.x, fs_uv.y, slice, ref_z));
    float vis = 1.0 - result;
    frag_color = vec4(vis, vis, vis, 1.0);
}
@end

@program shadow_debug shadow_debug_vs shadow_debug_fs
