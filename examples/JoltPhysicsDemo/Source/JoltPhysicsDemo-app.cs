using System;
using System.Collections.Generic;
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
using static physics_demo_shader_cs.Shaders;
using static water_shader_cs.Shaders;

public static unsafe class JoltphysicsdemoApp
{
    const ushort ObjLayerNonMoving = 0;
    const ushort ObjLayerMoving    = 1;
    const ushort ObjLayerDebris     = 2;  // only collides with ObjLayerMoving

    // Large enough for the mass-spawn demo (5 000 cubes + 5 000 spheres + floor)
    const int MAX_INSTANCES = 10200;

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
        public Vector3   color;
        public float     shapeType;  // 0=box, 1=sphere, 2=floor
    }

    class _state
    {
        public sg_pass_action pass_action;
        public sg_pipeline    pip_smooth;
        public sg_pipeline    pip_smooth_u32;  // same as pip_smooth but SG_INDEXTYPE_UINT32 (soft bodies)
        public sg_pipeline    pip_blend;       // alpha-blended pipeline for wireframe sensor shapes
        public sg_pipeline    pip_lines;       // line segment pipeline for Cosserat rod bodies
        public sg_pipeline    pip_water;        // dedicated water surface pipeline (Fresnel + specular shader)
        public float          time;             // accumulated render time for water shimmer animation
        public sg_bindings    cube_bind;
        public sg_bindings    sphere_bind;
        public sg_bindings    cylinder_bind;
        public sg_bindings    wf_sphere_bind;  // wireframe sphere (shares sphere VB/IB)
        public sg_bindings    wf_box_bind;     // wireframe box (shares cube VB/IB)
        public sg_buffer      tapCylInstanceBuf; // streaming instance buffer for TaperedCylinder draws

        // Physics infrastructure
        public JPH.TempAllocatorImpl              alloc;
        public JPH.JobSystemThreadPool            jobs;
        public JPH.BroadPhaseLayerInterfaceTable  bpInterface;
        public JPH.ObjectLayerPairFilterTable     pairFilter;
        public JPH.ObjectVsBroadPhaseLayerFilterTable objVsBP;
        public JPH.PhysicsSystem                  physicsSystem;
        public JPH.BodyInterface                  bodyInterface;
        public float                              physicsAccum;

        public Camera camera;

        public List<PhysicsBody> bodies;
        public Random            random;

        public InstanceData[] cubeInstances;
        public InstanceData[] sphereInstances;
        public InstanceData[] cylinderInstances;
        public InstanceData[] wfSphereInstances;   // wireframe (transparent) spheres
        public InstanceData[] wfBoxInstances;      // wireframe (transparent) boxes

        // Per-frame list of TaperedCylinder draws (one entry per body; rebuilt each frame)
        public List<(sg_bindings mesh, int idxCount, Matrix4x4 mdl, Vector3 col, bool smooth)> tapCylDraws;

        // Soft body render entries (owned by the demo, cleared on demo switch)
        public List<SoftBodyRenderEntry> softBodies;

        // Rod body render entries for Cosserat rod demos (line segment rendering)
        public List<RodBodyRenderEntry> rodBodies;

        // Debug line segments emitted by demos each frame (world-space, rendered with pip_lines)
        public sg_buffer         debugLinesBuf;
        public List<ConeVertex>  debugLines;
        // Debug triangles emitted by demos each frame (world-space, rendered with pip_water)
        public sg_buffer         debugTrisBuf;
        public List<ConeVertex>  debugTris;

        // World-space text labels, cleared on demo switch
        public List<(JPH.BodyID id, string label)> bodyLabels;

        // Demo browser
        public List<DemoBase> demos;
        public int            activeDemoIndex  = -1;
        public int            pendingDemoSwitch = -1;
        public bool           pendingReset     = false;
        public bool           paused           = false;
        public bool           startPaused      = false;
        public bool           lockCamera       = false;
#if __ANDROID__ || __IOS__
        public bool           virtualControlsEnabled = true;
#else
        public bool           virtualControlsEnabled = false;
#endif
        public bool           joystickOnRight  = false;

        // Reusable per-frame scratch objects — initialized after Jolt is set up
        public JPH.Vec3 scratchPos;
        public JPH.Quat scratchRot;
    }

    static _state state = new _state();

    static Matrix4x4 MakeModelMatrix(JPH.Vec3 pos, JPH.Quat rot, Vector3 scale) =>
        Matrix4x4.CreateScale(scale)
        * Matrix4x4.CreateFromQuaternion(
            new Quaternion(rot.GetX(), rot.GetY(), rot.GetZ(), rot.GetW()))
        * Matrix4x4.CreateTranslation(pos.GetX(), pos.GetY(), pos.GetZ());

    // ── Init ──────────────────────────────────────────────────────────────────
    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment          = sglue_environment(),
            shader_pool_size     = 64,
            buffer_pool_size     = 4096 * 2,
            sampler_pool_size    = 512,
            view_pool_size       = 512,
            uniform_buffer_size  = 64 * 1024 * 1024,
            logger = { func = &slog_func }
        });

        state.pass_action = default;
        state.pass_action.colors[0].load_action  = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value  = new sg_color { r = 0.38f, g = 0.52f, b = 0.72f, a = 1.0f };

        // ── Jolt init ────────────────────────────────────────────────────────
        JPH.Const_JoltHelpers.Init();

        state.alloc = new JPH.TempAllocatorImpl(128 * 1024 * 1024);
        int numThreads = Math.Max(1, Environment.ProcessorCount - 1);
        state.jobs = new JPH.JobSystemThreadPool(2048, 8, numThreads);

        state.bpInterface = new JPH.BroadPhaseLayerInterfaceTable(3, 2);
        using var bp0 = new JPH.BroadPhaseLayer(0);
        using var bp1 = new JPH.BroadPhaseLayer(1);
        state.bpInterface.MapObjectToBroadPhaseLayer(ObjLayerNonMoving, bp0);
        state.bpInterface.MapObjectToBroadPhaseLayer(ObjLayerMoving,    bp1);
        state.bpInterface.MapObjectToBroadPhaseLayer(ObjLayerDebris,    bp1);  // debris → moving BP bucket

        state.pairFilter = new JPH.ObjectLayerPairFilterTable(3);
        state.pairFilter.EnableCollision(ObjLayerMoving,    ObjLayerNonMoving);
        state.pairFilter.EnableCollision(ObjLayerMoving,    ObjLayerMoving);
        state.pairFilter.EnableCollision(ObjLayerDebris,    ObjLayerNonMoving); // debris ↔ floor only (matches C++ Layers.h)

        state.objVsBP = new JPH.ObjectVsBroadPhaseLayerFilterTable(
            state.bpInterface, 2,
            state.pairFilter, 3);

        state.physicsSystem = new JPH.PhysicsSystem();
        state.physicsSystem.Init(65536, 0, 65536, 65536,
            state.bpInterface,
            state.objVsBP,
            state.pairFilter);
        state.physicsSystem.SetGravity(new JPH.Vec3(0f, -9.81f, 0f));
        state.bodyInterface = state.physicsSystem.GetBodyInterface();
        state.scratchPos = new JPH.Vec3();
        state.scratchRot = new JPH.Quat();

        // ── Render resources ─────────────────────────────────────────────────
        state.cubeInstances      = new InstanceData[MAX_INSTANCES];
        state.sphereInstances    = new InstanceData[MAX_INSTANCES];
        state.cylinderInstances  = new InstanceData[MAX_INSTANCES];
        state.wfSphereInstances  = new InstanceData[MAX_INSTANCES];
        state.wfBoxInstances     = new InstanceData[MAX_INSTANCES];
        CreateCubeMesh();
        CreateSphereMesh();
        CreateCylinderMesh();
        CreateWireframeBindings();
        state.tapCylInstanceBuf = sg_make_buffer(new sg_buffer_desc
        {
            size  = (nuint)(MAX_INSTANCES * sizeof(InstanceData)),
            usage = new sg_buffer_usage { stream_update = true },
            label = "tapered-cyl-instances"
        });
        state.tapCylDraws = new List<(sg_bindings, int, Matrix4x4, Vector3, bool)>(64);
        state.softBodies  = new List<SoftBodyRenderEntry>(32);
        state.rodBodies   = new List<RodBodyRenderEntry>(32);
        state.debugLines  = new List<ConeVertex>(4096);
        state.debugLinesBuf = sg_make_buffer(new sg_buffer_desc
        {
            size  = 4096u * (nuint)System.Runtime.InteropServices.Marshal.SizeOf<ConeVertex>(),
            usage = new sg_buffer_usage { stream_update = true },
            label = "debug-lines-vb"
        });
        state.debugTris   = new List<ConeVertex>(4096);
        state.debugTrisBuf = sg_make_buffer(new sg_buffer_desc
        {
            size  = 4096u * (nuint)System.Runtime.InteropServices.Marshal.SizeOf<ConeVertex>(),
            usage = new sg_buffer_usage { stream_update = true },
            label = "debug-tris-vb"
        });
        state.bodyLabels  = new List<(JPH.BodyID, string)>();
        CreatePipeline();

        // ── Camera (reset when each demo loads) ──────────────────────────────
        state.camera = new Camera();
        state.camera.MoveSpeed        = 10;
        state.camera.MouseSensitivity = 0.3f;

        simgui_setup(new simgui_desc_t());

        // ── Register demos ────────────────────────────────────────────────────
        state.demos = new List<DemoBase>
        {
            new Demo_Simple(),
            new Demo_Stack(),
            new Demo_Wall(),
            new Demo_Pyramid(),
            new Demo_Island(),
            new Demo_Friction(),
            new Demo_Restitution(),
            new Demo_MassSpawn(),
            new Demo_Dominos(),
            new Demo_Avalanche(),
            new Demo_Crater(),
            new Demo_Stairs(),
            new Demo_Kinematic(),
            new Demo_Damping(),
            new Demo_HeavyOnLight(),
            new Demo_GravityFactor(),
            new Demo_Funnel(),
            new Demo_HighSpeed(),
            new Demo_Gyroscopic(), 
            new Demo_Bowling(), 
            new Demo_Billiards(),
            new Demo_BigVsSmall(), 
            new Demo_ChangeMotionType(),
            new Demo_ActivateDuringUpdate(), 
            new Demo_ChangeMotionQuality(),
            new Demo_ContactListener(),
            new Demo_ConveyorBelt(),
            new Demo_Sensor(),
            new Demo_ContactManifold(),
            new Demo_CenterOfMass(),
            new Demo_ChangeShape(),
            new Demo_ModifyMass(),
            new Demo_ChangeObjectLayer(),
            new Demo_WreckingBall(), 
            new Demo_NewtonsCradle(), 
            new Demo_Seesaw(),
            new Demo_ChainPendulum(), 
            new Demo_Elevator(), 
            new Demo_Mace(),
            new Demo_PointConstraint(), 
            new Demo_HingeConstraint(),
            new Demo_ConeConstraint(), 
            new Demo_Pulley(),
            new Demo_Spring(), 
            new Demo_SwingTwist(),
            new Demo_Gear(),
            new Demo_RackAndPinion(),
            new Demo_DistanceConstraint(),
            new Demo_FixedConstraint(),
            new Demo_SliderConstraint(),
            new Demo_PoweredHingeConstraint(),
            new Demo_PoweredSliderConstraint(),
            new Demo_SwingTwistConstraintFriction(),
            new Demo_ShapeBox(),
            new Demo_ShapeSphere(),
            new Demo_ShapeCapsule(),
            new Demo_ShapeCylinder(),
            new Demo_ShapeTaperedCapsule(),
            new Demo_ShapeTaperedCylinder(),
            new Demo_ShapeOffsetCOM(),
            new Demo_ShapeConvexHull(),
            new Demo_ShapeRotatedTranslated(),
            new Demo_ShapeStaticCompound(),
            new Demo_ShapeTriangle(),
            new Demo_ShapePlane(),
            new Demo_ShapeEmpty(),
            new Demo_ShapeMutableCompound(),
            new Demo_VehicleConstraint(),
            new Demo_Motorcycle(),
            new Demo_VehicleSixDOF(),
            new Demo_Tank(),
            new Demo_VehicleStress(),
            // ── Soft Body ──────────────────────────────────────────────
            new Demo_SoftBody_Shapes(),
            new Demo_SoftBody_VsFastMoving(),
            new Demo_SoftBody_Friction(),
            new Demo_SoftBody_Restitution(),
            new Demo_SoftBody_Pressure(),
            new Demo_SoftBody_GravityFactor(),
            new Demo_SoftBody_Force(),
            new Demo_SoftBody_Kinematic(),
            new Demo_SoftBody_UpdatePosition(),
            new Demo_SoftBody_StressTest(),
            new Demo_SoftBody_VertexRadius(),
            new Demo_SoftBody_LRAConstraint(),
            new Demo_SoftBody_BendConstraint(),
            new Demo_SoftBody_CosseratRod(),
            new Demo_SoftBody_ContactListener(),
            new Demo_SoftBody_Sensor(),
            new Demo_SoftBody_CustomUpdate(),
            new Demo_SoftBody_SkinnedConstraint(),

            // ── Character ──────────────────────────────────────────────────────
            new Demo_CharacterTest(),
            new Demo_CharacterVirtualTest(),

            // ── Rig ────────────────────────────────────────────────────────────
            new Demo_CreateRig(),
            new Demo_LoadRig(),
            new Demo_LoadSaveRig(),
            new Demo_LoadSaveBinaryRig(),
            new Demo_KinematicRig(),
            new Demo_SoftKeyframedRig(),
            new Demo_PoweredRig(),
            new Demo_RigPile(),
            new Demo_BigWorld(),
            new Demo_SkeletonMapper(),
            // ── Water ─────────────────────────────────────────────────────────────
            new Demo_WaterShape(),
            new Demo_Boat(),
        };

        // ── Load first demo ───────────────────────────────────────────────────
        state.bodies = new List<PhysicsBody>(256);
        LoadDemo(0);
    }

    // ── Frame ─────────────────────────────────────────────────────────────────
    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        float dt     = (float)sapp_frame_duration();
        int   width  = sapp_width();
        int   height = sapp_height();

        // Apply pending demo switch at the very start of the frame
        if (state.pendingDemoSwitch >= 0)
        {
            SwitchDemo(state.pendingDemoSwitch);
            state.pendingDemoSwitch = -1;
            state.paused = state.startPaused;
        }
        if (state.pendingReset)
        {
            LoadDemo(state.activeDemoIndex);
            state.pendingReset = false;
            state.paused = state.startPaused;
        }

        const float PhysicsStep = 1.0f / 60.0f;
        var activeDemo = state.demos[state.activeDemoIndex];
        state.debugLines.Clear();   // clear per-frame debug lines before Update populates them
        // debugTris are cleared inside the physics step so they persist on render-only frames.
        state.time += dt;
        if (!state.paused)
        {
            state.physicsAccum += dt;
            activeDemo.CameraLongitude = state.camera.Longitude;
            while (state.physicsAccum >= PhysicsStep)
            {
                state.debugTris.Clear();  // fresh tris for this physics step
                activeDemo.Update(PhysicsStep, state.bodyInterface, state.bodies);
                state.physicsSystem.Update(PhysicsStep, activeDemo.CollisionSteps, state.alloc, state.jobs);
                state.physicsAccum -= PhysicsStep;
            }
        }

        // ── Follow-camera: update orbit center to track the vehicle ───────────
        if (activeDemo.CameraFollowsPlayer)
        {
            state.camera.Center = activeDemo.GetFollowPosition(state.bodyInterface);

            // Third-person lag: smoothly swing camera to stay behind the vehicle.
            float targetYaw = activeDemo.GetFollowYaw(state.bodyInterface);
            if (!float.IsNaN(targetYaw))
            {
                // Camera longitude that places the eye *behind* the car (+Z forward → lon=180)
                float targetLon = -180f - targetYaw;
                float current   = state.camera.Longitude;
                // Shortest-path angular difference in (−180, +180]
                float diff = ((targetLon - current) % 360f + 540f) % 360f - 180f;
                // Exponential lag: 95 % correction per second, frame-rate independent
                state.camera.Longitude = current + diff * (1f - MathF.Pow(0.05f, dt));
            }
        }

        state.camera.Update(width, height, dt);

        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });

        int cubeCount      = 0;
        int sphereCount    = 0;
        int cylinderCount  = 0;
        int wfSphereCount  = 0;
        int wfBoxCount     = 0;
        state.tapCylDraws.Clear();

        var outPos = state.scratchPos;
        var outRot = state.scratchRot;
        foreach (var body in state.bodies)
        {
            state.bodyInterface.GetPositionAndRotation(body.bodyId, outPos, outRot);
            if (body.localOffset != Vector3.Zero)
            {
                // Sub-shape: transform local offset via the body's shape-origin-to-world matrix
                using var wt   = state.bodyInterface.GetWorldTransform(body.bodyId);
                using var lp   = new JPH.Vec3(body.localOffset.X, body.localOffset.Y, body.localOffset.Z);
                using var rOff = wt.Multiply3x3(lp);
                using var tr   = wt.GetTranslation();
                outPos.Set(tr.GetX() + rOff.GetX(), tr.GetY() + rOff.GetY(), tr.GetZ() + rOff.GetZ());
                if (body.localRotation.W != 0f)   // non-default local rotation (W==0 means "use body rot as-is")
                {
                    var bq = new Quaternion(outRot.GetX(), outRot.GetY(), outRot.GetZ(), outRot.GetW());
                    var wq = bq * body.localRotation;
                    outRot.Set(wq.X, wq.Y, wq.Z, wq.W);
                }
            }
            var model = MakeModelMatrix(outPos, outRot, body.scale);

            if (body.shape == RenderShape.Sphere)
            {
                if (body.wireframe)
                {
                    if (wfSphereCount < MAX_INSTANCES)
                        state.wfSphereInstances[wfSphereCount++] = new InstanceData { model = model, color = body.color, shapeType = 3.0f };
                }
                else
                {
                    if (sphereCount < MAX_INSTANCES)
                        state.sphereInstances[sphereCount++] = new InstanceData { model = model, color = body.color, shapeType = 1.0f };
                }
            }
            else if (body.shape == RenderShape.Capsule)
            {
                // scale = (radius, halfCylinderHeight, radius); render as cylinder + 2 sphere caps
                float r = body.scale.X;
                float h = body.scale.Y;
                var quat    = new Quaternion(outRot.GetX(), outRot.GetY(), outRot.GetZ(), outRot.GetW());
                var bPos    = new Vector3(outPos.GetX(), outPos.GetY(), outPos.GetZ());
                var upWorld = Vector3.Transform(new Vector3(0f, h, 0f), quat);
                var capSc   = new Vector3(r * 2f);
                if (cylinderCount < MAX_INSTANCES)
                    state.cylinderInstances[cylinderCount++] = new InstanceData { model = MakeModelMatrix(outPos, outRot, new Vector3(r * 2f, h * 2f, r * 2f)), color = body.color, shapeType = 0f };
                if (sphereCount < MAX_INSTANCES)
                    state.sphereInstances[sphereCount++] = new InstanceData { model = Matrix4x4.CreateScale(capSc) * Matrix4x4.CreateFromQuaternion(quat) * Matrix4x4.CreateTranslation(bPos + upWorld), color = body.color, shapeType = 1f };
                if (sphereCount < MAX_INSTANCES)
                    state.sphereInstances[sphereCount++] = new InstanceData { model = Matrix4x4.CreateScale(capSc) * Matrix4x4.CreateFromQuaternion(quat) * Matrix4x4.CreateTranslation(bPos - upWorld), color = body.color, shapeType = 1f };
            }
            else if (body.shape == RenderShape.TaperedCylinder)
            {
                state.tapCylDraws.Add((body.customMesh, body.customMeshIndexCount, model, body.color, body.smoothCustomMesh));
            }
            else if (body.shape == RenderShape.Cylinder)
            {
                if (cylinderCount < MAX_INSTANCES)
                    state.cylinderInstances[cylinderCount++] = new InstanceData { model = model, color = body.color, shapeType = 0.0f };

                // Gear teeth: small boxes arranged radially around the perimeter
                if (body.numTeeth > 0)
                {
                    float radius  = body.scale.X * 0.5f;
                    float axialH  = body.scale.Y;
                    float toothH  = body.toothHeight;
                    float toothT  = 0.015f;
                    var bquat = new Quaternion(outRot.GetX(), outRot.GetY(), outRot.GetZ(), outRot.GetW());
                    var bpos  = new Vector3(outPos.GetX(), outPos.GetY(), outPos.GetZ());
                    var ts    = new Vector3(toothH, axialH, toothT);
                    for (int ti = 0; ti < body.numTeeth && cubeCount < MAX_INSTANCES; ti++)
                    {
                        float a = 2f * MathF.PI * ti / body.numTeeth;
                        var localOff = new Vector3((radius + toothH * 0.5f) * MathF.Cos(a), 0f, (radius + toothH * 0.5f) * MathF.Sin(a));
                        var wpos = bpos + Vector3.Transform(localOff, bquat);
                        var tq   = Quaternion.Multiply(bquat, Quaternion.CreateFromAxisAngle(Vector3.UnitY, a));
                        var tm   = Matrix4x4.CreateScale(ts) * Matrix4x4.CreateFromQuaternion(tq) * Matrix4x4.CreateTranslation(wpos);
                        state.cubeInstances[cubeCount++] = new InstanceData { model = tm, color = body.color, shapeType = 0f };
                    }
                }
            }
            else
            {
                if (body.wireframe)
                {
                    // Wireframe box (sensor volume outline) — no teeth support needed for sensors
                    if (wfBoxCount < MAX_INSTANCES)
                        state.wfBoxInstances[wfBoxCount++] = new InstanceData { model = model, color = body.color, shapeType = 4.0f };
                }
                else
                {
                if (cubeCount < MAX_INSTANCES)
                    state.cubeInstances[cubeCount++] = new InstanceData { model = model, color = body.color, shapeType = (float)body.shape };

                // Rack teeth: small boxes along X axis on the +Y surface
                if (body.numTeeth > 0)
                {
                    float rackHalfH = body.scale.Y * 0.5f;
                    float rackLen   = body.scale.X;
                    float toothH    = body.toothHeight;
                    float spacing   = rackLen / body.numTeeth;
                    var bquat = new Quaternion(outRot.GetX(), outRot.GetY(), outRot.GetZ(), outRot.GetW());
                    var bpos  = new Vector3(outPos.GetX(), outPos.GetY(), outPos.GetZ());
                    var ts    = new Vector3(spacing * 0.65f, toothH, body.scale.Z * 0.8f);
                    for (int ti = 0; ti < body.numTeeth && cubeCount < MAX_INSTANCES; ti++)
                    {
                        float x = -rackLen * 0.5f + (ti + 0.5f) * spacing;
                        var localOff = new Vector3(x, rackHalfH + toothH * 0.5f, 0f);
                        var wpos = bpos + Vector3.Transform(localOff, bquat);
                        var tm   = Matrix4x4.CreateScale(ts) * Matrix4x4.CreateFromQuaternion(bquat) * Matrix4x4.CreateTranslation(wpos);
                        state.cubeInstances[cubeCount++] = new InstanceData { model = tm, color = body.color, shapeType = 0f };
                    }
                }
                } // end opaque box branch
            }
        }

        // ── Opaque pass ───────────────────────────────────────────────────────
        sg_apply_pipeline(state.pip_smooth);
        if (cubeCount     > 0) RenderCubesInstanced(cubeCount);
        if (sphereCount   > 0) RenderSpheresInstanced(sphereCount);
        if (cylinderCount > 0) RenderCylindersInstanced(cylinderCount);
        if (state.tapCylDraws.Count > 0 || state.softBodies.Count > 0 || state.rodBodies.Count > 0 || state.debugLines.Count > 0)
        {
            var vsP = new vs_params_t { vp = state.camera.ViewProj };
            var fsP = new fs_params_t
            {
                light_dir = Vector3.Normalize(new Vector3(0.6f, 1.0f, 0.4f)),
                view_pos  = state.camera.EyePos
            };
            foreach (var (mesh, idxCount, mdl, col, smooth) in state.tapCylDraws)
            {
                var inst   = new InstanceData { model = mdl, color = col, shapeType = smooth ? 6f : 5f };
                int offset = sg_append_buffer(state.tapCylInstanceBuf, SG_RANGE<InstanceData>(ref inst));
                var b      = mesh;
                b.vertex_buffers[1]        = state.tapCylInstanceBuf;
                b.vertex_buffer_offsets[1] = offset;
                sg_apply_bindings(b);
                sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vsP));
                sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fsP));
                sg_draw(0, (uint)idxCount, 1);
            }
            foreach (var sb in state.softBodies)
            {
                uint n;
                JPH.Mat44 comTransform;
                if (sb.standaloneBody != null)
                {
                    n = (uint)((JPH.SoftBodyMotionProperties)sb.standaloneBody.GetMotionProperties()!).GetVertices().Size();
                    comTransform = sb.standaloneBody.GetCenterOfMassTransform();
                }
                else
                {
                    n  = state.physicsSystem.GetSoftBodyVertexCount(sb.bodyId);
                    comTransform = state.bodyInterface.GetCenterOfMassTransform(sb.bodyId);
                }
                // Vertex positions are body-local; transform to world space via CenterOfMassTransform.
                // Normals are not computed here — the shader uses flat (dFdx/dFdy) shading for soft bodies.
                for (uint i = 0; i < n && i < (uint)sb.scratch.Length; i++)
                {
                    JPH.Const_Vec3 localP;
                    if (sb.standaloneBody != null)
                        localP = ((JPH.SoftBodyMotionProperties)sb.standaloneBody.GetMotionProperties()!).GetVertices()[(UIntPtr)i].mPosition;
                    else
                        localP = state.physicsSystem.GetSoftBodyVertexPosition(sb.bodyId, i);
                    using var worldP = comTransform * (JPH.Const_Vec3)localP;
                    localP.Dispose();
                    sb.scratch[i].position = new Vector3(worldP.GetX(), worldP.GetY(), worldP.GetZ());
                }
                comTransform.Dispose();
                sg_update_buffer(sb.vertexBuf, SG_RANGE<ConeVertex>(sb.scratch.AsSpan(0, (int)n)));
                sg_apply_pipeline(state.pip_smooth_u32);
                var inst   = new InstanceData { model = Matrix4x4.Identity, color = sb.color, shapeType = 7f };
                int offset = sg_append_buffer(state.tapCylInstanceBuf, SG_RANGE<InstanceData>(ref inst));
                var b      = default(sg_bindings);
                b.vertex_buffers[0]        = sb.vertexBuf;
                b.index_buffer             = sb.indexBuf;
                b.vertex_buffers[1]        = state.tapCylInstanceBuf;
                b.vertex_buffer_offsets[1] = offset;
                sg_apply_bindings(b);
                sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vsP));
                sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fsP));
                sg_draw(0, (uint)sb.indexCount, 1);
            }
            // ── Rod body pass (Cosserat rods, line segments) ──────────────────
            if (state.rodBodies.Count > 0)
            {
                sg_apply_pipeline(state.pip_lines);
                foreach (var rb in state.rodBodies)
                {
                    using var comTransform = state.bodyInterface.GetCenterOfMassTransform(rb.bodyId);
                    for (int ri = 0; ri < rb.rodCount; ri++)
                    {
                        for (int vi = 0; vi < 2; vi++)
                        {
                            uint vtxIdx = vi == 0 ? rb.rods[ri].v0 : rb.rods[ri].v1;
                            using var localP = state.physicsSystem.GetSoftBodyVertexPosition(rb.bodyId, vtxIdx);
                            using var worldP = comTransform * (JPH.Const_Vec3)localP;
                            rb.scratch[ri * 2 + vi].position = new Vector3(worldP.GetX(), worldP.GetY(), worldP.GetZ());
                        }
                    }
                    sg_update_buffer(rb.vertexBuf, SG_RANGE<ConeVertex>(rb.scratch.AsSpan(0, rb.rodCount * 2)));
                    var inst   = new InstanceData { model = Matrix4x4.Identity, color = rb.color, shapeType = 0f };
                    int offset = sg_append_buffer(state.tapCylInstanceBuf, SG_RANGE<InstanceData>(ref inst));
                    var b      = default(sg_bindings);
                    b.vertex_buffers[0]        = rb.vertexBuf;
                    b.vertex_buffers[1]        = state.tapCylInstanceBuf;
                    b.vertex_buffer_offsets[1] = offset;
                    sg_apply_bindings(b);
                    sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vsP));
                    sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fsP));
                    sg_draw(0, (uint)(rb.rodCount * 2), 1);
                }
            }
            // ── Debug lines pass (arbitrary world-space line segments from demos) ──
            if (state.debugLines.Count > 0)
            {
                const int kDebugLinesCap = 4096;
                int drawCount = System.Math.Min(state.debugLines.Count, kDebugLinesCap);
                sg_apply_pipeline(state.pip_lines);
                var debugSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(state.debugLines).Slice(0, drawCount);
                sg_update_buffer(state.debugLinesBuf, SG_RANGE<ConeVertex>(debugSpan));
                var inst   = new InstanceData { model = Matrix4x4.Identity, color = new Vector3(1f, 1f, 1f), shapeType = 9f };
                int offset = sg_append_buffer(state.tapCylInstanceBuf, SG_RANGE<InstanceData>(ref inst));
                var b      = default(sg_bindings);
                b.vertex_buffers[0]        = state.debugLinesBuf;
                b.vertex_buffers[1]        = state.tapCylInstanceBuf;
                b.vertex_buffer_offsets[1] = offset;
                sg_apply_bindings(b);
                sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vsP));
                sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fsP));
                sg_draw(0, (uint)drawCount, 1);
            }
        }

        // ── Wireframe (alpha-blend) pass — rendered after all opaque ──────────
        if (wfSphereCount > 0 || wfBoxCount > 0)
        {
            sg_apply_pipeline(state.pip_blend);
            if (wfSphereCount > 0) RenderWireframeSpheresInstanced(wfSphereCount);
            if (wfBoxCount    > 0) RenderWireframeBoxesInstanced(wfBoxCount);
        }

        // ── Water pass — drawn last so transparent blend shows objects underneath ──
        // Depth test ON (LESS_EQUAL): above-waterline objects occlude water correctly.
        // Depth write OFF: underwater object pixels already in framebuffer blend through.
        if (state.debugTris.Count > 0)
        {
            const int kWaterCap = 4096;
            int waterCount = System.Math.Min(state.debugTris.Count, kWaterCap);
            var trisSpan   = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(state.debugTris).Slice(0, waterCount);
            sg_update_buffer(state.debugTrisBuf, SG_RANGE<ConeVertex>(trisSpan));
            sg_apply_pipeline(state.pip_water);
            var wVsP = new water_vs_params_t { vp = state.camera.ViewProj };
            var wFsP = new water_fs_params_t
            {
                view_pos_time = new Vector4(state.camera.EyePos, state.time),
                light_dir     = new Vector4(0.6f, 1.0f, 0.4f, 0f),
                water_color   = new Vector4(0.04f, 0.20f, 0.52f, 0f),
            };
            var wb = default(sg_bindings);
            wb.vertex_buffers[0] = state.debugTrisBuf;
            sg_apply_bindings(wb);
            sg_apply_uniforms(UB_water_vs_params, SG_RANGE<water_vs_params_t>(ref wVsP));
            sg_apply_uniforms(UB_water_fs_params, SG_RANGE<water_fs_params_t>(ref wFsP));
            sg_draw(0, (uint)waterCount, 1);
        }

        DrawBrowserUI();
        simgui_render();

        sg_end_pass();
        sg_commit();
    }

    // ── Event ─────────────────────────────────────────────────────────────────
    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        if (simgui_handle_event(*e))
            return;
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN ||
            e->type == sapp_event_type.SAPP_EVENTTYPE_KEY_UP)
        {
            DemoBase.SetKeyDown((int)e->key_code, e->type == sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN);
        }
        // ── Touch events: multi-touch aware, handles joystick + buttons simultaneously ──
        bool isTouchEvent =
            e->type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN   ||
            e->type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_MOVED   ||
            e->type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED   ||
            e->type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_CANCELLED;
        if (isTouchEvent)
        {
            bool isEnding = e->type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED ||
                            e->type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_CANCELLED;

            // Always recompute hit areas from current screen size so first-touch is correct.
            { int sw2 = sapp_width(), sh2 = sapp_height();
              float br = MathF.Min(sw2, sh2) * 0.13f, mg = br * 0.55f;
              float jcx = state.joystickOnRight ? sw2 - mg - br : mg + br;
              _joyHitArea = new Vector3(jcx, sh2 - mg - br, br * 1.6f); }

            for (int ti = 0; ti < e->num_touches; ti++)
            {
                ref var tp = ref e->touches[ti];
                if (isEnding && tp.changed)
                {
                    _activeTouches.Remove(tp.identifier);
                    if (tp.identifier == _joyTouchId) _joyTouchId = nuint.MaxValue;
                }
                else if (!isEnding)
                {
                    _activeTouches[tp.identifier] = new Vector2(tp.pos_x, tp.pos_y);
                    // Assign joystick touch when a new finger lands in the joystick area.
                    if (e->type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN &&
                        tp.changed && _joyTouchId == nuint.MaxValue)
                    {
                        var jh2 = _joyHitArea;
                        float jd2x = tp.pos_x - jh2.X, jd2y = tp.pos_y - jh2.Y;
                        if (jd2x * jd2x + jd2y * jd2y <= jh2.Z * jh2.Z)
                            _joyTouchId = tp.identifier;
                    }
                }
            }
            if (_activeTouches.Count == 0) { _joyTouchId = nuint.MaxValue; }

            // Recalculate _vcCapturing: true if ANY active touch is on a virtual control.
            _vcCapturing = false;
            foreach (var pos in _activeTouches.Values)
            {
                var jh3 = _joyHitArea;
                float jd3x = pos.X - jh3.X, jd3y = pos.Y - jh3.Y;
                if (jd3x * jd3x + jd3y * jd3y <= jh3.Z * jh3.Z) { _vcCapturing = true; break; }
                foreach (var bh in _btnHitAreas)
                {
                    float bd3x = pos.X - bh.X, bd3y = pos.Y - bh.Y;
                    if (bd3x * bd3x + bd3y * bd3y <= bh.Z * bh.Z) { _vcCapturing = true; break; }
                }
                if (_vcCapturing) break;
            }
            if (_vcCapturing) { state.camera.CancelTouch(); return; }
            state.camera.HandleEvent(e);
            return;
        }

        // ── Mouse events (desktop / web) ──────────────────────────────────────
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN)
        {
            float mx = e->mouse_x, my = e->mouse_y;
            bool hitJoy = false, hitBtn = false;
            var jh = _joyHitArea;
            float jdx = mx - jh.X, jdy = my - jh.Y;
            if (jdx * jdx + jdy * jdy <= jh.Z * jh.Z) hitJoy = true;
            foreach (var bh in _btnHitAreas)
            {
                float bdx = mx - bh.X, bdy = my - bh.Y;
                if (bdx * bdx + bdy * bdy <= bh.Z * bh.Z) { hitBtn = true; break; }
            }
            if (hitJoy || hitBtn) { _vcCapturing = true; state.camera.CancelTouch(); return; }
        }
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP)
        {
            if (_vcCapturing) { _vcCapturing = false; return; }
        }
        if (_vcCapturing &&
            (e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE ||
             e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_SCROLL))
            return;
        state.camera.HandleEvent(e);
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────
    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        simgui_shutdown();
        state.scratchPos?.Dispose();
        state.scratchRot?.Dispose();
        state.physicsSystem?.Dispose();
        state.objVsBP?.Dispose();
        state.pairFilter?.Dispose();
        state.bpInterface?.Dispose();
        state.jobs?.Dispose();
        state.alloc?.Dispose();
        JPH.Const_JoltHelpers.Shutdown();
        sg_shutdown();
        if (Debugger.IsAttached) Environment.Exit(0);
    }

    // ── Demo switching ────────────────────────────────────────────────────────
    static void SwitchDemo(int newIndex)
    {
        if (newIndex == state.activeDemoIndex) return;
        LoadDemo(newIndex);
    }

    static void LoadDemo(int index)
    {
        if (state.activeDemoIndex >= 0)
        {
            state.demos[state.activeDemoIndex].Deactivate(state.physicsSystem);
            state.demos[state.activeDemoIndex].Cleanup(state.physicsSystem);
        }

        // Destroy soft body GPU buffers from previous demo
        if (state.softBodies != null && state.softBodies.Count > 0)
        {
            var addedIds = new System.Collections.Generic.List<JPH.BodyID>(state.softBodies.Count);
            foreach (var sb in state.softBodies)
            {
                sg_destroy_buffer(sb.vertexBuf);
                sg_destroy_buffer(sb.indexBuf);
                if (sb.standaloneBody != null)
                    state.bodyInterface.DestroyBody(sb.bodyId);
                else
                    addedIds.Add(sb.bodyId);
            }
            if (addedIds.Count > 0)
            {
                var ids = addedIds.ToArray();
                state.bodyInterface.RemoveBodies(ids);
                state.bodyInterface.DestroyBodies(ids);
            }
            state.softBodies.Clear();
        }

        // Destroy rod body GPU buffers and remove/destroy their Jolt physics bodies
        if (state.rodBodies != null && state.rodBodies.Count > 0)
        {
            foreach (var rb in state.rodBodies)
                sg_destroy_buffer(rb.vertexBuf);
            var rodIds = new JPH.BodyID[state.rodBodies.Count];
            for (int i = 0; i < state.rodBodies.Count; i++)
                rodIds[i] = state.rodBodies[i].bodyId;
            state.bodyInterface.RemoveBodies(rodIds);
            state.bodyInterface.DestroyBodies(rodIds);
            state.rodBodies.Clear();
        }

        state.bodyLabels?.Clear();

        if (state.bodies != null && state.bodies.Count > 0)
        {
            // Destroy per-body custom meshes (TaperedCylinder sub-shapes) before clearing bodies.
            foreach (var b in state.bodies)
            {
                if (b.shape == RenderShape.TaperedCylinder && b.customMesh.vertex_buffers[0].id != 0)
                {
                    sg_destroy_buffer(b.customMesh.vertex_buffers[0]);
                    sg_destroy_buffer(b.customMesh.index_buffer);
                }
            }
            // Deduplicate: some demos register the same bodyId multiple times (sub-shapes).
            var seen = new HashSet<uint>();
            var unique = new System.Collections.Generic.List<JPH.BodyID>(state.bodies.Count);
            foreach (var b in state.bodies)
            {
                if (seen.Add(b.bodyId.GetIndexAndSequenceNumber()))
                    unique.Add(b.bodyId);
            }
            var ids = unique.ToArray();
            state.bodyInterface.RemoveBodies(ids);
            state.bodyInterface.DestroyBodies(ids);
            state.bodies.Clear();
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();

        state.activeDemoIndex = index;
        state.random          = new Random(42);

        var demo = state.demos[index];
        demo.SetSoftBodyList(state.softBodies);
        demo.SetRodBodyList(state.rodBodies);
        demo.SetDebugLineList(state.debugLines);
        demo.SetDebugTriList(state.debugTris);
        demo.SetBodyLabelList(state.bodyLabels);
        demo.Init(state.bodyInterface, state.physicsSystem, state.bodies, state.random);
        state.physicsSystem.OptimizeBroadPhase();
        demo.Activate(state.physicsSystem);

        if (!state.lockCamera)
        {
            state.camera.Init(demo.GetCameraDesc());
            state.camera.MoveSpeed        = 10;
            state.camera.MouseSensitivity = 0.3f;
        }
        state.camera.SuppressArrowKeys = demo.CameraFollowsPlayer;
        // Pre-populate hit areas now that sapp_width/height are valid, so the
        // very first touch event can correctly detect the virtual control area.
        ComputeVirtualControlHitAreas(demo);
        state.camera.CancelTouch();
    }



    // ── Mesh creation ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates wireframe bind objects that share the opaque mesh VBs/IBs but
    /// have their own streaming instance buffers.
    /// Must be called AFTER CreateCubeMesh and CreateSphereMesh.
    /// </summary>
    static unsafe void CreateWireframeBindings()
    {
        // Wireframe sphere: reuse sphere mesh VB + IB
        state.wf_sphere_bind.vertex_buffers[0] = state.sphere_bind.vertex_buffers[0];
        state.wf_sphere_bind.index_buffer      = state.sphere_bind.index_buffer;
        state.wf_sphere_bind.vertex_buffers[1] = sg_make_buffer(new sg_buffer_desc
        {
            size  = (nuint)(MAX_INSTANCES * sizeof(InstanceData)),
            usage = new sg_buffer_usage { stream_update = true },
            label = "wf-sphere-instances"
        });

        // Wireframe box: reuse cube mesh VB + IB
        state.wf_box_bind.vertex_buffers[0] = state.cube_bind.vertex_buffers[0];
        state.wf_box_bind.index_buffer      = state.cube_bind.index_buffer;
        state.wf_box_bind.vertex_buffers[1] = sg_make_buffer(new sg_buffer_desc
        {
            size  = (nuint)(MAX_INSTANCES * sizeof(InstanceData)),
            usage = new sg_buffer_usage { stream_update = true },
            label = "wf-box-instances"
        });
    }

    static unsafe void CreateCubeMesh()
    {
        Vertex[] verts = new Vertex[24];
        // Front (Z+)
        verts[0]  = new Vertex { position = new Vector3(-0.5f,-0.5f, 0.5f), normal = new Vector3( 0, 0, 1) };
        verts[1]  = new Vertex { position = new Vector3( 0.5f,-0.5f, 0.5f), normal = new Vector3( 0, 0, 1) };
        verts[2]  = new Vertex { position = new Vector3( 0.5f, 0.5f, 0.5f), normal = new Vector3( 0, 0, 1) };
        verts[3]  = new Vertex { position = new Vector3(-0.5f, 0.5f, 0.5f), normal = new Vector3( 0, 0, 1) };
        // Back (Z-)
        verts[4]  = new Vertex { position = new Vector3( 0.5f,-0.5f,-0.5f), normal = new Vector3( 0, 0,-1) };
        verts[5]  = new Vertex { position = new Vector3(-0.5f,-0.5f,-0.5f), normal = new Vector3( 0, 0,-1) };
        verts[6]  = new Vertex { position = new Vector3(-0.5f, 0.5f,-0.5f), normal = new Vector3( 0, 0,-1) };
        verts[7]  = new Vertex { position = new Vector3( 0.5f, 0.5f,-0.5f), normal = new Vector3( 0, 0,-1) };
        // Top (Y+)
        verts[8]  = new Vertex { position = new Vector3(-0.5f, 0.5f, 0.5f), normal = new Vector3( 0, 1, 0) };
        verts[9]  = new Vertex { position = new Vector3( 0.5f, 0.5f, 0.5f), normal = new Vector3( 0, 1, 0) };
        verts[10] = new Vertex { position = new Vector3( 0.5f, 0.5f,-0.5f), normal = new Vector3( 0, 1, 0) };
        verts[11] = new Vertex { position = new Vector3(-0.5f, 0.5f,-0.5f), normal = new Vector3( 0, 1, 0) };
        // Bottom (Y-)
        verts[12] = new Vertex { position = new Vector3(-0.5f,-0.5f,-0.5f), normal = new Vector3( 0,-1, 0) };
        verts[13] = new Vertex { position = new Vector3( 0.5f,-0.5f,-0.5f), normal = new Vector3( 0,-1, 0) };
        verts[14] = new Vertex { position = new Vector3( 0.5f,-0.5f, 0.5f), normal = new Vector3( 0,-1, 0) };
        verts[15] = new Vertex { position = new Vector3(-0.5f,-0.5f, 0.5f), normal = new Vector3( 0,-1, 0) };
        // Right (X+)
        verts[16] = new Vertex { position = new Vector3( 0.5f,-0.5f, 0.5f), normal = new Vector3( 1, 0, 0) };
        verts[17] = new Vertex { position = new Vector3( 0.5f,-0.5f,-0.5f), normal = new Vector3( 1, 0, 0) };
        verts[18] = new Vertex { position = new Vector3( 0.5f, 0.5f,-0.5f), normal = new Vector3( 1, 0, 0) };
        verts[19] = new Vertex { position = new Vector3( 0.5f, 0.5f, 0.5f), normal = new Vector3( 1, 0, 0) };
        // Left (X-)
        verts[20] = new Vertex { position = new Vector3(-0.5f,-0.5f,-0.5f), normal = new Vector3(-1, 0, 0) };
        verts[21] = new Vertex { position = new Vector3(-0.5f,-0.5f, 0.5f), normal = new Vector3(-1, 0, 0) };
        verts[22] = new Vertex { position = new Vector3(-0.5f, 0.5f, 0.5f), normal = new Vector3(-1, 0, 0) };
        verts[23] = new Vertex { position = new Vector3(-0.5f, 0.5f,-0.5f), normal = new Vector3(-1, 0, 0) };

        ushort[] idx = new ushort[36]
        {
             0, 1, 2,  0, 2, 3,
             4, 5, 6,  4, 6, 7,
             8, 9,10,  8,10,11,
            12,13,14, 12,14,15,
            16,17,18, 16,18,19,
            20,21,22, 20,22,23
        };

        state.cube_bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc { data = SG_RANGE<Vertex>(verts) });
        state.cube_bind.index_buffer      = sg_make_buffer(new sg_buffer_desc
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data  = SG_RANGE(idx)
        });
        state.cube_bind.vertex_buffers[1] = sg_make_buffer(new sg_buffer_desc
        {
            size  = (nuint)(MAX_INSTANCES * sizeof(InstanceData)),
            usage = new sg_buffer_usage { stream_update = true },
            label = "cube-instances"
        });
    }

    static void CreatePipeline()
    {
        var shd = sg_make_shader(physics_demo_smooth_shader_desc(sg_query_backend()));

        // ── Opaque pipeline ───────────────────────────────────────────────
        var pip = default(sg_pipeline_desc);
        pip.shader = shd;
        pip.layout.attrs[ATTR_physics_demo_smooth_position] =
            new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT3, buffer_index = 0 };
        pip.layout.attrs[ATTR_physics_demo_smooth_normal] =
            new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT3, buffer_index = 0 };
        pip.layout.buffers[1].step_func = SG_VERTEXSTEP_PER_INSTANCE;
        pip.layout.buffers[1].stride    = sizeof(InstanceData);
        pip.layout.attrs[ATTR_physics_demo_smooth_inst_model_0] =
            new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 0 };
        pip.layout.attrs[ATTR_physics_demo_smooth_inst_model_1] =
            new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 16 };
        pip.layout.attrs[ATTR_physics_demo_smooth_inst_model_2] =
            new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 32 };
        pip.layout.attrs[ATTR_physics_demo_smooth_inst_model_3] =
            new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 48 };
        pip.layout.attrs[ATTR_physics_demo_smooth_inst_color] =
            new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 64 };
        pip.index_type          = SG_INDEXTYPE_UINT16;
        pip.cull_mode           = SG_CULLMODE_NONE;
        pip.face_winding        = sg_face_winding.SG_FACEWINDING_CCW;
        pip.depth.compare       = SG_COMPAREFUNC_LESS_EQUAL;
        pip.depth.write_enabled = true;
        state.pip_smooth = sg_make_pipeline(pip);

        // ── Soft-body pipeline (UINT32 indices, supports >65535 vertices) ─
        pip.index_type = SG_INDEXTYPE_UINT32;
        state.pip_smooth_u32 = sg_make_pipeline(pip);
        pip.index_type = SG_INDEXTYPE_UINT16; // restore for blend pipeline

        // ── Alpha-blend pipeline (wireframe sensor shapes) ────────────────
        // Same layout; no depth write; standard src-alpha blend.
        pip.depth.write_enabled           = false;
        pip.colors[0].blend.enabled       = true;
        pip.colors[0].blend.src_factor_rgb    = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA;
        pip.colors[0].blend.dst_factor_rgb    = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
        pip.colors[0].blend.src_factor_alpha  = sg_blend_factor.SG_BLENDFACTOR_ONE;
        pip.colors[0].blend.dst_factor_alpha  = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
        state.pip_blend = sg_make_pipeline(pip);

        // ── Line segment pipeline (Cosserat rods) ─────────────────────────
        // Non-indexed, opaque, drawn as line primitives.
        pip.colors[0].blend.enabled = false;
        pip.depth.write_enabled     = true;
        pip.primitive_type          = sg_primitive_type.SG_PRIMITIVETYPE_LINES;
        pip.index_type              = SG_INDEXTYPE_NONE;
        state.pip_lines = sg_make_pipeline(pip);

        // ── Water surface pipeline (dedicated Fresnel + specular shader) ────
        // Drawn first each frame so opaque bodies can correctly occlude it.
        var wpip = default(sg_pipeline_desc);
        wpip.shader = sg_make_shader(water_shader_desc(sg_query_backend()));
        wpip.layout.attrs[ATTR_water_position] =
            new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT3, buffer_index = 0 };
        wpip.layout.attrs[ATTR_water_normal] =
            new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT3, buffer_index = 0, offset = 12 };
        wpip.primitive_type                   = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES;
        wpip.index_type                        = SG_INDEXTYPE_NONE;
        wpip.cull_mode                         = SG_CULLMODE_NONE;
        wpip.face_winding                      = sg_face_winding.SG_FACEWINDING_CCW;
        wpip.depth.compare                     = SG_COMPAREFUNC_LESS_EQUAL;
        wpip.depth.write_enabled               = false; // OFF — lets underwater objects show through
        wpip.colors[0].blend.enabled           = true;
        wpip.colors[0].blend.src_factor_rgb    = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA;
        wpip.colors[0].blend.dst_factor_rgb    = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
        wpip.colors[0].blend.src_factor_alpha  = sg_blend_factor.SG_BLENDFACTOR_ONE;
        wpip.colors[0].blend.dst_factor_alpha  = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
        wpip.label                             = "water";
        state.pip_water = sg_make_pipeline(wpip);
    }

    static unsafe void CreateSphereMesh()
    {
        const int segments = 16;
        const int rings    = 8;
        var verts  = new List<Vertex>();
        var idx    = new List<ushort>();

        for (int ring = 0; ring <= rings; ring++)
        {
            float phi = MathF.PI * ring / rings;
            for (int seg = 0; seg <= segments; seg++)
            {
                float theta = 2.0f * MathF.PI * seg / segments;
                float x = MathF.Sin(phi) * MathF.Cos(theta);
                float y = MathF.Cos(phi);
                float z = MathF.Sin(phi) * MathF.Sin(theta);
                verts.Add(new Vertex
                {
                    position = new Vector3(x * 0.5f, y * 0.5f, z * 0.5f),
                    normal   = new Vector3(x, y, z)
                });
            }
        }

        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                int cur  = ring * (segments + 1) + seg;
                int next = cur + segments + 1;
                idx.Add((ushort)cur);
                idx.Add((ushort)(cur + 1));
                idx.Add((ushort)next);
                idx.Add((ushort)(cur + 1));
                idx.Add((ushort)(next + 1));
                idx.Add((ushort)next);
            }
        }

        state.sphere_bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc { data = SG_RANGE<Vertex>(verts.ToArray()) });
        state.sphere_bind.index_buffer      = sg_make_buffer(new sg_buffer_desc
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data  = SG_RANGE<ushort>(idx.ToArray())
        });
        state.sphere_bind.vertex_buffers[1] = sg_make_buffer(new sg_buffer_desc
        {
            size  = (nuint)(MAX_INSTANCES * sizeof(InstanceData)),
            usage = new sg_buffer_usage { stream_update = true },
            label = "sphere-instances"
        });
    }

    static unsafe void CreateCylinderMesh()
    {
        const int seg = 32;
        var verts = new List<Vertex>();
        var idx   = new List<ushort>();

        // Top cap (Y = +0.5, normal up)
        int topCenterIdx = verts.Count;
        verts.Add(new Vertex { position = new Vector3(0f, 0.5f, 0f), normal = new Vector3(0f, 1f, 0f) });
        int topRimStart = verts.Count;
        for (int i = 0; i < seg; i++)
        {
            float a = 2f * MathF.PI * i / seg;
            verts.Add(new Vertex { position = new Vector3(0.5f * MathF.Cos(a), 0.5f, 0.5f * MathF.Sin(a)), normal = new Vector3(0f, 1f, 0f) });
        }
        for (int i = 0; i < seg; i++)
        {
            idx.Add((ushort)topCenterIdx);
            idx.Add((ushort)(topRimStart + i));
            idx.Add((ushort)(topRimStart + (i + 1) % seg));
        }

        // Bottom cap (Y = -0.5, normal down)
        int botCenterIdx = verts.Count;
        verts.Add(new Vertex { position = new Vector3(0f, -0.5f, 0f), normal = new Vector3(0f, -1f, 0f) });
        int botRimStart = verts.Count;
        for (int i = 0; i < seg; i++)
        {
            float a = 2f * MathF.PI * i / seg;
            verts.Add(new Vertex { position = new Vector3(0.5f * MathF.Cos(a), -0.5f, 0.5f * MathF.Sin(a)), normal = new Vector3(0f, -1f, 0f) });
        }
        for (int i = 0; i < seg; i++)
        {
            idx.Add((ushort)botCenterIdx);
            idx.Add((ushort)(botRimStart + (i + 1) % seg));
            idx.Add((ushort)(botRimStart + i));
        }

        // Side (outward normals, seg+1 columns to close the seam)
        int sideStart = verts.Count;
        for (int i = 0; i <= seg; i++)
        {
            float a  = 2f * MathF.PI * i / seg;
            float nx = MathF.Cos(a), nz = MathF.Sin(a);
            verts.Add(new Vertex { position = new Vector3(0.5f * nx,  0.5f, 0.5f * nz), normal = new Vector3(nx, 0f, nz) });
            verts.Add(new Vertex { position = new Vector3(0.5f * nx, -0.5f, 0.5f * nz), normal = new Vector3(nx, 0f, nz) });
        }
        for (int i = 0; i < seg; i++)
        {
            int b = sideStart + i * 2;
            idx.Add((ushort)b);       idx.Add((ushort)(b + 2)); idx.Add((ushort)(b + 1));
            idx.Add((ushort)(b + 1)); idx.Add((ushort)(b + 2)); idx.Add((ushort)(b + 3));
        }

        state.cylinder_bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc { data = SG_RANGE<Vertex>(verts.ToArray()) });
        state.cylinder_bind.index_buffer      = sg_make_buffer(new sg_buffer_desc
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data  = SG_RANGE<ushort>(idx.ToArray())
        });
        state.cylinder_bind.vertex_buffers[1] = sg_make_buffer(new sg_buffer_desc
        {
            size  = (nuint)(MAX_INSTANCES * sizeof(InstanceData)),
            usage = new sg_buffer_usage { stream_update = true },
            label = "cylinder-instances"
        });
    }

    // ── Render helpers ────────────────────────────────────────────────────────
    static unsafe void RenderCubesInstanced(int count)
    {
        fixed (InstanceData* p = state.cubeInstances)
        {
            sg_update_buffer(state.cube_bind.vertex_buffers[1], new sg_range
            {
                ptr  = p,
                size = (nuint)(count * sizeof(InstanceData))
            });
        }

        var vsP = new vs_params_t { vp = state.camera.ViewProj };
        var fsP = new fs_params_t
        {
            light_dir = Vector3.Normalize(new Vector3(0.5f, 1f, 0.3f)),
            view_pos  = state.camera.EyePos
        };

        sg_apply_bindings(state.cube_bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vsP));
        sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fsP));
        sg_draw(0, 36, (uint)count);
    }

    static unsafe void RenderSpheresInstanced(int count)
    {
        fixed (InstanceData* p = state.sphereInstances)
        {
            sg_update_buffer(state.sphere_bind.vertex_buffers[1], new sg_range
            {
                ptr  = p,
                size = (nuint)(count * sizeof(InstanceData))
            });
        }

        var vsP = new vs_params_t { vp = state.camera.ViewProj };
        var fsP = new fs_params_t
        {
            light_dir = Vector3.Normalize(new Vector3(0.5f, 1f, 0.3f)),
            view_pos  = state.camera.EyePos
        };

        sg_apply_bindings(state.sphere_bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vsP));
        sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fsP));
        const uint sphereIndexCount = (uint)(16 /* segments */ * 8 /* rings */ * 6);
        sg_draw(0, sphereIndexCount, (uint)count);
    }

    static unsafe void RenderCylindersInstanced(int count)
    {
        fixed (InstanceData* p = state.cylinderInstances)
        {
            sg_update_buffer(state.cylinder_bind.vertex_buffers[1], new sg_range
            {
                ptr  = p,
                size = (nuint)(count * sizeof(InstanceData))
            });
        }

        var vsP = new vs_params_t { vp = state.camera.ViewProj };
        var fsP = new fs_params_t
        {
            light_dir = Vector3.Normalize(new Vector3(0.5f, 1f, 0.3f)),
            view_pos  = state.camera.EyePos
        };

        sg_apply_bindings(state.cylinder_bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vsP));
        sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fsP));
        const uint cylIndexCount = (uint)(32 /* seg */ * 12);  // caps + side
        sg_draw(0, cylIndexCount, (uint)count);
    }

    static unsafe void RenderWireframeSpheresInstanced(int count)
    {
        fixed (InstanceData* p = state.wfSphereInstances)
        {
            sg_update_buffer(state.wf_sphere_bind.vertex_buffers[1], new sg_range
            {
                ptr  = p,
                size = (nuint)(count * sizeof(InstanceData))
            });
        }

        var vsP = new vs_params_t { vp = state.camera.ViewProj };
        var fsP = new fs_params_t
        {
            light_dir = Vector3.Normalize(new Vector3(0.5f, 1f, 0.3f)),
            view_pos  = state.camera.EyePos
        };

        sg_apply_bindings(state.wf_sphere_bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vsP));
        sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fsP));
        const uint sphereIndexCount = (uint)(16 /* segments */ * 8 /* rings */ * 6);
        sg_draw(0, sphereIndexCount, (uint)count);
    }

    static unsafe void RenderWireframeBoxesInstanced(int count)
    {
        fixed (InstanceData* p = state.wfBoxInstances)
        {
            sg_update_buffer(state.wf_box_bind.vertex_buffers[1], new sg_range
            {
                ptr  = p,
                size = (nuint)(count * sizeof(InstanceData))
            });
        }

        var vsP = new vs_params_t { vp = state.camera.ViewProj };
        var fsP = new fs_params_t
        {
            light_dir = Vector3.Normalize(new Vector3(0.5f, 1f, 0.3f)),
            view_pos  = state.camera.EyePos
        };

        sg_apply_bindings(state.wf_box_bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vsP));
        sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fsP));
        sg_draw(0, 36, (uint)count);
    }

    // ── ImGui browser UI ──────────────────────────────────────────────────────
    static void DrawBrowserUI()
    {
        simgui_new_frame(new simgui_frame_desc_t
        {
            width      = sapp_width(),
            height     = sapp_height(),
            delta_time = (float)sapp_frame_duration(),
            dpi_scale  = 1
        });

        var activeDemo = state.demos[state.activeDemoIndex];
        var noDecor = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;

        // ── Left panel: demo list ─────────────────────────────────────────────
        // Shorten the panel so it doesn't overlap the virtual joystick in the bottom-left.
        float listPanelH;
        {
            int sw2 = sapp_width(), sh2 = sapp_height();
            bool hasVC = activeDemo.VirtualControls != VirtualControlsType.None && state.virtualControlsEnabled;
            if (hasVC)
            {
                float br = MathF.Min(sw2, sh2) * 0.13f;
                float mg = br * 0.55f;
                if (!state.joystickOnRight)
                {
                    // Joystick is bottom-left — stop panel above joystick hit circle.
                    float joyTop = sh2 - mg - br * 2.6f; // baseCy - 1.6*br
                    listPanelH = joyTop - 20f;
                }
                else if (activeDemo.VirtualActionButtons.Length > 0)
                {
                    // Buttons are bottom-left when joystick is flipped — stop panel above button top.
                    float btnSize = br * 0.72f;
                    float btnTop  = sh2 - mg - btnSize; // by for the button row
                    listPanelH = btnTop - 10f;
                }
                else
                {
                    listPanelH = sh2 - 20f;
                }
            }
            else
            {
                listPanelH = sh2 - 20f;
            }
            listPanelH = MathF.Max(listPanelH, 60f); // always at least visible
        }
        igSetNextWindowSize(new Vector2(220, listPanelH), ImGuiCond.Always);
        igSetNextWindowPos(new Vector2(10, 10), ImGuiCond.Always, Vector2.Zero);
        byte open = 1;
        if (igBegin("Jolt Samples", ref open, noDecor))
        {
            string lastCategory = "";
            for (int i = 0; i < state.demos.Count; i++)
            {
                var demo = state.demos[i];
                if (demo.Category != lastCategory)
                {
                    if (lastCategory.Length > 0) igSpacing();
                    igTextColored(new System.Numerics.Vector4(0.9f, 0.7f, 0.3f, 1f), demo.Category);
                    igSeparator();
                    lastCategory = demo.Category;
                }
                bool selected = i == state.activeDemoIndex;
                if (igSelectable_Bool(demo.Name, selected, 0, Vector2.Zero) && !selected)
                    state.pendingDemoSwitch = i;
            }
        }
        igEnd();

        // ── Top-right panel: stats ────────────────────────────────────────────
        float statsH = activeDemo.VirtualControls != VirtualControlsType.None ? 230f : 178f;
        igSetNextWindowSize(new Vector2(220, statsH), ImGuiCond.Always);
        igSetNextWindowPos(new Vector2(sapp_width() - 230f, 10), ImGuiCond.Always, Vector2.Zero);
        if (igBegin("Stats", ref open, noDecor))
        {
            float fps = 1.0f / (float)sapp_frame_duration();
            igText($"FPS: {fps:F1}");
            igText($"Bodies: {state.physicsSystem.GetNumBodies()}");
            Vector3 cam = state.camera.EyePos;
            igText($"Cam: ({cam.X:F1}, {cam.Y:F1}, {cam.Z:F1})");
            igSeparator();
            byte startPausedByte = state.startPaused ? (byte)1 : (byte)0;
            igCheckbox("Start paused", ref startPausedByte);
            state.startPaused = startPausedByte != 0;
            byte lockCameraByte = state.lockCamera ? (byte)1 : (byte)0;
            igCheckbox("Lock camera", ref lockCameraByte);
            state.lockCamera = lockCameraByte != 0;
            if (activeDemo.VirtualControls != VirtualControlsType.None)
            {
                byte vctrlByte = state.virtualControlsEnabled ? (byte)1 : (byte)0;
                igCheckbox("Virtual controls", ref vctrlByte);
                state.virtualControlsEnabled = vctrlByte != 0;
                if (state.virtualControlsEnabled)
                {
                    byte flipByte = state.joystickOnRight ? (byte)1 : (byte)0;
                    igCheckbox("Flip joy side", ref flipByte);
                    state.joystickOnRight = flipByte != 0;
                }
            }
            if (igButton(state.paused ? "Resume" : "Pause", new Vector2(-1, 0)))
                state.paused = !state.paused;
            if (igButton("Reset", new Vector2(-1, 0)))
                state.pendingReset = true;
        }
        igEnd();

        // ── World-space body labels ───────────────────────────────────────────
        if (state.bodyLabels != null && state.bodyLabels.Count > 0)
        {
            int sw = sapp_width(), sh = sapp_height();
            var dl = igGetForegroundDrawList_ViewportPtr(igGetMainViewport());
            foreach (var (id, label) in state.bodyLabels)
            {
                using var comT = state.bodyInterface.GetCenterOfMassTransform(id);
                using var tr   = comT.GetTranslation();
                float wx = tr.GetX();
                float wy = tr.GetY() + 2.0f; // offset above body
                float wz = tr.GetZ();
                var clip = Vector4.Transform(new Vector4(wx, wy, wz, 1f), state.camera.ViewProj);
                if (clip.W <= 0f) continue;
                float sx = (clip.X / clip.W + 1f) * 0.5f * sw;
                float sy = (1f - clip.Y / clip.W) * 0.5f * sh;
                ImDrawList_AddText_Vec2(dl, new Vector2(sx, sy), 0xFFFFFFFFu, label, null);
            }
        }

        DrawVirtualControls(activeDemo);
    }

    // ── Virtual joystick and action buttons ───────────────────────────────────
    // State persisted across frames for the joystick knob position.
    static Vector2 _joyKnob = Vector2.Zero; // current knob offset (relative to base center), in pixels
    // Hit areas (cx, cy, r) updated each frame so the Event handler can block camera input.
    static Vector3   _joyHitArea  = Vector3.Zero;                 // cx, cy, r
    static Vector3[] _btnHitAreas = Array.Empty<Vector3>();       // cx, cy, r per button
    static bool      _vcCapturing = false;                        // true while a pointer is held over a virtual control
    // Multi-touch: all currently active touches keyed by Sokol touch identifier.
    static readonly Dictionary<nuint, Vector2> _activeTouches = new();
    static nuint _joyTouchId = nuint.MaxValue;                    // identifier of the finger currently on the joystick

    // Computes _joyHitArea / _btnHitAreas from the current screen dimensions.
    // Called from DrawVirtualControls each frame and lazily from Event() before the first frame.
    static void ComputeVirtualControlHitAreas(DemoBase activeDemo)
    {
        if (activeDemo.VirtualControls == VirtualControlsType.None || !state.virtualControlsEnabled) return;
        int sw = sapp_width(), sh = sapp_height();
        float baseRadius = MathF.Min(sw, sh) * 0.13f;
        float margin     = baseRadius * 0.55f;
        float baseCx     = state.joystickOnRight ? sw - margin - baseRadius : margin + baseRadius;
        float baseCy     = sh - margin - baseRadius;
        _joyHitArea = new Vector3(baseCx, baseCy, baseRadius * 1.6f);
        var actionBtns = activeDemo.VirtualActionButtons;
        float btnSize  = baseRadius * 0.72f;
        float btnPad   = btnSize * 0.22f;
        if (_btnHitAreas.Length != actionBtns.Length)
            _btnHitAreas = new Vector3[actionBtns.Length];
        for (int i = 0; i < actionBtns.Length; i++)
        {
            float bx = state.joystickOnRight
                ? margin + i * (btnSize + btnPad)
                : sw - margin - btnSize - i * (btnSize + btnPad);
            float by = sh - margin - btnSize;
            _btnHitAreas[i] = new Vector3(bx + btnSize * 0.5f, by + btnSize * 0.5f, btnSize * 0.5f);
        }
    }

    static void DrawVirtualControls(DemoBase activeDemo)
    {
        if (activeDemo.VirtualControls == VirtualControlsType.None) return;
        if (!state.virtualControlsEnabled) return;

        // Clear virtual key state — rebuilt from current joystick/button hit-test below.
        DemoBase.ClearVirtualKeys();

        int sw = sapp_width();
        int sh = sapp_height();

        // ── Layout constants (scale with screen size) ─────────────────────────
        float baseRadius  = MathF.Min(sw, sh) * 0.13f;
        float knobRadius  = baseRadius * 0.42f;
        float margin      = baseRadius * 0.55f;
        float areaH       = (baseRadius + knobRadius) * 2f + margin * 2f;
        float baseCx      = state.joystickOnRight ? sw - margin - baseRadius : margin + baseRadius;
        float baseCy      = sh - margin - baseRadius;

        // ── Action buttons on the right side ──────────────────────────────────
        var actionBtns = activeDemo.VirtualActionButtons;
        float btnSize  = baseRadius * 0.72f;
        float btnPad   = btnSize * 0.22f;

        // ── Update hit areas so the Event handler can suppress camera input ───
        ComputeVirtualControlHitAreas(activeDemo);

        // Draw joystick using ImGui overlay
        var winFlags =
            ImGuiWindowFlags.NoResize     | ImGuiWindowFlags.NoMove  |
            ImGuiWindowFlags.NoCollapse   | ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoScrollbar  | ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoInputs;

        // ── Full-screen invisible overlay for draw-list ───────────────────────
        igSetNextWindowPos(Vector2.Zero, ImGuiCond.Always, Vector2.Zero);
        igSetNextWindowSize(new Vector2(sw, sh), ImGuiCond.Always);
        igSetNextWindowBgAlpha(0f);
        byte ovc = 1;
        igBegin("##vc_overlay", ref ovc, winFlags);
        var dl = igGetWindowDrawList();

        // ── Draw joystick base ────────────────────────────────────────────────
        uint colBase = igGetColorU32_Vec4(new Vector4(1f, 1f, 1f, 0.15f));
        uint colRing = igGetColorU32_Vec4(new Vector4(1f, 1f, 1f, 0.35f));
        uint colKnob = igGetColorU32_Vec4(new Vector4(0.9f, 0.9f, 1.0f, 0.72f));

        ImDrawList_AddCircleFilled(dl, new Vector2(baseCx, baseCy), baseRadius, colBase, 40);
        ImDrawList_AddCircle(dl, new Vector2(baseCx, baseCy), baseRadius, colRing, 40, 2.5f);
        ImDrawList_AddCircleFilled(dl, new Vector2(baseCx + _joyKnob.X, baseCy + _joyKnob.Y), knobRadius, colKnob, 32);
        ImDrawList_AddCircle(dl, new Vector2(baseCx + _joyKnob.X, baseCy + _joyKnob.Y), knobRadius,
            igGetColorU32_Vec4(new Vector4(0.6f, 0.6f, 1.0f, 0.90f)), 32, 2f);

        // ── Draw action buttons ───────────────────────────────────────────────
        for (int i = 0; i < actionBtns.Length; i++)
        {
            float bx = state.joystickOnRight
                ? margin + i * (btnSize + btnPad)
                : sw - margin - btnSize - i * (btnSize + btnPad);
            float by = sh - margin - btnSize;
            bool  isDown = DemoBase.IsVirtualKeyDown(actionBtns[i].Key);
            uint  colBtn = isDown
                ? igGetColorU32_Vec4(new Vector4(0.55f, 0.65f, 1.0f, 0.92f))
                : igGetColorU32_Vec4(new Vector4(0.30f, 0.35f, 0.55f, 0.72f));
            ImDrawList_AddCircleFilled(dl, new Vector2(bx + btnSize * 0.5f, by + btnSize * 0.5f), btnSize * 0.5f, colBtn, 32);
            ImDrawList_AddCircle(dl, new Vector2(bx + btnSize * 0.5f, by + btnSize * 0.5f), btnSize * 0.5f,
                igGetColorU32_Vec4(new Vector4(0.8f, 0.8f, 1.0f, 0.60f)), 32, 2f);
            // Label
            Vector2 tSz = default;
            igCalcTextSize(ref tSz, actionBtns[i].Label, null, false, -1f);
            ImDrawList_AddText_Vec2(dl,
                new Vector2(bx + btnSize * 0.5f - tSz.X * 0.5f, by + btnSize * 0.5f - tSz.Y * 0.5f),
                0xFFFFFFFF, actionBtns[i].Label, null);
        }

        igEnd();

        // ── Hit-test joystick & buttons against all active touches (multi-touch) ──
        // On desktop (no touch events) fall back to ImGui primary pointer.
        bool anyPointerDown;
        float joyPx, joyPy; // position of the joystick finger (or mouse on desktop)
        if (_activeTouches.Count > 0)
        {
            anyPointerDown = true;
            // Drive the joystick from its dedicated touch; -999 if that finger lifted.
            if (_joyTouchId != nuint.MaxValue && _activeTouches.TryGetValue(_joyTouchId, out var jtp))
                { joyPx = jtp.X; joyPy = jtp.Y; }
            else
                { joyPx = -9999f; joyPy = -9999f; }
        }
        else
        {
            var io = igGetIO_Nil();
            anyPointerDown = io->MouseDown[0] != 0;
            joyPx = io->MousePos.X;
            joyPy = io->MousePos.Y;
        }

        if (!anyPointerDown)
        {
            _joyKnob = Vector2.Zero;
            _vcCapturing = false;
            return;
        }

        // Joystick — driven by its dedicated finger (or the mouse on desktop)
        float dx = joyPx - baseCx;
        float dy = joyPy - baseCy;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist <= baseRadius * 1.6f)
        {
            if (dist > baseRadius)
            {
                dx = dx / dist * baseRadius;
                dy = dy / dist * baseRadius;
                dist = baseRadius;
            }
            _joyKnob = new Vector2(dx, dy);

            float deadzone = baseRadius * 0.30f;
            var kc = activeDemo.VirtualControls;
            if (kc == VirtualControlsType.Arrows)
            {
                if (dy < -deadzone) DemoBase.SetVirtualKeyDown((int)SApp.sapp_keycode.SAPP_KEYCODE_UP,    true);
                if (dy >  deadzone) DemoBase.SetVirtualKeyDown((int)SApp.sapp_keycode.SAPP_KEYCODE_DOWN,  true);
                if (dx < -deadzone) DemoBase.SetVirtualKeyDown((int)SApp.sapp_keycode.SAPP_KEYCODE_LEFT,  true);
                if (dx >  deadzone) DemoBase.SetVirtualKeyDown((int)SApp.sapp_keycode.SAPP_KEYCODE_RIGHT, true);
            }
            else if (kc == VirtualControlsType.WASD)
            {
                if (dy < -deadzone) DemoBase.SetVirtualKeyDown((int)SApp.sapp_keycode.SAPP_KEYCODE_W, true);
                if (dy >  deadzone) DemoBase.SetVirtualKeyDown((int)SApp.sapp_keycode.SAPP_KEYCODE_S, true);
                if (dx < -deadzone) DemoBase.SetVirtualKeyDown((int)SApp.sapp_keycode.SAPP_KEYCODE_A, true);
                if (dx >  deadzone) DemoBase.SetVirtualKeyDown((int)SApp.sapp_keycode.SAPP_KEYCODE_D, true);
            }
        }
        else
        {
            _joyKnob = Vector2.Zero;
        }

        // Buttons — checked against ALL active touches so any finger can press them
        // simultaneously with the joystick finger.
        for (int i = 0; i < actionBtns.Length; i++)
        {
            float bx  = state.joystickOnRight
                ? margin + i * (btnSize + btnPad)
                : sw - margin - btnSize - i * (btnSize + btnPad);
            float by  = sh - margin - btnSize;
            float bcx = bx + btnSize * 0.5f;
            float bcy = by + btnSize * 0.5f;
            float r2  = (btnSize * 0.5f) * (btnSize * 0.5f);
            // Check every active touch (or the mouse on desktop)
            if (_activeTouches.Count > 0)
            {
                foreach (var p in _activeTouches.Values)
                {
                    float adx = p.X - bcx, ady = p.Y - bcy;
                    if (adx * adx + ady * ady <= r2)
                        { DemoBase.SetVirtualKeyDown((int)actionBtns[i].Key, true); break; }
                }
            }
            else
            {
                float adx = joyPx - bcx, ady = joyPy - bcy;
                if (adx * adx + ady * ady <= r2)
                    DemoBase.SetVirtualKeyDown((int)actionBtns[i].Key, true);
            }
        }
    }

    // ── Entry point ───────────────────────────────────────────────────────────
    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb    = &Init,
            frame_cb   = &Frame,
            event_cb   = &Event,
            cleanup_cb = &Cleanup,
            width      = 0,
            height     = 0,
            sample_count = 4,
            window_title = "JoltPhysics Sample Browser",
            icon = { sokol_default = true },
            logger = { func = &slog_func }
        };
    }
}
