using System;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Collections.Generic;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_primitive_type;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.Box2D;
using static box2d_sapp_shader_cs.Shaders;
public static unsafe class Box2dApp
{
    const int MAX_VERTICES = 30000;
    const int MAX_BODIES = 300;
    
    enum ShapeType
    {
        Box,
        Circle,
        Triangle
    }
    
    struct Vertex
    {
        public Vector2 pos;
        public Vector4 color;
    }

    class _state
    {
        public sg_pass_action pass_action;
        public sg_pipeline pip;
        public sg_bindings bind;
        public sg_buffer vbuf;
        
        public b2WorldId worldId;
        public b2BodyId groundId;
        public b2BodyId[] bodies = new b2BodyId[MAX_BODIES];
        public Vector4[] body_colors = new Vector4[MAX_BODIES];
        public Vector2[] body_sizes = new Vector2[MAX_BODIES]; // Store width, height for each body
        public ShapeType[] body_shapes = new ShapeType[MAX_BODIES]; // Store shape type for each body
        public int bodyCount;
        
        public List<Vertex> vertices = new List<Vertex>();
        public float spawnTimer;
    }

    static _state state = new _state();

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = { func = &slog_func }
        });

        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.1f, g = 0.1f, b = 0.1f, a = 1.0f };

        // Create dynamic vertex buffer
        state.vbuf = sg_make_buffer(new sg_buffer_desc()
        {
            size = (nuint)(MAX_VERTICES * sizeof(Vertex)),
            usage = new sg_buffer_usage { stream_update = true },
            label = "box2d-vertices"
        });

        // Create shader and pipeline
        sg_shader shd = sg_make_shader(box2d_shader_desc(sg_query_backend()));

        var pipeline_desc = default(sg_pipeline_desc);
        pipeline_desc.shader = shd;
        pipeline_desc.layout.attrs[ATTR_box2d_position].format = SG_VERTEXFORMAT_FLOAT2;
        pipeline_desc.layout.attrs[ATTR_box2d_color0].format = SG_VERTEXFORMAT_FLOAT4;
        pipeline_desc.primitive_type = SG_PRIMITIVETYPE_TRIANGLES;
        pipeline_desc.label = "box2d-pipeline";
        
        state.pip = sg_make_pipeline(pipeline_desc);

        state.bind = new sg_bindings();
        state.bind.vertex_buffers[0] = state.vbuf;

        // Create Box2D world
        b2WorldDef worldDef = b2DefaultWorldDef();
        worldDef.gravity = new b2Vec2 { x = 0.0f, y = -10.0f };
        state.worldId = b2CreateWorld(worldDef);

        // Create ground
        b2BodyDef groundBodyDef = b2DefaultBodyDef();
        groundBodyDef.position = new b2Vec2 { x = 0.0f, y = -10.0f };
        state.groundId = b2CreateBody(state.worldId, groundBodyDef);

        b2Polygon groundBox = b2MakeBox(20.0f, 1.0f);
        b2ShapeDef groundShapeDef = b2DefaultShapeDef();
        b2CreatePolygonShape(state.groundId, groundShapeDef, groundBox);

        // Create some initial falling shapes
        CreateBox(0.0f, 15.0f, 1.0f, 1.0f, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
        CreateCircle(-3.0f, 20.0f, 1.0f, new Vector4(0.3f, 1.0f, 0.3f, 1.0f));
        CreateTriangle(3.0f, 25.0f, 1.0f, new Vector4(0.3f, 0.3f, 1.0f, 1.0f));
    }

    static unsafe void CreateBox(float x, float y, float width, float height, Vector4 color)
    {
        if (state.bodyCount >= MAX_BODIES) return;

        b2BodyDef bodyDef = b2DefaultBodyDef();
        bodyDef.type = b2BodyType.b2_dynamicBody;
        bodyDef.position = new b2Vec2 { x = x, y = y };
        b2BodyId bodyId = b2CreateBody(state.worldId, bodyDef);

        b2Polygon box = b2MakeBox(width, height);
        b2ShapeDef shapeDef = b2DefaultShapeDef();
        shapeDef.density = 1.0f;
        b2CreatePolygonShape(bodyId, shapeDef, box);

        state.bodies[state.bodyCount] = bodyId;
        state.body_colors[state.bodyCount] = color;
        state.body_sizes[state.bodyCount] = new Vector2(width, height);
        state.body_shapes[state.bodyCount] = ShapeType.Box;
        state.bodyCount++;
    }

    static unsafe void CreateCircle(float x, float y, float radius, Vector4 color)
    {
        if (state.bodyCount >= MAX_BODIES) return;

        b2BodyDef bodyDef = b2DefaultBodyDef();
        bodyDef.type = b2BodyType.b2_dynamicBody;
        bodyDef.position = new b2Vec2 { x = x, y = y };
        b2BodyId bodyId = b2CreateBody(state.worldId, bodyDef);

        b2Circle circle = new b2Circle();
        circle.center = new b2Vec2 { x = 0.0f, y = 0.0f };
        circle.radius = radius;
        b2ShapeDef shapeDef = b2DefaultShapeDef();
        shapeDef.density = 1.0f;
        b2CreateCircleShape(bodyId, shapeDef, circle);

        state.bodies[state.bodyCount] = bodyId;
        state.body_colors[state.bodyCount] = color;
        state.body_sizes[state.bodyCount] = new Vector2(radius, radius);
        state.body_shapes[state.bodyCount] = ShapeType.Circle;
        state.bodyCount++;
    }

    static unsafe void CreateTriangle(float x, float y, float size, Vector4 color)
    {
        if (state.bodyCount >= MAX_BODIES) return;

        b2BodyDef bodyDef = b2DefaultBodyDef();
        bodyDef.type = b2BodyType.b2_dynamicBody;
        bodyDef.position = new b2Vec2 { x = x, y = y };
        b2BodyId bodyId = b2CreateBody(state.worldId, bodyDef);

        // Create triangle vertices
        b2Vec2[] verts = new b2Vec2[3];
        verts[0] = new b2Vec2 { x = 0.0f, y = size };
        verts[1] = new b2Vec2 { x = -size, y = -size };
        verts[2] = new b2Vec2 { x = size, y = -size };
        
        fixed (b2Vec2* pVerts = verts)
        {
            b2Hull hull = b2ComputeHull(in *pVerts, 3);
            b2Polygon triangle = b2MakePolygon(hull, 0.0f);
        
            b2ShapeDef shapeDef = b2DefaultShapeDef();
            shapeDef.density = 1.0f;
            b2CreatePolygonShape(bodyId, shapeDef, triangle);
        }

        state.bodies[state.bodyCount] = bodyId;
        state.body_colors[state.bodyCount] = color;
        state.body_sizes[state.bodyCount] = new Vector2(size, size);
        state.body_shapes[state.bodyCount] = ShapeType.Triangle;
        state.bodyCount++;
    }

    static unsafe void AddBoxVertices(b2Vec2 center, float angle, float width, float height, Vector4 color)
    {
        float c = MathF.Cos(angle);
        float s = MathF.Sin(angle);

        Vector2[] corners = new Vector2[]
        {
            new Vector2(-width, -height),
            new Vector2( width, -height),
            new Vector2( width,  height),
            new Vector2(-width,  height)
        };

        Vector2[] transformed = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            float rx = corners[i].X * c - corners[i].Y * s;
            float ry = corners[i].X * s + corners[i].Y * c;
            transformed[i] = new Vector2(center.x + rx, center.y + ry);
        }

        // Two triangles for the box
        state.vertices.Add(new Vertex { pos = transformed[0], color = color });
        state.vertices.Add(new Vertex { pos = transformed[1], color = color });
        state.vertices.Add(new Vertex { pos = transformed[2], color = color });
        
        state.vertices.Add(new Vertex { pos = transformed[0], color = color });
        state.vertices.Add(new Vertex { pos = transformed[2], color = color });
        state.vertices.Add(new Vertex { pos = transformed[3], color = color });
    }

    static unsafe void AddCircleVertices(b2Vec2 center, float radius, Vector4 color)
    {
        int segments = 16;
        Vector2 centerV = new Vector2(center.x, center.y);
        
        for (int i = 0; i < segments; i++)
        {
            float angle1 = (float)(i * 2.0 * Math.PI / segments);
            float angle2 = (float)((i + 1) * 2.0 * Math.PI / segments);
            
            Vector2 p1 = centerV + new Vector2(MathF.Cos(angle1) * radius, MathF.Sin(angle1) * radius);
            Vector2 p2 = centerV + new Vector2(MathF.Cos(angle2) * radius, MathF.Sin(angle2) * radius);
            
            state.vertices.Add(new Vertex { pos = centerV, color = color });
            state.vertices.Add(new Vertex { pos = p1, color = color });
            state.vertices.Add(new Vertex { pos = p2, color = color });
        }
    }

    static unsafe void AddTriangleVertices(b2Vec2 center, float angle, float size, Vector4 color)
    {
        float c = MathF.Cos(angle);
        float s = MathF.Sin(angle);

        Vector2[] corners = new Vector2[]
        {
            new Vector2(0.0f, size),
            new Vector2(-size, -size),
            new Vector2(size, -size)
        };

        Vector2[] transformed = new Vector2[3];
        for (int i = 0; i < 3; i++)
        {
            float rx = corners[i].X * c - corners[i].Y * s;
            float ry = corners[i].X * s + corners[i].Y * c;
            transformed[i] = new Vector2(center.x + rx, center.y + ry);
        }

        state.vertices.Add(new Vertex { pos = transformed[0], color = color });
        state.vertices.Add(new Vertex { pos = transformed[1], color = color });
        state.vertices.Add(new Vertex { pos = transformed[2], color = color });
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        float dt = (float)sapp_frame_duration();
        state.spawnTimer += dt;

        // Check if world is valid
        if (state.worldId.index1 == 0)
        {
            return;
        }

        // Step the physics simulation
        float timeStep = 1.0f / 60.0f;
        int subStepCount = 4;
        try
        {
            b2World_Step(state.worldId, timeStep, subStepCount);
        }
        catch
        {
            return;
        }

        // Add new shapes periodically
        if (state.spawnTimer > 0.3f && state.bodyCount < MAX_BODIES)
        {
            state.spawnTimer = 0.0f;
            
            // Spawn 5 objects per iteration
            int objectsToSpawn = Math.Min(5, MAX_BODIES - state.bodyCount);
            for (int i = 0; i < objectsToSpawn; i++)
            {
                float x = (float)((Random.Shared.NextDouble() - 0.5) * 10.0);
                float y = 40.0f + i * 2.0f; // Offset vertically to avoid overlap
                float size = (float)(0.5 + Random.Shared.NextDouble() * 1.0);
                Vector4 color = new Vector4(
                    (float)Random.Shared.NextDouble(),
                    (float)Random.Shared.NextDouble(),
                    (float)Random.Shared.NextDouble(),
                    1.0f);
                
                int shapeType = Random.Shared.Next(0, 3);
                if (shapeType == 0)
                    CreateBox(x, y, size, size, color);
                else if (shapeType == 1)
                    CreateCircle(x, y, size, color);
                else
                    CreateTriangle(x, y, size, color);
            }
        }

        // Build vertex buffer
        if (state.vertices == null)
        {
            Error("Vertices list is null!");
            return;
        }
        
        state.vertices.Clear();

        // Draw ground
        if (state.groundId.index1 != 0)
        {
            try
            {
                b2Vec2 groundPos = b2Body_GetPosition(state.groundId);
                b2Rot groundRot = b2Body_GetRotation(state.groundId);
                float groundAngle = MathF.Atan2(groundRot.s, groundRot.c);
                AddBoxVertices(groundPos, groundAngle, 20.0f, 1.0f, new Vector4(0.5f, 0.5f, 0.5f, 1.0f));
            }
            catch (Exception ex)
            {
                Error($"Error rendering ground: {ex.Message}");
            }
        }

        // Draw all dynamic bodies
        if (state.bodies == null || state.body_sizes == null || state.body_colors == null)
        {
            Error("Arrays are null!");
            return;
        }

        int maxCount = Math.Min(state.bodyCount, MAX_BODIES);
        for (int i = 0; i < maxCount; i++)
        {
            if (i >= state.bodies.Length || i >= state.body_sizes.Length || i >= state.body_colors.Length)
            {
                Error($"Index {i} out of bounds. bodyCount={state.bodyCount}, arrays={state.bodies.Length}");
                break;
            }
                
            if (state.bodies[i].index1 != 0)
            {
                try
                {
                    b2Vec2 pos = b2Body_GetPosition(state.bodies[i]);
                    b2Rot rot = b2Body_GetRotation(state.bodies[i]);
                    float angle = MathF.Atan2(rot.s, rot.c);
                    
                    switch (state.body_shapes[i])
                    {
                        case ShapeType.Box:
                            AddBoxVertices(pos, angle, state.body_sizes[i].X, state.body_sizes[i].Y, state.body_colors[i]);
                            break;
                        case ShapeType.Circle:
                            AddCircleVertices(pos, state.body_sizes[i].X, state.body_colors[i]);
                            break;
                        case ShapeType.Triangle:
                            AddTriangleVertices(pos, angle, state.body_sizes[i].X, state.body_colors[i]);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error rendering body {i}: {ex.Message}");
                }
            }
        }

        // Create projection matrix (orthographic)
        float aspect = sapp_widthf() / sapp_heightf();
        float zoom = 25.0f;
        Matrix4x4 proj = Matrix4x4.CreateOrthographic(zoom * aspect, zoom, -1.0f, 1.0f);

        vs_params_t vs_params = new vs_params_t();
        vs_params.mvp = proj;

        // Update vertex buffer and draw
        if (state.vertices.Count > 0)
        {
            var vertArray = state.vertices.ToArray();
            fixed (Vertex* verts = vertArray)
            {
                var range = new sg_range { ptr = verts, size = (nuint)(vertArray.Length * sizeof(Vertex)) };
                sg_update_buffer(state.vbuf, range);
            }

            sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
            sg_apply_pipeline(state.pip);
            sg_apply_uniforms(box2d_sapp_shader_cs.Shaders.UB_vs_params, SG_RANGE<box2d_sapp_shader_cs.Shaders.vs_params_t>(ref vs_params));
            sg_apply_bindings(state.bind);
            sg_draw(0, (uint)state.vertices.Count, 1);
            sg_end_pass();
        }
        else
        {
            sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
            sg_end_pass();
        }

        sg_commit();
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN && 
            e->mouse_button == sapp_mousebutton.SAPP_MOUSEBUTTON_LEFT)
        {
            // Add box at mouse position
            float aspect = sapp_widthf() / sapp_heightf();
            float zoom = 25.0f;
            float x = (e->mouse_x / sapp_widthf() - 0.5f) * zoom * aspect;
            float y = -(e->mouse_y / sapp_heightf() - 0.5f) * zoom;
            
            // Cycle through shapes on click
            int shapeType = (int)(e->frame_count % 3);
            if (shapeType == 0)
                CreateBox(x, y, 1.0f, 1.0f, new Vector4(1.0f, 0.5f, 0.2f, 1.0f));
            else if (shapeType == 1)
                CreateCircle(x, y, 1.0f, new Vector4(1.0f, 0.5f, 0.2f, 1.0f));
            else
                CreateTriangle(x, y, 1.0f, new Vector4(1.0f, 0.5f, 0.2f, 1.0f));
        }
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        if (state.worldId.index1 != 0)
        {
            b2DestroyWorld(state.worldId);
        }

        sg_shutdown();

        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
        }
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb = &Init,
            frame_cb = &Frame,
            event_cb = &Event,
            cleanup_cb = &Cleanup,
            width = 1024,
            height = 768,
            sample_count = 4,
            window_title = "Box2D Physics Demo (sokol-app)",
            icon = { sokol_default = true },
            logger = { func = &slog_func }
        };
    }
}
