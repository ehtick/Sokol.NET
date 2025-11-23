// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace JoltPhysicsSharp;

using JPH_ObjectLayer = uint;
using unsafe JPH_CastRayCollector = delegate* unmanaged<nint, RayCastResult*, float>;
using unsafe JPH_CastRayResultCallback = delegate* unmanaged<nint, RayCastResult*, void>;
using unsafe JPH_CastShapeCollector = delegate* unmanaged<nint, ShapeCastResult*, float>;
using unsafe JPH_CastShapeResultCallback = delegate* unmanaged<nint, ShapeCastResult*, void>;
using unsafe JPH_CollidePointCollector = delegate* unmanaged<nint, CollidePointResult*, float>;
using unsafe JPH_CollidePointResultCallback = delegate* unmanaged<nint, CollidePointResult*, void>;
//public delegate float RayCastBodyCollector(nint userData, in BroadPhaseCastResult result);
using unsafe JPH_CollideShapeBodyCollectorCallback = delegate* unmanaged<nint, BodyID*, float>;
using unsafe JPH_CollideShapeCollector = delegate* unmanaged<nint, CollideShapeResult*, float>;
using unsafe JPH_CollideShapeResultCallback = delegate* unmanaged<nint, CollideShapeResult*, void>;
using unsafe JPH_QueueJobCallback = delegate* unmanaged<nint, /*JPH_JobFunction*/delegate* unmanaged<nint, void>, nint, void>;
using unsafe JPH_QueueJobsCallback = delegate* unmanaged<nint, /*JPH_JobFunction**/delegate* unmanaged<nint, void>, void**, uint, void>;
using unsafe JPH_RayCastBodyCollectorCallback = delegate* unmanaged<nint, BroadPhaseCastResult*, float>;

internal static unsafe partial class JoltApi
{
    private const DllImportSearchPath DefaultDllImportSearchPath = DllImportSearchPath.ApplicationDirectory | DllImportSearchPath.UserDirectories | DllImportSearchPath.UseDllDirectoryForDependencies;

    /// <summary>
    /// Raised whenever a native library is loaded by Jolt.
    /// Handlers can be added to this event to customize how libraries are loaded, and they will be used first whenever a new native library is being resolved.
    /// </summary>
    public static event DllImportResolver? JoltDllImporterResolver;

    private const string LibName = "joltc";
    private const string LibDoubleName = "joltc_double";

    public static bool DoublePrecision { get; set; }

    static JoltApi()
    {
        // NativeLibrary.SetDllImportResolver(typeof(JoltApi).Assembly, OnDllImport);
    }

    private static nint OnDllImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != LibName)
        {
            return nint.Zero;
        }

        nint nativeLibrary = nint.Zero;
        DllImportResolver? resolver = JoltDllImporterResolver;
        if (resolver != null)
        {
            nativeLibrary = resolver(libraryName, assembly, searchPath);
        }

        if (nativeLibrary != nint.Zero)
        {
            return nativeLibrary;
        }

#if DEBUG
        string debugLibrarySuffix = "d";
#else
        string debugLibrarySuffix = Debugger.IsAttached ? "d" : string.Empty;
