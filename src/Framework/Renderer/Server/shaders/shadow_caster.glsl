@ctype mat4 System.Numerics.Matrix4x4

@vs shadow_caster_vs
layout(binding=0) uniform shadow_caster_vs_params
{
    mat4 mvp;
};

layout(location=0) in vec3 in_pos;

void main()
{
    gl_Position = mvp * vec4(in_pos, 1.0);
}
@end

@fs shadow_caster_fs
void main()
{
}
@end

@program shadow_caster shadow_caster_vs shadow_caster_fs