using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Sokol;
using static Sokol.SG;
using static Sokol.Utils;

// ── Shared render types ────────────────────────────────────────────────────

// Capsule: scale = (radius, halfCylinderHeight, radius); rendered as cylinder + 2 sphere caps.
// TaperedCylinder: custom mesh per body (call CreateTaperedConeMesh); scale = Vector3.One; localOffset to position.
public enum RenderShape { Box, Sphere, Floor, Cylinder, Capsule, TaperedCylinder }

/// <summary>
/// Represents one physics body that the renderer should draw each frame.
/// </summary>
public struct PhysicsBody
{
    public JPH.BodyID  bodyId;
    public Vector3     color;
    public RenderShape shape;
    /// <summary>Full world-space size of the rendered mesh (half-extents × 2 for boxes, diameter for spheres).</summary>
    public Vector3     scale;
    /// <summary>Number of gear/rack teeth to render procedurally. 0 = no teeth.</summary>
    public int         numTeeth;
    /// <summary>Radial height of each tooth (metres).</summary>
    public float       toothHeight;
    /// <summary>When true, the shape is rendered as a wireframe (transparent except edges/grid lines).</summary>
    public bool        wireframe;
    /// <summary>
    /// Sub-shape offset in compound SHAPE LOCAL space. When non-zero, GetWorldTransform is used
    /// to place this entry at the correct world position of the sub-shape.
    /// </summary>
    public Vector3     localOffset;
    /// <summary>
    /// Additional local rotation for a compound sub-shape (applied as bodyRot * localRotation).
    /// W == 0 (default) means no extra rotation — body rotation is used as-is.
    /// </summary>
    public Quaternion  localRotation;
    /// <summary>
    /// Per-body custom GPU mesh used by RenderShape.TaperedCylinder.
    /// vertex_buffers[0] = VB, index_buffer = IB. Zero by default (unused).
    /// Must be destroyed via sg_destroy_buffer when the body is removed.
    /// </summary>
    public sg_bindings customMesh;
    /// <summary>Number of indices in the custom mesh (0 = unused).</summary>
    public int         customMeshIndexCount;
    /// <summary>
    /// When true, the custom mesh uses smooth interpolated vertex normals (shapeType 6).
    /// When false (default), flat per-face normals are used (shapeType 5).
    /// </summary>
    public bool        smoothCustomMesh;
}

// ── Truncated-cone vertex (same memory layout as the app's Vertex struct) ──
[StructLayout(LayoutKind.Sequential)]
public struct ConeVertex
{
    public Vector3 position;
    public Vector3 normal;
}

// ── Soft body render entry ─────────────────────────────────────────────────

/// <summary>
/// Tracks GPU resources for rendering one Cosserat rod body as line segments per-frame.
/// Each rod is a pair of vertex indices; vertex positions are read from the physics system.
/// </summary>
public class RodBodyRenderEntry
{
    public JPH.BodyID         bodyId;
    public sg_buffer          vertexBuf;  // streaming: 2 × ConeVertex per rod each frame
    public int                rodCount;   // number of rod segments
    public Vector3            color;
    public ConeVertex[]       scratch;    // pre-allocated CPU scratch [rodCount * 2]
    public (uint v0, uint v1)[] rods;    // rod endpoint vertex indices
}

/// <summary>
/// Tracks GPU resources for rendering one soft body per-frame.
/// The vertex buffer is streaming (position data changes every frame);
/// the index buffer is static (face topology set once at Init).
/// </summary>
public class SoftBodyRenderEntry
{
    public JPH.BodyID   bodyId;
    public sg_buffer    vertexBuf;  // streaming: written each frame via sg_update_buffer
    public sg_buffer    indexBuf;   // static: uploaded once from face list at Init
    public int          indexCount;
    public Vector3      color;
    public ConeVertex[] scratch;    // pre-allocated CPU scratch for vertex upload
    public (uint v0, uint v1, uint v2)[] faces; // CPU-side topology for normal computation
    /// <summary>
    /// Non-null when the body was NOT added to the physics system.
    /// The render loop reads vertices directly from this Body object
    /// instead of going through the PhysicsSystem API.
    /// </summary>
    public JPH.Body?    standaloneBody;
}

// ── Abstract demo base ─────────────────────────────────────────────────────

/// <summary>
/// Abstract base for all Jolt sample demos.
/// Each demo owns its own set of physics bodies (floor + dynamic/static objects).
/// The app shell calls Init once, Update every frame, and removes all bodies when switching.
/// </summary>
public abstract class DemoBase
{
    public abstract string Name     { get; }
    public abstract string Category { get; }

    /// <summary>
    /// Populate <paramref name="bodies"/> with every physics body this demo owns.
    /// Static bodies (floor, walls) and dynamic bodies both go here.
    /// </summary>
    public abstract void Init(
        JPH.BodyInterface bi,
        JPH.PhysicsSystem sys,
        List<PhysicsBody> bodies,
        Random            random);

    /// <summary>
    /// Called once per frame after the physics step.
    /// Override for kinematic movement, spawning logic, etc.
    /// </summary>
    public virtual void Update(float dt, JPH.BodyInterface bi, List<PhysicsBody> bodies) { }

    /// <summary>
    /// Called before bodies are destroyed when switching demos.
    /// Override to remove constraints from the physics system.
    /// </summary>
    public virtual void Cleanup(JPH.PhysicsSystem sys) { }

    /// <summary>
    /// Called after Init + OptimizeBroadPhase, before the first frame.
    /// Override to apply per-demo PhysicsSettings (e.g. velocity solver steps).
    /// </summary>
    public virtual void Activate(JPH.PhysicsSystem sys) { }

    /// <summary>
    /// Called before Cleanup when switching away from this demo.
    /// Override to restore any PhysicsSettings changed in Activate.
    /// </summary>
    public virtual void Deactivate(JPH.PhysicsSystem sys) { }

    /// <summary>
    /// Number of collision steps to use per frame for this demo.
    /// </summary>
    public virtual int CollisionSteps => 1;

    /// <summary>
    /// When true, the camera follows the player each frame via <see cref="GetFollowPosition"/>.
    /// The app shell will also suppress arrow keys from reaching the camera.
    /// </summary>
    public virtual bool CameraFollowsPlayer => false;

    /// <summary>Type of virtual joystick/controls used by this demo. None by default.</summary>
    public virtual VirtualControlsType VirtualControls => VirtualControlsType.None;

    /// <summary>Action buttons shown alongside the virtual joystick. Empty by default.</summary>
    public virtual VirtualActionButton[] VirtualActionButtons => new VirtualActionButton[0];

    /// <summary>
    /// Returns the world position the camera should track this frame.
    /// Only called when <see cref="CameraFollowsPlayer"/> is true.
    /// </summary>
    public virtual Vector3 GetFollowPosition(JPH.BodyInterface bi) => Vector3.Zero;

    /// <summary>
    /// Returns the vehicle's world yaw in degrees, used for third-person camera orientation.
    /// Returns <c>float.NaN</c> when yaw-follow is not applicable for this demo.
    /// </summary>
    public virtual float GetFollowYaw(JPH.BodyInterface bi) => float.NaN;

