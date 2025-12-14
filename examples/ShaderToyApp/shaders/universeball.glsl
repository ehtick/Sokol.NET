//------------------------------------------------------------------------------
//  Universe Ball shader based on ShaderToy example
//  https://www.shadertoy.com/view/WcGcWV
//  License: Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported
//  Inspired by @Jaenam's gem shaders
//------------------------------------------------------------------------------

@vs vs
layout(binding=0) uniform vs_params {
    float aspect;
    float time;
};

in vec2 position;
out vec2 uv;

void main() {
    gl_Position = vec4(position, 0.0, 1.0);
    uv.x = position.x * aspect;
    uv.y = position.y;
}
@end

@fs fs
layout(binding=1) uniform fs_params {
    vec2 iResolution;
    float iTime;
    vec4 iMouse;
};

in vec2 uv;
out vec4 frag_color;

#define PALETTE vec3(6,4,2)

void main() {
    // OpenGL/WebGL: Y origin at bottom (matches ShaderToy)
    // Metal/D3D: Y origin at top (flip to match ShaderToy convention)
#if SOKOL_GLSL
    vec2 u = gl_FragCoord.xy;
#else
    vec2 u = vec2(gl_FragCoord.x, iResolution.y - gl_FragCoord.y);
#endif
    vec4 o = vec4(0.0);
    
    float n = 0.0, i = 0.0, s = 0.0, t = iTime * 0.2, d = 0.0, v = 0.0;
    vec3 q = vec3(0.0), p = vec3(iResolution, 0.0), c = vec3(0.0);
    
    u = (u + u - p.xy) / p.y;
    vec2 l = u - (u.yx * 0.9 + 0.3 - vec2(-0.35, 0.15));
    
    for(; i++ < 5e1 && d < 5e1;
        d += s = min(q.y = 0.01 + 0.6 * abs(24.0 - length(q.xy)),
                     v = max(s, dot(abs(fract(p) - 0.5), vec3(0.04)))),
        c += (1.0 + cos(p.z + PALETTE)) / v
          + d * vec3(5,2,1) / q.y / 1e1
          + 7.0 * vec3(3,4,1) / length(l)
    )
    {
        q = p = vec3(u * d, d - 16.0);
        s = length(p) - 8.0;
        p.xy *= mat2(cos(t + p.z * 0.6 + vec4(0,33,11,0)));
        p += cos(t + p.zxy) + cos(t + p.yzx * s) / s / 4.0;
        p += 0.5 * cos(t + dot(cos(t + p), p) * p);
        
        for(n = 0.02; n < 2.0; n *= 1.6) {
            q.y -= abs(dot(sin(4.0 * t + 0.3 * q / n), q - q + n));
        }
    }

    c = mix(c, c.yzx, smoothstep(2.0, 0.1, length(u) * 1.0));
    o.rgb = tanh(c * c / 6e7 / length(u - 0.3) + 0.1 * length(u));
    
    frag_color = o;
}
@end

@program universeball vs fs
