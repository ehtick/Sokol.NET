using System;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.SG.sg_vertex_step;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;
using static Sokol.SImgui;
using Imgui;
using static Imgui.ImguiNative;
using static Bullet;
using static physics_demo_shader_cs.Shaders;

public static unsafe class BulletphysicsApp
{
    const int START_AMOUNT = 5000;
    const int MAX_INSTANCES = START_AMOUNT * 2 + 100;

    [StructLayout(LayoutKind.Sequential)]
    struct Vertex
    {
        public Vector3 position;
        public Vector3 normal;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct InstanceData
    {
        public Matrix4x4 model;
        public Vector3 color;
    }

    // Holds a physics body and the objects Bullet keeps raw pointers to
    class PhysicsBody
    {
        public Bullet.BtRigidBody body = null!;
        public Bullet.BtCollisionShape shape = null!;
        public Bullet.BtDefaultMotionState motionState = null!;
        public Vector3 color;
        public bool isSphere;
    }

    class _state
    {
        public sg_pass_action pass_action;
        public sg_pipeline pip_smooth;
        public sg_bindings cube_bind;
        public sg_bindings sphere_bind;

        public Bullet.BtDefaultCollisionConfiguration config = null!;
        public Bullet.BtBroadphaseInterface broadphase = null!;
        public Bullet.BtDynamicsWorld world = null!;

#if WEB
        public Bullet.BtCollisionDispatcher dispatcher = null!;
        public Bullet.BtSequentialImpulseConstraintSolver solver = null!;
#else
        public Bullet.BtCollisionDispatcherMt dispatcher = null!;
        public Bullet.BtSequentialImpulseConstraintSolverMt solverMt = null!;
        public Bullet.BtConstraintSolverPoolMt solverPool = null!;
        public Bullet.BtITaskScheduler? taskScheduler;
#endif

        // Ground body + its GC-anchored resources
        public Bullet.BtRigidBody groundBody = null!;
        public Bullet.BtBoxShape groundShape = null!;
        public Bullet.BtDefaultMotionState groundMotion = null!;

        public List<PhysicsBody> bodies = new();
        public Random random = new();

        public InstanceData[] cubeInstances = new InstanceData[MAX_INSTANCES];
        public InstanceData[] sphereInstances = new InstanceData[MAX_INSTANCES];

        public int cubeCount;
        public int sphereCount;

        public Camera camera = new();

        public double dtAccum;
        public int    dtCount;
        public double dtSmoothed = 1.0 / 60.0;
        public double dtTimer;
    }

    static _state state = new _state();

    static void InitPhysics()
    {
        state.config     = new Bullet.BtDefaultCollisionConfiguration(new Bullet._ByValue_BtDefaultCollisionConfiguration());
        state.broadphase = new Bullet.BtDbvtBroadphase();

#if WEB
        state.dispatcher = new Bullet.BtCollisionDispatcher((Bullet.BtCollisionConfiguration)state.config);
        state.solver     = new Bullet.BtSequentialImpulseConstraintSolver();
        state.world      = new Bullet.BtDiscreteDynamicsWorld(
                                (Bullet.BtDispatcher)state.dispatcher,
                                (Bullet.BtBroadphaseInterface)state.broadphase,
                                (Bullet.BtConstraintSolver)state.solver,
                                (Bullet.BtCollisionConfiguration)state.config);
#else
        state.taskScheduler = Bullet.BtCreateDefaultTaskScheduler();
        Bullet.BtSetTaskScheduler(state.taskScheduler);
        int numThreads       = Math.Max(1, Environment.ProcessorCount - 1);
        state.dispatcher     = new Bullet.BtCollisionDispatcherMt((Bullet.BtCollisionConfiguration)state.config);
        state.solverPool     = new Bullet.BtConstraintSolverPoolMt(numThreads);
        state.solverMt       = new Bullet.BtSequentialImpulseConstraintSolverMt();
        state.world          = new Bullet.BtDiscreteDynamicsWorldMt(
                                (Bullet.BtDispatcher)state.dispatcher,
                                (Bullet.BtBroadphaseInterface)state.broadphase,
                                state.solverPool,
                                (Bullet.BtConstraintSolver)state.solverMt,
                                (Bullet.BtCollisionConfiguration)state.config);
#endif
        state.world.SetGravity(new Bullet.BtVector3(0.0, -9.81, 0.0));

        // Ground (static, mass=0)
        state.groundShape = new Bullet.BtBoxShape(new Bullet.BtVector3(25.0, 2.5, 25.0));
        var groundTrans   = new Bullet.BtTransform(new Bullet.BtQuaternion(0, 0, 0, 1), new Bullet.BtVector3(0, -2.5, 0));
        state.groundMotion = new Bullet.BtDefaultMotionState(groundTrans);
        var groundInfo    = new Bullet.BtRigidBody.BtRigidBodyConstructionInfo(
                                0.0,
                                (Bullet.BtMotionState)state.groundMotion,
                                (Bullet.BtCollisionShape)state.groundShape,
                                new Bullet.BtVector3(0, 0, 0));
        state.groundBody  = new Bullet.BtRigidBody(groundInfo);
        state.world.AddRigidBody(state.groundBody);

        for (int i = 0; i < START_AMOUNT; i++)
            SpawnCube(new Vector3(state.random.NextSingle() * 4 - 2, 10 + i * 2, state.random.NextSingle() * 4 - 2));
        for (int i = 0; i < START_AMOUNT; i++)
            SpawnSphere(new Vector3(state.random.NextSingle() * 4 - 2, 15 + i * 2, state.random.NextSingle() * 4 - 2));
    }

