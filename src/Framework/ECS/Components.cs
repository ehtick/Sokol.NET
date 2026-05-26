using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.Physics;

namespace GameEditor.Framework.ECS.Components
{
    public struct Transform
    {
        public Vector3 Position;
        public Vector3 EulerAngles;
        public Vector3 Scale;
        public Entity? Parent;

        public static Transform Default => new Transform
        {
            Position = Vector3.Zero,
            EulerAngles = Vector3.Zero,
            Scale = Vector3.One,
            Parent = null
        };

        public Matrix4x4 LocalMatrix =>
            Matrix4x4.CreateScale(Scale) *
            Matrix4x4.CreateFromYawPitchRoll(
                EulerAngles.Y * MathF.PI / 180f,
                EulerAngles.X * MathF.PI / 180f,
                EulerAngles.Z * MathF.PI / 180f) *
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
                var rot = Matrix4x4.CreateFromYawPitchRoll(
                    EulerAngles.Y * MathF.PI / 180f,
                    EulerAngles.X * MathF.PI / 180f,
                    EulerAngles.Z * MathF.PI / 180f);
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
        public ColliderShape Shape;
        public bool IsTrigger;
        public ushort Layer;
        public ushort LayerMask;

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
}
