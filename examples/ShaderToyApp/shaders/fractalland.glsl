// "Fractal Cartoon" - former "DE edge detection" by Kali
// https://www.shadertoy.com/view/XsBXWt
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

#define NYAN 
#define WAVES
#define BORDER

#define RAY_STEPS 150

#define BRIGHTNESS 1.2
#define GAMMA 1.4
#define SATURATION .65

#define detail .001
#define t iTime*.5

const vec3 origin=vec3(-1.,.7,0.);
float det=0.0;

// 2D rotation function
mat2 rot(float a) {
    return mat2(cos(a),sin(a),-sin(a),cos(a));    
}

// "Amazing Surface" fractal
vec4 formula(vec4 p) {
    p.xz = abs(p.xz+1.)-abs(p.xz-1.)-p.xz;
    p.y-=.25;
    p.xy*=rot(radians(35.));
    p=p*2./clamp(dot(p.xyz,p.xyz),.2,1.);
    return p;
}

// Distance function
float de(vec3 pos) {
#ifdef WAVES
    pos.y+=sin(pos.z-t*6.)*.15; //waves!
#endif
    float hid=0.;
    vec3 tpos=pos;
    tpos.z=abs(3.-mod(tpos.z,6.));
    vec4 p=vec4(tpos,1.);
    for (int i=0; i<4; i++) {p=formula(p);}
    float fr=(length(max(vec2(0.),p.yz-1.5))-1.)/p.w;
    float ro=max(abs(pos.x+1.)-.3,pos.y-.35);
          ro=max(ro,-max(abs(pos.x+1.)-.1,pos.y-.5));
    pos.z=abs(.25-mod(pos.z,.5));
          ro=max(ro,-max(abs(pos.z)-.2,pos.y-.3));
          ro=max(ro,-max(abs(pos.z)-.01,-pos.y+.32));
    float d=min(fr,ro);
    return d;
}

// Camera path
vec3 path(float ti) {
    ti*=1.5;
    vec3  p=vec3(sin(ti),(1.-sin(ti*2.))*.5,-ti*5.)*.5;
    return p;
}

// Calc normals, and here is edge detection, set to variable "edge"
float edge=0.;
vec3 normal(vec3 p) { 
    vec3 e = vec3(0.0,det*5.,0.0);

    float d1 = de(p-e.yxx), d2 = de(p+e.yxx);
    float d3 = de(p-e.xyx), d4 = de(p+e.xyx);
    float d5 = de(p-e.xxy), d6 = de(p+e.xxy);
    float d = de(p);
    edge=abs(d-0.5*(d2+d1))+abs(d-0.5*(d4+d3))+abs(d-0.5*(d6+d5));//edge finder
    edge=min(1.,pow(edge,.55)*15.);
    return normalize(vec3(d1-d2,d3-d4,d5-d6));
}

// Simplified rainbow without texture
vec4 rainbow(vec2 p) {
    float q = max(p.x,-0.1);
    float s = sin(p.x*7.0+t*70.0)*0.08;
    p.y+=s;
    p.y*=1.1;
    
    vec4 c;
    if (p.x>0.0) c=vec4(0,0,0,0); else
    if (0.0/6.0<p.y&&p.y<1.0/6.0) c= vec4(255,43,14,255)/255.0; else
    if (1.0/6.0<p.y&&p.y<2.0/6.0) c= vec4(255,168,6,255)/255.0; else
    if (2.0/6.0<p.y&&p.y<3.0/6.0) c= vec4(255,244,0,255)/255.0; else
    if (3.0/6.0<p.y&&p.y<4.0/6.0) c= vec4(51,234,5,255)/255.0; else
    if (4.0/6.0<p.y&&p.y<5.0/6.0) c= vec4(8,163,255,255)/255.0; else
    if (5.0/6.0<p.y&&p.y<6.0/6.0) c= vec4(122,85,255,255)/255.0; else
    if (abs(p.y)-.05<0.0001) c=vec4(0.,0.,0.,1.); else
    if (abs(p.y-1.)-.05<0.0001) c=vec4(0.,0.,0.,1.); else
        c=vec4(0,0,0,0);
    c.a*=.8-min(.8,abs(p.x*.08));
    c.xyz=mix(c.xyz,vec3(length(c.xyz)),.15);
    return c;
}

