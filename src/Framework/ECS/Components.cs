using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.Physics;

namespace GameEditor.Framework.ECS.Components
{
    public struct Transform
    {
        public Vector3    Position;
        /// <summary>Canonical rotation storage. Use this in all hot paths (physics, rendering).</summary>
        public Quaternion Rotation;
        public Vector3    Scale;
        public Entity?    Parent;

        /// <summary>Euler angles in degrees (YXZ order). Computed from <see cref="Rotation"/>.
        /// Prefer <see cref="Rotation"/> in hot paths to avoid decomposition overhead.</summary>
        public Vector3 EulerAngles
        {
            get => QuaternionToEuler(Rotation);
            set => Rotation = Quaternion.CreateFromYawPitchRoll(
                value.Y * MathF.PI / 180f,
                value.X * MathF.PI / 180f,
                value.Z * MathF.PI / 180f);
        }

        public static Transform Default => new Transform
        {
            Position = Vector3.Zero,
            Rotation = Quaternion.Identity,
            Scale    = Vector3.One,
            Parent   = null
        };

        public Matrix4x4 LocalMatrix =>
            Matrix4x4.CreateScale(Scale) *
            Matrix4x4.CreateFromQuaternion(Rotation) *
            Matrix4x4.CreateTranslation(Position);

        // Walks the parent chain to compute the world-space matrix.
        public static Matrix4x4 GetWorldMatrix(ECSWorld world, in Transform transform, int depth = 0)
        {
            Matrix4x4 local = transform.LocalMatrix;
            if (!transform.Parent.HasValue || depth > 32) return local;
            Entity parent = transform.Parent.Value;
            if (!parent.IsAlive || !world.TryGetComponent<Transform>(parent, out var parentTransform))
                return local;
            return local * GetWorldMatrix(world, parentTransform, depth + 1);
        }

        // Converts a quaternion to Euler angles (degrees) matching CreateFromYawPitchRoll(Y,X,Z).
        public static Vector3 QuaternionToEuler(Quaternion q)
        {
            // Extract rotation matrix entries for M = Ry * Rx * Rz (row-vector convention)
            Matrix4x4 m = Matrix4x4.CreateFromQuaternion(q);
            float pitch = MathF.Asin(Math.Clamp(-m.M32, -1f, 1f));
            float yaw, roll;
            if (MathF.Abs(m.M32) < 0.9999f)
            {
                yaw  = MathF.Atan2(m.M31, m.M33);
                roll = MathF.Atan2(m.M12, m.M22);
            }
            else
            {
                yaw  = MathF.Atan2(-m.M13, m.M11);
                roll = 0f;
            }
            const float Rad2Deg = 180f / MathF.PI;
            return new Vector3(pitch * Rad2Deg, yaw * Rad2Deg, roll * Rad2Deg);
        }

        public Vector3 Forward
        {
            get
            {
                var rot = Matrix4x4.CreateFromQuaternion(Rotation);
                return new Vector3(rot.M31, rot.M32, rot.M33);
            }
        }
    }

    public struct NameTag
    {
        public string Name;
    }

    public struct ActiveFlag
    {
        public bool Active;
    }

    public struct MeshRenderer
    {
        public string MeshPath;
        public bool Visible;
    }

    public struct CameraComponent
    {
        public float Fov;
        public float NearZ;
        public float FarZ;
        public bool IsMain;
        public bool IsOrthographic;
        public float OrthoSize;   // half-height in world units (ortho mode only)
    }

    public enum LightType { Directional, Point, Spot }

    public struct LightComponent
    {
        public LightType Type;
        public System.Numerics.Vector3 Color;
        public float Intensity;
        public float Range;
        public float InnerAngle;  // spot only — half-angle of inner cone, degrees (default 25)
        public float OuterAngle;  // spot only — half-angle of outer cone, degrees (default 35)
    }

    public struct RigidbodyComponent
    {
        public RigidbodyMotionType MotionType;
        public float Mass;
        public bool UseGravity;
        public float Friction;
        public float Restitution;
        public float LinearDamping;
        public float AngularDamping;
        /// <summary>All collision shapes on this body. Always has at least one entry at play time.</summary>
        public List<ShapeEntry>? Shapes;
        public bool IsTrigger;
        public ushort Layer;
        public ushort LayerMask;

        /// <summary>Convenience accessor — type of the first (or only) shape entry.</summary>
        public readonly ColliderShape Shape => Shapes?.Count > 0 ? Shapes[0].Shape : ColliderShape.Box;

        public bool IsStatic
        {
            readonly get => MotionType == RigidbodyMotionType.Static;
            set => MotionType = value ? RigidbodyMotionType.Static : RigidbodyMotionType.Dynamic;
        }

        public bool IsKinematic
        {
            readonly get => MotionType == RigidbodyMotionType.Kinematic;
            set => MotionType = value ? RigidbodyMotionType.Kinematic : RigidbodyMotionType.Dynamic;
        }
    }

    public struct ScriptComponent
    {
        public string TypeName;
        /// <summary>Serialized public field values, keyed by field name. Null = no overrides.</summary>
        public Dictionary<string, string>? Properties;
    }

    public struct ScriptCollectionComponent
    {
        /// <summary>Additional scripts attached to the entity (beyond primary ScriptComponent).</summary>
        public List<ScriptComponent>? Scripts;
    }

    /// <summary>
    /// Constraint (joint) between two bodies. BodyA/BodyB reference sibling entities that
    /// carry <see cref="RigidbodyComponent"/>. At play-mode start SceneManager resolves them
    /// to <see cref="GameEditor.Framework.Physics.PhysicsBodyHandle"/> references and calls
    /// <see cref="GameEditor.Framework.Physics.IPhysicsWorld.CreateConstraint"/>.
    /// </summary>
    public struct ConstraintComponent
    {
        public GameEditor.Framework.Physics.ConstraintType Type;
        /// <summary>Entity with a RigidbodyComponent. Null entity = world anchor.</summary>
        public Frent.Entity BodyA;
        /// <summary>Entity with a RigidbodyComponent.</summary>
        public Frent.Entity BodyB;
        public System.Numerics.Vector3 LocalAnchorA;
        public System.Numerics.Vector3 LocalAnchorB;
        /// <summary>Primary axis in body-A space (hinge axis, slider axis, cone twist axis).</summary>
        public System.Numerics.Vector3 LocalAxisA;
        /// <summary>Primary axis in body-B space.</summary>
        public System.Numerics.Vector3 LocalAxisB;
        public float MinLimit;
        public float MaxLimit;

        /// <summary>Runtime-only: handle returned by CreateConstraint. Not serialized.</summary>
        public GameEditor.Framework.Physics.ConstraintHandle RuntimeHandle;
    }
}
