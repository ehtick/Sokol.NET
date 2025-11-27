@ctype mat4 System.Numerics.Matrix4x4
@ctype vec4 System.Numerics.Vector4
@ctype vec3 System.Numerics.Vector3
@ctype vec2 System.Numerics.Vector2

@vs vs
in vec2 position;
in vec4 color0;

out vec4 color;

layout(binding=0) uniform vs_params {
    mat4 mvp;
};

void main() {
    gl_Position = mvp * vec4(position, 0.0, 1.0);
    color = color0;
}
@end

@fs fs
in vec4 color;
out vec4 frag_color;

void main() {
    frag_color = color;
}
@end

@program box2d vs fs
