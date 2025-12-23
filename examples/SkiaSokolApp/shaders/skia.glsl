//------------------------------------------------------------------------------
//  shaders for SkiaSharp sample with basic lighting
//------------------------------------------------------------------------------
@ctype mat4 mat44_t
@ctype vec3 vec3_t

@vs vs
layout(binding=0) uniform vs_params {
    mat4 mvp;
    mat4 model;
};

in vec4 position;
in vec2 texcoord0;
in vec3 normal;

layout(location=0) out vec2 uv;
layout(location=1) out vec3 world_normal;
layout(location=2) out vec3 world_pos;

void main() {
    vec4 world_position = model * position;
    gl_Position = mvp * position;
    uv = texcoord0;
    
    // Transform normal to world space
    mat3 normal_matrix = mat3(transpose(inverse(model)));
    world_normal = normalize(normal_matrix * normal);
    world_pos = world_position.xyz;
}
@end

@fs fs
layout(binding=0) uniform texture2D tex;
layout(binding=0) uniform sampler smp;

layout(binding=1) uniform fs_params {
    vec3 light_dir;
    vec3 view_pos;
};

layout(location=0) in vec2 uv;
layout(location=1) in vec3 world_normal;
layout(location=2) in vec3 world_pos;
out vec4 frag_color;

void main() {
    vec4 tex_color = texture(sampler2D(tex, smp), uv);
    
    vec3 N = normalize(world_normal);
    vec3 L = normalize(light_dir);
    vec3 V = normalize(view_pos - world_pos);
    vec3 H = normalize(L + V);
    
    // Ambient
    vec3 ambient = 0.7 * tex_color.rgb;
    
    // Diffuse
    float diff = max(dot(N, L), 0.0);
    vec3 diffuse = diff * tex_color.rgb;
    
    // Specular
    float spec = pow(max(dot(N, H), 0.0), 16.0);
    vec3 specular = vec3(0.2) * spec;
    
    vec3 result = ambient + diffuse + specular;
    frag_color = vec4(result, tex_color.a);
}
@end

@program skia vs fs