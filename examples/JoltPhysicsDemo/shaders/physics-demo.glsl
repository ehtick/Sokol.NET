@ctype mat4 mat44_t
@ctype vec3 vec3_t


// Vertex shader with smooth shading
@vs vs_smooth
layout(binding=0) uniform vs_params {
    mat4 vp;
};

layout(location=0) in vec3 position;
layout(location=1) in vec3 normal;

// Per-instance attributes
layout(location=2) in vec4 inst_model_0;
layout(location=3) in vec4 inst_model_1;
layout(location=4) in vec4 inst_model_2;
layout(location=5) in vec4 inst_model_3;
layout(location=6) in vec4 inst_color;  // .xyz=color, .w=shape_type (0=box,1=sphere,2=floor)

out vec3 world_normal;
out vec3 world_pos;
out vec3 color;
out vec3 local_pos;
out vec3 local_normal;
out float shape_type;

void main() {
    mat4 model = mat4(inst_model_0, inst_model_1, inst_model_2, inst_model_3);

    vec4 world_position = model * vec4(position, 1.0);
    gl_Position = vp * world_position;
    mat3 normal_matrix = mat3(transpose(inverse(model)));
    world_normal  = normalize(normal_matrix * normal);
    world_pos     = world_position.xyz;
    color         = inst_color.xyz;
    local_pos     = position;
    local_normal  = normal;
    shape_type    = inst_color.w;
}
@end


// Fragment shader for smooth shading with procedural surface patterns
@fs fs_smooth
in vec3 world_normal;
in vec3 world_pos;
in vec3 color;
in vec3 local_pos;
in vec3 local_normal;
in float shape_type;
out vec4 frag_color;

layout(binding=1) uniform fs_params {
    vec3 light_dir;
    vec3 view_pos;
};

// --- 2D value noise helpers (used by terrain branch) ---
float _hash2(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}
float _vnoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(_hash2(i),           _hash2(i + vec2(1.0, 0.0)), f.x),
               mix(_hash2(i + vec2(0.0, 1.0)), _hash2(i + vec2(1.0, 1.0)), f.x), f.y);
}