#endif

        if (OperatingSystem.IsWindows())
        {
            string dllName = DoublePrecision ? $"{LibDoubleName}{debugLibrarySuffix}.dll" : $"{LibName}{debugLibrarySuffix}.dll";

            if (NativeLibrary.TryLoad(dllName, assembly, searchPath, out nativeLibrary))
            {
                return nativeLibrary;
            }

            // Try without the debug suffix
            dllName = DoublePrecision ? $"{LibDoubleName}.dll" : $"{LibName}.dll";

            if (NativeLibrary.TryLoad(dllName, assembly, searchPath, out nativeLibrary))
            {
                return nativeLibrary;
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            string dllName = DoublePrecision ? $"lib{LibDoubleName}{debugLibrarySuffix}.so" : $"lib{LibName}{debugLibrarySuffix}.so";

            if (NativeLibrary.TryLoad(dllName, assembly, searchPath, out nativeLibrary))
            {
                return nativeLibrary;
            }

            // Try without the debug suffix
            dllName = DoublePrecision ? $"lib{LibDoubleName}.so" : $"lib{LibName}.so";

            if (NativeLibrary.TryLoad(dllName, assembly, searchPath, out nativeLibrary))
            {
                return nativeLibrary;
            }
        }
        else if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
        {
            string dllName = DoublePrecision ? $"lib{LibDoubleName}{debugLibrarySuffix}.dylib" : $"lib{LibName}{debugLibrarySuffix}.dylib";

            if (NativeLibrary.TryLoad(dllName, assembly, searchPath, out nativeLibrary))
            {
                return nativeLibrary;
            }

            // Try without the debug suffix
            dllName = DoublePrecision ? $"lib{LibDoubleName}.dylib" : $"lib{LibName}.dylib";

            if (NativeLibrary.TryLoad(dllName, assembly, searchPath, out nativeLibrary))
            {
                return nativeLibrary;
            }
        }

        string libraryLoadName = DoublePrecision ? $"lib{LibDoubleName}{debugLibrarySuffix}" : $"lib{LibName}{debugLibrarySuffix}";

        if (NativeLibrary.TryLoad(libraryLoadName, assembly, searchPath, out nativeLibrary))
        {
            return nativeLibrary;
        }

        // Try without the debug suffix
        libraryLoadName = DoublePrecision ? $"lib{LibDoubleName}" : $"lib{LibName}";

        if (NativeLibrary.TryLoad(libraryLoadName, assembly, searchPath, out nativeLibrary))
        {
            return nativeLibrary;
        }

        libraryLoadName = DoublePrecision ? LibDoubleName : LibName;
        if (NativeLibrary.TryLoad(libraryLoadName, assembly, searchPath, out nativeLibrary))
        {
            return nativeLibrary;
        }

        return nint.Zero;
    }

    /// <summary>Converts an unmanaged string to a managed version.</summary>
    /// <param name="unmanaged">The unmanaged string to convert.</param>
    /// <returns>A managed string.</returns>
    public static string? ConvertToManaged(byte* unmanaged)
    {
        if (unmanaged == null)
            return null;

        return UTF8EncodingRelaxed.Default.GetString(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(unmanaged));
    }

    /// <summary>Converts an unmanaged string to a managed version.</summary>
    /// <param name="unmanaged">The unmanaged string to convert.</param>
    /// <returns>A managed string.</returns>
    public static string? ConvertToManaged(byte* unmanaged, int maxLength)
    {
        if (unmanaged == null)
            return null;

        var span = new ReadOnlySpan<byte>(unmanaged, maxLength);
        var indexOfZero = span.IndexOf((byte)0);
        return indexOfZero < 0 ? UTF8EncodingRelaxed.Default.GetString(span) : UTF8EncodingRelaxed.Default.GetString(span.Slice(0, indexOfZero));
    }

    public struct Mat4
    {
        public Column__FixedBuffer column;

        [InlineArray(4)]
        public partial struct Column__FixedBuffer
        {
            public Vector4 e0;
        }
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Init();

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Shutdown();

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SetTraceHandler(delegate* unmanaged<byte*, void> callback);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SetAssertFailureHandler(delegate* unmanaged<byte*, byte*, byte*, uint, byte> callback);

    // JobSystem
#pragma warning disable CS0649
    public struct JPH_JobSystemConfig
    {
        public nint context;
        public JPH_QueueJobCallback queueJob;
        public JPH_QueueJobsCallback queueJobs;
        public uint maxConcurrency;
        public uint maxBarriers;
    }
#pragma warning restore CS0649

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_JobSystemThreadPool_Create(JobSystemThreadPoolConfig* config);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_JobSystemThreadPool_Create(in JobSystemThreadPoolConfig config);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_JobSystemCallback_Create(JPH_JobSystemConfig* config);

    // JobSystem
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_JobSystem_Destroy(nint jobSystem);

    // BroadPhaseLayerInterface
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BroadPhaseLayerInterfaceMask_Create(uint numBroadPhaseLayers);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BroadPhaseLayerInterfaceMask_ConfigureLayer(nint bpInterface, byte broadPhaseLayer, uint groupsToInclude, uint groupsToExclude);


#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BroadPhaseLayerInterfaceTable_Create(uint numObjectLayers, uint numBroadPhaseLayers);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BroadPhaseLayerInterfaceTable_MapObjectToBroadPhaseLayer(nint bpInterface, uint objectLayer, byte broadPhaseLayer);

    //  ObjectVsBroadPhaseLayerFilter
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_ObjectVsBroadPhaseLayerFilterMask_Create(nint broadPhaseLayerInterface);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_ObjectVsBroadPhaseLayerFilterTable_Create(nint broadPhaseLayerInterface, uint numBroadPhaseLayers, nint objectLayerPairFilter, uint numObjectLayers);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ObjectVsBroadPhaseLayerFilter_Destroy(nint handle);

    #region JPH_ObjectLayerPairFilter
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_ObjectLayerPairFilterMask_Create();

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern ObjectLayer JPH_ObjectLayerPairFilterMask_GetObjectLayer(uint group, uint mask);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_ObjectLayerPairFilterMask_GetGroup(uint layer);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_ObjectLayerPairFilterMask_GetMask(uint layer);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_ObjectLayerPairFilterTable_Create(uint numObjectLayers);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ObjectLayerPairFilterTable_DisableCollision(nint objectFilter, ObjectLayer layer1, ObjectLayer layer2);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ObjectLayerPairFilterTable_EnableCollision(nint objectFilter, ObjectLayer layer1, ObjectLayer layer2);
    #endregion

    //  BroadPhaseLayerFilter
    public struct JPH_BroadPhaseLayerFilter_Procs
    {
        public delegate* unmanaged<nint, byte, byte> ShouldCollide;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BroadPhaseLayerFilter_SetProcs(in JPH_BroadPhaseLayerFilter_Procs procs);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BroadPhaseLayerFilter_Create(nint userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BroadPhaseLayerFilter_Destroy(nint handle);

    //  ObjectLayerFilter
    public struct JPH_ObjectLayerFilter_Procs
    {
        public delegate* unmanaged<nint, uint, byte> ShouldCollide;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ObjectLayerFilter_SetProcs(in JPH_ObjectLayerFilter_Procs procs);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_ObjectLayerFilter_Create(nint userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ObjectLayerFilter_Destroy(nint handle);

    //  BodyFilter
    public struct JPH_BodyFilter_Procs
    {
        public delegate* unmanaged<nint, uint, byte> ShouldCollide;
        public delegate* unmanaged<nint, nint, byte> ShouldCollideLocked;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyFilter_SetProcs(in JPH_BodyFilter_Procs procs);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyFilter_Create(nint userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyFilter_Destroy(nint handle);

    //  ShapeFilter
    public struct JPH_ShapeFilter_Procs
    {
        public delegate* unmanaged<nint, nint, SubShapeID*, byte> ShouldCollide;
        public delegate* unmanaged<nint, nint, SubShapeID*, nint, SubShapeID*, byte> ShouldCollide2;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ShapeFilter_SetProcs(in JPH_ShapeFilter_Procs procs);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_ShapeFilter_Create(nint userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ShapeFilter_Destroy(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_ShapeFilter_GetBodyID2(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ShapeFilter_SetBodyID2(nint handle, uint id);

    //  SimShapeFilter
    public struct JPH_SimShapeFilter_Procs
    {
        public delegate* unmanaged<nint, nint, nint, SubShapeID*, nint, nint, SubShapeID*, byte> ShouldCollide;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SimShapeFilter_SetProcs(in JPH_SimShapeFilter_Procs procs);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_SimShapeFilter_Create(nint userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SimShapeFilter_Destroy(nint handle);

    //  BodyDrawFilter
    public struct JPH_BodyDrawFilter_Procs
    {
        public delegate* unmanaged<nint, nint, byte> ShouldDraw;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyDrawFilter_SetProcs(in JPH_BodyDrawFilter_Procs procs);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyDrawFilter_Create(nint userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyDrawFilter_Destroy(nint handle);

    //  PhysicsStepListener
    public struct PhysicsStepListenerContextNative
    {
        public float deltaTime;
        public Bool32 isFirstStep;
        public Bool32 isLastStep;
        public nint physicsSystem;
    }

    public struct JPH_PhysicsStepListener_Procs
    {
        public delegate* unmanaged<nint, PhysicsStepListenerContextNative*, void> OnStep;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsStepListener_SetProcs(in JPH_PhysicsStepListener_Procs procs);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_PhysicsStepListener_Create(nint userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsStepListener_Destroy(nint handle);

    #region GroupFilter
    public struct JPH_CollisionGroup
    {
        public nint groupFilter;
        public CollisionGroupID groupID;
        public CollisionSubGroupID subGroupID;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_GroupFilter_Destroy(nint groupFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_GroupFilter_CanCollide(nint groupFilter, JPH_CollisionGroup* group1, JPH_CollisionGroup* group2);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_GroupFilterTable_Create(uint numSubGroups/* = 0*/);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_GroupFilterTable_DisableCollision(nint table, CollisionSubGroupID subGroup1, CollisionSubGroupID subGroup2);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_GroupFilterTable_EnableCollision(nint table, CollisionSubGroupID subGroup1, CollisionSubGroupID subGroup2);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_GroupFilterTable_IsCollisionEnabled(nint table, CollisionSubGroupID subGroup1, CollisionSubGroupID subGroup2);
    #endregion

    #region Shape/ShapeSettings
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ShapeSettings_Destroy(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern ulong JPH_ShapeSettings_GetUserData(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ShapeSettings_SetUserData(nint settings, ulong userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Shape_Destroy(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern ShapeType JPH_Shape_GetType(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern ShapeSubType JPH_Shape_GetSubType(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern ulong JPH_Shape_GetUserData(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Shape_SetUserData(nint shape, ulong userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Shape_MustBeStatic(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Shape_GetCenterOfMass(nint handle, out Vector3 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Shape_GetLocalBounds(nint shape, out BoundingBox box);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_Shape_GetSubShapeIDBitsRecursive(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_Shape_GetInnerRadius(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_Shape_GetVolume(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Shape_IsValidScale(nint handle, in Vector3 scale);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Shape_MakeScaleValid(nint handle, in Vector3 scale, out Vector3 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_Shape_ScaleShape(nint handle, in Vector3 scale);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Shape_GetMassProperties(nint shape, out MassProperties properties);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Shape_GetWorldSpaceBounds(nint shape, in Mat4 centerOfMassTransform, in Vector3 scale, out BoundingBox result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Shape_GetWorldSpaceBounds(nint shape, in RMatrix4x4 centerOfMassTransform, in Vector3 scale, out BoundingBox result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_Shape_GetMaterial(nint shape, SubShapeID subShapeID);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Shape_GetSurfaceNormal(nint shape, SubShapeID subShapeID, in Vector3 localPosition, out Vector3 normal);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Shape_CastRay(nint shape, in Vector3 origin, in Vector3 direction, out RayCastResult hit);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Shape_CastRay2(nint shape, in Vector3 origin, in Vector3 direction, in RayCastSettings settings, CollisionCollectorType collectorType, JPH_CastRayResultCallback callback, nint userData, nint shapeFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Shape_CollidePoint(nint shape, in Vector3 point, nint shapeFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Shape_CollidePoint2(nint shape, in Vector3 point, CollisionCollectorType collectorType, JPH_CollidePointResultCallback callback, nint userData, nint shapeFilter);
    #endregion

    #region ConvexShape
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_ConvexShape_GetDensity(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ConvexShape_SetDensity(nint shape, float value);
    #endregion

    #region BoxShape/BoxShapeSettings
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BoxShapeSettings_Create(in Vector3 halfExtent, float convexRadius);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BoxShapeSettings_CreateShape(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BoxShape_Create(in Vector3 halfExtent, float convexRadius);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BoxShape_GetHalfExtent(nint handle, out Vector3 halfExtent);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_BoxShape_GetConvexRadius(nint handle);
    #endregion

    #region SphereShape/SphereShapeSettings
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_SphereShapeSettings_Create(float radius);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_SphereShapeSettings_CreateShape(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_SphereShapeSettings_GetRadius(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SphereShapeSettings_SetRadius(nint shape, float radius);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_SphereShape_Create(float radius);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_SphereShape_GetRadius(nint shape);
    #endregion

    #region PlaneShape/PlaneShapeSettings
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_PlaneShapeSettings_Create(in Plane plane, nint material, float halfExtent);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_PlaneShapeSettings_CreateShape(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_PlaneShape_Create(in Plane plane, nint material, float halfExtent);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PlaneShape_GetPlane(nint handle, out Plane result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_PlaneShape_GetHalfExtent(nint handle);
    #endregion

    #region TriangleShape/TriangleShapeSettings
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_TriangleShapeSettings_Create(in Vector3 v1, in Vector3 v2, in Vector3 v3, float convexRadius);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_TriangleShapeSettings_CreateShape(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_TriangleShape_Create(in Vector3 v1, in Vector3 v2, in Vector3 v3, float convexRadius);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_TriangleShape_GetConvexRadius(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_TriangleShape_GetVertex1(nint handle, Vector3* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_TriangleShape_GetVertex2(nint handle, Vector3* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_TriangleShape_GetVertex3(nint handle, Vector3* result);
    #endregion

    #region CapsuleShape/CapsuleShapeSettings
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_CapsuleShapeSettings_Create(float halfHeightOfCylinder, float radius);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_CapsuleShapeSettings_CreateShape(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_CapsuleShape_Create(float halfHeightOfCylinder, float radius);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_CapsuleShape_GetRadius(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_CapsuleShape_GetHalfHeightOfCylinder(nint handle);
    #endregion

    #region CylinderShape/CylinderShapeSettings
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_CylinderShapeSettings_Create(float halfHeight, float radius, float convexRadius);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_CylinderShapeSettings_CreateShape(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_CylinderShape_Create(float halfHeight, float radius);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_CylinderShape_GetRadius(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_CylinderShape_GetHalfHeight(nint handle);
    #endregion

    #region TaperedCylinderShape/TaperedCylinderShapeSettings
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_TaperedCylinderShapeSettings_Create(float halfHeightOfTaperedCylinder, float topRadius, float bottomRadius, float convexRadius, nint material);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_TaperedCylinderShapeSettings_CreateShape(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_TaperedCylinderShape_GetTopRadius(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_TaperedCylinderShape_GetBottomRadius(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_TaperedCylinderShape_GetConvexRadius(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_TaperedCylinderShape_GetHalfHeight(nint shape);
    #endregion

    #region ConvexHullShape/ConvexHullShapeSettings
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_ConvexHullShapeSettings_Create(Vector3* points, int pointsCount, float maxConvexRadius);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_ConvexHullShapeSettings_CreateShape(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_ConvexHullShape_GetNumPoints(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ConvexHullShape_GetPoint(nint shape, uint index, Vector3* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_ConvexHullShape_GetNumFaces(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_ConvexHullShape_GetFaceVertices(nint shape, uint faceIndex, uint maxVertices, uint* vertices);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_ConvexHullShape_GetNumVerticesInFace(nint shape, uint faceIndex);

    /* ConvexShape */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_ConvexShapeSettings_GetDensity(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ConvexShapeSettings_SetDensity(nint shape, float value);
    #endregion

    #region MeshShape/MeshShapeSettings
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_MeshShapeSettings_Create(Triangle* triangle, int triangleCount);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_MeshShapeSettings_Create2(Vector3* vertices, int verticesCount, IndexedTriangle* triangles, int triangleCount);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_MeshShapeSettings_GetMaxTrianglesPerLeaf(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MeshShapeSettings_SetMaxTrianglesPerLeaf(nint settings, uint value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MeshShapeSettings_GetActiveEdgeCosThresholdAngle(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MeshShapeSettings_SetActiveEdgeCosThresholdAngle(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_MeshShapeSettings_GetPerTriangleUserData(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MeshShapeSettings_SetPerTriangleUserData(nint settings, [MarshalAs(UnmanagedType.U1)] bool value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern MeshShapeBuildQuality JPH_MeshShapeSettings_GetBuildQuality(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MeshShapeSettings_SetBuildQuality(nint settings, MeshShapeBuildQuality value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MeshShapeSettings_Sanitize(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_MeshShapeSettings_CreateShape(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_MeshShape_GetTriangleUserData(nint shape, SubShapeID id);
    #endregion

    #region HeightFieldShape/HeightFieldShapeSettings
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_HeightFieldShapeSettings_Create(float* samples, in Vector3 offset, in Vector3 scale, uint sampleCount, byte* materialIndices = default);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HeightFieldShapeSettings_DetermineMinAndMaxSample(nint settings, out float outMinValue, out float outMaxValue, out float outQuantizationScale);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_HeightFieldShapeSettings_CalculateBitsPerSampleForError(nint settings, float maxError);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HeightFieldShapeSettings_GetOffset(nint shape, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HeightFieldShapeSettings_SetOffset(nint settings, in Vector3 value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HeightFieldShapeSettings_GetScale(nint shape, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HeightFieldShapeSettings_SetScale(nint settings, in Vector3 value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_HeightFieldShapeSettings_GetSampleCount(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HeightFieldShapeSettings_SetSampleCount(nint settings, uint value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_HeightFieldShapeSettings_GetMinHeightValue(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HeightFieldShapeSettings_SetMinHeightValue(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_HeightFieldShapeSettings_GetMaxHeightValue(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HeightFieldShapeSettings_SetMaxHeightValue(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_HeightFieldShapeSettings_GetBlockSize(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HeightFieldShapeSettings_SetBlockSize(nint settings, uint value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_HeightFieldShapeSettings_GetBitsPerSample(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HeightFieldShapeSettings_SetBitsPerSample(nint settings, uint value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_HeightFieldShapeSettings_GetActiveEdgeCosThresholdAngle(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HeightFieldShapeSettings_SetActiveEdgeCosThresholdAngle(nint settings, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_HeightFieldShapeSettings_CreateShape(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_HeightFieldShape_GetSampleCount(nint shape);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_HeightFieldShape_GetBlockSize(nint shape);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_HeightFieldShape_GetMaterial(nint shape, uint x, uint y);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HeightFieldShape_GetPosition(nint shape, uint x, uint y, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_HeightFieldShape_IsNoCollision(nint shape, uint x, uint y);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_HeightFieldShape_ProjectOntoSurface(nint shape, in Vector3 localPosition, out Vector3 surfacePosition, out SubShapeID subShapeID);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_HeightFieldShape_GetMinHeightValue(nint shape);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_HeightFieldShape_GetMaxHeightValue(nint shape);
    #endregion

    #region TaperedCapsuleShape/TaperedCapsuleShapeSettings
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_TaperedCapsuleShapeSettings_Create(float halfHeightOfTaperedCylinder, float topRadius, float bottomRadius);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_TaperedCapsuleShapeSettings_CreateShape(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_TaperedCapsuleShape_GetTopRadius(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_TaperedCapsuleShape_GetBottomRadius(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_TaperedCapsuleShape_GetHalfHeight(nint shape);
    #endregion

    /* CompoundShape */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CompoundShapeSettings_AddShape(nint handle, Vector3* position, Quaternion* rotation, nint shapeSettings, uint userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CompoundShapeSettings_AddShape2(nint handle, Vector3* position, Quaternion* rotation, nint shape, uint userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_CompoundShape_GetNumSubShapes(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_StaticCompoundShapeSettings_Create();

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_StaticCompoundShape_Create(nint settings);

    /* MutableCompoundShape */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_MutableCompoundShapeSettings_Create();

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_MutableCompoundShape_Create(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_MutableCompoundShape_AddShape(nint shape, in Vector3 position, in Quaternion rotation, /*const JPH_Shape**/nint child, uint userData, uint index);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MutableCompoundShape_RemoveShape(nint shape, uint index);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MutableCompoundShape_ModifyShape(nint shape, uint index, in Vector3 position, in Quaternion rotation);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MutableCompoundShape_ModifyShape2(nint shape, uint index, in Vector3 position, in Quaternion rotation, /*const JPH_Shape**/nint newShape);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MutableCompoundShape_AdjustCenterOfMass(nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_DecoratedShape_GetInnerShape(nint settings);

    /* RotatedTranslatedShape */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_RotatedTranslatedShapeSettings_Create(in Vector3 position, in Quaternion rotation, nint shapeSettings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_RotatedTranslatedShapeSettings_Create2(in Vector3 position, in Quaternion rotation, nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_RotatedTranslatedShapeSettings_CreateShape(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_RotatedTranslatedShape_Create(in Vector3 position, in Quaternion rotation, nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_RotatedTranslatedShape_GetPosition(nint shape, out Vector3 position);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_RotatedTranslatedShape_GetRotation(nint shape, out Quaternion rotation);

    /* ScaledShape */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_ScaledShapeSettings_Create(nint shapeSettings, in Vector3 scale);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_ScaledShapeSettings_Create2(nint shape, in Vector3 scale);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_ScaledShapeSettings_CreateShape(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_ScaledShape_Create(nint shape, in Vector3 scale);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ScaledShape_GetScale(nint shape, out Vector3 result);

    /* JPH_OffsetCenterOfMassShape */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_OffsetCenterOfMassShapeSettings_Create(in Vector3 offset, nint shapeSettings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_OffsetCenterOfMassShapeSettings_Create2(in Vector3 offset, nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_OffsetCenterOfMassShapeSettings_CreateShape(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_OffsetCenterOfMassShape_Create(in Vector3 offset, nint shape);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_OffsetCenterOfMassShape_GetOffset(nint handle, out Vector3 offset);

    #region EmptyShape/EmptyShapeSettings
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_EmptyShapeSettings_Create(in Vector3 centerOfMass);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_EmptyShapeSettings_CreateShape(nint settings);
    #endregion

    /* BodyCreationSettings */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyCreationSettings_Create();

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", EntryPoint = "JPH_BodyCreationSettings_Create2", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", EntryPoint = "JPH_BodyCreationSettings_Create2", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyCreationSettings_Create2(nint shapeSettings, Vector3* position, Quaternion* rotation, MotionType motionType, JPH_ObjectLayer objectLayer);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", EntryPoint = "JPH_BodyCreationSettings_Create2", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", EntryPoint = "JPH_BodyCreationSettings_Create2", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyCreationSettings_Create2Double(nint shapeSettings, RVector3* position, Quaternion* rotation, MotionType motionType, JPH_ObjectLayer objectLayer);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", EntryPoint = "JPH_BodyCreationSettings_Create3", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", EntryPoint = "JPH_BodyCreationSettings_Create3", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyCreationSettings_Create3(nint shape, Vector3* position, Quaternion* rotation, MotionType motionType, JPH_ObjectLayer objectLayer);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", EntryPoint = "JPH_BodyCreationSettings_Create3", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", EntryPoint = "JPH_BodyCreationSettings_Create3", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyCreationSettings_Create3Double(nint shape, RVector3* position, Quaternion* rotation, MotionType motionType, JPH_ObjectLayer objectLayer);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_Destroy(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_GetPosition(nint settings, Vector3* velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetPosition(nint settings, Vector3* velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_GetRotation(nint settings, Quaternion* velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetRotation(nint settings, Quaternion* velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_GetLinearVelocity(nint settings, Vector3* velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetLinearVelocity(nint settings, Vector3* velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_GetAngularVelocity(nint settings, Vector3* velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetAngularVelocity(nint settings, Vector3* velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern ulong JPH_BodyCreationSettings_GetUserData(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetUserData(nint settings, ulong value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern ObjectLayer JPH_BodyCreationSettings_GetObjectLayer(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetObjectLayer(nint settings, in ObjectLayer value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_GetCollisionGroup(nint settings, out JPH_CollisionGroup result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetCollisionGroup(nint settings, in JPH_CollisionGroup value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern MotionType JPH_BodyCreationSettings_GetMotionType(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetMotionType(nint settings, MotionType value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern AllowedDOFs JPH_BodyCreationSettings_GetAllowedDOFs(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetAllowedDOFs(nint settings, AllowedDOFs value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BodyCreationSettings_GetAllowDynamicOrKinematic(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetAllowDynamicOrKinematic(nint settings, [MarshalAs(UnmanagedType.U1)] bool value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BodyCreationSettings_GetIsSensor(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetIsSensor(nint settings, [MarshalAs(UnmanagedType.U1)] bool value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BodyCreationSettings_GetCollideKinematicVsNonDynamic(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetCollideKinematicVsNonDynamic(nint settings, [MarshalAs(UnmanagedType.U1)] bool value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BodyCreationSettings_GetUseManifoldReduction(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetUseManifoldReduction(nint settings, [MarshalAs(UnmanagedType.U1)] bool value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BodyCreationSettings_GetApplyGyroscopicForce(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetApplyGyroscopicForce(nint settings, [MarshalAs(UnmanagedType.U1)] bool value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern MotionQuality JPH_BodyCreationSettings_GetMotionQuality(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetMotionQuality(nint settings, in MotionQuality value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BodyCreationSettings_GetEnhancedInternalEdgeRemoval(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetEnhancedInternalEdgeRemoval(nint settings, [MarshalAs(UnmanagedType.U1)] bool value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BodyCreationSettings_GetAllowSleeping(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetAllowSleeping(nint settings, [MarshalAs(UnmanagedType.U1)] bool value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_BodyCreationSettings_GetFriction(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetFriction(nint settings, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_BodyCreationSettings_GetRestitution(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetRestitution(nint settings, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_BodyCreationSettings_GetLinearDamping(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetLinearDamping(nint settings, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_BodyCreationSettings_GetAngularDamping(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetAngularDamping(nint settings, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_BodyCreationSettings_GetMaxLinearVelocity(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetMaxLinearVelocity(nint settings, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_BodyCreationSettings_GetMaxAngularVelocity(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetMaxAngularVelocity(nint settings, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_BodyCreationSettings_GetGravityFactor(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetGravityFactor(nint settings, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_BodyCreationSettings_GetNumVelocityStepsOverride(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetNumVelocityStepsOverride(nint settings, uint value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_BodyCreationSettings_GetNumPositionStepsOverride(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetNumPositionStepsOverride(nint settings, uint value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern OverrideMassProperties JPH_BodyCreationSettings_GetOverrideMassProperties(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetOverrideMassProperties(nint settings, OverrideMassProperties value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_BodyCreationSettings_GetInertiaMultiplier(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetInertiaMultiplier(nint settings, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_GetMassPropertiesOverride(nint settings, out MassProperties result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyCreationSettings_SetMassPropertiesOverride(nint settings, MassProperties* massProperties);

    /* SoftBodyCreationSettings */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_SoftBodyCreationSettings_Create();

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SoftBodyCreationSettings_Destroy(nint settings);

    #region JPH_Constraint
    public struct JPH_ConstraintSettings
    {
        public Bool8 enabled;
        public uint constraintPriority;
        public uint numVelocityStepsOverride;
        public uint numPositionStepsOverride;
        public float drawConstraintSize;
        public ulong userData;
    }

    public struct JPH_FixedConstraintSettings
    {
        public JPH_ConstraintSettings baseSettings;    /* Inherics JPH_ConstraintSettings */

        public ConstraintSpace space;
        public Bool8 autoDetectPoint;
        public Vector3 point1; /* JPH_RVec3 */
        public Vector3 axisX1;
        public Vector3 axisY1;
        public Vector3 point2; /* JPH_RVec3 */
        public Vector3 axisX2;
        public Vector3 axisY2;
    }

    public struct JPH_DistanceConstraintSettings
    {
        public JPH_ConstraintSettings baseSettings;    /* Inherics JPH_ConstraintSettings */

        public ConstraintSpace space;
        public Vector3 point1;                /* JPH_RVec3 */
        public Vector3 point2;                /* JPH_RVec3 */
        public float minDistance;
        public float maxDistance;
        public SpringSettings limitsSpringSettings;
    }

    public struct JPH_PointConstraintSettings
    {
        public JPH_ConstraintSettings baseSettings;    /* Inherics JPH_ConstraintSettings */

        public ConstraintSpace space;
        public Vector3 point1;                /* JPH_RVec3 */
        public Vector3 point2;                /* JPH_RVec3 */
    }

    public struct JPH_HingeConstraintSettings
    {
        public JPH_ConstraintSettings baseSettings;    /* Inherics JPH_ConstraintSettings */

        public ConstraintSpace space;
        public Vector3 point1;                /* JPH_RVec3 */
        public Vector3 hingeAxis1;
        public Vector3 normalAxis1;
        public Vector3 point2;                /* JPH_RVec3 */
        public Vector3 hingeAxis2;
        public Vector3 normalAxis2;
        public float limitsMin;
        public float limitsMax;
        public SpringSettings limitsSpringSettings;
        public float maxFrictionTorque;
        public MotorSettings motorSettings;
    }

    public struct JPH_SliderConstraintSettings
    {
        public JPH_ConstraintSettings baseSettings;    /* Inherics JPH_ConstraintSettings */

        public ConstraintSpace space;
        public Bool8 autoDetectPoint;
        public Vector3 point1;                /* JPH_RVec3 */
        public Vector3 sliderAxis1;
        public Vector3 normalAxis1;
        public Vector3 point2;                /* JPH_RVec3 */
        public Vector3 sliderAxis2;
        public Vector3 normalAxis2;
        public float limitsMin;
        public float limitsMax;
        public SpringSettings limitsSpringSettings;
        public float maxFrictionForce;
        public MotorSettings motorSettings;
    }

    public struct JPH_ConeConstraintSettings
    {
        public JPH_ConstraintSettings baseSettings;    /* Inherics JPH_ConstraintSettings */

        public ConstraintSpace space;
        public Vector3 point1;                /* JPH_RVec3 */
        public Vector3 twistAxis1;
        public Vector3 point2;                /* JPH_RVec3 */
        public Vector3 twistAxis2;
        public float halfConeAngle;
    }

    public struct JPH_SwingTwistConstraintSettings
    {
        public JPH_ConstraintSettings baseSettings;    /* Inherics JPH_ConstraintSettings */

        public ConstraintSpace space;
        public Vector3 position1;             /* JPH_RVec3 */
        public Vector3 twistAxis1;
        public Vector3 planeAxis1;
        public Vector3 position2;             /* JPH_RVec3 */
        public Vector3 twistAxis2;
        public Vector3 planeAxis2;
        public SwingType swingType;
        public float normalHalfConeAngle;
        public float planeHalfConeAngle;
        public float twistMinAngle;
        public float twistMaxAngle;
        public float maxFrictionTorque;
        public MotorSettings swingMotorSettings;
        public MotorSettings twistMotorSettings;
    }

    public struct JPH_SixDOFConstraintSettings
    {
        public JPH_ConstraintSettings baseSettings;    /* Inherics JPH_ConstraintSettings */

        public ConstraintSpace space;
        public Vector3 position1;             /* JPH_RVec3 */
        public Vector3 axisX1;
        public Vector3 axisY1;
        public Vector3 position2;             /* JPH_RVec3 */
        public Vector3 axisX2;
        public Vector3 axisY2;
        public fixed float maxFriction[(int)SixDOFConstraintAxis.Count];
        public SwingType swingType;
        public fixed float limitMin[(int)SixDOFConstraintAxis.Count];
        public fixed float limitMax[(int)SixDOFConstraintAxis.Count];

        public limitsSpringSettings__FixedBuffer limitsSpringSettings;
        public motorSettings__FixedBuffer motorSettings;

        [InlineArray((int)SixDOFConstraintAxis.NumTranslation)]
        public partial struct limitsSpringSettings__FixedBuffer
        {
            public SpringSettings e0;
        }

        [InlineArray((int)SixDOFConstraintAxis.Count)]
        public partial struct motorSettings__FixedBuffer
        {
            public MotorSettings e0;
        }
    }

    public struct JPH_GearConstraintSettings
    {
        public JPH_ConstraintSettings baseSettings;    /* Inherics JPH_ConstraintSettings */

        public ConstraintSpace space;
        public Vector3 hingeAxis1;
        public Vector3 hingeAxis2;
        public float ratio;
    }

    /* JPH_Constraint */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Constraint_Destroy(nint constraint);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern ConstraintType JPH_Constraint_GetType(nint constraint);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern ConstraintSubType JPH_Constraint_GetSubType(nint constraint);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_Constraint_GetConstraintPriority(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Constraint_SetConstraintPriority(nint handle, uint value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_Constraint_GetNumVelocityStepsOverride(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Constraint_SetNumVelocityStepsOverride(nint constraint, uint value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_Constraint_GetNumPositionStepsOverride(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Constraint_SetNumPositionStepsOverride(nint constraint, uint value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Constraint_GetEnabled(nint constraint);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Constraint_SetEnabled(nint constraint, [MarshalAs(UnmanagedType.U1)] bool value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern ulong JPH_Constraint_GetUserData(nint constraint);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Constraint_SetUserData(nint constraint, ulong value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Constraint_NotifyShapeChanged(nint constraint, uint bodyID, in Vector3 deltaCOM);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Constraint_ResetWarmStart(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Constraint_IsActive(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Constraint_SetupVelocityConstraint(nint constraint, float deltaTime);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Constraint_WarmStartVelocityConstraint(nint constraint, float warmStartImpulseRatio);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Constraint_SolveVelocityConstraint(nint constraint, float deltaTime);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Constraint_SolvePositionConstraint(nint constraint, float deltaTime, float baumgarte);
    #endregion

    #region JPH_TwoBodyConstraint
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_TwoBodyConstraint_GetBody1(nint constraint);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_TwoBodyConstraint_GetBody2(nint constraint);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_TwoBodyConstraint_GetConstraintToBody1Matrix(nint constraint, Mat4* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_TwoBodyConstraint_GetConstraintToBody2Matrix(nint constraint, Mat4* result);
    #endregion

    #region JPH_FixedConstraint
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_FixedConstraintSettings_Init(JPH_FixedConstraintSettings* settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_FixedConstraint_Create(JPH_FixedConstraintSettings* settings, nint body1, nint body2);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_FixedConstraint_GetSettings(nint constraint, out JPH_FixedConstraintSettings settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_FixedConstraint_GetTotalLambdaPosition(nint handle, Vector3* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_FixedConstraint_GetTotalLambdaRotation(nint handle, Vector3* result);
    #endregion

    #region JPH_DistanceConstraint
    /* JPH_DistanceConstraint */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DistanceConstraintSettings_Init(JPH_DistanceConstraintSettings* settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_DistanceConstraint_Create(JPH_DistanceConstraintSettings* settings, nint body1, nint body2);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DistanceConstraint_GetSettings(nint constraint, out JPH_DistanceConstraintSettings settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_DistanceConstraint_SetDistance(nint constraint, float minDistance, float maxDistance);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_DistanceConstraint_GetMinDistance(nint constraint);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_DistanceConstraint_GetMaxDistance(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DistanceConstraint_GetLimitsSpringSettings(nint constraint, SpringSettings* result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DistanceConstraint_SetLimitsSpringSettings(nint constraint, SpringSettings* settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_DistanceConstraint_GetTotalLambdaPosition(nint constraint);
    #endregion

    #region JPH_PointConstraint
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PointConstraintSettings_Init(JPH_PointConstraintSettings* settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_PointConstraint_Create(JPH_PointConstraintSettings* settings, nint body1, nint body2);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PointConstraint_GetSettings(nint constraint, out JPH_PointConstraintSettings settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PointConstraint_SetPoint1(nint handle, ConstraintSpace space, Vector3* value); // RVec3

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PointConstraint_SetPoint2(nint handle, ConstraintSpace space, Vector3* value); // RVec3

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PointConstraint_GetTotalLambdaPosition(nint handle, out Vector3 result);
    #endregion

    #region JPH_HingeConstraint
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraintSettings_Init(JPH_HingeConstraintSettings* settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_HingeConstraint_Create(JPH_HingeConstraintSettings* settings, nint body1, nint body2);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_GetSettings(nint constraint, out JPH_HingeConstraintSettings settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_GetLocalSpacePoint1(nint constraint, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_GetLocalSpacePoint2(nint constraint, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_GetLocalSpaceHingeAxis1(nint constraint, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_GetLocalSpaceHingeAxis2(nint constraint, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_GetLocalSpaceNormalAxis1(nint constraint, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_GetLocalSpaceNormalAxis2(nint constraint, out Vector3 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_HingeConstraint_GetCurrentAngle(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_SetMaxFrictionTorque(nint constraint, float frictionTorque);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_HingeConstraint_GetMaxFrictionTorque(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_SetMotorSettings(nint constraint, MotorSettings* settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_GetMotorSettings(nint constraint, out MotorSettings result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_SetMotorState(nint constraint, MotorState state);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern MotorState JPH_HingeConstraint_GetMotorState(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_SetTargetAngularVelocity(nint constraint, float angularVelocity);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_HingeConstraint_GetTargetAngularVelocity(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_SetTargetAngle(nint constraint, float angle);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_HingeConstraint_GetTargetAngle(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_SetLimits(nint constraint, float inLimitsMin, float inLimitsMax);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_HingeConstraint_GetLimitsMin(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_HingeConstraint_GetLimitsMax(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_HingeConstraint_HasLimits(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_GetLimitsSpringSettings(nint constraint, out SpringSettings result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_SetLimitsSpringSettings(nint constraint, SpringSettings* settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_GetTotalLambdaPosition(nint constraint, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_HingeConstraint_GetTotalLambdaRotation(nint constraint, out Vector2 rotation);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_HingeConstraint_GetTotalLambdaRotationLimits(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_HingeConstraint_GetTotalLambdaMotor(nint constraint);
    #endregion

    #region JPH_SliderConstraint
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SliderConstraintSettings_Init(JPH_SliderConstraintSettings* settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SliderConstraintSettings_SetSliderAxis(JPH_SliderConstraintSettings* settings, in Vector3 value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_SliderConstraint_Create(JPH_SliderConstraintSettings* settings, nint body1, nint body2);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SliderConstraint_GetSettings(nint constraint, out JPH_SliderConstraintSettings settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_SliderConstraint_GetCurrentPosition(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SliderConstraint_SetMaxFrictionForce(nint constraint, float frictionForce);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_SliderConstraint_GetMaxFrictionForce(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SliderConstraint_SetMotorSettings(nint constraint, MotorSettings* settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SliderConstraint_GetMotorSettings(nint constraint, out MotorSettings result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SliderConstraint_SetMotorState(nint constraint, MotorState state);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern MotorState JPH_SliderConstraint_GetMotorState(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SliderConstraint_SetTargetVelocity(nint constraint, float velocity);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_SliderConstraint_GetTargetVelocity(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SliderConstraint_SetTargetPosition(nint constraint, float position);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_SliderConstraint_GetTargetPosition(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SliderConstraint_SetLimits(nint constraint, float inLimitsMin, float inLimitsMax);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_SliderConstraint_GetLimitsMin(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_SliderConstraint_GetLimitsMax(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_SliderConstraint_HasLimits(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SliderConstraint_GetLimitsSpringSettings(nint constraint, out SpringSettings result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SliderConstraint_SetLimitsSpringSettings(nint constraint, SpringSettings* settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SliderConstraint_GetTotalLambdaPosition(nint constraint, out Vector2 position);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_SliderConstraint_GetTotalLambdaPositionLimits(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SliderConstraint_GetTotalLambdaRotation(nint constraint, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_SliderConstraint_GetTotalLambdaMotor(nint constraint);
    #endregion

    #region JPH_ConeConstraint
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ConeConstraintSettings_Init(JPH_ConeConstraintSettings* settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_ConeConstraint_Create(JPH_ConeConstraintSettings* settings, nint body1, nint body2);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ConeConstraint_GetSettings(nint constraint, out JPH_ConeConstraintSettings settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ConeConstraint_SetHalfConeAngle(nint constraint, float halfConeAngle);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_ConeConstraint_GetCosHalfConeAngle(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ConeConstraint_GetTotalLambdaPosition(nint constraint, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_ConeConstraint_GetTotalLambdaRotation(nint constraint);
    #endregion

    #region JPH_SwingTwistConstraint
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SwingTwistConstraintSettings_Init(JPH_SwingTwistConstraintSettings* settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_SwingTwistConstraint_Create(JPH_SwingTwistConstraintSettings* settings, nint body1, nint body2);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SwingTwistConstraint_GetSettings(nint constraint, out JPH_SwingTwistConstraintSettings settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_SwingTwistConstraint_GetNormalHalfConeAngle(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SwingTwistConstraint_GetTotalLambdaPosition(nint constraint, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_SwingTwistConstraint_GetTotalLambdaTwist(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_SwingTwistConstraint_GetTotalLambdaSwingY(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_SwingTwistConstraint_GetTotalLambdaSwingZ(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SwingTwistConstraint_GetTotalLambdaMotor(nint constraint, out Vector3 result);

    #endregion

    #region JPH_SixDOFConstraint
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SixDOFConstraintSettings_Init(JPH_SixDOFConstraintSettings* settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SixDOFConstraintSettings_MakeFreeAxis(JPH_SixDOFConstraintSettings* settings, SixDOFConstraintAxis axis);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_SixDOFConstraintSettings_IsFreeAxis(JPH_SixDOFConstraintSettings* settings, SixDOFConstraintAxis axis);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SixDOFConstraintSettings_MakeFixedAxis(JPH_SixDOFConstraintSettings* settings, SixDOFConstraintAxis axis);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_SixDOFConstraintSettings_IsFixedAxis(JPH_SixDOFConstraintSettings* settings, SixDOFConstraintAxis axis);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SixDOFConstraintSettings_SetLimitedAxis(JPH_SixDOFConstraintSettings* settings, SixDOFConstraintAxis axis, float min, float max);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_SixDOFConstraint_Create(JPH_SixDOFConstraintSettings* settings, nint body1, nint body2);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SixDOFConstraint_GetSettings(nint constraint, out JPH_SixDOFConstraintSettings settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_SixDOFConstraint_GetLimitsMin(nint handle, uint axis);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_SixDOFConstraint_GetLimitsMax(nint handle, uint axis);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SixDOFConstraint_GetTotalLambdaPosition(nint constraint, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SixDOFConstraint_GetTotalLambdaRotation(nint constraint, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SixDOFConstraint_GetTotalLambdaMotorTranslation(nint constraint, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_SixDOFConstraint_GetTotalLambdaMotorRotation(nint constraint, out Vector3 result);
    #endregion

    #region JPH_GearConstraint
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_GearConstraintSettings_Init(JPH_GearConstraintSettings* settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_GearConstraint_Create(JPH_GearConstraintSettings* settings, nint body1, nint body2);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_GearConstraint_GetSettings(nint constraint, out JPH_GearConstraintSettings settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_GearConstraint_SetConstraints(nint handle, nint gear1, nint gear2);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_GearConstraint_GetTotalLambda(nint handle);
    #endregion

    /* PhysicsSystem */
    [StructLayout(LayoutKind.Sequential)]
    public struct NativePhysicsSystemSettings
    {
        public int maxBodies; /* 10240 */
        public int numBodyMutexes; /* 0 */
        public int maxBodyPairs; /* 65536 */
        public int maxContactConstraints; /* 10240 */
        private int _padding;
        public /*BroadPhaseLayerInterfaceTable*/ nint broadPhaseLayerInterface;
        public /*ObjectLayerPairFilterTable**/ nint objectLayerPairFilter;
        public /*ObjectVsBroadPhaseLayerFilterTable* */nint objectVsBroadPhaseLayerFilter;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_PhysicsSystem_Create(NativePhysicsSystemSettings* settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_Destroy(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_GetPhysicsSettings(nint handle, PhysicsSettings* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_SetPhysicsSettings(nint handle, PhysicsSettings* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_OptimizeBroadPhase(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern PhysicsUpdateError JPH_PhysicsSystem_Update(nint handle, float deltaTime, int collisionSteps, nint jobSystem);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_SetContactListener(nint system, nint listener);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_SetBodyActivationListener(nint system, nint listener);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_SetSimShapeFilter(nint system, nint shapeFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_PhysicsSystem_GetNumBodies(nint system);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_PhysicsSystem_GetNumActiveBodies(nint system, BodyType type);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_PhysicsSystem_GetMaxBodies(nint system);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_PhysicsSystem_GetNumConstraints(nint system);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_GetGravity(nint handle, out Vector3 velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_SetGravity(nint handle, in Vector3 velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_AddConstraint(nint handle, nint constraint);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_RemoveConstraint(nint handle, nint constraint);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_AddConstraints(nint handle, nint* constraints, uint count);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_RemoveConstraints(nint handle, nint* constraints, uint count);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_AddStepListener(nint handle, nint listener);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_RemoveStepListener(nint handle, nint listener);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_GetBodies(nint handle, BodyID* ids, uint count);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_GetConstraints(nint handle, nint* constraints, uint count);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_ActivateBodiesInAABox(nint system, in BoundingBox box, JPH_ObjectLayer layer);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_PhysicsSystem_WereBodiesInContact(nint system, uint inBody1ID, uint inBody2ID);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_DrawBodies(nint system, DrawSettings* settings, nint renderer, nint bodyFilter);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_DrawConstraints(nint system, nint renderer);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_DrawConstraintLimits(nint system, nint renderer);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsSystem_DrawConstraintReferenceFrame(nint system, nint renderer);

    /* Material */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_PhysicsMaterial_Create(string name, uint color);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_PhysicsMaterial_Destroy(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern string? JPH_PhysicsMaterial_GetDebugName(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_PhysicsMaterial_GetDebugColor(nint handle);

    /* BodyInterface */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_PhysicsSystem_GetBodyInterface(nint system);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_PhysicsSystem_GetBodyInterfaceNoLock(nint system);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyInterface_CreateBody(nint handle, nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyInterface_CreateSoftBody(nint handle, nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_BodyInterface_CreateAndAddBody(nint handle, nint bodyID, Activation activation);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyInterface_CreateBodyWithID(nint handle, uint bodyID, nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyInterface_CreateBodyWithoutID(nint handle, nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_DestroyBody(nint handle, uint bodyID);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_DestroyBodyWithoutID(nint handle, nint body);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BodyInterface_AssignBodyID(nint handle, nint body);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BodyInterface_AssignBodyID2(nint handle, nint body, uint bodyID);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyInterface_UnassignBodyID(nint handle, uint bodyID);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_AddBody(nint handle, uint bodyID, Activation activationMode);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_RemoveBody(nint handle, uint bodyID);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_RemoveAndDestroyBody(nint handle, uint bodyID);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_GetLinearVelocity(nint handle, uint bodyID, Vector3* velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetLinearVelocity(nint handle, uint bodyID, Vector3* velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", EntryPoint = "JPH_BodyInterface_GetCenterOfMassPosition", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", EntryPoint = "JPH_BodyInterface_GetCenterOfMassPosition", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_GetCenterOfMassPosition(nint handle, uint bodyID, Vector3* position);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", EntryPoint = "JPH_BodyInterface_GetCenterOfMassPosition", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", EntryPoint = "JPH_BodyInterface_GetCenterOfMassPosition", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_GetCenterOfMassPositionDouble(nint handle, uint bodyID, RVector3* position);


#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BodyInterface_IsAdded(nint handle, uint bodyID);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern BodyType JPH_BodyInterface_GetBodyType(nint handle, uint bodyID);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern MotionType JPH_BodyInterface_GetMotionType(nint handle, uint bodyID);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetMotionType(nint handle, uint bodyID, MotionType motionType, Activation activationMode);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern MotionQuality JPH_BodyInterface_GetMotionQuality(nint handle, uint bodyID);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetMotionQuality(nint handle, uint bodyID, MotionQuality quality);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_BodyInterface_GetRestitution(nint handle, uint bodyID);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetRestitution(nint handle, uint bodyID, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_BodyInterface_GetFriction(nint handle, uint bodyID);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetFriction(nint handle, uint bodyID, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", EntryPoint = "JPH_BodyInterface_SetPosition", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", EntryPoint = "JPH_BodyInterface_SetPosition", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetPosition(nint handle, uint bodyId, in Vector3 position, Activation activationMode);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", EntryPoint = "JPH_BodyInterface_SetPosition", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", EntryPoint = "JPH_BodyInterface_SetPosition", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetPositionDouble(nint handle, uint bodyId, in RVector3 position, Activation activationMode);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", EntryPoint = "JPH_BodyInterface_GetPosition", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", EntryPoint = "JPH_BodyInterface_GetPosition", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_GetPosition(nint handle, uint bodyId, out Vector3 position);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", EntryPoint = "JPH_BodyInterface_GetPosition", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", EntryPoint = "JPH_BodyInterface_GetPosition", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_GetPositionDouble(nint handle, uint bodyId, out RVector3 position);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetRotation(nint handle, uint bodyId, in Quaternion rotation, Activation activationMode);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_GetRotation(nint handle, uint bodyId, out Quaternion rotation);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetPositionAndRotation(nint handle, uint bodyID, in Vector3 position, in Quaternion rotation, Activation activationMode);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetPositionAndRotation(nint handle, uint bodyID, in RVector3 position, in Quaternion rotation, Activation activationMode);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetPositionAndRotationWhenChanged(nint handle, uint bodyID, in Vector3 position, in Quaternion rotation, Activation activationMode);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetPositionAndRotationWhenChanged(nint handle, uint bodyID, in RVector3 position, in Quaternion rotation, Activation activationMode);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetPositionRotationAndVelocity(nint handle, uint bodyID, in Vector3 position, in Quaternion rotation, in Vector3 linearVelocity, in Vector3 angularVelocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetPositionRotationAndVelocity(nint handle, uint bodyID, in RVector3 position, in Quaternion rotation, in Vector3 linearVelocity, in Vector3 angularVelocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_GetCollisionGroup(nint handle, uint bodyId, out JPH_CollisionGroup result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetCollisionGroup(nint handle, uint bodyId, in JPH_CollisionGroup* group);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyInterface_GetShape(nint handle, uint bodyId);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetShape(nint handle, uint bodyId, nint shape, [MarshalAs(UnmanagedType.U1)] bool updateMassProperties, Activation activationMode);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_NotifyShapeChanged(nint handle, uint bodyId, in Vector3 previousCenterOfMass, [MarshalAs(UnmanagedType.U1)] bool updateMassProperties, Activation activationMode);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_ActivateBody(nint handle, uint bodyId);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_ActivateBodies(nint handle, uint* bodyIDs, uint count);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyInterface_ActivateBodiesInAABox(nint handle, in BoundingBox box, nint broadPhaseLayerFilter, nint objectLayerFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_DeactivateBody(nint handle, uint bodyId);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_DeactivateBodies(nint handle, uint* bodyIDs, uint count);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BodyInterface_IsActive(nint handle, uint bodyID);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_ResetSleepTimer(nint bodyInterface, uint bodyID);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetObjectLayer(nint handle, uint bodyId, JPH_ObjectLayer layer);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern JPH_ObjectLayer JPH_BodyInterface_GetObjectLayer(nint handle, uint bodyId);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", EntryPoint = "JPH_BodyInterface_GetWorldTransform", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", EntryPoint = "JPH_BodyInterface_GetWorldTransform", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_GetWorldTransform(nint handle, uint bodyId, Mat4* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", EntryPoint = "JPH_BodyInterface_GetWorldTransform", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", EntryPoint = "JPH_BodyInterface_GetWorldTransform", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_GetWorldTransformDouble(nint handle, uint bodyId, RMatrix4x4* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", EntryPoint = "JPH_BodyInterface_GetCenterOfMassTransform", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", EntryPoint = "JPH_BodyInterface_GetCenterOfMassTransform", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_GetCenterOfMassTransform(nint handle, uint bodyId, Mat4* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", EntryPoint = "JPH_BodyInterface_GetCenterOfMassTransform", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", EntryPoint = "JPH_BodyInterface_GetCenterOfMassTransform", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_GetCenterOfMassTransformDouble(nint handle, uint bodyId, RMatrix4x4* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_MoveKinematic(nint handle, uint bodyId, in Vector3 targetPosition, in Quaternion targetRotation, float deltaTime);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_MoveKinematic(nint handle, uint bodyId, in RVector3 targetPosition, in Quaternion targetRotation, float deltaTime);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BodyInterface_ApplyBuoyancyImpulse(nint handle, in BodyID bodyId, in Vector3 surfacePosition, in Vector3 surfaceNormal, float buoyancy, float linearDrag, float angularDrag, in Vector3 fluidVelocity, in Vector3 gravity, float deltaTime);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BodyInterface_ApplyBuoyancyImpulse(nint handle, in BodyID bodyId, in RVector3 surfacePosition, in Vector3 surfaceNormal, float buoyancy, float linearDrag, float angularDrag, in Vector3 fluidVelocity, in Vector3 gravity, float deltaTime);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetLinearAndAngularVelocity(nint handle, uint bodyId, in Vector3 linearVelocity, in Vector3 angularVelocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_GetLinearAndAngularVelocity(nint handle, uint bodyId, out Vector3 linearVelocity, out Vector3 angularVelocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_AddLinearVelocity(nint handle, uint bodyId, in Vector3 linearVelocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_AddLinearAndAngularVelocity(nint handle, uint bodyId, in Vector3 linearVelocity, in Vector3 angularVelocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetAngularVelocity(nint handle, uint bodyId, in Vector3 angularVelocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_GetAngularVelocity(nint handle, uint bodyId, out Vector3 angularVelocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_GetPointVelocity(nint handle, uint bodyId, in /*RVec3*/Vector3* point, Vector3* velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_AddForce(nint handle, uint bodyId, in Vector3 force);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_AddForce2(nint handle, uint bodyId, in Vector3 force, in /*RVec3*/Vector3 point);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_AddTorque(nint handle, uint bodyId, in Vector3 torque);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_AddForceAndTorque(nint handle, uint bodyId, in Vector3 force, in Vector3 torque);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_AddImpulse(nint handle, uint bodyId, in Vector3 impulse);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_AddImpulse2(nint handle, uint bodyId, in Vector3 impulse, in /*RVec3*/Vector3 point);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_AddAngularImpulse(nint handle, uint bodyId, in Vector3 angularImpulse);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_GetInverseInertia(nint handle, uint bodyId, Mat4* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetGravityFactor(nint handle, uint bodyId, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_BodyInterface_GetGravityFactor(nint handle, uint bodyId);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetUseManifoldReduction(nint handle, uint bodyId, [MarshalAs(UnmanagedType.U1)] bool value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BodyInterface_GetUseManifoldReduction(nint handle, uint bodyId);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetUserData(nint handle, uint bodyId, ulong userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern ulong JPH_BodyInterface_GetUserData(nint handle, uint bodyId);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_SetIsSensor(nint handle, uint bodyId, [MarshalAs(UnmanagedType.U1)] bool value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BodyInterface_IsSensor(nint handle, uint bodyId);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyInterface_GetMaterial(nint handle, uint bodyId, SubShapeID subShapeID);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyInterface_InvalidateContactCache(nint handle, uint bodyId);

    /* BodyLockInterface */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_PhysicsSystem_GetBodyLockInterface(nint system);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_PhysicsSystem_GetBodyLockInterfaceNoLock(nint system);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyLockInterface_LockRead(nint lockInterface, uint bodyID, out BodyLockRead @lock);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyLockInterface_UnlockRead(nint lockInterface, in BodyLockRead @lock);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyLockInterface_LockWrite(nint lockInterface, uint bodyID, out BodyLockWrite @lock);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyLockInterface_UnlockWrite(nint lockInterface, in BodyLockWrite @lock);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyLockInterface_LockMultiRead(nint lockInterface, uint* bodyIDs, uint count);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyLockMultiRead_Destroy(nint ioLock);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyLockMultiRead_GetBody(nint ioLock, uint bodyIndex);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyLockInterface_LockMultiWrite(nint lockInterface, uint* bodyIDs, uint count);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyLockMultiWrite_Destroy(nint ioLock);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyLockMultiWrite_GetBody(nint ioLock, uint bodyIndex);

    /* JPH_MotionProperties */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern AllowedDOFs JPH_MotionProperties_GetAllowedDOFs(nint properties);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotionProperties_SetLinearDamping(nint properties, float damping);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MotionProperties_GetLinearDamping(nint properties);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotionProperties_SetAngularDamping(nint properties, float damping);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MotionProperties_GetAngularDamping(nint properties);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MotionProperties_GetInverseMassUnchecked(nint properties);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MotionProperties_SetMassProperties(nint properties, AllowedDOFs allowedDOFs, in MassProperties massProperties);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotionProperties_SetInverseMass(nint properties, float inverseMass);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotionProperties_GetInverseInertiaDiagonal(nint properties, Vector3* result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotionProperties_GetInertiaRotation(nint properties, Quaternion* result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotionProperties_SetInverseInertia(nint properties, Vector3* diagonal, Quaternion* rot);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotionProperties_ScaleToMass(nint properties, float mass);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MassProperties_DecomposePrincipalMomentsOfInertia(in MassProperties properties, Mat4* rotation, out Vector3 diagonal);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MassProperties_ScaleToMass(MassProperties* properties, float mass);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MassProperties_GetEquivalentSolidBoxSize(float mass, in Vector3 inertiaDiagonal, out Vector3 result);


    /* BodyLockInterface */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_PhysicsSystem_GetBroadPhaseQuery(nint system);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_PhysicsSystem_GetNarrowPhaseQuery(nint system);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_PhysicsSystem_GetNarrowPhaseQueryNoLock(nint system);

    public struct JPH_CollideSettingsBase
    {
        public ActiveEdgeMode activeEdgeMode;
        public CollectFacesMode collectFacesMode;
        public float collisionTolerance;
        public float penetrationTolerance;
        public Vector3 activeEdgeMovementDirection;
    }

    public struct JPH_CollideShapeSettings
    {
        public JPH_CollideSettingsBase @base;
        public float maxSeparationDistance;

        /// How backfacing triangles should be treated
        public BackFaceMode backFaceMode;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CollideShapeSettings_Init(JPH_CollideShapeSettings* settings);

    public struct JPH_ShapeCastSettings
    {
        public JPH_CollideSettingsBase @base;    /* Inherics JPH_CollideSettingsBase */
        public BackFaceMode backFaceModeTriangles;
        public BackFaceMode backFaceModeConvex;
        public bool useShrunkenShapeAndConvexRadius;
        public bool returnDeepestPoint;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ShapeCastSettings_Init(JPH_ShapeCastSettings* settings);

    #region BroadPhaseQuery
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BroadPhaseQuery_CastRay(nint query,
        in Vector3 origin, in Vector3 direction,
        JPH_RayCastBodyCollectorCallback callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BroadPhaseQuery_CollideAABox(nint query,
        in BoundingBox box,
        JPH_CollideShapeBodyCollectorCallback callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BroadPhaseQuery_CollideSphere(nint query,
        in Vector3 center, float radius,
        JPH_CollideShapeBodyCollectorCallback callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_BroadPhaseQuery_CollidePoint(nint query,
        in Vector3 point,
        JPH_CollideShapeBodyCollectorCallback callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter);
    #endregion

    #region NarrowPhaseQuery
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CastRay(nint system,
        in Vector3 origin, in Vector3 direction,
        out RayCastResult hit,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CastRay(nint system,
        in RVector3 origin, in Vector3 direction,
        out RayCastResult hit,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CastRay2(nint system,
        in Vector3 origin, in Vector3 direction,
        RayCastSettings* rayCastSettings,
        JPH_CastRayCollector callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter,
        nint shapeFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CastRay2(nint system,
        in RVector3 origin, in Vector3 direction,
        RayCastSettings* rayCastSettings,
        JPH_CastRayCollector callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter,
        nint shapeFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CastRay3(nint system,
        in Vector3 origin, in Vector3 direction,
        RayCastSettings* rayCastSettings,
        CollisionCollectorType collectorType,
        JPH_CastRayResultCallback callback, nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter,
        nint shapeFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CastRay3(nint system,
        in RVector3 origin, in Vector3 direction,
        RayCastSettings* rayCastSettings,
        CollisionCollectorType collectorType,
        JPH_CastRayResultCallback callback, nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter,
        nint shapeFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CollidePoint(nint query,
        in Vector3 point,
        JPH_CollidePointCollector callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter,
        nint shapeFilter
        );

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CollidePoint(nint query,
        in RVector3 point,
        JPH_CollidePointCollector callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter,
        nint shapeFilter
        );

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CollidePoint2(nint query,
        in Vector3 point,
        CollisionCollectorType collectorType,
        JPH_CollidePointResultCallback callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter,
        nint shapeFilter
        );

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CollidePoint2(nint query,
        in RVector3 point,
        CollisionCollectorType collectorType,
        JPH_CollidePointResultCallback callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter,
        nint shapeFilter
        );

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CollideShape(nint query,
        nint shape, in Vector3 scale, in Mat4 centerOfMassTransform,
        JPH_CollideShapeSettings* settings,
        in Vector3 baseOffset,
        JPH_CollideShapeCollector callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter,
        nint shapeFilter
        );

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CollideShape(nint query,
        nint shape, in Vector3 scale, in RMatrix4x4 centerOfMassTransform,
        JPH_CollideShapeSettings* settings,
        in RVector3 baseOffset,
        JPH_CollideShapeCollector callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter,
        nint shapeFilter
        );

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CollideShape2(nint query,
        nint shape, in Vector3 scale, in Mat4 centerOfMassTransform,
        JPH_CollideShapeSettings* settings,
        in Vector3 baseOffset,
        CollisionCollectorType collectorType,
        JPH_CollideShapeResultCallback callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter,
        nint shapeFilter
        );

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CollideShape2(nint query,
        nint shape, in Vector3 scale, in RMatrix4x4 centerOfMassTransform,
        JPH_CollideShapeSettings* settings,
        in RVector3 baseOffset,
        CollisionCollectorType collectorType,
        JPH_CollideShapeResultCallback callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter,
        nint shapeFilter
        );

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CastShape(nint query,
        nint shape,
        in Mat4 worldTransform, in Vector3 direction,
        JPH_ShapeCastSettings* settings,
        in Vector3 baseOffset,
        JPH_CastShapeCollector callback, nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter,
        nint shapeFilter
        );

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CastShape(nint query,
        nint shape,
        in RMatrix4x4 worldTransform, in Vector3 direction,
        JPH_ShapeCastSettings* settings,
        in RVector3 baseOffset,
        JPH_CastShapeCollector callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter,
        nint shapeFilter
        );

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CastShape2(nint query,
        nint shape,
        in Mat4 worldTransform, in Vector3 direction,
        JPH_ShapeCastSettings* settings,
        in Vector3 baseOffset,
        CollisionCollectorType collectorType,
        JPH_CastShapeResultCallback callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter,
        nint shapeFilter
        );

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_NarrowPhaseQuery_CastShape2(nint query,
        nint shape,
        in RMatrix4x4 worldTransform, in Vector3 direction,
        JPH_ShapeCastSettings* settings,
        in RVector3 baseOffset,
        CollisionCollectorType collectorType,
        JPH_CastShapeResultCallback callback,
        nint userData,
        nint broadPhaseLayerFilter,
        nint objectLayerFilter,
        nint bodyFilter,
        nint shapeFilter
        );
    #endregion

    /* Body */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_Body_GetID(nint body);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern BodyType JPH_Body_GetBodyType(nint body);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetWorldSpaceBounds(nint body, out BoundingBox box);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetWorldSpaceSurfaceNormal(nint body, uint subShapeID, in Vector3 position, out Vector3 normal);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetWorldSpaceSurfaceNormal(nint body, uint subShapeID, in RVector3 position, out Vector3 normal);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_IsActive(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_IsStatic(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_IsKinematic(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_IsDynamic(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_CanBeKinematicOrDynamic(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_IsSensor(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_SetIsSensor(nint handle, [MarshalAs(UnmanagedType.U1)] bool value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_SetCollideKinematicVsNonDynamic(nint handle, [MarshalAs(UnmanagedType.U1)] bool value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_GetCollideKinematicVsNonDynamic(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_SetUseManifoldReduction(nint handle, [MarshalAs(UnmanagedType.U1)] bool value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_GetUseManifoldReduction(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_GetUseManifoldReductionWithBody(nint handle, nint other);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_SetEnhancedInternalEdgeRemoval(nint handle, [MarshalAs(UnmanagedType.U1)] bool value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_GetEnhancedInternalEdgeRemoval(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_GetEnhancedInternalEdgeRemovalWithBody(nint handle, nint other);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_SetApplyGyroscopicForce(nint handle, [MarshalAs(UnmanagedType.U1)] bool value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_GetApplyGyroscopicForce(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_Body_GetMotionProperties(nint handle);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_Body_GetMotionPropertiesUnchecked(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern MotionType JPH_Body_GetMotionType(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_SetMotionType(nint handle, MotionType motionType);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern BroadPhaseLayer JPH_Body_GetBroadPhaseLayer(nint handle);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern ObjectLayer JPH_Body_GetObjectLayer(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetCollisionGroup(nint body, out JPH_CollisionGroup result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_SetCollisionGroup(nint body, in JPH_CollisionGroup value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_GetAllowSleeping(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_SetAllowSleeping(nint handle, [MarshalAs(UnmanagedType.U1)] bool motionType);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_ResetSleepTimer(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_Body_GetFriction(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_SetFriction(nint handle, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_Body_GetRestitution(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_SetRestitution(nint handle, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetPosition(nint handle, out Vector3 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetPosition(nint handle, out RVector3 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetRotation(nint handle, out Quaternion result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetCenterOfMassPosition(nint handle, out Vector3 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetCenterOfMassPosition(nint handle, out RVector3 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetWorldTransform(nint handle, Mat4* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetCenterOfMassTransform(nint handle, Mat4* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetWorldTransform(nint handle, out RMatrix4x4 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetCenterOfMassTransform(nint handle, out RMatrix4x4 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetInverseCenterOfMassTransform(nint handle, Mat4* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetInverseCenterOfMassTransform(nint handle, out RMatrix4x4 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetLinearVelocity(nint handle, out Vector3 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_SetLinearVelocity(nint handle, in Vector3 velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_SetLinearVelocityClamped(nint handle, in Vector3 velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetAngularVelocity(nint handle, out Vector3 velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_SetAngularVelocity(nint handle, in Vector3 velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_SetAngularVelocityClamped(nint handle, in Vector3 velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetPointVelocityCOM(nint handle, in Vector3 pointRelativeToCOM, out Vector3 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetPointVelocity(nint handle, in Vector3 point, out Vector3 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetPointVelocity(nint handle, in RVector3 point, out Vector3 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_AddForce(nint handle, in Vector3 velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_AddForceAtPosition(nint handle, in Vector3 velocity, in Vector3 position);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_AddForceAtPosition(nint handle, in Vector3 velocity, in RVector3 position);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_AddTorque(nint handle, in Vector3 value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetAccumulatedForce(nint handle, out Vector3 force);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetAccumulatedTorque(nint handle, out Vector3 force);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_ResetForce(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_ResetTorque(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_ResetMotion(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_GetInverseInertia(nint handle, Mat4* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_AddImpulse(nint handle, in Vector3 impulse);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_AddImpulseAtPosition(nint handle, in Vector3 impulse, in Vector3 position);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_AddImpulseAtPosition(nint handle, in Vector3 impulse, in RVector3 position);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_AddAngularImpulse(nint handle, in Vector3 angularImpulse);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_MoveKinematic(nint handle, in Vector3 targetPosition, in Quaternion targetRotation, float deltaTime);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_MoveKinematic(nint handle, in RVector3 targetPosition, in Quaternion targetRotation, float deltaTime);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_ApplyBuoyancyImpulse(nint handle, in Vector3 surfacePosition, in Vector3 surfaceNormal, float buoyancy, float linearDrag, float angularDrag, in Vector3 fluidVelocity, in Vector3 gravity, float deltaTime);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_ApplyBuoyancyImpulse(nint handle, in RVector3 surfacePosition, in Vector3 surfaceNormal, float buoyancy, float linearDrag, float angularDrag, in Vector3 fluidVelocity, in Vector3 gravity, float deltaTime);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_IsInBroadPhase(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Body_IsCollisionCacheInvalid(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_Body_GetShape(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Body_SetUserData(nint handle, ulong userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern ulong JPH_Body_GetUserData(nint handle);

    // ContactListener
    public struct JPH_ContactListener_Procs
    {
        public delegate* unmanaged<nint, nint, nint, Vector3*, CollideShapeResult*, uint> OnContactValidate;
        public delegate* unmanaged<nint, nint, nint, nint, ContactSettings*, void> OnContactAdded;
        public delegate* unmanaged<nint, nint, nint, nint, ContactSettings*, void> OnContactPersisted;
        public delegate* unmanaged<nint, SubShapeIDPair*, void> OnContactRemoved;
    }

    public struct JPH_ContactListener_ProcsDouble
    {
        public delegate* unmanaged<nint, nint, nint, RVector3*, CollideShapeResult*, uint> OnContactValidate;
        public delegate* unmanaged<nint, nint, nint, nint, ContactSettings*, void> OnContactAdded;
        public delegate* unmanaged<nint, nint, nint, nint, ContactSettings*, void> OnContactPersisted;
        public delegate* unmanaged<nint, SubShapeIDPair*, void> OnContactRemoved;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ContactListener_SetProcs(in JPH_ContactListener_Procs procs);

#if !WEB
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", EntryPoint = "JPH_ContactListener_SetProcs", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", EntryPoint = "JPH_ContactListener_SetProcs", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_ContactListener_SetProcsDouble(in JPH_ContactListener_ProcsDouble procs);
#endif

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_ContactListener_Create(nint userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ContactListener_Destroy(nint handle);

    // BodyActivationListener
    public struct JPH_BodyActivationListener_Procs
    {
        public delegate* unmanaged<nint, uint, ulong, void> OnBodyActivated;
        public delegate* unmanaged<nint, uint, ulong, void> OnBodyDeactivated;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyActivationListener_SetProcs(in JPH_BodyActivationListener_Procs procs);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_BodyActivationListener_Create(nint userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_BodyActivationListener_Destroy(nint handle);

    /* ContactManifold */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ContactManifold_GetWorldSpaceNormal(nint manifold, out Vector3 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_ContactManifold_GetPenetrationDepth(nint manifold);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern SubShapeID JPH_ContactManifold_GetSubShapeID1(nint manifold);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern SubShapeID JPH_ContactManifold_GetSubShapeID2(nint manifold);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_ContactManifold_GetPointCount(nint manifold);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ContactManifold_GetWorldSpaceContactPointOn1(nint manifold, uint index, Vector3* result); // JPH_RVec3
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_ContactManifold_GetWorldSpaceContactPointOn2(nint manifold, uint index, Vector3* result); // JPH_RVec3

    #region CharacterBase
    public struct JPH_CharacterBaseSettings
    {
        public Vector3 up;
        public Plane supportingVolume;
        public float maxSlopeAngle;
        public bool enhancedInternalEdgeRemoval;
        public /*const JPH_Shape**/nint shape;
    }

    /* CharacterBase */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterBase_Destroy(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_CharacterBase_GetCosMaxSlopeAngle(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterBase_SetMaxSlopeAngle(nint handle, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterBase_GetUp(nint handle, out Vector3 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterBase_SetUp(nint handle, in Vector3 value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_CharacterBase_IsSlopeTooSteep(nint handle, in Vector3 value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_CharacterBase_GetShape(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern GroundState JPH_CharacterBase_GetGroundState(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_CharacterBase_IsSupported(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterBase_GetGroundPosition(nint handle, out Vector3 position); // RVec3

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterBase_GetGroundNormal(nint handle, out Vector3 normal);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterBase_GetGroundVelocity(nint handle, out Vector3 velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_CharacterBase_GetGroundMaterial(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_CharacterBase_GetGroundBodyId(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_CharacterBase_GetGroundSubShapeId(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern ulong JPH_CharacterBase_GetGroundUserData(nint handle);
    #endregion

    #region Characted
    public struct JPH_CharacterSettings     /* Inherics JPH_CharacterBaseSettings */
    {
        public JPH_CharacterBaseSettings baseSettings;
        public ObjectLayer layer;
        public float mass;
        public float friction;
        public float gravityFactor;
        public AllowedDOFs allowedDOFs;
    }

    /* CharacterSettings */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterSettings_Init(JPH_CharacterSettings* settings);

    /* Character */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_Character_Create(JPH_CharacterSettings* settings, in Vector3 position, in Quaternion rotation, ulong userData,/*JPH_PhysicsSystem**/ nint system);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_Character_Create(JPH_CharacterSettings* settings, in RVector3 position, in Quaternion rotation, ulong userData, nint physicsSystem);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_AddToPhysicsSystem(nint character, Activation activationMode /*= JPH_ActivationActivate */, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_RemoveFromPhysicsSystem(nint character, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_Activate(nint character, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_PostSimulation(nint character, float maxSeparationDistance, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_SetLinearAndAngularVelocity(nint character, in Vector3 linearVelocity, in Vector3 angularVelocity, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_GetLinearVelocity(nint character, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_SetLinearVelocity(nint character, in Vector3 value, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_AddLinearVelocity(nint character, in Vector3 value, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_AddImpulse(nint character, in Vector3 value, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_Character_GetBodyID(nint character);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_GetPositionAndRotation(nint character, out Vector3 position, out Quaternion rotation, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_GetPositionAndRotation(nint character, out RVector3 position, out Quaternion rotation, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_SetPositionAndRotation(nint character, in Vector3 position, in Quaternion rotation, Activation activationMode, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_SetPositionAndRotation(nint character, in RVector3 position, in Quaternion rotation, Activation activationMode, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_GetPosition(nint character, out Vector3 position, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_GetPosition(nint character, out RVector3 position, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_SetPosition(nint character, in Vector3 position, Activation activationMode, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_SetPosition(nint character, in RVector3 position, Activation activationMode, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_GetRotation(nint character, out Quaternion rotation, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_SetRotation(nint character, in Quaternion rotation, Activation activationMode, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_GetCenterOfMassPosition(nint character, out Vector3 result, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_GetCenterOfMassPosition(nint character, out RVector3 result, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_GetWorldTransform(nint character, Mat4* result, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_GetWorldTransform(nint character, out RMatrix4x4 result, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern JPH_ObjectLayer JPH_Character_GetLayer(nint character);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_SetLayer(nint character, JPH_ObjectLayer value, [MarshalAs(UnmanagedType.U1)] bool lockBodies /*= true*/);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Character_SetShape(nint character, /*const JPH_Shape**/nint shape, float maxPenetrationDepth, [MarshalAs(UnmanagedType.U1)] bool lockBodies /*= true*/);
    #endregion

    #region CharacterVirtual
    /* CharacterVirtualSettings */
    public struct JPH_CharacterVirtualSettings     /* Inherics JPH_CharacterBaseSettings */
    {
        public JPH_CharacterBaseSettings baseSettings;
        public CharacterID ID;
        public float mass;
        public float maxStrength;
        public Vector3 shapeOffset;
        public BackFaceMode backFaceMode;
        public float predictiveContactDistance;
        public uint maxCollisionIterations;
        public uint maxConstraintIterations;
        public float minTimeRemaining;
        public float collisionTolerance;
        public float characterPadding;
        public uint maxNumHits;
        public float hitReductionCosMaxAngle;
        public float penetrationRecoverySpeed;
        public /*const JPH_Shape**/nint innerBodyShape;
        public BodyID innerBodyIDOverride;
        public ObjectLayer innerBodyLayer;
    }


#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtualSettings_Init(JPH_CharacterVirtualSettings* settings);
    /* CharacterVirtual */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_CharacterVirtual_Create(JPH_CharacterVirtualSettings* settings, in Vector3 position, in Quaternion rotation, ulong userData, nint physicsSystem);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_CharacterVirtual_Create(JPH_CharacterVirtualSettings* settings, in RVector3 position, in Quaternion rotation, ulong userData, nint physicsSystem);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern CharacterID JPH_CharacterVirtual_GetID(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_CharacterVirtual_SetListener(nint handle, nint listener);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_CharacterVirtual_SetCharacterVsCharacterCollision(nint handle, nint listener);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_GetLinearVelocity(nint handle, out Vector3 velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_SetLinearVelocity(nint handle, in Vector3 velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_GetPosition(nint handle, out Vector3 position); // RVec3

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_SetPosition(nint handle, in Vector3 position);// RVec3

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_GetRotation(nint handle, out Quaternion rotation);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_SetRotation(nint handle, in Quaternion rotation);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_GetWorldTransform(nint shape, Mat4* result); //RMatrix4x4

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_GetCenterOfMassTransform(nint shape, Mat4* result); //RMatrix4x4

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_CharacterVirtual_GetMass(nint handle);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_SetMass(nint handle, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_CharacterVirtual_GetMaxStrength(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_SetMaxStrength(nint settings, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_CharacterVirtual_GetPenetrationRecoverySpeed(nint character);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_SetPenetrationRecoverySpeed(nint character, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_CharacterVirtual_GetEnhancedInternalEdgeRemoval(nint character);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_SetEnhancedInternalEdgeRemoval(nint character, [MarshalAs(UnmanagedType.U1)] bool value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_CharacterVirtual_GetCharacterPadding(nint character);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_CharacterVirtual_GetMaxNumHits(nint character);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_SetMaxNumHits(nint character, uint value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_CharacterVirtual_GetHitReductionCosMaxAngle(nint character);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_SetHitReductionCosMaxAngle(nint character, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_CharacterVirtual_GetMaxHitsExceeded(nint character);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_GetShapeOffset(nint character, out Vector3 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_SetShapeOffset(nint character, in Vector3 value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern ulong JPH_CharacterVirtual_GetUserData(nint character);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_SetUserData(nint character, ulong value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_CharacterVirtual_GetInnerBodyID(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_Update(nint handle, float deltaTime, JPH_ObjectLayer layer, nint physicsSytem, nint bodyFilter, nint shapeFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_ExtendedUpdate(nint handle, float deltaTime, ExtendedUpdateSettings* settings, JPH_ObjectLayer layer, nint physicsSytem, nint bodyFilter, nint shapeFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_RefreshContacts(nint handle, JPH_ObjectLayer layer, nint physicsSytem, nint bodyFilter, nint shapeFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_CancelVelocityTowardsSteepSlopes(nint handle, in Vector3 desiredVelocity, out Vector3 velocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_StartTrackingContactChanges(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_FinishTrackingContactChanges(nint handle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_CharacterVirtual_CanWalkStairs(nint character, in Vector3 linearVelocity);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_CharacterVirtual_WalkStairs(nint character, float deltaTime,
        in Vector3 stepUp, in Vector3 stepForward, in Vector3 stepForwardTest, in Vector3 stepDownExtra,
        in ObjectLayer layer, nint system, nint bodyFilter, nint shapeFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_CharacterVirtual_StickToFloor(nint character, in Vector3 stepDown, in ObjectLayer layer, nint system, nint bodyFilter, nint shapeFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_UpdateGroundVelocity(nint character);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_CharacterVirtual_SetShape(
        nint character,
        nint shape, float maxPenetrationDepth,
        JPH_ObjectLayer layer,
        nint system, nint bodyFilter,
        nint shapeFilter);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_SetInnerBodyShape(nint character, nint shape);

    public struct JPH_CharacterVirtualContact
    {
        public ulong hash;
        public BodyID bodyB;
        public CharacterID characterIDB;
        public SubShapeID subShapeIDB;
        public Vector3 position; /*  JPH_RVec3 */
        public Vector3 linearVelocity;
        public Vector3 contactNormal;
        public Vector3 surfaceNormal;
        public float distance;
        public float fraction;
        public MotionType motionTypeB;
        public Bool8 isSensorB;
        public /*const JPH_CharacterVirtual**/nint characterB;
        public ulong userData;
        public /*const JPH_PhysicsMaterial**/nint material;
        public Bool8 hadCollision;
        public Bool8 wasDiscarded;
        public Bool8 canPushCharacter;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern int JPH_CharacterVirtual_GetNumActiveContacts(nint character);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVirtual_GetActiveContact(nint character, int index, JPH_CharacterVirtualContact* result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_CharacterVirtual_HasCollidedWithBody(nint character, in BodyID bodyId);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_CharacterVirtual_HasCollidedWith(nint character, CharacterID other);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_CharacterVirtual_HasCollidedWithCharacter(nint character, nint other);

    public struct JPH_CharacterContactListener_Procs
    {
        public delegate* unmanaged<nint, nint, nint, Vector3*, Vector3*, void> OnAdjustBodyVelocity;
        public delegate* unmanaged<nint, nint, uint, uint, byte> OnContactValidate;
        public delegate* unmanaged<nint, nint, nint, uint, byte> OnCharacterContactValidate;
        public delegate* unmanaged<nint, nint, uint, uint, Vector3*, Vector3*, CharacterContactSettings*, void> OnContactAdded;
        public delegate* unmanaged<nint, nint, uint, uint, Vector3*, Vector3*, CharacterContactSettings*, void> OnContactPersisted;
        public delegate* unmanaged<nint, nint, uint, uint, void> OnContactRemoved;
        public delegate* unmanaged<nint, nint, nint, uint, Vector3*, Vector3*, CharacterContactSettings*, void> OnCharacterContactAdded;
        public delegate* unmanaged<nint, nint, nint, uint, Vector3*, Vector3*, CharacterContactSettings*, void> OnCharacterContactPersisted;
        public delegate* unmanaged<nint, nint, uint, uint, void> OnCharacterContactRemoved;
        public delegate* unmanaged<nint, nint, uint, uint, Vector3*, Vector3*, Vector3*, nint, Vector3*, Vector3*, void> OnContactSolve;
        public delegate* unmanaged<nint, nint, nint, uint, Vector3*, Vector3*, Vector3*, nint, Vector3*, Vector3*, void> OnCharacterContactSolve;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterContactListener_SetProcs(in JPH_CharacterContactListener_Procs procs);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_CharacterContactListener_Create(nint userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterContactListener_Destroy(nint listener);
    #endregion

    #region CharacterVsCharacterCollision
    public struct JPH_CharacterVsCharacterCollision_Procs
    {
        public delegate* unmanaged<nint, nint, Mat4*, JPH_CollideShapeSettings*, Vector3*, void> CollideCharacter;
        public delegate* unmanaged<nint, nint, Mat4*, Vector3*, JPH_ShapeCastSettings*, Vector3*, void> CastCharacter;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVsCharacterCollision_SetProcs(in JPH_CharacterVsCharacterCollision_Procs procs);


#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_CharacterVsCharacterCollision_Create(nint userData);


#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_CharacterVsCharacterCollision_CreateSimple();

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVsCharacterCollisionSimple_AddCharacter(nint characterVsCharacter, nint character);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVsCharacterCollisionSimple_RemoveCharacter(nint characterVsCharacter, nint character);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_CharacterVsCharacterCollision_Destroy(nint handle);
    #endregion

    #region DebugRenderer
    public struct JPH_DebugRenderer_Procs
    {
        public delegate* unmanaged<nint, Vector3*, Vector3*, uint, void> DrawLine;
        public delegate* unmanaged<nint, Vector3*, Vector3*, Vector3*, uint, DebugRenderer.CastShadow, void> DrawTriangle;
        public delegate* unmanaged<nint, Vector3*, byte*, uint, float, void> DrawText3D;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_SetProcs(in JPH_DebugRenderer_Procs procs);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_DebugRenderer_Create(nint userData);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_Destroy(nint renderer);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_NextFrame(nint renderer);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_SetCameraPos(nint renderer, in Vector3 position);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_SetCameraPos(nint renderer, in RVector3 position);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawLine(nint renderer, in Vector3 from, in Vector3 to, uint color);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawLine(nint renderer, in RVector3 from, in RVector3 to, uint color);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawWireBox(nint renderer, in BoundingBox box, uint color);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawWireBox2(nint renderer, in Mat4 matrix, in BoundingBox box, uint color);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawWireBox2(nint renderer, in RMatrix4x4 matrix, in BoundingBox box, uint color);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawMarker(nint renderer, /*RVec3*/ in Vector3 position, uint color, float size);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawArrow(nint renderer, /*RVec3*/ in Vector3 from, /*RVec3*/ in Vector3 to, uint color, float size);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawCoordinateSystem(nint renderer, /*RMatrix4x4*/ in Mat4 matrix, float size);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawPlane(nint renderer, /*RVec3*/ in Vector3 point, in Vector3 normal, uint color, float size);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawWireTriangle(nint renderer, /*RVec3*/ in Vector3 v1, /*RVec3*/ in Vector3 v2, /*RVec3*/ in Vector3 v3, uint color);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawWireSphere(nint renderer, /*RVec3*/ in Vector3 center, float radius, uint color, int level);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawWireUnitSphere(nint renderer, /*RMatrix4x4*/ in Mat4 matrix, uint color, int level);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawTriangle(nint renderer, /*RVec3*/ in Vector3 v1, /*RVec3*/ in Vector3 v2, /*RVec3*/ in Vector3 v3, uint color, DebugRenderer.CastShadow castShadow);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawBox(nint renderer, in BoundingBox box, uint color, DebugRenderer.CastShadow castShadow, DebugRenderer.DrawMode drawMode);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawBox2(nint renderer, /*RMatrix4x4*/ in Mat4 matrix, in BoundingBox box, uint color, DebugRenderer.CastShadow castShadow, DebugRenderer.DrawMode drawMode);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawSphere(nint renderer, /*RVec3*/ in Vector3 center, float radius, uint color, DebugRenderer.CastShadow castShadow, DebugRenderer.DrawMode drawMode);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawUnitSphere(nint renderer, /*RMatrix4x4*/ in Mat4 matrix, uint color, DebugRenderer.CastShadow castShadow, DebugRenderer.DrawMode drawMode);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawCapsule(nint renderer, /*RMatrix4x4*/ in Mat4 matrix, float halfHeightOfCylinder, float radius, uint color, DebugRenderer.CastShadow castShadow, DebugRenderer.DrawMode drawMode);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawCylinder(nint renderer, /*RMatrix4x4*/ in Mat4 matrix, float halfHeight, float radius, uint color, DebugRenderer.CastShadow castShadow, DebugRenderer.DrawMode drawMode);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawOpenCone(nint renderer, /*RVec3*/ in Vector3 top, in Vector3 axis, in Vector3 perpendicular, float halfAngle, float length, uint color, DebugRenderer.CastShadow castShadow, DebugRenderer.DrawMode drawMode);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawSwingConeLimits(nint renderer, /*RMatrix4x4*/ in Mat4 matrix, float swingYHalfAngle, float swingZHalfAngle, float edgeLength, uint color, DebugRenderer.CastShadow castShadow, DebugRenderer.DrawMode drawMode);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawSwingPyramidLimits(nint renderer, /*RMatrix4x4*/ in Mat4 matrix, float minSwingYAngle, float maxSwingYAngle, float minSwingZAngle, float maxSwingZAngle, float edgeLength, uint color, DebugRenderer.CastShadow castShadow, DebugRenderer.DrawMode drawMode);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawPie(nint renderer, /*RVec3*/ in Vector3 center, float radius, in Vector3 normal, in Vector3 axis, float minAngle, float maxAngle, uint color, DebugRenderer.CastShadow castShadow, DebugRenderer.DrawMode drawMode);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_DebugRenderer_DrawTaperedCylinder(nint renderer, /*RMatrix4x4*/ in Mat4 matrix, float top, float bottom, float topRadius, float bottomRadius, uint color, DebugRenderer.CastShadow castShadow, DebugRenderer.DrawMode drawMode);
    #endregion

    #region Skeleton
    public readonly struct SkeletonJoint
    {
        public readonly byte* name;
        public readonly byte* parentName;
        public readonly int parentJointIndex;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_Skeleton_Create();
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Skeleton_Destroy(nint skeleton);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_Skeleton_AddJoint(nint skeleton, string name);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_Skeleton_AddJoint2(nint skeleton, string name, int parentIndex);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern uint JPH_Skeleton_AddJoint3(nint skeleton, string name, string parentName);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern int JPH_Skeleton_GetJointCount(nint skeleton);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Skeleton_GetJoint(nint skeleton, int index, out SkeletonJoint joint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern int JPH_Skeleton_GetJointIndex(nint skeleton, string name);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Skeleton_CalculateParentJointIndices(nint skeleton);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Skeleton_AreJointsCorrectlyOrdered(nint skeleton);
    #endregion

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_EstimateCollisionResponse(nint body1, nint body2, nint manifold, float combinedFriction, float combinedRestitution, float minVelocityForRestitution, int numIterations, CollisionEstimationResult* result);

    #region Ragdoll
    /* Ragdoll */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_RagdollSettings_Create();
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_RagdollSettings_Destroy(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_RagdollSettings_GetSkeleton(nint character);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_RagdollSettings_SetSkeleton(nint character, nint skeleton);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_RagdollSettings_Stabilize(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_RagdollSettings_DisableParentChildCollisions(nint settings, Mat4* jointMatrices /*=nullptr*/, float minSeparationDistance/* = 0.0f*/);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_RagdollSettings_CalculateBodyIndexToConstraintIndex(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern int JPH_RagdollSettings_GetConstraintIndexForBodyIndex(nint settings, int bodyIndex);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_RagdollSettings_CalculateConstraintIndexToBodyIdxPair(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_RagdollSettings_CreateRagdoll(nint settings, nint system, uint collisionGroup, ulong userData);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Ragdoll_Destroy(nint ragdoll);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Ragdoll_AddToPhysicsSystem(nint ragdoll, Activation activationMode /*= JPH_ActivationActivate */, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Ragdoll_RemoveFromPhysicsSystem(nint ragdoll, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Ragdoll_Activate(nint ragdoll, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Ragdoll_IsActive(nint ragdoll, [MarshalAs(UnmanagedType.U1)] bool lockBodies /* = true */);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Ragdoll_ResetWarmStart(nint ragdoll);
    #endregion

    public struct JPH_VehicleAntiRollBar
    {
        public int leftWheel;
        public int rightWheel;
        public float stiffness;
    }

    public struct JPH_VehicleEngineSettings
    {
        public float maxTorque;
        public float minRPM;
        public float maxRPM;
        //public LinearCurve			normalizedTorque;
        public float inertia;
        public float angularDamping;
    }

    public struct JPH_VehicleDifferentialSettings
    {
        public int leftWheel;
        public int rightWheel;
        public float differentialRatio;
        public float leftRightSplit;
        public float limitedSlipRatio;
        public float engineTorqueRatio;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleAntiRollBar_Init(JPH_VehicleAntiRollBar* settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleEngineSettings_Init(JPH_VehicleEngineSettings* settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleDifferentialSettings_Init(JPH_VehicleDifferentialSettings* settings);

    #region VehicleTransmission
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_VehicleTransmissionSettings_Create();
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleTransmissionSettings_Destroy(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern TransmissionMode JPH_VehicleTransmissionSettings_GetMode(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleTransmissionSettings_SetMode(nint settings, TransmissionMode value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern int JPH_VehicleTransmissionSettings_GetGearRatioCount(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_VehicleTransmissionSettings_GetGearRatio(nint settings, int index);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float* JPH_VehicleTransmissionSettings_GetGearRatios(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleTransmissionSettings_SetGearRatio(nint settings, int index, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleTransmissionSettings_SetGearRatios(nint settings, float* values, int count);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern int JPH_VehicleTransmissionSettings_GetReverseGearRatioCount(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_VehicleTransmissionSettings_GetReverseGearRatio(nint settings, int index);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleTransmissionSettings_SetReverseGearRatio(nint settings, int index, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float* JPH_VehicleTransmissionSettings_GetReverseGearRatios(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleTransmissionSettings_SetReverseGearRatios(nint settings, float* values, int count);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_VehicleTransmissionSettings_GetSwitchTime(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleTransmissionSettings_SetSwitchTime(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_VehicleTransmissionSettings_GetClutchReleaseTime(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleTransmissionSettings_SetClutchReleaseTime(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_VehicleTransmissionSettings_GetSwitchLatency(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleTransmissionSettings_SetSwitchLatency(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_VehicleTransmissionSettings_GetShiftUpRPM(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleTransmissionSettings_SetShiftUpRPM(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_VehicleTransmissionSettings_GetShiftDownRPM(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleTransmissionSettings_SetShiftDownRPM(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_VehicleTransmissionSettings_GetClutchStrength(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleTransmissionSettings_SetClutchStrength(nint settings, float value);
    #endregion

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleControllerSettings_Destroy(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_VehicleController_GetConstraint(nint controller);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleCollisionTester_Destroy(nint tester);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern ObjectLayer JPH_VehicleCollisionTester_GetObjectLayer(nint tester);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleCollisionTester_SetObjectLayer(nint tester, JPH_ObjectLayer value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_VehicleCollisionTesterRay_Create(JPH_ObjectLayer layer, in Vector3 up, float maxSlopeAngle);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_VehicleCollisionTesterCastSphere_Create(JPH_ObjectLayer layer, float radius, in Vector3 up, float maxSlopeAngle);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_VehicleCollisionTesterCastCylinder_Create(JPH_ObjectLayer layer, float convexRadiusFraction);

    #region VehicleConstraint
    public struct JPH_VehicleConstraintSettings
    {
        public JPH_ConstraintSettings baseSettings;    /* Inherics JPH_ConstraintSettings */

        public Vector3 up;
        public Vector3 forward;
        public float maxPitchRollAngle;
        public int wheelsCount;
        public nint* wheels;
        public int antiRollBarsCount;
        public JPH_VehicleAntiRollBar* antiRollBars;
        public /*JPH_VehicleControllerSettings*/nint controller;
    }

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_VehicleConstraintSettings_Init(JPH_VehicleConstraintSettings* settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_VehicleConstraint_Create(nint body, JPH_VehicleConstraintSettings* settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_VehicleConstraint_AsPhysicsStepListener(nint constraint);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleConstraint_SetMaxPitchRollAngle(nint constraint, float maxPitchRollAngle);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleConstraint_SetVehicleCollisionTester(nint constraint, nint tester);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleConstraint_OverrideGravity(nint constraint, in Vector3 value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_VehicleConstraint_IsGravityOverridden(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleConstraint_GetGravityOverride(nint constraint, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleConstraint_ResetGravityOverride(nint constraint);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleConstraint_GetLocalForward(nint constraint, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleConstraint_GetLocalUp(nint constraint, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleConstraint_GetWorldUp(nint constraint, out Vector3 result);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_VehicleConstraint_GetVehicleBody(nint constraint);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_VehicleConstraint_GetController(nint constraint);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern int JPH_VehicleConstraint_GetWheelsCount(nint constraint);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_VehicleConstraint_GetWheel(nint constraint, int index);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleConstraint_GetWheelLocalBasis(nint constraint, nint wheel, out Vector3 forward, out Vector3 up, out Vector3 right);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleConstraint_GetWheelLocalTransform(nint constraint, int wheelIndex, in Vector3 wheelRight, in Vector3 wheelUp, Mat4* result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleConstraint_GetWheelWorldTransform(nint constraint, int wheelIndex, in Vector3 wheelRight, in Vector3 wheelUp, Mat4* result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_VehicleConstraint_GetWheelWorldTransform(nint constraint, int wheelIndex, in Vector3 wheelRight, in Vector3 wheelUp, RMatrix4x4* result);
    #endregion

    #region Wheel
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_WheelSettings_Create();

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_Destroy(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_GetPosition(nint settings, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_SetPosition(nint settings, in Vector3 value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_GetSuspensionForcePoint(nint settings, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_SetSuspensionForcePoint(nint settings, in Vector3 value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_GetSuspensionDirection(nint settings, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_SetSuspensionDirection(nint settings, in Vector3 value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_GetSteeringAxis(nint settings, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_SetSteeringAxis(nint settings, in Vector3 value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_GetWheelUp(nint settings, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_SetWheelUp(nint settings, in Vector3 value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_GetWheelForward(nint settings, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_SetWheelForward(nint settings, in Vector3 value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheelSettings_GetSuspensionMinLength(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_SetSuspensionMinLength(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheelSettings_GetSuspensionMaxLength(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_SetSuspensionMaxLength(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheelSettings_GetSuspensionPreloadLength(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_SetSuspensionPreloadLength(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_GetSuspensionSpring(nint settings, out SpringSettings result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_SetSuspensionSpring(nint settings, in SpringSettings springSettings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheelSettings_GetRadius(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_SetRadius(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheelSettings_GetWidth(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_SetWidth(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_WheelSettings_GetEnableSuspensionForcePoint(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettings_SetEnableSuspensionForcePoint(nint settings, [MarshalAs(UnmanagedType.U1)] bool value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_Wheel_Create(nint wheelSettings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Wheel_Destroy(nint wheel);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_Wheel_GetSettings(nint wheel);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_Wheel_GetAngularVelocity(nint wheel);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Wheel_SetAngularVelocity(nint wheel, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_Wheel_GetRotationAngle(nint wheel);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Wheel_SetRotationAngle(nint wheel, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_Wheel_GetSteerAngle(nint wheel);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Wheel_SetSteerAngle(nint wheel, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Wheel_HasContact(nint wheel);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern BodyID JPH_Wheel_GetContactBodyID(nint wheel);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern SubShapeID JPH_Wheel_GetContactSubShapeID(nint wheel);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Wheel_GetContactPosition(nint wheel, out Vector3 result); // JPH_RVec3
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Wheel_GetContactPointVelocity(nint wheel, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Wheel_GetContactNormal(nint wheel, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Wheel_GetContactLongitudinal(nint wheel, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_Wheel_GetContactLateral(nint wheel, out Vector3 result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_Wheel_GetSuspensionLength(nint wheel);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_Wheel_GetSuspensionLambda(nint wheel);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_Wheel_GetLongitudinalLambda(nint wheel);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_Wheel_GetLateralLambda(nint wheel);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_Wheel_HasHitHardPoint(nint wheel);
    #endregion

    #region WheeledVehicleController
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_WheelSettingsWV_Create();

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheelSettingsWV_GetInertia(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettingsWV_SetInertia(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheelSettingsWV_GetAngularDamping(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettingsWV_SetAngularDamping(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheelSettingsWV_GetMaxSteerAngle(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettingsWV_SetMaxSteerAngle(nint settings, float value);
    //JPH_CAPI JPH_LinearCurve* JPH_WheelSettingsWV_GetLongitudinalFriction(const JPH_WheelSettingsWV* settings);
    //JPH_CAPI void JPH_WheelSettingsWV_SetLongitudinalFriction(JPH_WheelSettingsWV* settings, const JPH_LinearCurve* value);
    //JPH_CAPI JPH_LinearCurve* JPH_WheelSettingsWV_GetLateralFriction(const JPH_WheelSettingsWV* settings);
    //JPH_CAPI void JPH_WheelSettingsWV_SetLateralFriction(JPH_WheelSettingsWV* settings, const JPH_LinearCurve* value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheelSettingsWV_GetMaxBrakeTorque(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettingsWV_SetMaxBrakeTorque(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheelSettingsWV_GetMaxHandBrakeTorque(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettingsWV_SetMaxHandBrakeTorque(nint settings, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_WheelWV_Create(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_WheelWV_GetSettings(nint wheel);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelWV_ApplyTorque(nint wheel, float torque, float deltaTime);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_WheeledVehicleControllerSettings_Create();

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheeledVehicleControllerSettings_GetEngine(nint settings, out JPH_VehicleEngineSettings result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheeledVehicleControllerSettings_SetEngine(nint settings, in JPH_VehicleEngineSettings value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern /*JPH_VehicleTransmissionSettings**/nint JPH_WheeledVehicleControllerSettings_GetTransmission(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheeledVehicleControllerSettings_SetTransmission(nint settings, nint value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern int JPH_WheeledVehicleControllerSettings_GetDifferentialsCount(nint settings);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheeledVehicleControllerSettings_SetDifferentialsCount(nint settings, int count);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheeledVehicleControllerSettings_GetDifferential(nint settings, int index, JPH_VehicleDifferentialSettings* result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheeledVehicleControllerSettings_SetDifferential(nint settings, int index, JPH_VehicleDifferentialSettings* value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheeledVehicleControllerSettings_SetDifferentials(nint settings, JPH_VehicleDifferentialSettings* values, int count);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheeledVehicleControllerSettings_GetDifferentialLimitedSlipRatio(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheeledVehicleControllerSettings_SetDifferentialLimitedSlipRatio(nint settings, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheeledVehicleController_SetDriverInput(nint controller, float forward, float right, float brake, float handBrake);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheeledVehicleController_SetForwardInput(nint controller, float forward);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheeledVehicleController_GetForwardInput(nint controller);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheeledVehicleController_SetRightInput(nint controller, float rightRatio);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheeledVehicleController_GetRightInput(nint controller);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheeledVehicleController_SetBrakeInput(nint controller, float brakeInput);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheeledVehicleController_GetBrakeInput(nint controller);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheeledVehicleController_SetHandBrakeInput(nint controller, float handBrakeInput);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheeledVehicleController_GetHandBrakeInput(nint controller);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheeledVehicleController_GetWheelSpeedAtClutch(nint controller);
    #endregion

    #region MotorcycleController
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_MotorcycleControllerSettings_Create();
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MotorcycleControllerSettings_GetMaxLeanAngle(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotorcycleControllerSettings_SetMaxLeanAngle(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MotorcycleControllerSettings_GetLeanSpringConstant(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotorcycleControllerSettings_SetLeanSpringConstant(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MotorcycleControllerSettings_GetLeanSpringDamping(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotorcycleControllerSettings_SetLeanSpringDamping(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MotorcycleControllerSettings_GetLeanSpringIntegrationCoefficient(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotorcycleControllerSettings_SetLeanSpringIntegrationCoefficient(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MotorcycleControllerSettings_GetLeanSpringIntegrationCoefficientDecay(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotorcycleControllerSettings_SetLeanSpringIntegrationCoefficientDecay(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MotorcycleControllerSettings_GetLeanSmoothingFactor(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotorcycleControllerSettings_SetLeanSmoothingFactor(nint settings, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MotorcycleController_GetWheelBase(nint controller);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_MotorcycleController_IsLeanControllerEnabled(nint controller);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotorcycleController_EnableLeanController(nint controller, [MarshalAs(UnmanagedType.U1)] bool value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool JPH_MotorcycleController_IsLeanSteeringLimitEnabled(nint controller);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotorcycleController_EnableLeanSteeringLimit(nint controller, [MarshalAs(UnmanagedType.U1)] bool value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MotorcycleController_GetLeanSpringConstant(nint controller);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotorcycleController_SetLeanSpringConstant(nint controller, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MotorcycleController_GetLeanSpringDamping(nint controller);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotorcycleController_SetLeanSpringDamping(nint controller, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MotorcycleController_GetLeanSpringIntegrationCoefficient(nint controller);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotorcycleController_SetLeanSpringIntegrationCoefficient(nint controller, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MotorcycleController_GetLeanSpringIntegrationCoefficientDecay(nint controller);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotorcycleController_SetLeanSpringIntegrationCoefficientDecay(nint controller, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_MotorcycleController_GetLeanSmoothingFactor(nint controller);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_MotorcycleController_SetLeanSmoothingFactor(nint controller, float value);
    #endregion

    #region TrackedVehicleController
    /* WheelTV */
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_WheelSettingsTV_Create();
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheelSettingsTV_GetLongitudinalFriction(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettingsTV_SetLongitudinalFriction(nint settings, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_WheelSettingsTV_GetLateralFriction(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_WheelSettingsTV_SetLateralFriction(nint settings, float value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_WheelTV_Create(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_WheelTV_GetSettings(nint wheel);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern nint JPH_TrackedVehicleControllerSettings_Create();

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_TrackedVehicleControllerSettings_GetEngine(nint settings, out JPH_VehicleEngineSettings result);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_TrackedVehicleControllerSettings_SetEngine(nint settings, in JPH_VehicleEngineSettings value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern /*JPH_VehicleTransmissionSettings**/nint JPH_TrackedVehicleControllerSettings_GetTransmission(nint settings);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_TrackedVehicleControllerSettings_SetTransmission(nint settings, nint value);

#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_TrackedVehicleController_SetDriverInput(nint controller, float forward, float leftRatio, float rightRatio, float brake);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_TrackedVehicleController_GetForwardInput(nint controller);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_TrackedVehicleController_SetForwardInput(nint controller, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_TrackedVehicleController_GetLeftRatio(nint controller);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_TrackedVehicleController_SetLeftRatio(nint controller, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_TrackedVehicleController_GetRightRatio(nint controller);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_TrackedVehicleController_SetRightRatio(nint controller, float value);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern float JPH_TrackedVehicleController_GetBrakeInput(nint controller);
#if __IOS__
    [DllImport("@rpath/joltc.framework/joltc", CallingConvention = CallingConvention.Cdecl)]
#else
    [DllImport("joltc", CallingConvention = CallingConvention.Cdecl)]
#endif
    public static extern void JPH_TrackedVehicleController_SetBrakeInput(nint controller, float value);
    #endregion

    sealed class UTF8EncodingRelaxed : UTF8Encoding
    {
        public static new readonly UTF8EncodingRelaxed Default = new UTF8EncodingRelaxed();

        private UTF8EncodingRelaxed() : base(false, false)
        {
        }
    }
}