    /// <summary>Camera description used when this demo is activated.</summary>
    public virtual CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 50,
        Latitude  = 25,
        Longitude = 45,
        Center    = new Vector3(0, 5, 0),
        Aspect    = 60,
        NearZ     = 0.1f,
        FarZ      = 1000.0f
    };

    // ── Layer constants (must match the app shell) ─────────────────────────
    protected const ushort LayerNonMoving = 0;
    protected const ushort LayerMoving    = 1;
    protected const ushort LayerDebris    = 2;  // only collides with LayerMoving

    // ── Soft body render entries (set by the app shell before Init) ────────
    protected List<SoftBodyRenderEntry>? _softBodies;

    /// <summary>Called by the app shell before Init to supply the shared soft body render list.</summary>
    public void SetSoftBodyList(List<SoftBodyRenderEntry> list) => _softBodies = list;

    // ── Rod body render entries (set by the app shell before Init) ────────
    protected List<RodBodyRenderEntry>? _rodBodies;

    /// <summary>Called by the app shell before Init to supply the shared rod body render list.</summary>
    public void SetRodBodyList(List<RodBodyRenderEntry> list) => _rodBodies = list;

    // ── Debug line segments (SG_PRIMITIVETYPE_LINES, drawn each frame) ────
    protected List<ConeVertex>? _debugLines;

    /// <summary>Called by the app shell before Init to supply the shared debug lines list.</summary>
    public void SetDebugLineList(List<ConeVertex> list) => _debugLines = list;

    /// <summary>Adds a world-space line segment drawn this frame via <c>pip_lines</c>.</summary>
    protected void AddDebugLine(Vector3 a, Vector3 b)
    {
        if (_debugLines == null) return;
        _debugLines.Add(new ConeVertex { position = a });
        _debugLines.Add(new ConeVertex { position = b });
    }

    // ── Debug triangles (SG_PRIMITIVETYPE_TRIANGLES, alpha-blended, drawn each frame) ────
    protected List<ConeVertex>? _debugTris;

    /// <summary>Called by the app shell before Init to supply the shared debug triangles list.</summary>
    public void SetDebugTriList(List<ConeVertex> list) => _debugTris = list;

    /// <summary>Adds a world-space filled triangle drawn this frame via <c>pip_tris_blend</c>.</summary>
    protected void AddDebugTri(Vector3 a, Vector3 b, Vector3 c)
    {
        if (_debugTris == null) return;
        var n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        _debugTris.Add(new ConeVertex { position = a, normal = n });
        _debugTris.Add(new ConeVertex { position = b, normal = n });
        _debugTris.Add(new ConeVertex { position = c, normal = n });
    }

    /// <summary>Adds 12 wireframe edges of an axis-aligned box (24 line vertices total).</summary>
    protected void AddDebugBox(Vector3 min, Vector3 max)
    {
        // Bottom face
        AddDebugLine(new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z));
        AddDebugLine(new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, min.Y, max.Z));
        AddDebugLine(new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, min.Y, max.Z));
        AddDebugLine(new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, min.Y, min.Z));
        // Top face
        AddDebugLine(new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z));
        AddDebugLine(new Vector3(max.X, max.Y, min.Z), new Vector3(max.X, max.Y, max.Z));
        AddDebugLine(new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z));
        AddDebugLine(new Vector3(min.X, max.Y, max.Z), new Vector3(min.X, max.Y, min.Z));
        // Vertical edges
        AddDebugLine(new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, max.Y, min.Z));
        AddDebugLine(new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, max.Y, min.Z));
        AddDebugLine(new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, max.Y, max.Z));
        AddDebugLine(new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, max.Y, max.Z));
    }

    // ── Body labels (world-space text overlays, set by the app shell before Init) ──
    protected List<(JPH.BodyID id, string label)>? _bodyLabels;

    /// <summary>Called by the app shell before Init to supply the shared body label list.</summary>
    public void SetBodyLabelList(List<(JPH.BodyID id, string label)> list) => _bodyLabels = list;

    /// <summary>Associate a floating world-space text label with a body.</summary>
    protected void SetBodyLabel(JPH.BodyID id, string label) => _bodyLabels?.Add((id, label));

    // ── Camera longitude (set by the app shell each frame before Update) ──────
    /// <summary>
    /// Current camera longitude in degrees. Set by the app shell each frame so demos
    /// can compute camera-relative movement directions.
    /// </summary>
    public float CameraLongitude { get; set; }

    // ── Key-state (updated by the app shell via SetKeyDown) ────────────────────
    private  static readonly bool[] _keysDown        = new bool[512];
    private  static readonly bool[] _virtualKeysDown = new bool[512];
    internal static void SetKeyDown(int keycode, bool down)        { if ((uint)keycode < 512) _keysDown[keycode]        = down; }
    internal static void SetVirtualKeyDown(int keycode, bool down) { if ((uint)keycode < 512) _virtualKeysDown[keycode] = down; }
    internal static void ClearVirtualKeys() => Array.Clear(_virtualKeysDown, 0, _virtualKeysDown.Length);
    internal static bool IsVirtualKeyDown(SApp.sapp_keycode key) => _virtualKeysDown[(int)key];
    protected static bool IsKeyDown(SApp.sapp_keycode key) => _keysDown[(int)key] || _virtualKeysDown[(int)key];

    // ── Floor helper ───────────────────────────────────────────────────────

    /// <summary>Create a standard flat floor and add it to the body list.</summary>
    protected static JPH.BodyID AddFloor(
        JPH.BodyInterface bi, List<PhysicsBody> bodies,
        float hx = 100f, float hy = 1f, float hz = 100f,
        float cx = 0f,   float cy = -1f, float cz = 0f)
    {
        float centreY = cy < -0.5f ? -hy : cy;
        var id = AddBox(bi, bodies, hx, hy, hz, cx, centreY, cz,
            Quaternion.Identity,
            JPH.EMotionType.Static, LayerNonMoving,
            new Vector3(0.9f, 0.7f, 0.3f),
            friction: 0.5f);
        // Override shape type so the floor gets its own checkerboard pattern
        var last = bodies[bodies.Count - 1];
        last.shape = RenderShape.Floor;
        bodies[bodies.Count - 1] = last;
        return id;
    }

    // ── Box helper ─────────────────────────────────────────────────────────

    /// <summary>
    /// Create, add, and register a box body.
    /// </summary>
    /// <param name="hx">Half-extent X (metres)</param>
    /// <param name="hy">Half-extent Y (metres)</param>
    /// <param name="hz">Half-extent Z (metres)</param>
    /// <param name="px">Centre X (world space)</param>
    /// <param name="py">Centre Y (world space)</param>
    /// <param name="pz">Centre Z (world space)</param>
    protected static unsafe JPH.BodyID AddBox(
        JPH.BodyInterface bi, List<PhysicsBody> bodies,
        float hx, float hy, float hz,
        float px, float py, float pz,
        Quaternion rotation,
        JPH.EMotionType motionType, ushort layer,
        Vector3 color,
        float friction      = 0.5f,
        float restitution   = 0.0f,
        bool  allowSleeping = true,
        float mass          = 0f)
    {
        using var half = new JPH.Vec3(hx, hy, hz);
        using var ss   = new JPH.BoxShapeSettings(half);
        using var cs   = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(px, py, pz);
        cs.mRotation.Set(rotation.X, rotation.Y, rotation.Z, rotation.W);
        cs.mMotionType     = motionType;
        cs.mObjectLayer    = layer;
        cs.mFriction       = friction;
        cs.mRestitution    = restitution;
        cs.mAllowSleeping  = allowSleeping;
        if (mass > 0f) { cs.SetOverrideMassProperties(1); cs.SetMassOverride(mass); }

        var activation = motionType == JPH.EMotionType.Static
            ? JPH.EActivation.DontActivate
            : JPH.EActivation.Activate;

        var id = bi.CreateAndAddBody(cs, activation);
        bodies.Add(new PhysicsBody
        {
            bodyId = id,
            color  = color,
            shape  = RenderShape.Box,
            scale  = new Vector3(hx * 2f, hy * 2f, hz * 2f)
        });
        return id;
    }

    // ── Sphere helper ──────────────────────────────────────────────────────

    /// <summary>Create, add, and register a sphere body.</summary>
    protected static unsafe JPH.BodyID AddSphere(
        JPH.BodyInterface bi, List<PhysicsBody> bodies,
        float radius,
        float px, float py, float pz,
        JPH.EMotionType motionType, ushort layer,
        Vector3 color,
        float friction      = 0.5f,
        float restitution   = 0.0f,
        float linearDamping = 0.05f,
        bool  allowSleeping = true,
        float mass          = 0f)
    {
        using var ss = new JPH.SphereShapeSettings(radius);
        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(px, py, pz);
        cs.mMotionType    = motionType;
        cs.mObjectLayer   = layer;
        cs.mFriction      = friction;
        cs.mRestitution   = restitution;
        cs.mLinearDamping = linearDamping;
        cs.mAllowSleeping = allowSleeping;
        if (mass > 0f) { cs.SetOverrideMassProperties(1); cs.SetMassOverride(mass); }

        var activation = motionType == JPH.EMotionType.Static
            ? JPH.EActivation.DontActivate
            : JPH.EActivation.Activate;

        var id = bi.CreateAndAddBody(cs, activation);
        bodies.Add(new PhysicsBody
        {
            bodyId = id,
            color  = color,
            shape  = RenderShape.Sphere,
            scale  = new Vector3(radius * 2f)
        });
        return id;
    }

    // ── Cylinder helper ────────────────────────────────────────────────────

    /// <summary>
    /// Create, add, and register a cylinder body.
    /// The cylinder is Y-axis aligned by default (top at +Y, bottom at -Y).
    /// </summary>
    protected static unsafe JPH.BodyID AddCylinder(
        JPH.BodyInterface bi, List<PhysicsBody> bodies,
        float halfHeight, float radius,
        float px, float py, float pz,
        Quaternion rotation,
        JPH.EMotionType motionType, ushort layer,
        Vector3 color,
        float friction      = 0.5f,
        float restitution   = 0.0f,
        bool  allowSleeping = true,
        float mass          = 0f)
    {
        using var ss = new JPH.CylinderShapeSettings(halfHeight, radius);
        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(px, py, pz);
        cs.mRotation.Set(rotation.X, rotation.Y, rotation.Z, rotation.W);
        cs.mMotionType    = motionType;
        cs.mObjectLayer   = layer;
        cs.mFriction      = friction;
        cs.mRestitution   = restitution;
        cs.mAllowSleeping = allowSleeping;
        if (mass > 0f) { cs.SetOverrideMassProperties(1); cs.SetMassOverride(mass); }

        var activation = motionType == JPH.EMotionType.Static
            ? JPH.EActivation.DontActivate
            : JPH.EActivation.Activate;

        var id = bi.CreateAndAddBody(cs, activation);
        bodies.Add(new PhysicsBody
        {
            bodyId = id,
            color  = color,
            shape  = RenderShape.Cylinder,
            scale  = new Vector3(radius * 2f, halfHeight * 2f, radius * 2f)
        });
        return id;
    }

    // ── Capsule helper ─────────────────────────────────────────────────────

    /// <summary>
    /// Create, add, and register a capsule body.
    /// <paramref name="radius"/> is the sphere-cap radius; <paramref name="halfCylH"/> is the half-height of the cylindrical part.
    /// Rendered as cylinder (2*halfCylH tall) + two sphere caps.
    /// </summary>
    protected static unsafe JPH.BodyID AddCapsule(
        JPH.BodyInterface bi, List<PhysicsBody> bodies,
        float radius, float halfCylH,
        float px, float py, float pz,
        Quaternion rotation,
        JPH.EMotionType motionType, ushort layer,
        Vector3 color,
        float friction      = 0.5f,
        float restitution   = 0.0f,
        bool  allowSleeping = true,
        float mass          = 0f)
    {
        using var ss = new JPH.CapsuleShapeSettings(halfCylH, radius);
        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(ss);
        cs.mPosition.Set(px, py, pz);
        cs.mRotation.Set(rotation.X, rotation.Y, rotation.Z, rotation.W);
        cs.mMotionType    = motionType;
        cs.mObjectLayer   = layer;
        cs.mFriction      = friction;
        cs.mRestitution   = restitution;
        cs.mAllowSleeping = allowSleeping;
        if (mass > 0f) { cs.SetOverrideMassProperties(1); cs.SetMassOverride(mass); }

        var activation = motionType == JPH.EMotionType.Static
            ? JPH.EActivation.DontActivate
            : JPH.EActivation.Activate;

        var id = bi.CreateAndAddBody(cs, activation);
        bodies.Add(new PhysicsBody
        {
            bodyId = id,
            color  = color,
            shape  = RenderShape.Capsule,
            // scale = (radius, halfCylH, radius) as expected by the capsule renderer
            scale  = new Vector3(radius, halfCylH, radius)
        });
        return id;
    }

    /// <summary>
    /// Converts HSV (hue 0-1, sat 0-1, val 0-1) to a linear RGB Vector3.
    /// Useful for assigning visually distinct colors to a set of bodies.
    /// </summary>
    protected static Vector3 HsvToRgb(float h, float s, float v)
    {
        if (s <= 0f) return new Vector3(v, v, v);
        h = (h % 1f + 1f) % 1f * 6f;
        int   i = (int)h;
        float f = h - i;
        float p = v * (1f - s);
        float q = v * (1f - s * f);
        float t = v * (1f - s * (1f - f));
        return i switch
        {
            0 => new Vector3(v, t, p),
            1 => new Vector3(q, v, p),
            2 => new Vector3(p, v, t),
            3 => new Vector3(p, q, v),
            4 => new Vector3(t, p, v),
            _ => new Vector3(v, p, q),
        };
    }

    // ── Asset loading helper ──────────────────────────────────────────────────

    /// <summary>
    /// Load a file relative to the app assets directory and return its bytes.
    /// </summary>
    protected static unsafe byte[] LoadAsset(string rel)
    {
        IntPtr dirPtr = SFilesystem.sfs_get_assets_dir();
        string dir = Marshal.PtrToStringUTF8(dirPtr) ?? "";
        SFilesystem.sfs_free_path(dirPtr);
        if(dir == "") throw new System.IO.DirectoryNotFoundException("Assets directory not found");
        string fullPath = System.IO.Path.Combine(dir, rel);
        IntPtr fh = SFilesystem.sfs_open_file(fullPath, SFilesystem.sfs_open_mode_t.SFS_OPEN_READ);
        if (fh == IntPtr.Zero) throw new System.IO.FileNotFoundException(fullPath);
        long size = SFilesystem.sfs_get_file_size(fh);
        byte[] data = new byte[size];
        fixed (byte* p = data) SFilesystem.sfs_read_file(fh, p, size);
        SFilesystem.sfs_close_file(fh);
        return data;
    }

    // ── Distinct colors (port of JPH::Color::sGetDistinctColor) ───────────

    // Same 36-color table used by the C++ Jolt Samples renderer.
    private static readonly Vector3[] s_distinctColors =
    {
        new(1.000f,0.000f,0.000f), new(0.800f,0.561f,0.400f), new(0.886f,0.949f,0.000f),
        new(0.161f,0.651f,0.486f), new(0.000f,0.667f,1.000f), new(0.271f,0.149f,0.600f),
        new(0.600f,0.149f,0.510f), new(0.898f,0.224f,0.314f), new(0.800f,0.000f,0.000f),
        new(1.000f,0.667f,0.000f), new(0.333f,0.502f,0.000f), new(0.251f,1.000f,0.851f),
        new(0.000f,0.294f,0.549f), new(0.631f,0.451f,0.902f), new(0.949f,0.239f,0.616f),
        new(0.698f,0.396f,0.349f), new(0.549f,0.369f,0.000f), new(0.710f,0.851f,0.424f),
        new(0.251f,0.949f,1.000f), new(0.302f,0.459f,0.600f), new(0.616f,0.239f,0.949f),
        new(0.549f,0.000f,0.220f), new(0.498f,0.224f,0.125f), new(0.800f,0.678f,0.200f),
        new(0.251f,1.000f,0.251f), new(0.149f,0.569f,0.600f), new(0.000f,0.400f,1.000f),
        new(0.949f,0.000f,0.886f), new(0.600f,0.302f,0.420f), new(0.898f,0.361f,0.000f),
        new(0.549f,0.494f,0.275f), new(0.000f,0.702f,0.278f), new(0.000f,0.761f,0.949f),
        new(0.106f,0.000f,0.800f), new(0.902f,0.451f,0.871f), new(0.498f,0.000f,0.067f),
    };

    protected static Vector3 GetDistinctColor(int index) =>
        s_distinctColors[index % s_distinctColors.Length];

    // ── Ragdoll layer remap ────────────────────────────────────────────────

    // The C++ RagdollLoader::sLoad unconditionally forces every part to Layers::MOVING
    // after reading from file, ignoring whatever layer is stored.  Replicate that here:
    // all loaded ragdoll bodies go into LayerMoving (1) so they collide with the floor.
    protected static void RemapRagdollLayers(JPH.RagdollSettings settings)
    {
        var parts = settings.mParts;
        for (var i = (UIntPtr)0; i < parts.Size(); i++)
            parts[i].mObjectLayer = LayerMoving;
    }

    // ── Skeleton pose line drawing ─────────────────────────────────────────

    /// <summary>
    /// Draw a skeleton pose as lines (bone from each joint to its parent).
    /// <paramref name="offset"/> is added to every joint position (world-space shift).
    /// </summary>
    protected void DrawSkeletonPose(JPH.SkeletonPose pose, Vector3 offset = default)
    {
        var skeleton = pose.GetSkeleton();
        if (skeleton == null) return;
        int count = skeleton.GetJointCount();
        using var rootVec = pose.GetRootOffset();
        var root = new Vector3(rootVec.GetX(), rootVec.GetY(), rootVec.GetZ()) + offset;
        for (int i = 0; i < count; i++)
        {
            int parent = skeleton.GetJoint(i).mParentJointIndex;
            if (parent < 0) continue;
            using var mI = pose.GetJointMatrix(i);
            using var mP = pose.GetJointMatrix(parent);
            using var tI = mI.GetTranslation();
            using var tP = mP.GetTranslation();
            var a = root + new Vector3(tI.GetX(), tI.GetY(), tI.GetZ());
            var b = root + new Vector3(tP.GetX(), tP.GetY(), tP.GetZ());
            AddDebugLine(a, b);
        }
    }

    // ── Playground terrain / obstacles ────────────────────────────────────

    /// <summary>
    /// Creates a hilly 100×100 m height-field terrain centred on the origin.
    /// Physics: Jolt HeightFieldShape. Visual: smooth CPU mesh (green shader path).
    /// </summary>
    protected static unsafe void CreatePlaygroundTerrain(
        JPH.BodyInterface bi, List<PhysicsBody> bodies,
        int sampleCount = 200, float cellSize = 1.5f, float maxHeight = 6.0f)
    {
        int   N         = sampleCount;
        float CellSize  = cellSize;
        float MaxHeight = maxHeight;
        float OriginX   = -N * CellSize * 0.5f;
        float OriginZ   = -N * CellSize * 0.5f;

        var heights = new float[N * N];
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                float wx = OriginX + x * CellSize;
                float wz = OriginZ + z * CellSize;
                float h = TerrainNoise(wx * 0.03f, wz * 0.03f) * 0.55f
                        + TerrainNoise(wx * 0.07f,  wz * 0.07f) * 0.30f
                        + TerrainNoise(wx * 0.15f,  wz * 0.15f) * 0.15f;
                // Flatten a 20m radius around the spawn (origin) so the car always lands safely
                float distFromOrigin = MathF.Sqrt(wx * wx + wz * wz);
                float flattenFactor  = Math.Clamp((distFromOrigin - 10f) / 15f, 0f, 1f);
                heights[z * N + x] = Math.Clamp(h * 0.5f + 0.5f, 0f, 1f) * flattenFactor;
            }

        using var hf = new JPH.HeightFieldShapeSettings();
        hf.mSampleCount = (uint)N;
        hf.mOffset.Set(OriginX, 0f, OriginZ);
        hf.mScale.Set(CellSize, MaxHeight, CellSize);
        hf.mHeightSamples.ResizeWithDefaultValue((UIntPtr)(N * N), 0f);
        for (int i = 0; i < N * N; i++)
            hf.mHeightSamples[(UIntPtr)i] = heights[i];

        using var cs = new JPH.BodyCreationSettings();
        cs.SetShapeSettings(hf);
        cs.mPosition.Set(0f, 0f, 0f);
        cs.mMotionType  = JPH.EMotionType.Static;
        cs.mObjectLayer = LayerNonMoving;
        cs.mFriction    = 0.8f;
        var id = bi.CreateAndAddBody(cs, JPH.EActivation.DontActivate);

        var verts = new ConeVertex[N * N];
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                float wx = OriginX + x * CellSize;
                float wz = OriginZ + z * CellSize;
                float wy = heights[z * N + x] * MaxHeight;
                float hL = x > 0     ? heights[z * N + (x - 1)] * MaxHeight : wy;
                float hR = x < N - 1 ? heights[z * N + (x + 1)] * MaxHeight : wy;
                float hD = z > 0     ? heights[(z - 1) * N + x] * MaxHeight : wy;
                float hU = z < N - 1 ? heights[(z + 1) * N + x] * MaxHeight : wy;
                float dxH = (hR - hL) / (2f * CellSize);
                float dzH = (hU - hD) / (2f * CellSize);
                verts[z * N + x] = new ConeVertex
                {
                    position = new Vector3(wx, wy, wz),
                    normal   = Vector3.Normalize(new Vector3(-dxH, 1f, -dzH)),
                };
            }

        int triCount = (N - 1) * (N - 1) * 6;
        var idx = new ushort[triCount];
        int ii = 0;
        for (int z = 0; z < N - 1; z++)
            for (int x = 0; x < N - 1; x++)
            {
                int a = z * N + x; int b = a + 1; int c = a + N; int d = c + 1;
                idx[ii++] = (ushort)a; idx[ii++] = (ushort)c; idx[ii++] = (ushort)b;
                idx[ii++] = (ushort)b; idx[ii++] = (ushort)c; idx[ii++] = (ushort)d;
            }

        var bindings = default(sg_bindings);
        bindings.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc { data = SG_RANGE<ConeVertex>(verts) });
        bindings.index_buffer      = sg_make_buffer(new sg_buffer_desc
            { usage = new sg_buffer_usage { index_buffer = true }, data = SG_RANGE<ushort>(idx) });

        bodies.Add(new PhysicsBody
        {
            bodyId               = id,
            color                = new Vector3(0.55f, 0.45f, 0.35f),
            shape                = RenderShape.TaperedCylinder,
            scale                = Vector3.One,
            customMesh           = bindings,
            customMeshIndexCount = triCount,
            smoothCustomMesh     = true,
        });
    }

    /// <summary>
    /// Creates a 3-row staggered brick wall at z=<paramref name="posZ"/>, centred on x=0.
    /// </summary>
    protected static unsafe void CreateWallObstacle(JPH.BodyInterface bi, List<PhysicsBody> bodies, float posZ, float centreX = 0f)
    {
        // 5 rows, alternating 10/9 bricks, ~10m wide
        int[]   rowBricks = { 10, 9, 10, 9, 10 };
        float[] rowOffset = { 0f, 0.5f, 0f, 0.5f, 0f };
        for (int row = 0; row < rowBricks.Length; row++)
        {
            float y = 2f + row * 1.0f;
            for (int col = 0; col < rowBricks[row]; col++)
            {
                float x = centreX - 4.5f + col * 1.0f + rowOffset[row];
                AddBox(bi, bodies,
                    0.5f, 0.5f, 0.5f,
                    x, y, posZ,
                    Quaternion.Identity,
                    JPH.EMotionType.Dynamic, LayerMoving,
                    new Vector3(0.7f, 0.5f, 0.3f));
            }
        }
    }

    /// <summary>
    /// Scatters flat box debris and small spheres in front of the wall.
    /// </summary>
    protected static unsafe void CreateRubble(JPH.BodyInterface bi, List<PhysicsBody> bodies, Random rng)
    {
        const float Spread = 120f;  // half-width of scatter area (terrain is ~150m each side)

        // Flat board debris — 80 pieces randomly placed
        for (int i = 0; i < 80; i++)
        {
            float x   = (float)(rng.NextDouble() * Spread * 2f - Spread);
            float z   = 15f + (float)(rng.NextDouble() * (Spread * 2f - 20f));
            float yaw = (float)(rng.NextDouble() * MathF.PI * 2f);
            float hw  = 0.3f + (float)(rng.NextDouble() * 0.5f);
            float hd  = 0.3f + (float)(rng.NextDouble() * 0.5f);
            AddBox(bi, bodies,
                hw, 0.08f, hd,
                x, 3f, z,
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw),
                JPH.EMotionType.Dynamic, LayerMoving,
                new Vector3(0.45f + (float)rng.NextDouble() * 0.2f,
                            0.45f + (float)rng.NextDouble() * 0.15f,
                            0.55f + (float)rng.NextDouble() * 0.1f));
        }

        // Loose rocks (spheres) — 80 pieces randomly placed
        for (int i = 0; i < 80; i++)
        {
            float x = (float)(rng.NextDouble() * Spread * 2f - Spread);
            float z = 15f + (float)(rng.NextDouble() * (Spread * 2f - 20f));
            float r = 0.2f + (float)(rng.NextDouble() * 0.5f);
            AddSphere(bi, bodies, r, x, 3f, z,
                JPH.EMotionType.Dynamic, LayerMoving,
                new Vector3(0.55f + (float)rng.NextDouble() * 0.2f,
                            0.38f + (float)rng.NextDouble() * 0.1f,
                            0.22f));
        }

        // Stacked crate piles — 8 randomly placed
        for (int p = 0; p < 8; p++)
        {
            float cx = (float)(rng.NextDouble() * Spread * 2f - Spread);
            float cz = 20f + (float)(rng.NextDouble() * (Spread * 2f - 30f));
            int   stacks = 2 + rng.Next(4);
            for (int s = 0; s < stacks; s++)
                AddBox(bi, bodies,
                    0.6f, 0.6f, 0.6f,
                    cx, 2f + s * 1.2f, cz,
                    Quaternion.CreateFromAxisAngle(Vector3.UnitY,
                        (float)(rng.NextDouble() * 0.4f - 0.2f)),
                    JPH.EMotionType.Dynamic, LayerMoving,
                    new Vector3(0.55f, 0.42f, 0.28f));
        }
    }

    // ── Bridge ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a suspended chain bridge at x = -25, spanning z ≈ 2..40 at y = 7.
    /// Plank 0 is a long static entry ramp (half-extents 2.5×0.25×22.5, tilted -10° around X).
    /// Planks 1–18 are dynamic (half-extents 2.5×0.25×1.0); plank 19 is a static exit anchor.
    /// Adjacent planks are pinned by zero-length DistanceConstraints on their shared edge corners.
    /// All planks share sub-group 0 in their GroupFilterTable so they never collide with each other.
    /// Constraints are appended to <paramref name="outConstraints"/> for caller cleanup.
    /// </summary>
    protected static unsafe void CreateBridge(
        JPH.BodyInterface             bi,
        JPH.PhysicsSystem             sys,
        List<PhysicsBody>             bodies,
        List<JPH.TwoBodyConstraint?>  outConstraints)
    {
        const int   cChainLength = 20;
        const float halfX = 2.5f, halfY = 0.25f, halfZ = 1.0f;
        const float largeHalfZ = 22.5f;
        const float startX = -25f, startY = 7f;

        // All planks share one GroupFilterTable instance; same sub-group 0 → never collide.
        using var groupFilter = new JPH.GroupFilterTable(1);

        // Ramp rotation: -10° around X axis.
        var rampQ = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -10f * MathF.PI / 180f);

        // The ramp body center is offset from the theoretical grid position by rotating
        // (0, 0, -(largeHalfZ - halfZ)) = (0, 0, -21.5) by rampQ:
        //   dY =  21.5 * sin(10°),   dZ = -21.5 * cos(10°)
        float rampOffY =  21.5f * MathF.Sin(10f * MathF.PI / 180f);
        float rampOffZ =  21.5f * MathF.Cos(10f * MathF.PI / 180f);

        float prevZ  = 0f;
        JPH.Body? prevBody = null;

        for (int i = 0; i < cChainLength; i++)
        {
            float posZ = prevZ + 2.0f * halfZ;   // advance 2 m along Z each step

            using var cs = new JPH.BodyCreationSettings();

            float renderHX, renderHY, renderHZ;

            if (i == 0)
            {
                // Long static entry ramp, tilted -10° around X.
                cs.mPosition.Set(startX, startY - rampOffY, posZ - rampOffZ);
                cs.mRotation.Set(rampQ.X, rampQ.Y, rampQ.Z, rampQ.W);
                cs.mMotionType  = JPH.EMotionType.Static;
                cs.mObjectLayer = LayerNonMoving;
                renderHX = halfX; renderHY = halfY; renderHZ = largeHalfZ;
            }
            else
            {
                cs.mPosition.Set(startX, startY, posZ);
                cs.mRotation.Set(0f, 0f, 0f, 1f);
                bool isLast = i == cChainLength - 1;
                cs.mMotionType  = isLast ? JPH.EMotionType.Static    : JPH.EMotionType.Dynamic;
                cs.mObjectLayer = isLast ? LayerNonMoving             : LayerMoving;
                renderHX = halfX; renderHY = halfY; renderHZ = halfZ;
            }

            using var shapeHE = new JPH.Vec3(renderHX, renderHY, renderHZ);
            using var shapeSS = new JPH.BoxShapeSettings(shapeHE);
            cs.SetShapeSettings(shapeSS);
            cs.mFriction = 1.0f;
            cs.mCollisionGroup.SetGroupFilter(groupFilter);
            cs.mCollisionGroup.SetGroupID(2);
            cs.mCollisionGroup.SetSubGroupID(0);

            var activation = cs.mMotionType == JPH.EMotionType.Static
                ? JPH.EActivation.DontActivate
                : JPH.EActivation.Activate;

            var body = bi.CreateBody(cs)!;
            bi.AddBody(body.GetID(), activation);

            bodies.Add(new PhysicsBody
            {
                bodyId = body.GetID(),
                color  = new Vector3(0.55f, 0.38f, 0.22f),
                shape  = RenderShape.Box,
                scale  = new Vector3(renderHX * 2f, renderHY * 2f, renderHZ * 2f),
            });

            // Pin adjacent planks at their shared edge corners (world-space distance constraint).
            if (prevBody != null)
            {
                // Both mPoint1 and mPoint2 evaluate to the same world position (shared edge),
                // so the auto-detected constraint distance is 0 — a rigid pin joint.
                float sharedZ = posZ - halfZ;   // == prevZ + halfZ

                using var dcL = new JPH.DistanceConstraintSettings();
                dcL.mPoint1.Set(startX - halfX, startY, sharedZ);
                dcL.mPoint2.Set(startX - halfX, startY, sharedZ);
                var cL = dcL.Create(prevBody, body);
                outConstraints.Add(cL);
                sys.AddConstraint(cL!);

                using var dcR = new JPH.DistanceConstraintSettings();
                dcR.mPoint1.Set(startX + halfX, startY, sharedZ);
                dcR.mPoint2.Set(startX + halfX, startY, sharedZ);
                var cR = dcR.Create(prevBody, body);
                outConstraints.Add(cR);
                sys.AddConstraint(cR!);
            }

            prevBody = body;
            prevZ    = posZ;
        }
    }

    // ── Noise helpers (2D value noise) ─────────────────────────────────────

    /// <summary>Smooth value noise in [-1, 1] at (x, y).</summary>
    protected static float TerrainNoise(float x, float y)
    {
        int   ix = (int)MathF.Floor(x);
        int   iy = (int)MathF.Floor(y);
        float fx = x - MathF.Floor(x);
        float fy = y - MathF.Floor(y);
        float ux = fx * fx * (3f - 2f * fx);
        float uy = fy * fy * (3f - 2f * fy);
        float a = TerrainRand(ix,     iy);
        float b = TerrainRand(ix + 1, iy);
        float c = TerrainRand(ix,     iy + 1);
        float d = TerrainRand(ix + 1, iy + 1);
        return a + (b - a) * ux + (c - a) * uy + (d - b - c + a) * ux * uy;
    }

    static float TerrainRand(int x, int y)
    {
        uint n = (uint)(x * 1619 + y * 31337 + 1013904223);
        n = (n >> 16) ^ (n * 0x45d9f3b);
        n = (n >> 16) ^ (n * 0x45d9f3b);
        return (int)n / (float)int.MaxValue;
    }

    // ── Soft body helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Creates a cloth grid (gridX × gridZ vertices, each spaced <paramref name="spacing"/> apart),
    /// applies the given invMass and perturbation functions, then calls CreateConstraints.
    /// Also tracks face indices for rendering.
    /// Returns the shared settings and the list of face tuples (v0,v1,v2).
    /// </summary>
    protected static JPH.SoftBodySharedSettings CreateClothSettings(
        int gridX, int gridZ, float spacing,
        Func<uint, uint, float>? invMassFunc,
        Func<uint, uint, Vector3>? perturbFunc,
        JPH.SoftBodySharedSettings.EBendType bendType,
        List<(uint v0, uint v1, uint v2)> outFaces,
        JPH.SoftBodySharedSettings.Const_VertexAttributes? vertexAttr = null,
        bool skipConstraints = false)
    {
        var settings = new JPH.SoftBodySharedSettings();

        // Centre the cloth so the body's centre-of-mass aligns with the spawn position,
        // matching C++ SoftBodyCreator::CreateCloth (cOffsetX / cOffsetZ).
        float offsetX = -0.5f * spacing * (gridX - 1);
        float offsetZ = -0.5f * spacing * (gridZ - 1);

        for (uint z = 0; z < (uint)gridZ; z++)
        for (uint x = 0; x < (uint)gridX; x++)
        {
            float invMass = invMassFunc != null ? invMassFunc(x, z) : 1f;
            Vector3 perturb = perturbFunc != null ? perturbFunc(x, z) : Vector3.Zero;
            float px = offsetX + x * spacing + perturb.X;
            float py = perturb.Y;
            float pz = offsetZ + z * spacing + perturb.Z;
            using var pos = new JPH.Const_Float3(px, py, pz);
            using var v = new JPH.SoftBodySharedSettings.Const_Vertex(pos, null, invMass);
            settings.mVertices.PushBack(v);
        }

        // Faces: two triangles per quad
        for (uint z = 0; z < (uint)(gridZ - 1); z++)
        for (uint x = 0; x < (uint)(gridX - 1); x++)
        {
            uint a = z * (uint)gridX + x;
            uint b = a + 1;
            uint c = a + (uint)gridX;
            uint d = c + 1;
            using var f1 = new JPH.SoftBodySharedSettings.Const_Face(a, c, b);
            using var f2 = new JPH.SoftBodySharedSettings.Const_Face(b, c, d);
            settings.AddFace(f1);
            settings.AddFace(f2);
        }

        // C++ SoftBodyCreator::CreateCloth default is VertexAttributes { 1e-5f, 1e-5f, 1e-5f }.
        // Const_VertexAttributes() default-constructs to { 0, 0, FLT_MAX } which is too stiff.
        if (!skipConstraints)
        {
            using var defaultAttr = vertexAttr ?? new JPH.SoftBodySharedSettings.VertexAttributes(1.0e-5f, 1.0e-5f, 1.0e-5f);
            settings.CreateConstraints(defaultAttr, 1u, bendType);
            settings.Optimize();
            ReadFacesFromSettings(settings, outFaces);
        }
        return settings;
    }

    /// <summary>Cloth with the four corners pinned (invMass = 0), all others = 1.</summary>
    protected static JPH.SoftBodySharedSettings CreateClothWithFixatedCornersSettings(
        int gridX, int gridZ, float spacing,
        List<(uint v0, uint v1, uint v2)> outFaces)
    {
        Func<uint, uint, float> invMass = (x, z) =>
            ((x == 0 || x == (uint)(gridX - 1)) && (z == 0 || z == (uint)(gridZ - 1))) ? 0f : 1f;
        return CreateClothSettings(gridX, gridZ, spacing, invMass, null,
            JPH.SoftBodySharedSettings.EBendType.None, outFaces);
    }

    /// <summary>
    /// Builds sphere soft body settings matching C++ SoftBodyCreator::CreateSphere vertex layout:
    /// index 0 = south pole, index 1 = north pole, index 2+ = ring vertices (theta=1..numTheta-2).
    /// Tracks face indices in outFaces (when skipConstraints is false).
    /// Pass skipConstraints=true to get vertices+faces only (add rod constraints, then call
    /// CalculateRodProperties, Optimize, and ReadFacesFromSettings manually).
    /// </summary>
    protected static JPH.SoftBodySharedSettings CreateSphereSettings(
        float radius, int numTheta, int numPhi,
        List<(uint v0, uint v1, uint v2)> outFaces,
        JPH.SoftBodySharedSettings.EBendType bendType = JPH.SoftBodySharedSettings.EBendType.None,
        bool skipConstraints = false)
    {
        var settings = new JPH.SoftBodySharedSettings();

        // South pole (index 0)
        using (var pos = new JPH.Const_Float3(0f, -radius, 0f))
        using (var v = new JPH.SoftBodySharedSettings.Const_Vertex(pos))
            settings.mVertices.PushBack(v);
        // North pole (index 1)
        using (var pos = new JPH.Const_Float3(0f, radius, 0f))
        using (var v = new JPH.SoftBodySharedSettings.Const_Vertex(pos))
            settings.mVertices.PushBack(v);
        // Ring vertices: theta=1..numTheta-2, phi=0..numPhi-1
        for (uint t = 1; t < (uint)(numTheta - 1); t++)
        for (uint p = 0; p < (uint)numPhi; p++)
        {
            float theta = MathF.PI * t / (numTheta - 1);
            float phi   = 2f * MathF.PI * p / numPhi;
            using var pos = new JPH.Const_Float3(
                radius * MathF.Sin(theta) * MathF.Cos(phi),
                -radius * MathF.Cos(theta),
                radius * MathF.Sin(theta) * MathF.Sin(phi));
            using var v = new JPH.SoftBodySharedSettings.Const_Vertex(pos);
            settings.mVertices.PushBack(v);
        }

        // VI(t,p): index 0=south pole, 1=north pole, 2+(t-1)*numPhi+p=ring
        uint VI(uint t, uint p) =>
            t == 0 ? 0u :
            t == (uint)(numTheta - 1) ? 1u :
            2u + (t - 1) * (uint)numPhi + p % (uint)numPhi;

        // South cap triangles
        for (uint p = 0; p < (uint)numPhi; p++)
        {
            uint pNext = (p + 1) % (uint)numPhi;
            using var f = new JPH.SoftBodySharedSettings.Const_Face(0u, VI(1u, p), VI(1u, pNext));
            settings.AddFace(f);
        }
        // Body quad rings — triangulation matches SoftBodyCreator::CreateSphere exactly:
        // face1 = (VI(t,p), VI(t+1,p), VI(t+1,pNext))  i.e. (v0,v2,v3)
        // face2 = (VI(t,p), VI(t+1,pNext), VI(t,pNext)) i.e. (v0,v3,v1)
        for (uint t = 1; t < (uint)(numTheta - 2); t++)
        for (uint p = 0; p < (uint)numPhi; p++)
        {
            uint pNext = (p + 1) % (uint)numPhi;
            uint v0 = VI(t, p), v1 = VI(t, pNext);
            uint v2 = VI(t + 1, p), v3 = VI(t + 1, pNext);
            using var f1 = new JPH.SoftBodySharedSettings.Const_Face(v0, v2, v3);
            using var f2 = new JPH.SoftBodySharedSettings.Const_Face(v0, v3, v1);
            settings.AddFace(f1);
            settings.AddFace(f2);
        }
        // North cap triangles
        uint lastRing = (uint)(numTheta - 2);
        for (uint p = 0; p < (uint)numPhi; p++)
        {
            uint pNext = (p + 1) % (uint)numPhi;
            using var f = new JPH.SoftBodySharedSettings.Const_Face(VI(lastRing, p), 1u, VI(lastRing, pNext));
            settings.AddFace(f);
        }

        if (!skipConstraints)
        {
            using var defaultAttr = new JPH.SoftBodySharedSettings.Const_VertexAttributes(1e-4f, 1e-4f, 1e-3f);
            settings.CreateConstraints(defaultAttr, 1u, bendType);
            settings.Optimize();
            ReadFacesFromSettings(settings, outFaces);
        }
        return settings;
    }

    /// <summary>
    /// Creates a soft body from shared settings, adds it to the physics system, and registers
    /// a <see cref="SoftBodyRenderEntry"/> in <see cref="_softBodies"/>.
    /// </summary>
    protected JPH.BodyID RegisterSoftBody(
        JPH.BodyInterface bi,
        JPH.SoftBodySharedSettings settings,
        List<(uint v0, uint v1, uint v2)> faces,
        float px, float py, float pz,
        float qx, float qy, float qz, float qw,
        Vector3 color,
        System.Action<JPH.SoftBodyCreationSettings>? configure = null)
    {
        uint vertCount = (uint)settings.mVertices.Size();

        using var pos = new JPH.Vec3(px, py, pz);
        using var rot = new JPH.Quat(qx, qy, qz, qw);
        using var cs = new JPH.SoftBodyCreationSettings(
            settings, pos, rot, LayerMoving);
        configure?.Invoke(cs);

        var id = bi.CreateAndAddSoftBody(cs, JPH.EActivation.Activate);

        // Build index buffer from face list
        var idxArr = new uint[faces.Count * 3];
        for (int i = 0; i < faces.Count; i++)
        {
            idxArr[i * 3 + 0] = faces[i].v0;
            idxArr[i * 3 + 1] = faces[i].v1;
            idxArr[i * 3 + 2] = faces[i].v2;
        }

        var vb = sg_make_buffer(new sg_buffer_desc
        {
            size  = (nuint)(vertCount * (uint)System.Runtime.InteropServices.Marshal.SizeOf<ConeVertex>()),
            usage = new sg_buffer_usage { stream_update = true },
            label = "softbody-vb"
        });
        var ib = sg_make_buffer(new sg_buffer_desc
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data  = SG_RANGE<uint>(idxArr),
            label = "softbody-ib"
        });

        _softBodies!.Add(new SoftBodyRenderEntry
        {
            bodyId     = id,
            vertexBuf  = vb,
            indexBuf   = ib,
            indexCount = idxArr.Length,
            color      = color,
            scratch    = new ConeVertex[vertCount],
            faces      = faces.ToArray(),
        });

        return id;
    }

    /// <summary>
    /// Builds GPU buffers for a soft body that was created manually via
    /// bi.CreateSoftBody() / bi.AddBody() (instead of RegisterSoftBody).
    /// Adds the resulting <see cref="SoftBodyRenderEntry"/> to _softBodies.
    /// </summary>
    protected SoftBodyRenderEntry BuildSoftBodyRenderEntry(
        JPH.BodyID id,
        uint vertCount,
        List<(uint v0, uint v1, uint v2)> faces,
        Vector3 color)
    {
        var idxArr = new uint[faces.Count * 3];
        for (int i = 0; i < faces.Count; i++)
        {
            idxArr[i * 3 + 0] = faces[i].v0;
            idxArr[i * 3 + 1] = faces[i].v1;
            idxArr[i * 3 + 2] = faces[i].v2;
        }

        var vb = sg_make_buffer(new sg_buffer_desc
        {
            size  = (nuint)(vertCount * (uint)System.Runtime.InteropServices.Marshal.SizeOf<ConeVertex>()),
            usage = new sg_buffer_usage { stream_update = true },
            label = "softbody-manual-vb"
        });
        var ib = sg_make_buffer(new sg_buffer_desc
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data  = SG_RANGE<uint>(idxArr),
            label = "softbody-manual-ib"
        });

        var entry = new SoftBodyRenderEntry
        {
            bodyId     = id,
            vertexBuf  = vb,
            indexBuf   = ib,
            indexCount = idxArr.Length,
            color      = color,
            scratch    = new ConeVertex[vertCount],
            faces      = faces.ToArray(),
        };
        _softBodies!.Add(entry);
        return entry;
    }

    /// <summary>
    /// Registers a soft body that was created with bi.CreateSoftBody but NOT added
    /// to the physics system. The render loop will use BodyGetSoftBodyVertex* APIs
    /// to read vertex positions directly from the body object each frame.
    /// </summary>
    protected void RegisterStandaloneSoftBody(
        JPH.Body body,
        uint vertCount,
        List<(uint v0, uint v1, uint v2)> faces,
        Vector3 color)
    {
        var idxArr = new uint[faces.Count * 3];
        for (int i = 0; i < faces.Count; i++)
        {
            idxArr[i * 3 + 0] = faces[i].v0;
            idxArr[i * 3 + 1] = faces[i].v1;
            idxArr[i * 3 + 2] = faces[i].v2;
        }

        var vb = sg_make_buffer(new sg_buffer_desc
        {
            size  = (nuint)(vertCount * (uint)System.Runtime.InteropServices.Marshal.SizeOf<ConeVertex>()),
            usage = new sg_buffer_usage { stream_update = true },
            label = "softbody-standalone-vb"
        });
        var ib = sg_make_buffer(new sg_buffer_desc
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data  = SG_RANGE<uint>(idxArr),
            label = "softbody-standalone-ib"
        });

        _softBodies!.Add(new SoftBodyRenderEntry
        {
            bodyId         = body.GetID(),
            standaloneBody = body,
            vertexBuf      = vb,
            indexBuf       = ib,
            indexCount     = idxArr.Length,
            color          = color,
            scratch        = new ConeVertex[vertCount],
            faces          = faces.ToArray(),
        });
    }

    /// <summary>
    /// Registers a Cosserat-rod soft body for line-segment rendering.
    /// Reads the rod pairs from <paramref name="settings"/> (call after Optimize())
    /// and creates a streaming GPU vertex buffer sized for all rods.
    /// </summary>
    protected void RegisterRodBody(
        JPH.BodyID bodyId,
        JPH.SoftBodySharedSettings settings,
        Vector3 color)
    {
        var cs       = settings;
        uint rodCount = (uint)cs.mRodStretchShearConstraints.Size();
        var rods     = new (uint v0, uint v1)[rodCount];
        for (uint i = 0; i < rodCount; i++)
        {
            rods[i] = (
                cs.mRodStretchShearConstraints[(UIntPtr)i].mVertex[0],
                cs.mRodStretchShearConstraints[(UIntPtr)i].mVertex[1]);
        }

        var vb = sg_make_buffer(new sg_buffer_desc
        {
            size  = (nuint)((int)rodCount * 2 * System.Runtime.InteropServices.Marshal.SizeOf<ConeVertex>()),
            usage = new sg_buffer_usage { stream_update = true },
            label = "rod-body-vb"
        });

        _rodBodies!.Add(new RodBodyRenderEntry
        {
            bodyId    = bodyId,
            rodCount  = (int)rodCount,
            color     = color,
            rods      = rods,
            scratch   = new ConeVertex[(int)rodCount * 2],
            vertexBuf = vb,
        });
    }

    /// <summary>
    /// Reads all faces from a <see cref="JPH.SoftBodySharedSettings"/> (post-Optimize)
    /// into <paramref name="outFaces"/>. Always call this after Optimize(), never before.
    /// </summary>
    protected static void ReadFacesFromSettings(
        JPH.SoftBodySharedSettings settings,
        List<(uint v0, uint v1, uint v2)> outFaces)
    {
        var cs = settings;
        uint faceCount = (uint)cs.mFaces.Size();
        for (uint i = 0; i < faceCount; i++)
            outFaces.Add((
                cs.mFaces[(UIntPtr)i].mVertex[0],
                cs.mFaces[(UIntPtr)i].mVertex[1],
                cs.mFaces[(UIntPtr)i].mVertex[2]));
    }

    // ── Soft body cube helpers ──────────────────────────────────────────────

    /// <summary>
    /// Builds a face list for rendering the 6 outer faces of a (gridSize)^3 soft-body cube.
    /// Vertices are indexed as x + y*n + z*n^2. Returns outward-facing triangles.
    /// </summary>
    protected static List<(uint v0, uint v1, uint v2)> CreateCubeFaces(int gridSize)
    {
        uint n = (uint)gridSize;
        uint VI(uint x, uint y, uint z) => x + y * n + z * n * n;

        var faces = new List<(uint, uint, uint)>();
        int m = gridSize - 1;
        for (int a = 0; a < m; a++)
        for (int b = 0; b < m; b++)
        {
            uint a0 = (uint)a, a1 = (uint)(a + 1), b0 = (uint)b, b1 = (uint)(b + 1);
            // z = 0
            faces.Add((VI(a0, b0, 0), VI(a1, b0, 0), VI(a1, b1, 0)));
            faces.Add((VI(a0, b0, 0), VI(a1, b1, 0), VI(a0, b1, 0)));
            // z = n-1
            faces.Add((VI(a0, b0, n-1), VI(a0, b1, n-1), VI(a1, b1, n-1)));
            faces.Add((VI(a0, b0, n-1), VI(a1, b1, n-1), VI(a1, b0, n-1)));
            // y = 0
            faces.Add((VI(a0, 0, b0), VI(a0, 0, b1), VI(a1, 0, b1)));
            faces.Add((VI(a0, 0, b0), VI(a1, 0, b1), VI(a1, 0, b0)));
            // y = n-1
            faces.Add((VI(a0, n-1, b0), VI(a1, n-1, b1), VI(a0, n-1, b1)));
            faces.Add((VI(a0, n-1, b0), VI(a1, n-1, b0), VI(a1, n-1, b1)));
            // x = 0
            faces.Add((VI(0, a0, b0), VI(0, a1, b1), VI(0, a0, b1)));
            faces.Add((VI(0, a0, b0), VI(0, a1, b0), VI(0, a1, b1)));
            // x = n-1
            faces.Add((VI(n-1, a0, b0), VI(n-1, a0, b1), VI(n-1, a1, b1)));
            faces.Add((VI(n-1, a0, b0), VI(n-1, a1, b1), VI(n-1, a1, b0)));
        }
        return faces;
    }

    /// <summary>
    /// Creates a soft-body cube with (gridSize)^3 vertices and spacing, registers it for rendering.
    /// </summary>
    protected JPH.BodyID RegisterCubeSoftBody(
        JPH.BodyInterface bi,
        int gridSize, float spacing,
        float px, float py, float pz,
        float qx, float qy, float qz, float qw,
        Vector3 color,
        System.Action<JPH.SoftBodyCreationSettings>? configure = null)
    {
        var settings = SoftBodySharedSettings.CreateCube((uint)gridSize, spacing)!;
        // SoftBodySettingsCreateCube already calls Optimize() internally; read faces post-optimize.
        var faces = new List<(uint, uint, uint)>();
        ReadFacesFromSettings(settings, faces);
        return RegisterSoftBody(bi, settings, faces, px, py, pz, qx, qy, qz, qw, color, configure);
    }

    // ── Truncated cone mesh factory ────────────────────────────────────────

    /// <summary>
    /// Generates a truncated cone (frustum) mesh: y = -halfH at radius botR, y = +halfH at radius topR.
    /// Returns GPU bindings (VB + IB). The caller must call sg_destroy_buffer on both when done.
    /// <para>Set <c>scale = Vector3.One</c> and <c>localOffset</c> to the midpoint in shape-local space.</para>
    /// </summary>
    protected static unsafe sg_bindings CreateTaperedConeMesh(float topR, float botR, float halfH, out int indexCount)
    {
        const int seg = 32;
        var verts = new System.Collections.Generic.List<ConeVertex>();
        var idx   = new System.Collections.Generic.List<ushort>();

        // Top cap (y = +halfH, radius = topR); omit if topR == 0 (pointed cone tip)
        if (topR > 0f)
        {
            int topCenter   = verts.Count;
            verts.Add(new ConeVertex { position = new Vector3(0f, halfH, 0f), normal = Vector3.UnitY });
            int topRimStart = verts.Count;
            for (int i = 0; i < seg; i++)
            {
                float a = 2f * MathF.PI * i / seg;
                verts.Add(new ConeVertex { position = new Vector3(topR * MathF.Cos(a), halfH, topR * MathF.Sin(a)), normal = Vector3.UnitY });
            }
            for (int i = 0; i < seg; i++)
            {
                idx.Add((ushort)topCenter);
                idx.Add((ushort)(topRimStart + i));
                idx.Add((ushort)(topRimStart + (i + 1) % seg));
            }
        }

        // Bottom cap (y = -halfH, radius = botR); omit if botR == 0
        if (botR > 0f)
        {
            int botCenter   = verts.Count;
            verts.Add(new ConeVertex { position = new Vector3(0f, -halfH, 0f), normal = -Vector3.UnitY });
            int botRimStart = verts.Count;
            for (int i = 0; i < seg; i++)
            {
                float a = 2f * MathF.PI * i / seg;
                verts.Add(new ConeVertex { position = new Vector3(botR * MathF.Cos(a), -halfH, botR * MathF.Sin(a)), normal = -Vector3.UnitY });
            }
            for (int i = 0; i < seg; i++)
            {
                idx.Add((ushort)botCenter);
                idx.Add((ushort)(botRimStart + (i + 1) % seg));
                idx.Add((ushort)(botRimStart + i));
            }
        }

        // Lateral surface — outward normal for a cone: normalize(cos(a), -drdy, sin(a))
        // where drdy = (topR - botR) / (2 * halfH)
        float drdy    = (topR - botR) / (2f * halfH);
        float nLen    = MathF.Sqrt(1f + drdy * drdy);
        float sideNy  = -drdy / nLen;
        float sideNr  =  1f   / nLen;

        int sideStart = verts.Count;
        for (int i = 0; i <= seg; i++)
        {
            float a = 2f * MathF.PI * i / seg;
            float cx = MathF.Cos(a), cz = MathF.Sin(a);
            // top ring vertex then bottom ring vertex (interleaved, closed at i==seg by repeating i==0 angle)
            verts.Add(new ConeVertex { position = new Vector3(topR * cx,  halfH, topR * cz), normal = new Vector3(sideNr * cx, sideNy, sideNr * cz) });
            verts.Add(new ConeVertex { position = new Vector3(botR * cx, -halfH, botR * cz), normal = new Vector3(sideNr * cx, sideNy, sideNr * cz) });
        }
        for (int i = 0; i < seg; i++)
        {
            int b = sideStart + i * 2;
            idx.Add((ushort)b);       idx.Add((ushort)(b + 2)); idx.Add((ushort)(b + 1));
            idx.Add((ushort)(b + 1)); idx.Add((ushort)(b + 2)); idx.Add((ushort)(b + 3));
        }

        indexCount = idx.Count;
        var bindings = default(sg_bindings);
        bindings.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc { data = SG_RANGE<ConeVertex>(verts.ToArray()) });
        bindings.index_buffer      = sg_make_buffer(new sg_buffer_desc
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data  = SG_RANGE<ushort>(idx.ToArray())
        });
        return bindings;
    }

    /// <summary>
    /// Builds a smooth-shaded GPU mesh from the actual convex hull geometry.
    /// Each unique hull vertex carries the area-weighted average of all adjacent face normals,
    /// giving smooth lighting on rounded hulls (sphere approximations, cylinders, etc.).
    /// </summary>
    protected static unsafe sg_bindings CreateConvexHullMesh(JPH.Const_ConvexHullShape hull, out int indexCount)
    {
        // The generated wrapper for GetFaceVertices can only pass a pointer to one uint.
        // Use a local DllImport to pass a real uint[] with the correct size.
#if __IOS__
        [DllImport("@rpath/cjolt.framework/cjolt", EntryPoint = "JPH_ConvexHullShape_GetFaceVertices",
            CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
#else
        [DllImport("cjolt", EntryPoint = "JPH_ConvexHullShape_GetFaceVertices",
            CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
#endif
        extern static uint GetFaceVerticesRaw(
            JPH.Const_ConvexHullShape._Underlying* shape, uint faceIndex, uint maxVerts, uint* outVerts);

        uint numPts   = hull.GetNumPoints();
        uint numFaces = hull.GetNumFaces();

        // Gather hull vertex positions (GetPoint heap-allocates → must dispose)
        var positions = new Vector3[numPts];
        for (uint i = 0; i < numPts; i++)
        {
            using var pt = hull.GetPoint(i);
            positions[i] = new Vector3(pt.GetX(), pt.GetY(), pt.GetZ());
        }

        // Collect face vertex index arrays
        var faceIdx = new uint[numFaces][];
        for (uint f = 0; f < numFaces; f++)
        {
            uint k = hull.GetNumVerticesInFace(f);
            faceIdx[f] = new uint[k];
            fixed (uint* pIdx = faceIdx[f])
                GetFaceVerticesRaw(hull._UnderlyingPtr, f, k, pIdx);
        }

        // Build flat-shaded mesh: one vertex per polygon corner with the face's outward normal.
        // Flat normals give correct hard-edge shading with no averaging artefacts.
        var vertexList = new List<ConeVertex>();
        var indices    = new List<ushort>();
        for (uint f = 0; f < numFaces; f++)
        {
            var fi = faceIdx[f];
            if (fi.Length < 3) continue;
            var pa = positions[fi[0]];
            var pb = positions[fi[1]];
            var pc = positions[fi[2]];
            var rawN     = Vector3.Cross(pb - pa, pc - pa);
            var faceNorm = rawN.LengthSquared() > 0f ? Vector3.Normalize(rawN) : Vector3.UnitY;
            ushort baseIdx = (ushort)vertexList.Count;
            for (int i = 0; i < fi.Length; i++)
                vertexList.Add(new ConeVertex { position = positions[fi[i]], normal = faceNorm });
            for (int t = 1; t < fi.Length - 1; t++)
            {
                indices.Add(baseIdx);
                indices.Add((ushort)(baseIdx + t));
                indices.Add((ushort)(baseIdx + t + 1));
            }
        }
        var verts  = vertexList.ToArray();
        var idxArr = indices.ToArray();
        indexCount = idxArr.Length;
        var b = default(sg_bindings);
        b.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc { data = SG_RANGE<ConeVertex>(verts) });
        b.index_buffer      = sg_make_buffer(new sg_buffer_desc
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data  = SG_RANGE<ushort>(idxArr)
        });
        return b;
    }
}
