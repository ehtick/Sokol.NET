//------------------------------------------------------------------------------
//  Stormy Torus shader based on Jaenam's ShaderToy example
//  https://www.shadertoy.com/view/tcccRl
//  License: Creative Commons (CC BY-NC-SA 4.0)
//  Author: Jaenam
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
// Ensure high precision on all backends to avoid artifacts
precision highp float;
precision highp int;

layout(binding=1) uniform fs_params {
    vec2 iResolution;
    float iTime;
    vec4 iMouse;
};

in vec2 uv;
out vec4 frag_color;

void main() {
    // OpenGL/WebGL: Y origin at bottom (matches ShaderToy)
    // Metal/D3D: Y origin at top (flip to match ShaderToy convention)
#if SOKOL_GLSL
    vec2 I = gl_FragCoord.xy;
#else
    vec2 I = vec2(gl_FragCoord.x, iResolution.y - gl_FragCoord.y);
#endif
    vec4 O = vec4(0.0);
    
    float i = 0.0, d = 0.0, w = 0.0, t = iTime, m = 1.0;
    vec3 p = vec3(0.0), k, r = vec3(iResolution, 0.0), Z = vec3(0.0);
     
    for(O *= i; 
        i++ < 1e2 && abs(p.x) < 6.0; 
        
        d += w = 0.01 + 0.07 * abs(max(mix(sin(length(ceil(4.0*k.z) + k)), 
              sin(length(p) - 1.0), 
              smoothstep(5.0, 5.5, p.y)
              ), sqrt(dot(k,k) + 16.0 - 8.0*length(k.xy)) - 1.5
                                   ) - i/150.0),
        O += max(1.3/w * sin(vec4(1,2,3,1) + i*0.5), -length(k*k))
      )
    {
        k = vec3((I+I-r.xy)/r.y * d, d - 10.0);
        k.xz *= mat2(cos(sin(t/2.0)*0.785 + vec4(0,33,11,0)));

        if(k.y < -6.3) {
            k.y = -k.y - 9.0;
            m = 0.5;
        }
        
        p = k * 0.5;
        
        for(w = 0.01; w < 0.2; w += w) {
            p.yz += cos(p.xy*0.01) - abs(dot(sin(0.02*p.z + 0.03*p.y + t+t + 0.3*p/w), w + Z));
        }
    }
    
    frag_color = vec4(tanh(O*O/1e6)*m);
}
@end

@program stormytorus vs fs
