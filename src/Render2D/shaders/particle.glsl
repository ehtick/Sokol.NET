// particle.glsl — Sokol.Render2D GPU-instanced 2D particles (docs/RENDER2D.md §6.2).
// One instanced unit-quad pipeline: vbuf[0] = static [-1,1] quad corners, vbuf[1] = a per-particle
// instance stream {pos,size,rot,rgba,uvRect,mode}. The fragment shape is chosen PER INSTANCE
// (mode), so glow/disc/triangle particles from many systems batch into one draw (texture + blend are
// per-batch). Drawn into the offscreen scene target, above the scene, below the NanoVG HUD.

@vs particle_vs

layout(binding=0) uniform particle_params {
    vec4 viewport;   // .xy = framebuffer size in pixels (W, H)
};

in vec2  corner;     // static unit quad in [-1,1]  (vbuf 0)
in vec2  i_pos;      // instance: centre, screen px  (vbuf 1)
in vec2  i_size;     // instance: half-extent (x,y); x≫y = a streak (LineTrail)
in float i_rot;      // instance: rotation (radians)
in vec4  i_color;    // instance: straight RGBA
in vec4  i_uvrect;   // instance: texture sub-rect (xy=min, zw=max)
in float i_mode;     // instance: 0 Glow · 1 Disc · 2 Triangle · 3 Texture · 4 Ring · 5 Star

out vec2  v_local;   // quad-local [-1,1]
out vec2  v_texuv;
out vec4  v_color;
out float v_mode;

void main()
{
    float s = sin(i_rot), c = cos(i_rot);
    vec2  sc  = vec2(corner.x * i_size.x, corner.y * i_size.y);
    vec2  rot = vec2(sc.x * c - sc.y * s, sc.x * s + sc.y * c);
    vec2  pix = i_pos + rot;
    gl_Position = vec4(pix.x / viewport.x * 2.0 - 1.0, 1.0 - pix.y / viewport.y * 2.0, 0.0, 1.0);

    v_local = corner;
    v_texuv = mix(i_uvrect.xy, i_uvrect.zw, corner * 0.5 + 0.5);
    v_color = i_color;
    v_mode  = i_mode;
}

@end

@fs particle_fs

layout(binding=0) uniform texture2D part_tex;
layout(binding=0) uniform sampler   part_smp;

in vec2  v_local;
in vec2  v_texuv;
in vec4  v_color;
in float v_mode;
out vec4 frag_color;

void main()
{
    int  m   = int(v_mode + 0.5);
    vec4 col = v_color;

    if (m == 3) {                              // Texture: full vertexColor × sprite
        col *= texture(sampler2D(part_tex, part_smp), v_texuv);
    } else {
        float r = length(v_local);
        float a;
        if (m == 1)      a = 1.0 - smoothstep(0.85, 1.0, r);                 // Disc: soft-edged circle
        else if (m == 2) a = step(abs(v_local.x), (v_local.y + 1.0) * 0.5);  // Triangle: apex-up mask
        else if (m == 4) a = smoothstep(0.55, 0.72, r) * (1.0 - smoothstep(0.88, 1.0, r));  // Ring: hollow band
        else if (m == 5) {                                                  // Star: 5-point
            float ang  = atan(v_local.y, v_local.x);
            float lobe = 0.5 + 0.5 * cos(ang * 5.0);                         // 5 lobes 0..1
            float edge = mix(0.42, 1.0, lobe);
            a = 1.0 - smoothstep(edge - 0.07, edge, r);
        }
        else { a = clamp(1.0 - r, 0.0, 1.0); a *= a; }                       // Glow: quadratic radial falloff
        col.a *= a;
    }

    if (col.a < 0.003) discard;
    frag_color = col;
}

@end

@program particle particle_vs particle_fs
