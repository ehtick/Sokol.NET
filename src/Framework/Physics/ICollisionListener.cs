using System.Numerics;

namespace GameEditor.Framework.Physics
{
    /// <summary>Contact point data passed to collision events.</summary>
    public readonly struct ContactPoint
    {
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly float   Impulse;

        public ContactPoint(Vector3 point, Vector3 normal, float impulse)
        {
            Point   = point;
            Normal  = normal;
            Impulse = impulse;
        }
    }

    /// <summary>
    /// Receives collision and trigger events from the active physics backend.
    /// Implemented by SceneManager to dispatch to GameBehaviour lifecycle hooks.
    /// All callbacks are invoked on the main thread (never inside the physics step).
    /// </summary>
    public interface ICollisionListener
    {
        void OnCollisionEnter(PhysicsBodyHandle a, PhysicsBodyHandle b, ContactPoint contact);
        void OnCollisionStay(PhysicsBodyHandle a, PhysicsBodyHandle b);
        void OnCollisionExit(PhysicsBodyHandle a, PhysicsBodyHandle b);
        void OnTriggerEnter(PhysicsBodyHandle trigger, PhysicsBodyHandle other);
        void OnTriggerExit(PhysicsBodyHandle trigger, PhysicsBodyHandle other);
    }
}