    static void SpawnCube(Vector3 position)
    {
        var pb = new PhysicsBody { isSphere = false };
        pb.shape = new Bullet.BtBoxShape(new Bullet.BtVector3(0.5, 0.5, 0.5));

        var trans = new Bullet.BtTransform(new Bullet.BtQuaternion(0, 0, 0, 1), new Bullet.BtVector3(position.X, position.Y, position.Z));
        pb.motionState = new Bullet.BtDefaultMotionState(trans);

        var inertia = new Bullet.BtVector3(0, 0, 0);
        pb.shape.CalculateLocalInertia(1.0, inertia);

        var info = new Bullet.BtRigidBody.BtRigidBodyConstructionInfo(
                        1.0,
                        (Bullet.BtMotionState)pb.motionState,
                        (Bullet.BtCollisionShape)pb.shape,
                        inertia);
        pb.body = new Bullet.BtRigidBody(info);
        pb.color = new Vector3(
            state.random.NextSingle() * 0.5f + 0.5f,
            state.random.NextSingle() * 0.5f + 0.5f,
            state.random.NextSingle() * 0.5f + 0.5f);

        state.world.AddRigidBody(pb.body);
        pb.body.SetFriction(0.2);
        pb.body.SetRestitution(0.3);
        state.bodies.Add(pb);
        state.cubeCount++;
    }

    static void SpawnSphere(Vector3 position)
    {
        var pb = new PhysicsBody { isSphere = true };
        pb.shape = new Bullet.BtSphereShape(0.5);

        var trans = new Bullet.BtTransform(new Bullet.BtQuaternion(0, 0, 0, 1), new Bullet.BtVector3(position.X, position.Y, position.Z));
        pb.motionState = new Bullet.BtDefaultMotionState(trans);

        var inertia = new Bullet.BtVector3(0, 0, 0);
        pb.shape.CalculateLocalInertia(1.0, inertia);

        var info = new Bullet.BtRigidBody.BtRigidBodyConstructionInfo(
                        1.0,
                        (Bullet.BtMotionState)pb.motionState,
                        (Bullet.BtCollisionShape)pb.shape,
                        inertia);
        pb.body = new Bullet.BtRigidBody(info);
        pb.color = new Vector3(
            state.random.NextSingle() * 0.5f + 0.5f,
            state.random.NextSingle() * 0.5f + 0.5f,
            state.random.NextSingle() * 0.5f + 0.5f);

        state.world.AddRigidBody(pb.body);
        pb.body.SetFriction(0.2);
        pb.body.SetRestitution(0.3);
        state.bodies.Add(pb);
        state.sphereCount++;
    }

    static Matrix4x4 GetBodyMatrix(Bullet.BtDefaultMotionState motionState)
    {
        using var trans = new Bullet.BtTransform();
        motionState.GetWorldTransform(trans);

        var o = trans.GetOrigin();
        var q = trans.GetRotation();

        var position = new Vector3((float)o.X(), (float)o.Y(), (float)o.Z());
        var rotation = new Quaternion((float)q.X(), (float)q.Y(), (float)q.Z(), (float)q.W());

        return Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
    }

