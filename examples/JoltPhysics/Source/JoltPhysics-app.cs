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
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;
using static Sokol.SImgui;
using static Sokol.SG.sg_vertex_step;
using Imgui;
using static Imgui.ImguiNative;
using JoltPhysicsSharp;
using static physics_demo_shader_cs.Shaders;

public static unsafe class JoltphysicsApp
{
    const int START_AMOUNT = 5000;
    const int MAX_INSTANCES = START_AMOUNT*2 +100;

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

    struct PhysicsBody
    {
        public BodyID bodyId;
        public Vector3 color;
        public bool isSphere;
    }

    struct _state
    {
        public sg_pass_action pass_action;
        public sg_pipeline pip_smooth;
        public sg_bindings cube_bind;
        public sg_bindings sphere_bind;

        public PhysicsSystem physicsSystem;
        public BodyInterface bodyInterface;
        public JobSystemThreadPool jobSystem;
        public Camera camera;

        public List<PhysicsBody> bodies;
        public float spawnTimer;
        public Random random;

        public int cubeCount;
        public int sphereCount;
        public bool showStats;

        // Instance data for GPU instancing
        public InstanceData[] cubeInstances;
        public InstanceData[] sphereInstances;

        // Ground body ID
        public BodyID groundBodyId;
    }

