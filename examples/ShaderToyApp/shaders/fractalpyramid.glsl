// Fractal Pyramid
// https://www.shadertoy.com/view/tsXBzS
// License: Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License

@vs vs
layout(binding=0) uniform vs_params {
    float aspect;
    float time;
};

in vec4 position;
out vec2 uv;

void main() {
    gl_Position = position;
    uv = position.xy;
    uv.y *= aspect;
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

vec3 palette(float d){
    return mix(vec3(0.2,0.7,0.9),vec3(1.,0.,1.),d);
}

vec2 rotate(vec2 p,float a){
    float c = cos(a);
    float s = sin(a);
    return p*mat2(c,s,-s,c);
}

float map(vec3 p){
    for( int i = 0; i<8; ++i){
        float t = iTime*0.2;
        p.xz =rotate(p.xz,t);
        p.xy =rotate(p.xy,t*1.89);
        p.xz = abs(p.xz);
        p.xz-=.5;
    }
    return dot(sign(p),p)/5.;
}

vec4 rm (vec3 ro, vec3 rd){
    float t = 0.;
    vec3 col = vec3(0.);
    float d = 0.;
    for(float i =0.; i<64.; i++){
        vec3 p = ro + rd*t;
        d = map(p)*.5;
        if(d<0.02){
            break;
        }
        if(d>100.){
            break;
        }
        col+=palette(length(p)*.1)/(400.*(d));
        t+=d;
    }
    return vec4(col,1./(d*100.));
}

void main() {
    // OpenGL/WebGL: Y origin at bottom (matches ShaderToy)
    // Metal/D3D: Y origin at top (flip to match ShaderToy convention)
#if SOKOL_GLSL
    vec2 fragCoord = gl_FragCoord.xy;
#else
    vec2 fragCoord = vec2(gl_FragCoord.x, iResolution.y - gl_FragCoord.y);
#endif
    vec2 uv_local = (fragCoord-(iResolution.xy/2.))/iResolution.x;
    vec3 ro = vec3(0.,0.,-50.);
    ro.xz = rotate(ro.xz,iTime);
    vec3 cf = normalize(-ro);
    vec3 cs = normalize(cross(cf,vec3(0.,1.,0.)));
    vec3 cu = normalize(cross(cf,cs));
    
    vec3 uuv = ro+cf*3. + uv_local.x*cs + uv_local.y*cu;
    
    vec3 rd = normalize(uuv-ro);
    
    vec4 col = rm(ro,rd);
    
    frag_color = col;
}
@end

@program fractalpyramid vs fs
