@ctype mat4 System.Numerics.Matrix4x4

@vs shadow_caster_vs
#ifndef MAX_BONES
#define MAX_BONES 128   // must match RenderingConstants.MAX_BONES
#endif

layout(binding=0) uniform shadow_caster_vs_params
{
    // Non-skinned: mvp = model * lightViewProj.
    // Skinned:     mvp = lightViewProj only (model is applied separately below).
    mat4 mvp;
#ifdef SKINNING
    mat4 model;
    mat4 finalBonesMatrices[MAX_BONES];
#endif
};

layout(location=0) in vec3 in_pos;
#ifdef SKINNING
// Locations 1/2 (NOT 9/10 as in pbr.glsl): the shadow VS has no instance attrs, so sokol's
// "attrs must be continuous" rule requires 0,1,2 with no gaps. The pipeline maps these to the
// 80 B SkinnedVertex offsets 48 (joints) and 64 (weights).
layout(location=1) in vec4 joints_0;
layout(location=2) in vec4 weights_0;
#endif

void main()
{
#ifdef SKINNING
    mat4 skin = weights_0.x * finalBonesMatrices[int(joints_0.x)]
              + weights_0.y * finalBonesMatrices[int(joints_0.y)]
              + weights_0.z * finalBonesMatrices[int(joints_0.z)]
              + weights_0.w * finalBonesMatrices[int(joints_0.w)];
    if (skin == mat4(0)) skin = mat4(1);
    gl_Position = mvp * model * skin * vec4(in_pos, 1.0);
#else
    gl_Position = mvp * vec4(in_pos, 1.0);
#endif
}
@end

@fs shadow_caster_fs
void main()
{
}
@end

@program shadow_caster shadow_caster_vs shadow_caster_fs
