// scene.glsl — Sokol.Render2D batched 2D scene (see docs/RENDER2D.md §6.3).
// Colored triangles in screen-pixel space (origin top-left, matching Sokol.GUI.Renderer / the
// Camera's world→screen), transformed to clip space by the framebuffer size. The SgSceneRenderer
// tessellates every primitive (quad / rounded-quad / triangle / circle / rotated square / gradient)
// into this one vertex/colour stream and issues a single draw. No texture in M1 (vertex colour only).

@vs scene_vs

layout(binding=0) uniform scene_params {
    vec4 viewport;   // .xy = framebuffer size in pixels (W, H); .zw unused (std140 pad)
};

in vec2 pos;         // screen-pixel coordinates, origin top-left
in vec4 color0;

out vec4 color;

void main()
{
    // pixel → clip: x maps [0,W]→[-1,1]; y flips so pixel-top (y=0) → clip-top (+1).
    float x = pos.x / viewport.x * 2.0 - 1.0;
    float y = 1.0 - pos.y / viewport.y * 2.0;
    gl_Position = vec4(x, y, 0.0, 1.0);
    color = color0;
}

@end

@fs scene_fs

in vec4 color;
out vec4 frag_color;

void main()
{
    frag_color = color;
}

@end

@program scene scene_vs scene_fs