// Procedural Nyan Cat (simplified but recognizable)
vec4 nyan(vec2 p) {
    vec2 uv = p*vec2(0.4,1.0);
    float ns=3.0;
    float nt = iTime*ns; 
    nt-=mod(nt,240.0/256.0/6.0); 
    nt = mod(nt,240.0/256.0);
    float ny = mod(iTime*ns,1.0); 
    ny-=mod(ny,0.75); 
    ny*=-0.05;
    
    // Adjust UV for animation
    uv.y += ny;
    
    vec3 color = vec3(0.0);
    float alpha = 0.0;
    
    // Check bounds
    if (uv.x>-0.3 && uv.x<0.2 && uv.y<.3 && uv.y>-.3) {
        // Normalized coordinates for cat body
        vec2 cat = (uv + vec2(0.05, 0.0)) * vec2(5.0, 3.5);
        
        // Animated frame selection (simple 2-frame animation)
        float frame = mod(floor(iTime * 8.0), 2.0);
        cat.x += frame * 0.3;
        
        // Cat body (gray rectangle)
        if (abs(cat.x) < 0.5 && abs(cat.y) < 0.35) {
            color = vec3(0.5, 0.5, 0.55);
            alpha = 1.0;
            
            // Pop-Tart body (pink)
            if (abs(cat.x) < 0.4 && abs(cat.y) < 0.25) {
                color = vec3(1.0, 0.4, 0.7);
                
                // Sprinkles
                vec2 sprinkle = fract(cat * 5.0);
                if (length(sprinkle - 0.5) < 0.15) {
                    color = mix(color, vec3(1.0, 0.8, 0.2), 0.5);
                }
            }
            
            // Cat head (right side)
            vec2 head = cat - vec2(0.45, 0.15);
            if (length(head) < 0.2) {
                color = vec3(0.5, 0.5, 0.55);
            }
            
            // Ears
            vec2 ear1 = cat - vec2(0.5, 0.3);
            vec2 ear2 = cat - vec2(0.4, 0.3);
            if (length(ear1) < 0.08 || length(ear2) < 0.08) {
                color = vec3(0.5, 0.5, 0.55);
                alpha = 1.0;
            }
            
            // Eyes (black dots)
            vec2 eye1 = cat - vec2(0.5, 0.2);
            vec2 eye2 = cat - vec2(0.4, 0.2);
            if (length(eye1) < 0.03 || length(eye2) < 0.03) {
                color = vec3(0.0);
            }
            
            // Tail (left side) - wavy
            float tailX = cat.x + 0.5;
            float tailY = cat.y - 0.1 + sin(tailX * 3.0 + iTime * 5.0) * 0.15;
            if (tailX > 0.0 && tailX < 0.3 && abs(tailY) < 0.05) {
                color = vec3(0.5, 0.5, 0.55);
                alpha = 1.0;
            }
            
            // Feet
            vec2 foot1 = cat - vec2(0.2, -0.3);
            vec2 foot2 = cat - vec2(-0.2, -0.3);
            if ((abs(foot1.x) < 0.08 && abs(foot1.y) < 0.08) ||
                (abs(foot2.x) < 0.08 && abs(foot2.y) < 0.08)) {
                color = vec3(0.5, 0.5, 0.55);
                alpha = 1.0;
            }
        }
        
        // Outline
        if (alpha > 0.0 && (
            abs(abs(cat.x) - 0.5) < 0.02 || 
            abs(abs(cat.y) - 0.35) < 0.02)) {
            color *= 0.3;
        }
    }
    
    return vec4(color, alpha);
}