    static unsafe void CreateCubeMesh()
    {
        Vertex[] vertices = new Vertex[24];

        vertices[0]  = new Vertex { position = new Vector3(-0.5f, -0.5f,  0.5f), normal = new Vector3(0, 0, 1) };
        vertices[1]  = new Vertex { position = new Vector3( 0.5f, -0.5f,  0.5f), normal = new Vector3(0, 0, 1) };
        vertices[2]  = new Vertex { position = new Vector3( 0.5f,  0.5f,  0.5f), normal = new Vector3(0, 0, 1) };
        vertices[3]  = new Vertex { position = new Vector3(-0.5f,  0.5f,  0.5f), normal = new Vector3(0, 0, 1) };
        vertices[4]  = new Vertex { position = new Vector3( 0.5f, -0.5f, -0.5f), normal = new Vector3(0, 0, -1) };
        vertices[5]  = new Vertex { position = new Vector3(-0.5f, -0.5f, -0.5f), normal = new Vector3(0, 0, -1) };
        vertices[6]  = new Vertex { position = new Vector3(-0.5f,  0.5f, -0.5f), normal = new Vector3(0, 0, -1) };
        vertices[7]  = new Vertex { position = new Vector3( 0.5f,  0.5f, -0.5f), normal = new Vector3(0, 0, -1) };
        vertices[8]  = new Vertex { position = new Vector3(-0.5f,  0.5f,  0.5f), normal = new Vector3(0, 1, 0) };
        vertices[9]  = new Vertex { position = new Vector3( 0.5f,  0.5f,  0.5f), normal = new Vector3(0, 1, 0) };
        vertices[10] = new Vertex { position = new Vector3( 0.5f,  0.5f, -0.5f), normal = new Vector3(0, 1, 0) };
        vertices[11] = new Vertex { position = new Vector3(-0.5f,  0.5f, -0.5f), normal = new Vector3(0, 1, 0) };
        vertices[12] = new Vertex { position = new Vector3(-0.5f, -0.5f, -0.5f), normal = new Vector3(0, -1, 0) };
        vertices[13] = new Vertex { position = new Vector3( 0.5f, -0.5f, -0.5f), normal = new Vector3(0, -1, 0) };
        vertices[14] = new Vertex { position = new Vector3( 0.5f, -0.5f,  0.5f), normal = new Vector3(0, -1, 0) };
        vertices[15] = new Vertex { position = new Vector3(-0.5f, -0.5f,  0.5f), normal = new Vector3(0, -1, 0) };
        vertices[16] = new Vertex { position = new Vector3( 0.5f, -0.5f,  0.5f), normal = new Vector3(1, 0, 0) };
        vertices[17] = new Vertex { position = new Vector3( 0.5f, -0.5f, -0.5f), normal = new Vector3(1, 0, 0) };
        vertices[18] = new Vertex { position = new Vector3( 0.5f,  0.5f, -0.5f), normal = new Vector3(1, 0, 0) };
        vertices[19] = new Vertex { position = new Vector3( 0.5f,  0.5f,  0.5f), normal = new Vector3(1, 0, 0) };
        vertices[20] = new Vertex { position = new Vector3(-0.5f, -0.5f, -0.5f), normal = new Vector3(-1, 0, 0) };
        vertices[21] = new Vertex { position = new Vector3(-0.5f, -0.5f,  0.5f), normal = new Vector3(-1, 0, 0) };
        vertices[22] = new Vertex { position = new Vector3(-0.5f,  0.5f,  0.5f), normal = new Vector3(-1, 0, 0) };
        vertices[23] = new Vertex { position = new Vector3(-0.5f,  0.5f, -0.5f), normal = new Vector3(-1, 0, 0) };

        ushort[] indices = new ushort[36]
        {
             0,  1,  2,   0,  2,  3,
             4,  5,  6,   4,  6,  7,
             8,  9, 10,   8, 10, 11,
            12, 13, 14,  12, 14, 15,
            16, 17, 18,  16, 18, 19,
            20, 21, 22,  20, 22, 23,
        };

        state.cube_bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc { data = SG_RANGE<Vertex>(vertices) });
        state.cube_bind.index_buffer      = sg_make_buffer(new sg_buffer_desc { usage = new sg_buffer_usage { index_buffer = true }, data = SG_RANGE(indices) });
        state.cube_bind.vertex_buffers[1] = sg_make_buffer(new sg_buffer_desc
        {
            size  = (nuint)(MAX_INSTANCES * sizeof(InstanceData)),
            usage = new sg_buffer_usage { stream_update = true },
            label = "cube-instances",
        });
    }

