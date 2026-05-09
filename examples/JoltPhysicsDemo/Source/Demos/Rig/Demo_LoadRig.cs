using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Sokol;
using static Sokol.SG;
using static Sokol.Utils;

class Demo_LoadRig : DemoBase
{
    public override string Name     => "Load Rig";
    public override string Category => "Rig";

    public override CameraDesc GetCameraDesc() => new CameraDesc
    {
        Distance  = 5, Latitude  = 20, Longitude = 45,
        Center    = new Vector3(0, 1, 0),
        Aspect    = 60, NearZ = 0.1f, FarZ = 500.0f,
    };

    private JPH.Ragdoll? _ragdoll;
    private int          _ragdollBodyStart = -1;
    private int          _ragdollBodyCount = 0;
    private List<PhysicsBody>? _bodies;

    public override unsafe void Init(JPH.BodyInterface bi, JPH.PhysicsSystem sys, List<PhysicsBody> bodies, Random random)
    {
        _bodies = bodies;
        AddFloor(bi, bodies);

        // Load ragdoll settings from file
        JPH.RagdollSettings? settings;
        var data = LoadAsset("Human.tof");
        settings = data.RagdollSettingsLoadFromBuffer();
        if (settings == null) return;

        RemapRagdollLayers(settings);
        settings.GetSkeleton()?.CalculateParentJointIndices();
        settings.Stabilize();
        settings.CalculateConstraintPriorities();
        settings.DisableParentChildCollisions();
        settings.CalculateBodyIndexToConstraintIndex();
        settings.CalculateConstraintIndexToBodyIdxPair();

        // Create ragdoll
        _ragdoll = settings.CreateRagdoll(1, 0, sys);
        if (_ragdoll == null) return;
        _ragdoll.AddToPhysicsSystem(JPH.EActivation.Activate);

        // Register bodies for rendering
        _ragdollBodyStart = bodies.Count;
        _ragdollBodyCount = (int)_ragdoll.GetBodyCount();
        var scale = new Vector3(0.06f, 0.15f, 0.06f);
        for (int i = 0; i < _ragdollBodyCount; i++)
        {
            bodies.Add(new PhysicsBody
            {
                bodyId = _ragdoll.GetBodyID(i),
                shape  = RenderShape.Capsule,
                scale  = scale,
                color  = GetDistinctColor(i),
            });
        }
    }

    public override void Cleanup(JPH.PhysicsSystem sys)
    {
        if (_ragdoll != null)
        {
            if (_bodies != null && _ragdollBodyStart >= 0 && _ragdollBodyCount > 0)
            {
                _bodies.RemoveRange(_ragdollBodyStart, _ragdollBodyCount);
                _bodies = null;
            }
            _ragdollBodyStart = -1;
            _ragdollBodyCount = 0;
            _ragdoll.RemoveFromPhysicsSystem();
            _ragdoll.Dispose();
            _ragdoll = null;
        }
    }
}