// Raymarching and 2D graphics
vec3 raymarch(in vec3 from, in vec3 dir) {
    edge=0.;
    vec3 p = vec3(0.0), norm = vec3(0.0);
    float d = 100.;
    float totdist = 0.;
    for (int i=0; i<RAY_STEPS; i++) {
        if (d>det && totdist<25.0) {
            p=from+totdist*dir;
            d=de(p);
            det=detail*exp(.13*totdist);
            totdist+=d; 
        }
    }
    vec3 col=vec3(0.);
    p-=(det-d)*dir;
    norm=normal(p);
    col=(1.-abs(norm))*max(0.,1.-edge*.8); // set normal as color with dark edges
    totdist=clamp(totdist,0.,26.);
    dir.y-=.02;
    float sunsize=7.0;
    float an=atan(dir.x,dir.y)+iTime*1.5; // angle for drawing and rotating sun
    float s=pow(clamp(1.0-length(dir.xy)*sunsize-abs(.2-mod(an,.4)),0.,1.),.1); // sun
    float sb=pow(clamp(1.0-length(dir.xy)*(sunsize-.2)-abs(.2-mod(an,.4)),0.,1.),.1); // sun border
    float sg=pow(clamp(1.0-length(dir.xy)*(sunsize-4.5)-.5*abs(.2-mod(an,.4)),0.,1.),3.); // sun rays
    float y=mix(.45,1.2,pow(smoothstep(0.,1.,.75-dir.y),2.))*(1.-sb*.5); // gradient sky
    
    // set up background with sky and sun
    vec3 backg=vec3(0.5,0.,1.)*((1.-s)*(1.-sg)*y+(1.-sb)*sg*vec3(1.,.8,0.15)*3.);
         backg+=vec3(1.,.9,.1)*s;
         backg=max(backg,sg*vec3(1.,.9,.5));
    
    col=mix(vec3(1.,.9,.3),col,exp(-.004*totdist*totdist));// distant fading to sun color
    if (totdist>25.) col=backg; // hit background
    col=pow(col,vec3(GAMMA))*BRIGHTNESS;
    col=mix(vec3(length(col)),col,SATURATION);
    col*=vec3(1.,.9,.85);
    
#ifdef NYAN
    dir.yx*=rot(dir.x);
    vec2 ncatpos=(dir.xy+vec2(-3.+mod(-t,6.),-.27));
    vec4 ncat=nyan(ncatpos*5.);
    vec4 rain=rainbow(ncatpos*10.+vec2(.8,.5));
    if (totdist>8.) col=mix(col,max(vec3(.2),rain.xyz),rain.a*.9);
    if (totdist>8.) col=mix(col,max(vec3(.2),ncat.xyz),ncat.a*.9);
#endif
    return col;
}

// get camera position
vec3 move(inout vec3 dir) {
    vec3 go=path(t);
    vec3 adv=path(t+.7);
    float hd=de(adv);
    vec3 advec=normalize(adv-go);
    float an=adv.x-go.x; 
    an*=min(1.,abs(adv.z-go.z))*sign(adv.z-go.z)*.7;
    dir.xy*=mat2(cos(an),sin(an),-sin(an),cos(an));
    an=advec.y*1.7;
    dir.yz*=mat2(cos(an),sin(an),-sin(an),cos(an));
    an=atan(advec.x,advec.z);
    dir.xz*=mat2(cos(an),sin(an),-sin(an),cos(an));
    return go;
}

void main() {
    // OpenGL/WebGL: Y origin at bottom (matches ShaderToy)
    // Metal/D3D: Y origin at top (flip to match ShaderToy convention)
#if SOKOL_GLSL
    vec2 fragCoord = gl_FragCoord.xy;
#else
    vec2 fragCoord = vec2(gl_FragCoord.x, iResolution.y - gl_FragCoord.y);
#endif
    vec2 uv_local = fragCoord.xy / iResolution.xy*2.-1.;
    vec2 oriuv=uv_local;
    uv_local.y*=iResolution.y/iResolution.x;
    float fov=.9-max(0.,.7-iTime*.3);
    vec3 dir=normalize(vec3(uv_local*fov,1.));
    vec3 from=origin+move(dir);
    vec3 color=raymarch(from,dir); 
    
#ifdef BORDER
    color=mix(vec3(0.),color,pow(max(0.,.95-length(oriuv*oriuv*oriuv*vec2(1.05,1.1))),.3));
#endif
    
    frag_color = vec4(color,1.);
}
@end

@program fractalland vs fs
