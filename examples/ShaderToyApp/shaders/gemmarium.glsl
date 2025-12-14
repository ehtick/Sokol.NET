@ctype mat4 System.Numerics.Matrix4x4
@ctype vec2 System.Numerics.Vector2
@ctype vec3 System.Numerics.Vector3
@ctype vec4 System.Numerics.Vector4


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

/*================================
=           Gemmarium            =
=         Author: Jaenam         =
================================*/
// Date:    2025-11-28
// License: Creative Commons (CC BY-NC-SA 4.0)
//https://www.shadertoy.com/view/WftcWs

//Twigl (golfed) version --> https://x.com/Jaenam97/status/1994387530024718563?s=20
void main()
{   
    // OpenGL/WebGL: Y origin at bottom (matches ShaderToy)
    // Metal/D3D: Y origin at top (flip to match ShaderToy convention)
#if SOKOL_GLSL
    vec2 fragCoord = gl_FragCoord.xy;
#else
    vec2 fragCoord = vec2(gl_FragCoord.x, iResolution.y - gl_FragCoord.y);
#endif

    //Raymarch iterator
    float i = 0.0;
    //Depth
    float d = 0.0;
    //Raymarch step distance
    float s;
    // SDF
    float sd;
    // Noise iterator
    float n;
    // Time
    float t = iTime;
    // Brightness
    float m = 1.0;
    //Orb
    float l;

    // 3D sample point
    vec3 p;
    vec3 k;
    vec3 r = vec3(iResolution, 0.0);
    
    vec2 I = fragCoord;

    // Rotation matrix by pi/4
    mat2 R = mat2(cos(sin(t/2.0)*0.785 + vec4(0,33,11,0)));

    // Raymarch loop. Clear frag_color and raymarch 100 steps
    frag_color = vec4(0.0);
    for(i = 0.0; i < 100.0; i++){

        //Raymarch sample point --> scaled uvs + camera depth
        p = vec3((I+I-r.xy)/iResolution.y, d-10.0);    
        
        //Orb
        l = length(p.xy-vec2(0.2+sin(t)/4.0, 0.3+sin(t+t)/6.0));
        
        p.xy*=d;
        
        //Improving performance
        if(abs(p.x)>6.0) break;

        //Rotate about y-axis
        p.xz *= R;

        //Mirrored floor hack
        if(p.y < -6.3) {
            //Flip about y and shift
            p.y = -p.y-9.0;
            //Use half the brightness
            m = 0.5;
        }

        //Save sample point
        k=p;
        //Scale
        p*=0.5;
        //Turbulence loop (3D noise)
        for(n = 0.01; n < 1.0; n += n){

            //Accumulate noise on p.y 
            p.y += 0.9+abs(dot(sin(p.x + 2.0*t+p/n),  0.2+p-p )) * n;
        }
        //SDF mix
        sd = mix(
                 //Bottom half texture
                 sin(length(ceil(k*8.0).x+k)), 
                 //Upper half water/clouds noise + orb
                 mix(sin(length(p)-0.2),l,0.3-l),
                 //Blend
                 smoothstep(5.5, 6.0, p.y));

        //Step distance to object
        s = 0.012+0.08*abs(max(sd,length(k)-5.0)-i/150.0);
        d += s;
        
        // Uncomment section for ocean variant
        //vec4 ir = sin(vec4(1,2,3,1)+i*0.5)*1.5/s + vec4(1,2,3,1)*0.04/l; //iridescence + orb
        //vec4 c = vec4(1,2,3,1) * 0.12/s; //water 

        //frag_color += max(mix(ir,mix(c, ir, smoothstep(7.5, 8.5, p.y)),smoothstep(5.2, 6.5, p.y)), -length(k*k));
        
        //Color accumulation, using i iterator for iridescence. Attenuating with distance s and shading.
        frag_color += max(sin(vec4(1,2,3,1)+i*0.5)*1.5/s+vec4(1,2,3,1)*0.04/l,-length(k*k));

    }
    //Tanh tonemap and brightness multiplier
    frag_color = tanh(frag_color*frag_color/8e5)*m;  
}
@end

@program gemmarium vs fs
