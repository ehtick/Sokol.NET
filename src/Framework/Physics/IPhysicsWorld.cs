using System.Numerics;
using System.Collections.Generic;

namespace GameEditor.Framework.Physics
{
    /// <summary>Opaque handle to a physics body in whatever backend is active.</summary>
    public readonly struct PhysicsBodyHandle
    {
        public readonly int Value;
        public bool IsValid => Value > 0;

        public PhysicsBodyHandle(int value) => Value = value;
        public static readonly PhysicsBodyHandle Invalid = new PhysicsBodyHandle(0);
    }

    public enum ColliderShape { Box, Sphere, Capsule, Cylinder, Plane, ConvexHull, Mesh }

    public enum RigidbodyMotionType { Dynamic, Static, Kinematic }

    /// <summary>
    /// One collider shape within a compound body (or the sole shape of a single-shape body).
    /// Dimensions are in mesh-local units; <see cref="BodyDesc.Scale"/> is applied in CreateBody.
    /// </summary>
    public struct ShapeEntry
    {
        public ColliderShape Shape;

        // Box
        public Vector3 HalfExtent;
        // Sphere, Capsule, Cylinder
        public float Radius;
        // Capsule (half-height of cylindrical portion), Cylinder (half-height)
        public float HalfHeight;

        // Offset relative to body centre (compound bodies)
        public Vector3    Offset;
        public Quaternion OffsetRotation;

        // ConvexHull / Mesh source geometry (mesh-local coords; CreateBody scales by entity scale).
        // Not serialised — repopulated from MeshRenderer at play-mode start.
        public Vector3[]? MeshVertices;
        public uint[]?    MeshIndices;

        public static ShapeEntry Default(ColliderShape shape = ColliderShape.Box) => new ShapeEntry
        {
            Shape          = shape,
            HalfExtent     = new Vector3(0.5f, 0.5f, 0.5f),
            Radius         = 0.5f,
            HalfHeight     = 0.5f,
            OffsetRotation = Quaternion.Identity,
        };
    }

    public readonly struct BodyDesc
    {
        public readonly Vector3    Position;
        public readonly Quaternion Rotation;
        public readonly Vector3    Scale;
        public readonly RigidbodyMotionType MotionType;
        public readonly float      Mass;
        public readonly bool       UseGravity;
        public readonly float      Friction;
        public readonly float      Restitution;
        public readonly float      LinearDamping;
        public readonly float      AngularDamping;
        public readonly List<ShapeEntry> Shapes;
        public readonly bool       IsTrigger;
        public readonly ushort     Layer;
        public readonly ushort     LayerMask;

        public bool IsStatic    => MotionType == RigidbodyMotionType.Static;
        public bool IsKinematic => MotionType == RigidbodyMotionType.Kinematic;

        public BodyDesc(
            Vector3 position, Quaternion rotation, Vector3 scale,
            RigidbodyMotionType motionType, float mass, bool useGravity,
            List<ShapeEntry> shapes,
            float friction = 0.5f,
            float restitution = 0.0f,
            float linearDamping = 0.0f,
            float angularDamping = 0.05f,
            bool isTrigger = false,
            ushort layer = 1,
            ushort layerMask = 0xFFFF)
        {
            Position      = position;
            Rotation      = rotation;
            Scale         = scale;
            MotionType    = motionType;
            Mass          = mass;
            UseGravity    = useGravity;
            Shapes        = shapes ?? new List<ShapeEntry> { ShapeEntry.Default() };
            Friction      = friction;
            Restitution   = restitution;
            LinearDamping  = linearDamping;
            AngularDamping = angularDamping;
            IsTrigger  = isTrigger;
            Layer      = layer;
            LayerMask  = layerMask;
        }
    }

    public readonly struct RaycastHit
    {
        public readonly PhysicsBodyHandle Body;
        public readonly Vector3           Point;
        public readonly Vector3           Normal;
        public readonly float             Distance;

        public RaycastHit(PhysicsBodyHandle body, Vector3 point, Vector3 normal, float distance)
        {
            Body     = body;
            Point    = point;
            Normal   = normal;
            Distance = distance;
        }
    }

    /// <summary>
    /// Physics engine abstraction — implemented by JoltPhysicsWorld (3D) and Box2DPhysicsWorld (2D).
    /// All methods are called from the main thread.
    /// </summary>
    public interface IPhysicsWorld
    {
        void Initialize(Vector3 gravity);
        void Step(float deltaTime);
        void Shutdown();

        PhysicsBodyHandle CreateBody(BodyDesc desc);
        void DestroyBody(PhysicsBodyHandle handle);

        void SetPosition(PhysicsBodyHandle handle, Vector3 position);
        void SetRotation(PhysicsBodyHandle handle, Quaternion rotation);
        /// <summary>Move a kinematic body to a target pose. Computes implicit velocity so
        /// the body correctly pushes dynamic bodies it collides with.</summary>
        void MoveKinematic(PhysicsBodyHandle handle, Vector3 targetPosition, Quaternion targetRotation, float deltaTime);
        Vector3    GetPosition(PhysicsBodyHandle handle);
        Quaternion GetRotation(PhysicsBodyHandle handle);

        void SetLinearVelocity(PhysicsBodyHandle handle, Vector3 velocity);
        Vector3 GetLinearVelocity(PhysicsBodyHandle handle);
        void SetAngularVelocity(PhysicsBodyHandle handle, Vector3 velocity);
        Vector3 GetAngularVelocity(PhysicsBodyHandle handle);
        void AddForce(PhysicsBodyHandle handle, Vector3 force);
        void AddImpulse(PhysicsBodyHandle handle, Vector3 impulse);
        void AddTorque(PhysicsBodyHandle handle, Vector3 torque);

        bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit);
        int OverlapSphere(Vector3 center, float radius, List<PhysicsBodyHandle> results, int maxResults = 64);
        int OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, List<PhysicsBodyHandle> results, int maxResults = 64);

        void SetCollisionListener(ICollisionListener? listener);
    }
}