    static _state state = new _state();

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        Info("Init started");
        
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            shader_pool_size = 64,
            buffer_pool_size = 4096 * 2,
            sampler_pool_size = 512,
            view_pool_size = 512,
            uniform_buffer_size = 64 * 1024 * 1024,
            logger = {
                func = &slog_func,
            }
        });

       

        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.1f, g = 0.1f, b = 0.15f, a = 1.0f };

        
        // Initialize Jolt Physics
        Foundation.Init();
  
        // Create layer filters
        var objectLayerPairFilter = new ObjectLayerPairFilterTable(2);
 
        
        objectLayerPairFilter.EnableCollision(Layers.NON_MOVING, Layers.MOVING);
        objectLayerPairFilter.EnableCollision(Layers.MOVING, Layers.MOVING);



        var broadPhaseLayerInterface = new BroadPhaseLayerInterfaceTable(2, 2);

        
        broadPhaseLayerInterface.MapObjectToBroadPhaseLayer(Layers.NON_MOVING, 0);
        broadPhaseLayerInterface.MapObjectToBroadPhaseLayer(Layers.MOVING, 1);

        // Create physics system
        var physicsSystemSettings = new PhysicsSystemSettings
        {
            MaxBodies = 65536,
            MaxBodyPairs = 65536,
            MaxContactConstraints = 10240,
            NumBodyMutexes = 0,
            ObjectLayerPairFilter = objectLayerPairFilter,
            BroadPhaseLayerInterface = broadPhaseLayerInterface,
            ObjectVsBroadPhaseLayerFilter = new ObjectVsBroadPhaseLayerFilterTable(
                broadPhaseLayerInterface, 2,
                objectLayerPairFilter, 2)
        };

        state.physicsSystem = new PhysicsSystem(physicsSystemSettings);

        // Create job system for multi-threading
        // maxJobs: Maximum number of concurrent jobs (default: 2048)
        // maxBarriers: Maximum number of barriers (default: 8)
        // numThreads: Number of worker threads (default: ProcessorCount - 1)
        int numThreads = Math.Max(1, Environment.ProcessorCount - 1);
        Console.WriteLine($"Initializing JobSystem with {numThreads} worker threads");
        var jobSystemConfig = new JobSystemThreadPoolConfig
        {
            maxJobs = 4096,      // Increase for more concurrent work
            maxBarriers = 16,    // Increase if you have complex barriers
            numThreads = numThreads
        };
        state.jobSystem = new JobSystemThreadPool(jobSystemConfig);

        state.bodyInterface = state.physicsSystem.BodyInterface;

        // Set gravity
        state.physicsSystem.Gravity = new Vector3(0, -9.81f, 0);


        // Create ground plane (BoxShapeSettings uses half-extents)
        var groundShapeSettings = new BoxShapeSettings(new Vector3(25, 2.5f, 25));
        var groundShape = groundShapeSettings.Create();
        var groundBodySettings = new BodyCreationSettings(
            groundShape,
            new Vector3(0, -2.5f, 0),
            Quaternion.Identity,
            MotionType.Static,
            Layers.NON_MOVING);



        var groundBody = state.bodyInterface.CreateBody(groundBodySettings);
        state.bodyInterface.AddBody(groundBody, Activation.DontActivate);
        state.groundBodyId = groundBody.ID;



        // Initialize state
        state.bodies = new List<PhysicsBody>();
        state.random = new Random();
        state.spawnTimer = 0;

        // Initialize instance arrays
        state.cubeInstances = new InstanceData[MAX_INSTANCES];
        state.sphereInstances = new InstanceData[MAX_INSTANCES];

        // Create some initial objects
        for (int i = 0; i < START_AMOUNT; i++)
        {
            SpawnCube(new Vector3(state.random.NextSingle() * 4 - 2, 10 + i * 2, state.random.NextSingle() * 4 - 2));
        }
        for (int i = 0; i < START_AMOUNT; i++)
        {
            SpawnSphere(new Vector3(state.random.NextSingle() * 4 - 2, 15 + i * 2, state.random.NextSingle() * 4 - 2));
        }


        // Create cube mesh
        CreateCubeMesh();

        // Create sphere mesh
        CreateSphereMesh();


        // Create shaders and pipelines
        var shd_smooth = sg_make_shader(physics_demo_smooth_shader_desc(sg_query_backend()));

        var pip_desc = default(sg_pipeline_desc);
        pip_desc.shader = shd_smooth;
        // Geometry attributes from buffer 0
        pip_desc.layout.attrs[ATTR_physics_demo_smooth_position] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT3, buffer_index = 0 };
        pip_desc.layout.attrs[ATTR_physics_demo_smooth_normal] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT3, buffer_index = 0 };
        // Instance data in buffer slot 1
        pip_desc.layout.buffers[1].step_func = SG_VERTEXSTEP_PER_INSTANCE;
        pip_desc.layout.buffers[1].stride = sizeof(InstanceData);
        pip_desc.layout.attrs[ATTR_physics_demo_smooth_inst_model_0] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 0 };
        pip_desc.layout.attrs[ATTR_physics_demo_smooth_inst_model_1] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 16 };
        pip_desc.layout.attrs[ATTR_physics_demo_smooth_inst_model_2] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 32 };
        pip_desc.layout.attrs[ATTR_physics_demo_smooth_inst_model_3] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 48 };
        pip_desc.layout.attrs[ATTR_physics_demo_smooth_inst_color] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT3, buffer_index = 1, offset = 64 };
        pip_desc.index_type = SG_INDEXTYPE_UINT16;
        pip_desc.cull_mode = SG_CULLMODE_BACK;
        pip_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
        pip_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pip_desc.depth.write_enabled = true;
        state.pip_smooth = sg_make_pipeline(pip_desc);

        // Initialize camera
        state.camera = new Camera();
        state.camera.Init(new CameraDesc
        {
            Distance = 50,
            Latitude = 25,
            Longitude = 45,
            Center = new Vector3(0, 5, 0),
            Aspect = 60,
            NearZ = 0.1f,
            FarZ = 1000.0f
        });
        state.camera.MoveSpeed = 10;
        state.camera.MouseSensitivity = 0.3f;

        // Initialize ImGui
        simgui_setup(new simgui_desc_t());

        // Initialize stats
        state.showStats = true;
        state.cubeCount = START_AMOUNT;
        state.sphereCount = START_AMOUNT;

        Console.WriteLine("Init complete");
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        float deltaTime = (float)sapp_frame_duration();
        int width = sapp_width();
        int height = sapp_height();

        // Update camera
        state.camera.Update(width, height, deltaTime);

        // Step physics simulation with multithreading
        // collisionSteps: Number of collision detection steps per update (1-4 recommended)
        //   Higher = more accurate but slower
        // integrationSubSteps: Number of integration steps (default: 1)
        //   The job system will parallelize work across available threads
        const int collisionSteps = 1;
        state.physicsSystem.Update(deltaTime, collisionSteps, state.jobSystem);

        // Render
        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });

        // Prepare instance data for all bodies
        int cubeInstanceCount = 0;
        int sphereInstanceCount = 0;

        // Add ground plane as a cube instance
        var groundModel = Matrix4x4.CreateScale(new Vector3(50, 5, 50)) *
                         Matrix4x4.CreateTranslation(new Vector3(0, -2.5f, 0));
        state.cubeInstances[cubeInstanceCount++] = new InstanceData
        {
            model = groundModel,
            color = new Vector3(0.9f, 0.7f, 0.3f)
        };

        // Gather all cube and sphere instances
        foreach (var body in state.bodies)
        {
            var position = state.bodyInterface.GetPosition(body.bodyId);
            var rotation = state.bodyInterface.GetRotation(body.bodyId);

            var rotationMatrix = Matrix4x4.CreateFromQuaternion(rotation);
            var translation = Matrix4x4.CreateTranslation(position);
            var model = rotationMatrix * translation;

            if (body.isSphere)
            {
                if (sphereInstanceCount < MAX_INSTANCES)
                {
                    state.sphereInstances[sphereInstanceCount++] = new InstanceData
                    {
                        model = model,
                        color = body.color
                    };
                }
            }
            else
            {
                if (cubeInstanceCount < MAX_INSTANCES)
                {
                    state.cubeInstances[cubeInstanceCount++] = new InstanceData
                    {
                        model = model,
                        color = body.color
                    };
                }
            }
        }

        // Render all cubes with instancing
        if (cubeInstanceCount > 0)
        {
            sg_apply_pipeline(state.pip_smooth);
            RenderCubesInstanced(cubeInstanceCount);
        }

        // Render all spheres with instancing
        if (sphereInstanceCount > 0)
        {
            sg_apply_pipeline(state.pip_smooth);
            RenderSpheresInstanced(sphereInstanceCount);
        }

        // Render ImGui
        DrawStatsWindow();
        simgui_render();

        sg_end_pass();
        sg_commit();
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
        simgui_shutdown();

        state.jobSystem?.Dispose();

        // Clean up all bodies
        foreach (var body in state.bodies)
        {
            state.bodyInterface.RemoveBody(body.bodyId);
            state.bodyInterface.DestroyBody(body.bodyId);
        }
        state.bodyInterface.RemoveBody(state.groundBodyId);
        state.bodyInterface.DestroyBody(state.groundBodyId);

        state.jobSystem?.Dispose();
        state.physicsSystem?.Dispose();
        Foundation.Shutdown();

        sg_shutdown();

        // Force a complete shutdown if debugging
        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
        }
    }

    static void SpawnCube(Vector3 position)
    {
        var cubeShapeSettings = new BoxShapeSettings(new Vector3(1, 1, 1));
        var cubeShape = cubeShapeSettings.Create();

        var bodySettings = new BodyCreationSettings(
            cubeShape,
            position,
            Quaternion.Identity,
            MotionType.Dynamic,
            Layers.MOVING)
        {
            AllowSleeping = false  // Prevent bodies from sleeping to avoid floating bug
        };

        var body = state.bodyInterface.CreateBody(bodySettings);
        state.bodyInterface.AddBody(body, Activation.Activate);
        var bodyId = body.ID;

        // Set material properties
        state.bodyInterface.SetFriction(bodyId, 0.2f);
        state.bodyInterface.SetRestitution(bodyId, 0.3f);

        state.bodies.Add(new PhysicsBody
        {
            bodyId = bodyId,
            color = new Vector3(state.random.NextSingle() * 0.5f + 0.5f,
                               state.random.NextSingle() * 0.5f + 0.5f,
                               state.random.NextSingle() * 0.5f + 0.5f),
            isSphere = false
        });

        state.cubeCount++;
    }

    static void SpawnSphere(Vector3 position)
    {
        var sphereShapeSettings = new SphereShapeSettings(0.5f);
        var sphereShape = sphereShapeSettings.Create();

        var bodySettings = new BodyCreationSettings(
            sphereShape,
            position,
            Quaternion.Identity,
            MotionType.Dynamic,
            Layers.MOVING)
        {
            AllowSleeping = false  // Prevent bodies from sleeping to avoid floating bug
        };

        var body = state.bodyInterface.CreateBody(bodySettings);
        state.bodyInterface.AddBody(body, Activation.Activate);
        var bodyId = body.ID;

        // Set material properties
        state.bodyInterface.SetFriction(bodyId, 0.2f);
        state.bodyInterface.SetRestitution(bodyId, 0.3f);

        // Add random angular velocity for spinning
        var angularVelocity = new Vector3(
            (state.random.NextSingle() - 0.5f) * 4f,
            (state.random.NextSingle() - 0.5f) * 4f,
            (state.random.NextSingle() - 0.5f) * 4f);
        state.bodyInterface.SetAngularVelocity(bodyId, angularVelocity);

        state.bodies.Add(new PhysicsBody
        {
            bodyId = bodyId,
            color = new Vector3(state.random.NextSingle() * 0.5f + 0.5f,
                               state.random.NextSingle() * 0.5f + 0.5f,
                               state.random.NextSingle() * 0.5f + 0.5f),
            isSphere = true
        });

        state.sphereCount++;
    }

    static unsafe void CreateCubeMesh()
    {
        // Cube vertices with normals (24 vertices, 6 faces)
        Vertex[] vertices = new Vertex[24];

        // Front face (Z+)
        vertices[0] = new Vertex { position = new Vector3(-0.5f, -0.5f, 0.5f), normal = new Vector3(0, 0, 1) };
        vertices[1] = new Vertex { position = new Vector3(0.5f, -0.5f, 0.5f), normal = new Vector3(0, 0, 1) };
        vertices[2] = new Vertex { position = new Vector3(0.5f, 0.5f, 0.5f), normal = new Vector3(0, 0, 1) };
        vertices[3] = new Vertex { position = new Vector3(-0.5f, 0.5f, 0.5f), normal = new Vector3(0, 0, 1) };

        // Back face (Z-)
        vertices[4] = new Vertex { position = new Vector3(0.5f, -0.5f, -0.5f), normal = new Vector3(0, 0, -1) };
        vertices[5] = new Vertex { position = new Vector3(-0.5f, -0.5f, -0.5f), normal = new Vector3(0, 0, -1) };
        vertices[6] = new Vertex { position = new Vector3(-0.5f, 0.5f, -0.5f), normal = new Vector3(0, 0, -1) };
        vertices[7] = new Vertex { position = new Vector3(0.5f, 0.5f, -0.5f), normal = new Vector3(0, 0, -1) };

        // Top face (Y+)
        vertices[8] = new Vertex { position = new Vector3(-0.5f, 0.5f, 0.5f), normal = new Vector3(0, 1, 0) };
        vertices[9] = new Vertex { position = new Vector3(0.5f, 0.5f, 0.5f), normal = new Vector3(0, 1, 0) };
        vertices[10] = new Vertex { position = new Vector3(0.5f, 0.5f, -0.5f), normal = new Vector3(0, 1, 0) };
        vertices[11] = new Vertex { position = new Vector3(-0.5f, 0.5f, -0.5f), normal = new Vector3(0, 1, 0) };

        // Bottom face (Y-)
        vertices[12] = new Vertex { position = new Vector3(-0.5f, -0.5f, -0.5f), normal = new Vector3(0, -1, 0) };
        vertices[13] = new Vertex { position = new Vector3(0.5f, -0.5f, -0.5f), normal = new Vector3(0, -1, 0) };
        vertices[14] = new Vertex { position = new Vector3(0.5f, -0.5f, 0.5f), normal = new Vector3(0, -1, 0) };
        vertices[15] = new Vertex { position = new Vector3(-0.5f, -0.5f, 0.5f), normal = new Vector3(0, -1, 0) };

        // Right face (X+)
        vertices[16] = new Vertex { position = new Vector3(0.5f, -0.5f, 0.5f), normal = new Vector3(1, 0, 0) };
        vertices[17] = new Vertex { position = new Vector3(0.5f, -0.5f, -0.5f), normal = new Vector3(1, 0, 0) };
        vertices[18] = new Vertex { position = new Vector3(0.5f, 0.5f, -0.5f), normal = new Vector3(1, 0, 0) };
        vertices[19] = new Vertex { position = new Vector3(0.5f, 0.5f, 0.5f), normal = new Vector3(1, 0, 0) };

        // Left face (X-)
        vertices[20] = new Vertex { position = new Vector3(-0.5f, -0.5f, -0.5f), normal = new Vector3(-1, 0, 0) };
        vertices[21] = new Vertex { position = new Vector3(-0.5f, -0.5f, 0.5f), normal = new Vector3(-1, 0, 0) };
        vertices[22] = new Vertex { position = new Vector3(-0.5f, 0.5f, 0.5f), normal = new Vector3(-1, 0, 0) };
        vertices[23] = new Vertex { position = new Vector3(-0.5f, 0.5f, -0.5f), normal = new Vector3(-1, 0, 0) };

        ushort[] indices = new ushort[36]
        {
            0, 1, 2,  0, 2, 3,    // Front
            4, 5, 6,  4, 6, 7,    // Back
            8, 9, 10, 8, 10, 11,  // Top
            12, 13, 14, 12, 14, 15, // Bottom
            16, 17, 18, 16, 18, 19, // Right
            20, 21, 22, 20, 22, 23  // Left
        };

        state.cube_bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc
        {
            data = SG_RANGE<Vertex>(vertices)
        });

        state.cube_bind.index_buffer = sg_make_buffer(new sg_buffer_desc
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data = SG_RANGE(indices)
        });

        // Create instance buffer for cubes
        state.cube_bind.vertex_buffers[1] = sg_make_buffer(new sg_buffer_desc
        {
            size = (nuint)(MAX_INSTANCES * sizeof(InstanceData)),
            usage = new sg_buffer_usage { stream_update = true },
            label = "cube-instances"
        });
    }

    static unsafe void CreateSphereMesh()
    {
        // Create sphere with subdivision
        int segments = 16;
        int rings = 8;
        List<Vertex> vertices = new List<Vertex>();
        List<ushort> indices = new List<ushort>();

        // Generate vertices
        for (int ring = 0; ring <= rings; ring++)
        {
            float phi = MathF.PI * ring / rings;
            for (int seg = 0; seg <= segments; seg++)
            {
                float theta = 2.0f * MathF.PI * seg / segments;

                float x = MathF.Sin(phi) * MathF.Cos(theta);
                float y = MathF.Cos(phi);
                float z = MathF.Sin(phi) * MathF.Sin(theta);

                vertices.Add(new Vertex
                {
                    position = new Vector3(x * 0.5f, y * 0.5f, z * 0.5f),
                    normal = new Vector3(x, y, z)
                });
            }
        }

        // Generate indices (CCW winding when viewed from outside)
        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                int current = ring * (segments + 1) + seg;
                int next = current + segments + 1;

                // First triangle (CCW from outside)
                indices.Add((ushort)current);
                indices.Add((ushort)(current + 1));
                indices.Add((ushort)next);

                // Second triangle (CCW from outside)
                indices.Add((ushort)(current + 1));
                indices.Add((ushort)(next + 1));
                indices.Add((ushort)next);
            }
        }

        state.sphere_bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc
        {
            data = SG_RANGE<Vertex>(vertices.ToArray())
        });

        state.sphere_bind.index_buffer = sg_make_buffer(new sg_buffer_desc
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data = SG_RANGE<ushort>(indices.ToArray())
        });

        // Create instance buffer for spheres
        state.sphere_bind.vertex_buffers[1] = sg_make_buffer(new sg_buffer_desc
        {
            size = (nuint)(MAX_INSTANCES * sizeof(InstanceData)),
            usage = new sg_buffer_usage { stream_update = true },
            label = "sphere-instances"
        });
    }

    static unsafe void RenderCubesInstanced(int instanceCount)
    {
        fixed (InstanceData* instancePtr = state.cubeInstances)
        {
            sg_update_buffer(state.cube_bind.vertex_buffers[1], new sg_range
            {
                ptr = instancePtr,
                size = (nuint)(instanceCount * sizeof(InstanceData))
            });
        }

        var vs_params = new vs_params_t { vp = state.camera.ViewProj };
        var fs_params = new fs_params_t
        {
            light_dir = Vector3.Normalize(new Vector3(0.5f, 1, 0.3f)),
            view_pos = state.camera.EyePos
        };

        sg_apply_bindings(state.cube_bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
        sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fs_params));
        sg_draw(0, 36, (uint)instanceCount);
    }

    static unsafe void RenderSpheresInstanced(int instanceCount)
    {
        fixed (InstanceData* instancePtr = state.sphereInstances)
        {
            sg_update_buffer(state.sphere_bind.vertex_buffers[1], new sg_range
            {
                ptr = instancePtr,
                size = (nuint)(instanceCount * sizeof(InstanceData))
            });
        }

        var vs_params = new vs_params_t { vp = state.camera.ViewProj };
        var fs_params = new fs_params_t
        {
            light_dir = Vector3.Normalize(new Vector3(0.5f, 1, 0.3f)),
            view_pos = state.camera.EyePos
        };

        sg_apply_bindings(state.sphere_bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
        sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fs_params));

        int segments = 16;
        int rings = 8;
        uint indexCount = (uint)(rings * segments * 6);
        sg_draw(0, indexCount, (uint)instanceCount);
    }

    static void DrawStatsWindow()
    {
        if (!state.showStats)
            return;

        simgui_new_frame(new simgui_frame_desc_t
        {
            width = sapp_width(),
            height = sapp_height(),
            delta_time = (float)sapp_frame_duration(),
            dpi_scale = 1
        });

        igSetNextWindowSize(new Vector2(250, 250), ImGuiCond.Once);
        igSetNextWindowPos(new Vector2(30, 30), ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Statistics", ref open, ImGuiWindowFlags.None))
        {
            // FPS
            float fps = 1.0f / (float)sapp_frame_duration();
            igText($"FPS: {fps:F1}");
            igText($"Frame Time: {sapp_frame_duration() * 1000:F2} ms");

            igSeparator();

            // Rendering stats
            int drawCalls = (state.cubeCount > 0 ? 1 : 0) + (state.sphereCount > 0 ? 1 : 0);
            igText($"Draw Calls: {drawCalls}");
            igText($"Instanced: Yes");

            igSeparator();

            // Object counts
            igText($"Total Bodies: {state.bodies.Count}");
            igText($"Cubes: {state.cubeCount}");
            igText($"Spheres: {state.sphereCount}");

            igSeparator();

            // Physics info
            igText($"Static Bodies: 1 (Ground)");
            igText($"Active Bodies: {state.physicsSystem.GetNumActiveBodies(BodyType.Rigid)}");
            igText($"Physics Engine: Jolt");

            igSeparator();

            // Camera info
            Vector3 camPos = state.camera.EyePos;
            igText($"Camera: ({camPos.X:F1}, {camPos.Y:F1}, {camPos.Z:F1})");
        }
        igEnd();
    }

    // Jolt Physics layer definitions
    public static class Layers
    {
        public const byte NON_MOVING = 0;
        public const byte MOVING = 1;
        public const byte NUM_LAYERS = 2;
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
            window_title = "Jolt Physics Demo (Sokol.NET)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }
}