    static unsafe void CreateSphereMesh()
    {
        int segments = 16;
        int rings    = 8;
        var vertices = new List<Vertex>();
        var indices  = new List<ushort>();

        for (int ring = 0; ring <= rings; ring++)
        {
            float phi = MathF.PI * ring / rings;
            for (int seg = 0; seg <= segments; seg++)
            {
                float theta = 2.0f * MathF.PI * seg / segments;
                float x = MathF.Sin(phi) * MathF.Cos(theta);
                float y = MathF.Cos(phi);
                float z = MathF.Sin(phi) * MathF.Sin(theta);
                vertices.Add(new Vertex { position = new Vector3(x * 0.5f, y * 0.5f, z * 0.5f), normal = new Vector3(x, y, z) });
            }
        }

        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                int cur  = ring * (segments + 1) + seg;
                int next = cur + segments + 1;
                indices.Add((ushort)cur);       indices.Add((ushort)(cur + 1));  indices.Add((ushort)next);
                indices.Add((ushort)(cur + 1)); indices.Add((ushort)(next + 1)); indices.Add((ushort)next);
            }
        }

        state.sphere_bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc { data = SG_RANGE<Vertex>(vertices.ToArray()) });
        state.sphere_bind.index_buffer      = sg_make_buffer(new sg_buffer_desc { usage = new sg_buffer_usage { index_buffer = true }, data = SG_RANGE<ushort>(indices.ToArray()) });
        state.sphere_bind.vertex_buffers[1] = sg_make_buffer(new sg_buffer_desc
        {
            size  = (nuint)(MAX_INSTANCES * sizeof(InstanceData)),
            usage = new sg_buffer_usage { stream_update = true },
            label = "sphere-instances",
        });
    }

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = { func = &slog_func },
        });

        state.pass_action = default;
        state.pass_action.colors[0].load_action  = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value  = new sg_color { r = 0.1f, g = 0.1f, b = 0.15f, a = 1.0f };

        InitPhysics();
        CreateCubeMesh();
        CreateSphereMesh();

        var shd = sg_make_shader(physics_demo_smooth_shader_desc(sg_query_backend()));

        var pip = default(sg_pipeline_desc);
        pip.shader = shd;
        pip.layout.attrs[ATTR_physics_demo_smooth_position]    = new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT3, buffer_index = 0 };
        pip.layout.attrs[ATTR_physics_demo_smooth_normal]      = new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT3, buffer_index = 0 };
        pip.layout.buffers[1].step_func                        = SG_VERTEXSTEP_PER_INSTANCE;
        pip.layout.buffers[1].stride                           = sizeof(InstanceData);
        pip.layout.attrs[ATTR_physics_demo_smooth_inst_model_0] = new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset =  0 };
        pip.layout.attrs[ATTR_physics_demo_smooth_inst_model_1] = new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 16 };
        pip.layout.attrs[ATTR_physics_demo_smooth_inst_model_2] = new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 32 };
        pip.layout.attrs[ATTR_physics_demo_smooth_inst_model_3] = new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 48 };
        pip.layout.attrs[ATTR_physics_demo_smooth_inst_color]   = new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT3, buffer_index = 1, offset = 64 };
        pip.index_type          = SG_INDEXTYPE_UINT16;
        pip.cull_mode           = SG_CULLMODE_BACK;
        pip.face_winding        = sg_face_winding.SG_FACEWINDING_CCW;
        pip.depth.compare       = SG_COMPAREFUNC_LESS_EQUAL;
        pip.depth.write_enabled = true;
        state.pip_smooth        = sg_make_pipeline(pip);

        state.camera.Init(new CameraDesc
        {
            Distance  = 50,
            Latitude  = 25,
            Longitude = 45,
            Center    = new Vector3(0, 5, 0),
            Aspect    = 60,
            NearZ     = 0.1f,
            FarZ      = 1000.0f,
        });
        state.camera.MoveSpeed        = 10;
        state.camera.MouseSensitivity = 0.3f;

        simgui_setup(new simgui_desc_t());
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        double dt = sapp_frame_duration();
        state.dtAccum += dt;
        state.dtCount++;
        state.dtTimer += dt;
        if (state.dtTimer >= 0.5)
        {
            state.dtSmoothed = state.dtAccum / state.dtCount;
            state.dtAccum    = 0;
            state.dtCount    = 0;
            state.dtTimer    = 0;
        }
        int   width  = sapp_width();
        int   height = sapp_height();

        state.camera.Update(width, height, dt);
        state.world.StepSimulation(dt, null, 1.0 / 60.0);

        // Collect instance data
        int cubeCount   = 0;
        int sphereCount = 0;

        // Ground as a static cube instance
        var groundModel = Matrix4x4.CreateScale(new Vector3(50, 5, 50)) *
                          Matrix4x4.CreateTranslation(new Vector3(0, -2.5f, 0));
        state.cubeInstances[cubeCount++] = new InstanceData { model = groundModel, color = new Vector3(0.9f, 0.7f, 0.3f) };

        foreach (var pb in state.bodies)
        {
            var model = GetBodyMatrix(pb.motionState);
            if (pb.isSphere)
            {
                if (sphereCount < MAX_INSTANCES)
                    state.sphereInstances[sphereCount++] = new InstanceData { model = model, color = pb.color };
            }
            else
            {
                if (cubeCount < MAX_INSTANCES)
                    state.cubeInstances[cubeCount++] = new InstanceData { model = model, color = pb.color };
            }
        }

        // Render
        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
        sg_apply_pipeline(state.pip_smooth);

        var vsParams = new vs_params_t { vp = state.camera.ViewProj };
        var fsParams = new fs_params_t
        {
            light_dir = Vector3.Normalize(new Vector3(0.5f, 1, 0.3f)),
            view_pos  = state.camera.EyePos,
        };

        if (cubeCount > 0)
        {
            fixed (InstanceData* ptr = state.cubeInstances)
                sg_update_buffer(state.cube_bind.vertex_buffers[1], new sg_range { ptr = ptr, size = (nuint)(cubeCount * sizeof(InstanceData)) });
            sg_apply_bindings(state.cube_bind);
            sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vsParams));
            sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fsParams));
            sg_draw(0, 36, (uint)cubeCount);
        }

        if (sphereCount > 0)
        {
            fixed (InstanceData* ptr = state.sphereInstances)
                sg_update_buffer(state.sphere_bind.vertex_buffers[1], new sg_range { ptr = ptr, size = (nuint)(sphereCount * sizeof(InstanceData)) });
            sg_apply_bindings(state.sphere_bind);
            sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vsParams));
            sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fsParams));
            int   rings     = 8;
            int   segments  = 16;
            uint  idxCount  = (uint)(rings * segments * 6);
            sg_draw(0, idxCount, (uint)sphereCount);
        }

        DrawStatsWindow(dt, state.dtSmoothed);
        simgui_render();

        sg_end_pass();
        sg_commit();
    }

    static void DrawStatsWindow(double dt, double dtSmoothed)
    {
        simgui_new_frame(new simgui_frame_desc_t
        {
            width      = sapp_width(),
            height     = sapp_height(),
            delta_time = dt,
            dpi_scale  = 1,
        });

        igSetNextWindowSize(new Vector2(250, 200), ImGuiCond.Once);
        igSetNextWindowPos(new Vector2(30, 30), ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Statistics", ref open, ImGuiWindowFlags.None))
        {
            igText($"FPS: {1.0 / dtSmoothed:F1}");
            igText($"Frame: {dtSmoothed * 1000:F2} ms");
            igSeparator();
            igText($"Total bodies: {state.bodies.Count}");
            igText($"Cubes:   {state.cubeCount}");
            igText($"Spheres: {state.sphereCount}");
            igSeparator();
#if WEB
            igText("Physics: Bullet (single-thread)");
#else
            igText("Physics: Bullet (multi-thread)");
#endif
            igText("Left-click+drag: look");
            igText("Scroll: zoom  WASD: move");
        }
        igEnd();
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        if (simgui_handle_event(*e))
            return;
        state.camera.HandleEvent(e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        foreach (var pb in state.bodies)
            state.world.RemoveRigidBody(pb.body);
        state.world.RemoveRigidBody(state.groundBody);

        simgui_shutdown();
        sg_shutdown();

        if (Debugger.IsAttached)
            Environment.Exit(0);
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb    = &Init,
            frame_cb   = &Frame,
            event_cb   = &Event,
            cleanup_cb = &Cleanup,
            width      = 1024,
            height     = 768,
            sample_count = 4,
            window_title = "Bullet Physics Demo (Sokol.NET)",
            icon = { sokol_default = true },
            logger = { func = &slog_func },
        };
    }
}