void main() {
    vec3 N = normalize(world_normal);

    // For custom meshes (convex hulls, tapered shapes) override the vertex normal
    // with the geometric face normal derived from screen-space position derivatives.
    // This guarantees hard flat shading per triangle regardless of vertex data.
    // shape_type >= 5.5 = smooth custom mesh (terrain) — keep interpolated vertex normals.
    if (shape_type > 4.5 && shape_type < 5.5) {
        vec3 geom_N = normalize(cross(dFdx(world_pos), dFdy(world_pos)));
        // dFdx/dFdy can point either way — flip to match the hemisphere the vertex normal expects
        N = geom_N * sign(dot(geom_N, world_normal));
    } else if (shape_type > 6.5) {
        // Soft body: flat (per-face) shading using geometric normal from screen-space derivatives.
        // Matches the C++ reference renderer which uses per-triangle face normals.
        // Two-sided: flip for back faces so both sides are lit correctly.
        vec3 geom_N = normalize(cross(dFdx(world_pos), dFdy(world_pos)));
        N = gl_FrontFacing ? geom_N : -geom_N;
    } else if (shape_type > 5.5 && !gl_FrontFacing) {
        // Two-sided meshes (terrain): flip normal on back faces.
        N = -N;
    }

    vec3 L = normalize(light_dir);
    vec3 V = normalize(view_pos - world_pos);
    vec3 H = normalize(L + V);

    // Hemisphere ambient
    float hemi   = dot(N, vec3(0.0, 1.0, 0.0)) * 0.5 + 0.5;
    vec3 ambient = color * mix(vec3(0.12, 0.14, 0.22), vec3(0.55, 0.52, 0.42), hemi);

    // Key light (main directional)
    float diff    = max(dot(N, L), 0.0);
    vec3 diffuse  = diff * color * 0.72;

    // Soft fill light from the opposite-lower direction to lift shadow side
    vec3 Lf     = normalize(vec3(-L.x, -0.4, -L.z));
    float difff = max(dot(N, Lf), 0.0) * 0.18;
    vec3 fill   = difff * color;

    // Specular — subtle matte highlight
    float spec    = pow(max(dot(N, H), 0.0), 32.0);
    vec3 specular = vec3(0.10) * spec;

    vec3 lit_color = ambient + diffuse + fill + specular;

    // --- Procedural surface markings ---
    float pattern = 1.0;
    float alpha   = 1.0;

    if (shape_type < 0.5) {
        // Box: cross lines on each face + edge darkening
        vec3 lp = local_pos * 2.0;  // remap -0.5..+0.5 to -1..+1

        // Project onto face UV using the face normal
        vec3 an = abs(local_normal);
        vec2 uv;
        if (an.x > an.y && an.x > an.z)
            uv = lp.yz;
        else if (an.y > an.x && an.y > an.z)
            uv = lp.xz;
        else
            uv = lp.xy;

        // Cross: a + shape at face centre
        float cw = 0.08;
        float crossPat = 1.0 - 0.55 * max(step(abs(uv.y), cw), step(abs(uv.x), cw));

        // Edge darkening where two faces meet
        float ew     = 0.88;
        float ex     = step(ew, abs(lp.x));
        float ey     = step(ew, abs(lp.y));
        float ez     = step(ew, abs(lp.z));
        float onEdge = max(ex * ey, max(ex * ez, ey * ez));
        float edgePat = 1.0 - 0.65 * onEdge;

        pattern = crossPat * edgePat;

    } else if (shape_type < 1.5) {
        // Sphere: latitude / longitude grid lines
        vec3 dir = normalize(local_pos);
        float lat = asin(clamp(dir.y, -1.0, 1.0)) / 3.14159265 + 0.5;
        float lon = atan(dir.z, dir.x)             / (2.0 * 3.14159265) + 0.5;

        float latLine = abs(fract(lat * 8.0) - 0.5);  // 0 at line centres
        float lonLine = abs(fract(lon * 8.0) - 0.5);
        float lw      = 0.05;
        float grid    = step(lw, min(latLine, lonLine));
        pattern = 0.2 + 0.8 * grid;  // 0.2 on lines, 1.0 elsewhere

    } else if (shape_type < 2.5) {
        // Floor: world-space checkerboard + thin grid lines
        float gs      = 10.0;
        float checker = mod(floor(world_pos.x / gs) + floor(world_pos.z / gs), 2.0);
        pattern = 0.70 + 0.30 * checker;

        float fx  = fract(world_pos.x / gs);
        float fz  = fract(world_pos.z / gs);
        float lw  = 0.03;
        float isL = max(max(step(fx, lw), step(1.0 - lw, fx)),
                        max(step(fz, lw), step(1.0 - lw, fz)));
        pattern *= 1.0 - 0.4 * isL;

    } else if (shape_type < 3.5) {
        // Wireframe sphere: only grid lines are opaque, gaps are transparent
        vec3 dir = normalize(local_pos);
        float lat = asin(clamp(dir.y, -1.0, 1.0)) / 3.14159265 + 0.5;
        float lon = atan(dir.z, dir.x)             / (2.0 * 3.14159265) + 0.5;

        float latLine = abs(fract(lat * 8.0) - 0.5);
        float lonLine = abs(fract(lon * 8.0) - 0.5);
        float lw      = 0.07;
        float onLine  = 1.0 - step(lw, min(latLine, lonLine));  // 1 on lines, 0 elsewhere
        alpha   = onLine;

    } else if (shape_type < 4.5) {
        // Wireframe box: only edges are opaque, faces are transparent
        vec3 lp = local_pos * 2.0;
        float ew     = 0.80;
        float ex     = step(ew, abs(lp.x));
        float ey     = step(ew, abs(lp.y));
        float ez     = step(ew, abs(lp.z));
        float onEdge = max(ex * ey, max(ex * ez, ey * ez));
        alpha   = onEdge;

    } else if (shape_type > 5.5 && shape_type < 6.5) {
        // Smooth terrain: height-based coloring so valleys are visibly darker/browner
        // and hilltops are bright green — makes the elevation map directly visible.
        // maxHeight = 6.0 in C#, so world_pos.y is in [0, 6].
        float height01 = clamp(world_pos.y / 6.0, 0.0, 1.0);

        vec2 uv = world_pos.xz;

        // Small noise to break up solid bands
        float n1 = _vnoise(uv * 0.09 + vec2(3.1, 7.7));
        float n2 = _vnoise(uv * 0.20 + vec2(17.3, 31.1));
        float noise = n1 * 0.65 + n2 * 0.35;

        vec3 c_valley = vec3(0.28, 0.18, 0.09);  // dark moist earth / valley floor
        vec3 c_mid    = vec3(0.16, 0.52, 0.10);  // mid-slope grass
        vec3 c_peak   = vec3(0.38, 0.68, 0.12);  // hilltop — bright summer grass

        // Height-driven 3-stop gradient: valley → mid-slope → peak
        vec3 terrain_col = height01 < 0.5
            ? mix(c_valley, c_mid,  height01 * 2.0)
            : mix(c_mid,    c_peak, (height01 - 0.5) * 2.0);

        // Slight noise variation so it's not banded
        terrain_col *= 0.88 + noise * 0.24;

        // Strong lighting — large ambient contrast between lit sky and shadow ground
        vec3 t_ambient  = terrain_col * mix(vec3(0.38, 0.38, 0.38), vec3(0.62, 0.58, 0.45), hemi);
        vec3 t_diffuse  = max(dot(N, L), 0.0) * terrain_col * 0.65;
        vec3 t_fill     = max(dot(N, Lf), 0.0) * 0.30 * terrain_col;
        vec3 t_specular = vec3(0.04) * pow(max(dot(N, H), 0.0), 32.0);
        frag_color = vec4(t_ambient + t_diffuse + t_fill + t_specular, 1.0);
        return;

    } else if (shape_type > 6.5 && shape_type < 8.5) {
        // Soft body: plain lit color, no procedural pattern.
        pattern = 1.0;
    } else if (shape_type > 9.5) {
        // Water surface: opaque flat color — no per-vertex normals to avoid specular flicker on animated waves.
        frag_color = vec4(color, 1.0);
        return;
    } else if (shape_type > 8.5) {
        // Unlit (debug lines / wireframe): output raw instance color directly.
        frag_color = vec4(color, 1.0);
        return;
    } else {
        // Flat custom mesh (convex hull / tapered shapes): pattern stays 1.0.
        pattern = 1.0;
    }

    frag_color = vec4(lit_color * pattern, alpha);
}
@end

@program physics_demo_smooth vs_smooth fs_smooth
