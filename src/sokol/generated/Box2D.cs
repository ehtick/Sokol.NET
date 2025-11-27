// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

namespace Sokol
{
public static unsafe partial class Box2D
{
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2SetAllocator", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2SetAllocator", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2SetAllocator(IntPtr allocFcn, IntPtr freeFcn);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2GetByteCount", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2GetByteCount", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2GetByteCount();

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2SetAssertFcn", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2SetAssertFcn", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2SetAssertFcn(IntPtr assertFcn);

[StructLayout(LayoutKind.Sequential)]
public struct b2Version
{
    public int major;
    public int minor;
    public int revision;
}
#if WEB
public static b2Version b2GetVersion()
{
    b2Version result = default;
    b2GetVersion_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2GetVersion", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2GetVersion", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Version b2GetVersion();
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2InternalAssertFcn", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2InternalAssertFcn", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2InternalAssertFcn([M(U.LPUTF8Str)] string condition, [M(U.LPUTF8Str)] string fileName, int lineNumber);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2GetTicks", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2GetTicks", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern ulong b2GetTicks();

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2GetMilliseconds", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2GetMilliseconds", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2GetMilliseconds(ulong ticks);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2GetMillisecondsAndReset", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2GetMillisecondsAndReset", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2GetMillisecondsAndReset(ref ulong ticks);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Yield", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Yield", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Yield();

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Hash", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Hash", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern uint b2Hash(uint hash, in byte data, int count);

[StructLayout(LayoutKind.Sequential)]
public struct b2Vec2
{
    public float x;
    public float y;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2CosSin
{
    public float cosine;
    public float sine;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2Rot
{
    public float c;
    public float s;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2Transform
{
    public b2Vec2 p;
    public b2Rot q;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2Mat22
{
    public b2Vec2 cx;
    public b2Vec2 cy;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2AABB
{
    public b2Vec2 lowerBound;
    public b2Vec2 upperBound;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2Plane
{
    public b2Vec2 normal;
    public float offset;
}
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2IsValidFloat", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2IsValidFloat", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2IsValidFloat(float a);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2IsValidVec2", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2IsValidVec2", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2IsValidVec2(b2Vec2 v);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2IsValidRotation", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2IsValidRotation", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2IsValidRotation(b2Rot q);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2IsValidTransform", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2IsValidTransform", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2IsValidTransform(b2Transform t);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2IsValidAABB", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2IsValidAABB", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2IsValidAABB(b2AABB aabb);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2IsValidPlane", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2IsValidPlane", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2IsValidPlane(b2Plane a);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Atan2", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Atan2", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2Atan2(float y, float x);

#if WEB
public static b2CosSin b2ComputeCosSin(float radians)
{
    b2CosSin result = default;
    b2ComputeCosSin_internal(ref result, radians);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputeCosSin", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputeCosSin", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2CosSin b2ComputeCosSin(float radians);
#endif

#if WEB
public static b2Rot b2ComputeRotationBetweenUnitVectors(b2Vec2 v1, b2Vec2 v2)
{
    b2Rot result = default;
    b2ComputeRotationBetweenUnitVectors_internal(ref result, v1, v2);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputeRotationBetweenUnitVectors", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputeRotationBetweenUnitVectors", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Rot b2ComputeRotationBetweenUnitVectors(b2Vec2 v1, b2Vec2 v2);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2SetLengthUnitsPerMeter", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2SetLengthUnitsPerMeter", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2SetLengthUnitsPerMeter(float lengthUnits);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2GetLengthUnitsPerMeter", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2GetLengthUnitsPerMeter", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2GetLengthUnitsPerMeter();

[StructLayout(LayoutKind.Sequential)]
public struct b2SimplexCache_
{
}
[StructLayout(LayoutKind.Sequential)]
public struct b2Hull_
{
}
[StructLayout(LayoutKind.Sequential)]
public struct b2RayCastInput
{
    public b2Vec2 origin;
    public b2Vec2 translation;
    public float maxFraction;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2ShapeProxy
{
    #pragma warning disable 169
    public struct pointsCollection
    {
        public ref b2Vec2 this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private b2Vec2 _item0;
        private b2Vec2 _item1;
        private b2Vec2 _item2;
        private b2Vec2 _item3;
        private b2Vec2 _item4;
        private b2Vec2 _item5;
        private b2Vec2 _item6;
        private b2Vec2 _item7;
    }
    #pragma warning restore 169
    public pointsCollection points;
    public int count;
    public float radius;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2ShapeCastInput
{
    public b2ShapeProxy proxy;
    public b2Vec2 translation;
    public float maxFraction;
#if WEB
    private byte _canEncroach;
    public bool canEncroach { get => _canEncroach != 0; set => _canEncroach = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool canEncroach;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct b2CastOutput
{
    public b2Vec2 normal;
    public b2Vec2 point;
    public float fraction;
    public int iterations;
#if WEB
    private byte _hit;
    public bool hit { get => _hit != 0; set => _hit = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool hit;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct b2MassData
{
    public float mass;
    public b2Vec2 center;
    public float rotationalInertia;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2Circle
{
    public b2Vec2 center;
    public float radius;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2Capsule
{
    public b2Vec2 center1;
    public b2Vec2 center2;
    public float radius;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2Polygon
{
    #pragma warning disable 169
    public struct verticesCollection
    {
        public ref b2Vec2 this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private b2Vec2 _item0;
        private b2Vec2 _item1;
        private b2Vec2 _item2;
        private b2Vec2 _item3;
        private b2Vec2 _item4;
        private b2Vec2 _item5;
        private b2Vec2 _item6;
        private b2Vec2 _item7;
    }
    #pragma warning restore 169
    public verticesCollection vertices;
    #pragma warning disable 169
    public struct normalsCollection
    {
        public ref b2Vec2 this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private b2Vec2 _item0;
        private b2Vec2 _item1;
        private b2Vec2 _item2;
        private b2Vec2 _item3;
        private b2Vec2 _item4;
        private b2Vec2 _item5;
        private b2Vec2 _item6;
        private b2Vec2 _item7;
    }
    #pragma warning restore 169
    public normalsCollection normals;
    public b2Vec2 centroid;
    public float radius;
    public int count;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2Segment
{
    public b2Vec2 point1;
    public b2Vec2 point2;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2ChainSegment
{
    public b2Vec2 ghost1;
    public b2Segment segment;
    public b2Vec2 ghost2;
    public int chainId;
}
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2IsValidRay", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2IsValidRay", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2IsValidRay(in b2RayCastInput input);

#if WEB
public static b2Polygon b2MakePolygon(in b2Hull hull, float radius)
{
    b2Polygon result = default;
    b2MakePolygon_internal(ref result, hull, radius);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakePolygon", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakePolygon", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Polygon b2MakePolygon(in b2Hull hull, float radius);
#endif

#if WEB
public static b2Polygon b2MakeOffsetPolygon(in b2Hull hull, b2Vec2 position, b2Rot rotation)
{
    b2Polygon result = default;
    b2MakeOffsetPolygon_internal(ref result, hull, position, rotation);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeOffsetPolygon", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeOffsetPolygon", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Polygon b2MakeOffsetPolygon(in b2Hull hull, b2Vec2 position, b2Rot rotation);
#endif

#if WEB
public static b2Polygon b2MakeOffsetRoundedPolygon(in b2Hull hull, b2Vec2 position, b2Rot rotation, float radius)
{
    b2Polygon result = default;
    b2MakeOffsetRoundedPolygon_internal(ref result, hull, position, rotation, radius);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeOffsetRoundedPolygon", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeOffsetRoundedPolygon", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Polygon b2MakeOffsetRoundedPolygon(in b2Hull hull, b2Vec2 position, b2Rot rotation, float radius);
#endif

#if WEB
public static b2Polygon b2MakeSquare(float halfWidth)
{
    b2Polygon result = default;
    b2MakeSquare_internal(ref result, halfWidth);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeSquare", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeSquare", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Polygon b2MakeSquare(float halfWidth);
#endif

#if WEB
public static b2Polygon b2MakeBox(float halfWidth, float halfHeight)
{
    b2Polygon result = default;
    b2MakeBox_internal(ref result, halfWidth, halfHeight);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeBox", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeBox", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Polygon b2MakeBox(float halfWidth, float halfHeight);
#endif

#if WEB
public static b2Polygon b2MakeRoundedBox(float halfWidth, float halfHeight, float radius)
{
    b2Polygon result = default;
    b2MakeRoundedBox_internal(ref result, halfWidth, halfHeight, radius);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeRoundedBox", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeRoundedBox", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Polygon b2MakeRoundedBox(float halfWidth, float halfHeight, float radius);
#endif

#if WEB
public static b2Polygon b2MakeOffsetBox(float halfWidth, float halfHeight, b2Vec2 center, b2Rot rotation)
{
    b2Polygon result = default;
    b2MakeOffsetBox_internal(ref result, halfWidth, halfHeight, center, rotation);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeOffsetBox", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeOffsetBox", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Polygon b2MakeOffsetBox(float halfWidth, float halfHeight, b2Vec2 center, b2Rot rotation);
#endif

#if WEB
public static b2Polygon b2MakeOffsetRoundedBox(float halfWidth, float halfHeight, b2Vec2 center, b2Rot rotation, float radius)
{
    b2Polygon result = default;
    b2MakeOffsetRoundedBox_internal(ref result, halfWidth, halfHeight, center, rotation, radius);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeOffsetRoundedBox", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeOffsetRoundedBox", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Polygon b2MakeOffsetRoundedBox(float halfWidth, float halfHeight, b2Vec2 center, b2Rot rotation, float radius);
#endif

#if WEB
public static b2Polygon b2TransformPolygon(b2Transform transform, in b2Polygon polygon)
{
    b2Polygon result = default;
    b2TransformPolygon_internal(ref result, transform, polygon);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2TransformPolygon", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2TransformPolygon", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Polygon b2TransformPolygon(b2Transform transform, in b2Polygon polygon);
#endif

#if WEB
public static b2MassData b2ComputeCircleMass(in b2Circle shape, float density)
{
    b2MassData result = default;
    b2ComputeCircleMass_internal(ref result, shape, density);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputeCircleMass", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputeCircleMass", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2MassData b2ComputeCircleMass(in b2Circle shape, float density);
#endif

#if WEB
public static b2MassData b2ComputeCapsuleMass(in b2Capsule shape, float density)
{
    b2MassData result = default;
    b2ComputeCapsuleMass_internal(ref result, shape, density);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputeCapsuleMass", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputeCapsuleMass", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2MassData b2ComputeCapsuleMass(in b2Capsule shape, float density);
#endif

#if WEB
public static b2MassData b2ComputePolygonMass(in b2Polygon shape, float density)
{
    b2MassData result = default;
    b2ComputePolygonMass_internal(ref result, shape, density);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputePolygonMass", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputePolygonMass", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2MassData b2ComputePolygonMass(in b2Polygon shape, float density);
#endif

#if WEB
public static b2AABB b2ComputeCircleAABB(in b2Circle shape, b2Transform transform)
{
    b2AABB result = default;
    b2ComputeCircleAABB_internal(ref result, shape, transform);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputeCircleAABB", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputeCircleAABB", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2AABB b2ComputeCircleAABB(in b2Circle shape, b2Transform transform);
#endif

#if WEB
public static b2AABB b2ComputeCapsuleAABB(in b2Capsule shape, b2Transform transform)
{
    b2AABB result = default;
    b2ComputeCapsuleAABB_internal(ref result, shape, transform);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputeCapsuleAABB", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputeCapsuleAABB", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2AABB b2ComputeCapsuleAABB(in b2Capsule shape, b2Transform transform);
#endif

#if WEB
public static b2AABB b2ComputePolygonAABB(in b2Polygon shape, b2Transform transform)
{
    b2AABB result = default;
    b2ComputePolygonAABB_internal(ref result, shape, transform);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputePolygonAABB", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputePolygonAABB", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2AABB b2ComputePolygonAABB(in b2Polygon shape, b2Transform transform);
#endif

#if WEB
public static b2AABB b2ComputeSegmentAABB(in b2Segment shape, b2Transform transform)
{
    b2AABB result = default;
    b2ComputeSegmentAABB_internal(ref result, shape, transform);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputeSegmentAABB", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputeSegmentAABB", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2AABB b2ComputeSegmentAABB(in b2Segment shape, b2Transform transform);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PointInCircle", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PointInCircle", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2PointInCircle(in b2Circle shape, b2Vec2 point);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PointInCapsule", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PointInCapsule", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2PointInCapsule(in b2Capsule shape, b2Vec2 point);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PointInPolygon", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PointInPolygon", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2PointInPolygon(in b2Polygon shape, b2Vec2 point);

#if WEB
public static b2CastOutput b2RayCastCircle(in b2Circle shape, in b2RayCastInput input)
{
    b2CastOutput result = default;
    b2RayCastCircle_internal(ref result, shape, input);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RayCastCircle", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RayCastCircle", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2CastOutput b2RayCastCircle(in b2Circle shape, in b2RayCastInput input);
#endif

#if WEB
public static b2CastOutput b2RayCastCapsule(in b2Capsule shape, in b2RayCastInput input)
{
    b2CastOutput result = default;
    b2RayCastCapsule_internal(ref result, shape, input);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RayCastCapsule", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RayCastCapsule", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2CastOutput b2RayCastCapsule(in b2Capsule shape, in b2RayCastInput input);
#endif

#if WEB
public static b2CastOutput b2RayCastSegment(in b2Segment shape, in b2RayCastInput input, bool oneSided)
{
    b2CastOutput result = default;
    b2RayCastSegment_internal(ref result, shape, input, oneSided);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RayCastSegment", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RayCastSegment", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2CastOutput b2RayCastSegment(in b2Segment shape, in b2RayCastInput input, bool oneSided);
#endif

#if WEB
public static b2CastOutput b2RayCastPolygon(in b2Polygon shape, in b2RayCastInput input)
{
    b2CastOutput result = default;
    b2RayCastPolygon_internal(ref result, shape, input);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RayCastPolygon", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RayCastPolygon", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2CastOutput b2RayCastPolygon(in b2Polygon shape, in b2RayCastInput input);
#endif

#if WEB
public static b2CastOutput b2ShapeCastCircle(in b2Circle shape, in b2ShapeCastInput input)
{
    b2CastOutput result = default;
    b2ShapeCastCircle_internal(ref result, shape, input);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ShapeCastCircle", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ShapeCastCircle", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2CastOutput b2ShapeCastCircle(in b2Circle shape, in b2ShapeCastInput input);
#endif

#if WEB
public static b2CastOutput b2ShapeCastCapsule(in b2Capsule shape, in b2ShapeCastInput input)
{
    b2CastOutput result = default;
    b2ShapeCastCapsule_internal(ref result, shape, input);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ShapeCastCapsule", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ShapeCastCapsule", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2CastOutput b2ShapeCastCapsule(in b2Capsule shape, in b2ShapeCastInput input);
#endif

#if WEB
public static b2CastOutput b2ShapeCastSegment(in b2Segment shape, in b2ShapeCastInput input)
{
    b2CastOutput result = default;
    b2ShapeCastSegment_internal(ref result, shape, input);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ShapeCastSegment", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ShapeCastSegment", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2CastOutput b2ShapeCastSegment(in b2Segment shape, in b2ShapeCastInput input);
#endif

#if WEB
public static b2CastOutput b2ShapeCastPolygon(in b2Polygon shape, in b2ShapeCastInput input)
{
    b2CastOutput result = default;
    b2ShapeCastPolygon_internal(ref result, shape, input);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ShapeCastPolygon", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ShapeCastPolygon", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2CastOutput b2ShapeCastPolygon(in b2Polygon shape, in b2ShapeCastInput input);
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2Hull
{
    #pragma warning disable 169
    public struct pointsCollection
    {
        public ref b2Vec2 this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private b2Vec2 _item0;
        private b2Vec2 _item1;
        private b2Vec2 _item2;
        private b2Vec2 _item3;
        private b2Vec2 _item4;
        private b2Vec2 _item5;
        private b2Vec2 _item6;
        private b2Vec2 _item7;
    }
    #pragma warning restore 169
    public pointsCollection points;
    public int count;
}
#if WEB
public static b2Hull b2ComputeHull(in b2Vec2 points, int count)
{
    b2Hull result = default;
    b2ComputeHull_internal(ref result, points, count);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputeHull", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputeHull", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Hull b2ComputeHull(in b2Vec2 points, int count);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ValidateHull", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ValidateHull", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2ValidateHull(in b2Hull hull);

[StructLayout(LayoutKind.Sequential)]
public struct b2SegmentDistanceResult
{
    public b2Vec2 closest1;
    public b2Vec2 closest2;
    public float fraction1;
    public float fraction2;
    public float distanceSquared;
}
#if WEB
public static b2SegmentDistanceResult b2SegmentDistance(b2Vec2 p1, b2Vec2 q1, b2Vec2 p2, b2Vec2 q2)
{
    b2SegmentDistanceResult result = default;
    b2SegmentDistance_internal(ref result, p1, q1, p2, q2);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2SegmentDistance", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2SegmentDistance", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2SegmentDistanceResult b2SegmentDistance(b2Vec2 p1, b2Vec2 q1, b2Vec2 p2, b2Vec2 q2);
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2SimplexCache
{
    public ushort count;
    #pragma warning disable 169
    public struct indexACollection
    {
        public ref byte this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 3)[index];
        private byte _item0;
        private byte _item1;
        private byte _item2;
    }
    #pragma warning restore 169
    public indexACollection indexA;
    #pragma warning disable 169
    public struct indexBCollection
    {
        public ref byte this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 3)[index];
        private byte _item0;
        private byte _item1;
        private byte _item2;
    }
    #pragma warning restore 169
    public indexBCollection indexB;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2DistanceInput
{
    public b2ShapeProxy proxyA;
    public b2ShapeProxy proxyB;
    public b2Transform transformA;
    public b2Transform transformB;
#if WEB
    private byte _useRadii;
    public bool useRadii { get => _useRadii != 0; set => _useRadii = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool useRadii;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct b2DistanceOutput
{
    public b2Vec2 pointA;
    public b2Vec2 pointB;
    public b2Vec2 normal;
    public float distance;
    public int iterations;
    public int simplexCount;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2SimplexVertex
{
    public b2Vec2 wA;
    public b2Vec2 wB;
    public b2Vec2 w;
    public float a;
    public int indexA;
    public int indexB;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2Simplex
{
    public b2SimplexVertex v1;
    public b2SimplexVertex v2;
    public b2SimplexVertex v3;
    public int count;
}
#if WEB
public static b2DistanceOutput b2ShapeDistance(in b2DistanceInput input, b2SimplexCache* cache, b2Simplex* simplexes, int simplexCapacity)
{
    b2DistanceOutput result = default;
    b2ShapeDistance_internal(ref result, input, cache, simplexes, simplexCapacity);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ShapeDistance", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ShapeDistance", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2DistanceOutput b2ShapeDistance(in b2DistanceInput input, b2SimplexCache* cache, b2Simplex* simplexes, int simplexCapacity);
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2ShapeCastPairInput
{
    public b2ShapeProxy proxyA;
    public b2ShapeProxy proxyB;
    public b2Transform transformA;
    public b2Transform transformB;
    public b2Vec2 translationB;
    public float maxFraction;
#if WEB
    private byte _canEncroach;
    public bool canEncroach { get => _canEncroach != 0; set => _canEncroach = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool canEncroach;
#endif
}
#if WEB
public static b2CastOutput b2ShapeCast(in b2ShapeCastPairInput input)
{
    b2CastOutput result = default;
    b2ShapeCast_internal(ref result, input);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ShapeCast", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ShapeCast", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2CastOutput b2ShapeCast(in b2ShapeCastPairInput input);
#endif

#if WEB
public static b2ShapeProxy b2MakeProxy(in b2Vec2 points, int count, float radius)
{
    b2ShapeProxy result = default;
    b2MakeProxy_internal(ref result, points, count, radius);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeProxy", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeProxy", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2ShapeProxy b2MakeProxy(in b2Vec2 points, int count, float radius);
#endif

#if WEB
public static b2ShapeProxy b2MakeOffsetProxy(in b2Vec2 points, int count, float radius, b2Vec2 position, b2Rot rotation)
{
    b2ShapeProxy result = default;
    b2MakeOffsetProxy_internal(ref result, points, count, radius, position, rotation);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeOffsetProxy", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeOffsetProxy", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2ShapeProxy b2MakeOffsetProxy(in b2Vec2 points, int count, float radius, b2Vec2 position, b2Rot rotation);
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2Sweep
{
    public b2Vec2 localCenter;
    public b2Vec2 c1;
    public b2Vec2 c2;
    public b2Rot q1;
    public b2Rot q2;
}
#if WEB
public static b2Transform b2GetSweepTransform(in b2Sweep sweep, float time)
{
    b2Transform result = default;
    b2GetSweepTransform_internal(ref result, sweep, time);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2GetSweepTransform", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2GetSweepTransform", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Transform b2GetSweepTransform(in b2Sweep sweep, float time);
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2TOIInput
{
    public b2ShapeProxy proxyA;
    public b2ShapeProxy proxyB;
    public b2Sweep sweepA;
    public b2Sweep sweepB;
    public float maxFraction;
}
public enum b2TOIState
{
    b2_toiStateUnknown,
    b2_toiStateFailed,
    b2_toiStateOverlapped,
    b2_toiStateHit,
    b2_toiStateSeparated,
}
[StructLayout(LayoutKind.Sequential)]
public struct b2TOIOutput
{
    public b2TOIState state;
    public b2Vec2 point;
    public b2Vec2 normal;
    public float fraction;
}
#if WEB
public static b2TOIOutput b2TimeOfImpact(in b2TOIInput input)
{
    b2TOIOutput result = default;
    b2TimeOfImpact_internal(ref result, input);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2TimeOfImpact", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2TimeOfImpact", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2TOIOutput b2TimeOfImpact(in b2TOIInput input);
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2ManifoldPoint
{
    public b2Vec2 point;
    public b2Vec2 anchorA;
    public b2Vec2 anchorB;
    public float separation;
    public float normalImpulse;
    public float tangentImpulse;
    public float totalNormalImpulse;
    public float normalVelocity;
    public ushort id;
#if WEB
    private byte _persisted;
    public bool persisted { get => _persisted != 0; set => _persisted = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool persisted;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct b2Manifold
{
    public b2Vec2 normal;
    public float rollingImpulse;
    #pragma warning disable 169
    public struct pointsCollection
    {
        public ref b2ManifoldPoint this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 2)[index];
        private b2ManifoldPoint _item0;
        private b2ManifoldPoint _item1;
    }
    #pragma warning restore 169
    public pointsCollection points;
    public int pointCount;
}
#if WEB
public static b2Manifold b2CollideCircles(in b2Circle circleA, b2Transform xfA, in b2Circle circleB, b2Transform xfB)
{
    b2Manifold result = default;
    b2CollideCircles_internal(ref result, circleA, xfA, circleB, xfB);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideCircles", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideCircles", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Manifold b2CollideCircles(in b2Circle circleA, b2Transform xfA, in b2Circle circleB, b2Transform xfB);
#endif

#if WEB
public static b2Manifold b2CollideCapsuleAndCircle(in b2Capsule capsuleA, b2Transform xfA, in b2Circle circleB, b2Transform xfB)
{
    b2Manifold result = default;
    b2CollideCapsuleAndCircle_internal(ref result, capsuleA, xfA, circleB, xfB);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideCapsuleAndCircle", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideCapsuleAndCircle", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Manifold b2CollideCapsuleAndCircle(in b2Capsule capsuleA, b2Transform xfA, in b2Circle circleB, b2Transform xfB);
#endif

#if WEB
public static b2Manifold b2CollideSegmentAndCircle(in b2Segment segmentA, b2Transform xfA, in b2Circle circleB, b2Transform xfB)
{
    b2Manifold result = default;
    b2CollideSegmentAndCircle_internal(ref result, segmentA, xfA, circleB, xfB);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideSegmentAndCircle", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideSegmentAndCircle", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Manifold b2CollideSegmentAndCircle(in b2Segment segmentA, b2Transform xfA, in b2Circle circleB, b2Transform xfB);
#endif

#if WEB
public static b2Manifold b2CollidePolygonAndCircle(in b2Polygon polygonA, b2Transform xfA, in b2Circle circleB, b2Transform xfB)
{
    b2Manifold result = default;
    b2CollidePolygonAndCircle_internal(ref result, polygonA, xfA, circleB, xfB);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollidePolygonAndCircle", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollidePolygonAndCircle", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Manifold b2CollidePolygonAndCircle(in b2Polygon polygonA, b2Transform xfA, in b2Circle circleB, b2Transform xfB);
#endif

#if WEB
public static b2Manifold b2CollideCapsules(in b2Capsule capsuleA, b2Transform xfA, in b2Capsule capsuleB, b2Transform xfB)
{
    b2Manifold result = default;
    b2CollideCapsules_internal(ref result, capsuleA, xfA, capsuleB, xfB);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideCapsules", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideCapsules", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Manifold b2CollideCapsules(in b2Capsule capsuleA, b2Transform xfA, in b2Capsule capsuleB, b2Transform xfB);
#endif

#if WEB
public static b2Manifold b2CollideSegmentAndCapsule(in b2Segment segmentA, b2Transform xfA, in b2Capsule capsuleB, b2Transform xfB)
{
    b2Manifold result = default;
    b2CollideSegmentAndCapsule_internal(ref result, segmentA, xfA, capsuleB, xfB);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideSegmentAndCapsule", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideSegmentAndCapsule", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Manifold b2CollideSegmentAndCapsule(in b2Segment segmentA, b2Transform xfA, in b2Capsule capsuleB, b2Transform xfB);
#endif

#if WEB
public static b2Manifold b2CollidePolygonAndCapsule(in b2Polygon polygonA, b2Transform xfA, in b2Capsule capsuleB, b2Transform xfB)
{
    b2Manifold result = default;
    b2CollidePolygonAndCapsule_internal(ref result, polygonA, xfA, capsuleB, xfB);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollidePolygonAndCapsule", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollidePolygonAndCapsule", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Manifold b2CollidePolygonAndCapsule(in b2Polygon polygonA, b2Transform xfA, in b2Capsule capsuleB, b2Transform xfB);
#endif

#if WEB
public static b2Manifold b2CollidePolygons(in b2Polygon polygonA, b2Transform xfA, in b2Polygon polygonB, b2Transform xfB)
{
    b2Manifold result = default;
    b2CollidePolygons_internal(ref result, polygonA, xfA, polygonB, xfB);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollidePolygons", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollidePolygons", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Manifold b2CollidePolygons(in b2Polygon polygonA, b2Transform xfA, in b2Polygon polygonB, b2Transform xfB);
#endif

#if WEB
public static b2Manifold b2CollideSegmentAndPolygon(in b2Segment segmentA, b2Transform xfA, in b2Polygon polygonB, b2Transform xfB)
{
    b2Manifold result = default;
    b2CollideSegmentAndPolygon_internal(ref result, segmentA, xfA, polygonB, xfB);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideSegmentAndPolygon", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideSegmentAndPolygon", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Manifold b2CollideSegmentAndPolygon(in b2Segment segmentA, b2Transform xfA, in b2Polygon polygonB, b2Transform xfB);
#endif

#if WEB
public static b2Manifold b2CollideChainSegmentAndCircle(in b2ChainSegment segmentA, b2Transform xfA, in b2Circle circleB, b2Transform xfB)
{
    b2Manifold result = default;
    b2CollideChainSegmentAndCircle_internal(ref result, segmentA, xfA, circleB, xfB);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideChainSegmentAndCircle", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideChainSegmentAndCircle", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Manifold b2CollideChainSegmentAndCircle(in b2ChainSegment segmentA, b2Transform xfA, in b2Circle circleB, b2Transform xfB);
#endif

#if WEB
public static b2Manifold b2CollideChainSegmentAndCapsule(in b2ChainSegment segmentA, b2Transform xfA, in b2Capsule capsuleB, b2Transform xfB, b2SimplexCache* cache)
{
    b2Manifold result = default;
    b2CollideChainSegmentAndCapsule_internal(ref result, segmentA, xfA, capsuleB, xfB, cache);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideChainSegmentAndCapsule", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideChainSegmentAndCapsule", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Manifold b2CollideChainSegmentAndCapsule(in b2ChainSegment segmentA, b2Transform xfA, in b2Capsule capsuleB, b2Transform xfB, b2SimplexCache* cache);
#endif

#if WEB
public static b2Manifold b2CollideChainSegmentAndPolygon(in b2ChainSegment segmentA, b2Transform xfA, in b2Polygon polygonB, b2Transform xfB, b2SimplexCache* cache)
{
    b2Manifold result = default;
    b2CollideChainSegmentAndPolygon_internal(ref result, segmentA, xfA, polygonB, xfB, cache);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideChainSegmentAndPolygon", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideChainSegmentAndPolygon", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Manifold b2CollideChainSegmentAndPolygon(in b2ChainSegment segmentA, b2Transform xfA, in b2Polygon polygonB, b2Transform xfB, b2SimplexCache* cache);
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2DynamicTree
{
// FIXME: nodes: struct b2TreeNode *;
    public int root;
    public int nodeCount;
    public int nodeCapacity;
    public int freeList;
    public int proxyCount;
// FIXME: leafIndices: int *;
    public b2AABB* leafBoxes;
    public b2Vec2* leafCenters;
// FIXME: binIndices: int *;
    public int rebuildCapacity;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2TreeStats
{
    public int nodeVisits;
    public int leafVisits;
}
#if WEB
public static b2DynamicTree b2DynamicTree_Create()
{
    b2DynamicTree result = default;
    b2DynamicTree_Create_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_Create", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_Create", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2DynamicTree b2DynamicTree_Create();
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_Destroy", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_Destroy", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DynamicTree_Destroy(b2DynamicTree* tree);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_CreateProxy", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_CreateProxy", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2DynamicTree_CreateProxy(b2DynamicTree* tree, b2AABB aabb, ulong categoryBits, ulong userData);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_DestroyProxy", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_DestroyProxy", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DynamicTree_DestroyProxy(b2DynamicTree* tree, int proxyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_MoveProxy", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_MoveProxy", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DynamicTree_MoveProxy(b2DynamicTree* tree, int proxyId, b2AABB aabb);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_EnlargeProxy", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_EnlargeProxy", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DynamicTree_EnlargeProxy(b2DynamicTree* tree, int proxyId, b2AABB aabb);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_SetCategoryBits", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_SetCategoryBits", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DynamicTree_SetCategoryBits(b2DynamicTree* tree, int proxyId, ulong categoryBits);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_GetCategoryBits", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_GetCategoryBits", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern ulong b2DynamicTree_GetCategoryBits(b2DynamicTree* tree, int proxyId);

#if WEB
public static b2TreeStats b2DynamicTree_Query(in b2DynamicTree tree, b2AABB aabb, ulong maskBits, IntPtr callback, void* context)
{
    b2TreeStats result = default;
    b2DynamicTree_Query_internal(ref result, tree, aabb, maskBits, callback, context);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_Query", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_Query", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2TreeStats b2DynamicTree_Query(in b2DynamicTree tree, b2AABB aabb, ulong maskBits, IntPtr callback, void* context);
#endif

#if WEB
public static b2TreeStats b2DynamicTree_QueryAll(in b2DynamicTree tree, b2AABB aabb, IntPtr callback, void* context)
{
    b2TreeStats result = default;
    b2DynamicTree_QueryAll_internal(ref result, tree, aabb, callback, context);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_QueryAll", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_QueryAll", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2TreeStats b2DynamicTree_QueryAll(in b2DynamicTree tree, b2AABB aabb, IntPtr callback, void* context);
#endif

#if WEB
public static b2TreeStats b2DynamicTree_RayCast(in b2DynamicTree tree, in b2RayCastInput input, ulong maskBits, IntPtr callback, void* context)
{
    b2TreeStats result = default;
    b2DynamicTree_RayCast_internal(ref result, tree, input, maskBits, callback, context);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_RayCast", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_RayCast", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2TreeStats b2DynamicTree_RayCast(in b2DynamicTree tree, in b2RayCastInput input, ulong maskBits, IntPtr callback, void* context);
#endif

#if WEB
public static b2TreeStats b2DynamicTree_ShapeCast(in b2DynamicTree tree, in b2ShapeCastInput input, ulong maskBits, IntPtr callback, void* context)
{
    b2TreeStats result = default;
    b2DynamicTree_ShapeCast_internal(ref result, tree, input, maskBits, callback, context);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_ShapeCast", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_ShapeCast", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2TreeStats b2DynamicTree_ShapeCast(in b2DynamicTree tree, in b2ShapeCastInput input, ulong maskBits, IntPtr callback, void* context);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_GetHeight", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_GetHeight", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2DynamicTree_GetHeight(in b2DynamicTree tree);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_GetAreaRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_GetAreaRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2DynamicTree_GetAreaRatio(in b2DynamicTree tree);

#if WEB
public static b2AABB b2DynamicTree_GetRootBounds(in b2DynamicTree tree)
{
    b2AABB result = default;
    b2DynamicTree_GetRootBounds_internal(ref result, tree);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_GetRootBounds", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_GetRootBounds", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2AABB b2DynamicTree_GetRootBounds(in b2DynamicTree tree);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_GetProxyCount", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_GetProxyCount", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2DynamicTree_GetProxyCount(in b2DynamicTree tree);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_Rebuild", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_Rebuild", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2DynamicTree_Rebuild(b2DynamicTree* tree, bool fullBuild);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_GetByteCount", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_GetByteCount", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2DynamicTree_GetByteCount(in b2DynamicTree tree);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_GetUserData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_GetUserData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern ulong b2DynamicTree_GetUserData(in b2DynamicTree tree, int proxyId);

#if WEB
public static b2AABB b2DynamicTree_GetAABB(in b2DynamicTree tree, int proxyId)
{
    b2AABB result = default;
    b2DynamicTree_GetAABB_internal(ref result, tree, proxyId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_GetAABB", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_GetAABB", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2AABB b2DynamicTree_GetAABB(in b2DynamicTree tree, int proxyId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_Validate", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_Validate", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DynamicTree_Validate(in b2DynamicTree tree);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_ValidateNoEnlarged", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_ValidateNoEnlarged", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DynamicTree_ValidateNoEnlarged(in b2DynamicTree tree);

[StructLayout(LayoutKind.Sequential)]
public struct b2PlaneResult
{
    public b2Plane plane;
    public b2Vec2 point;
#if WEB
    private byte _hit;
    public bool hit { get => _hit != 0; set => _hit = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool hit;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct b2CollisionPlane
{
    public b2Plane plane;
    public float pushLimit;
    public float push;
#if WEB
    private byte _clipVelocity;
    public bool clipVelocity { get => _clipVelocity != 0; set => _clipVelocity = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool clipVelocity;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct b2PlaneSolverResult
{
    public b2Vec2 translation;
    public int iterationCount;
}
#if WEB
public static b2PlaneSolverResult b2SolvePlanes(b2Vec2 targetDelta, b2CollisionPlane* planes, int count)
{
    b2PlaneSolverResult result = default;
    b2SolvePlanes_internal(ref result, targetDelta, planes, count);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2SolvePlanes", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2SolvePlanes", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2PlaneSolverResult b2SolvePlanes(b2Vec2 targetDelta, b2CollisionPlane* planes, int count);
#endif

#if WEB
public static b2Vec2 b2ClipVector(b2Vec2 vector, in b2CollisionPlane planes, int count)
{
    b2Vec2 result = default;
    b2ClipVector_internal(ref result, vector, planes, count);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ClipVector", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ClipVector", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Vec2 b2ClipVector(b2Vec2 vector, in b2CollisionPlane planes, int count);
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2WorldId
{
    public ushort index1;
    public ushort generation;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2BodyId
{
    public int index1;
    public ushort world0;
    public ushort generation;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2ShapeId
{
    public int index1;
    public ushort world0;
    public ushort generation;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2ChainId
{
    public int index1;
    public ushort world0;
    public ushort generation;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2JointId
{
    public int index1;
    public ushort world0;
    public ushort generation;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2ContactId
{
    public int index1;
    public ushort world0;
    public short padding;
    public uint generation;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2RayResult
{
    public b2ShapeId shapeId;
    public b2Vec2 point;
    public b2Vec2 normal;
    public float fraction;
    public int nodeVisits;
    public int leafVisits;
#if WEB
    private byte _hit;
    public bool hit { get => _hit != 0; set => _hit = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool hit;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct b2WorldDef
{
    public b2Vec2 gravity;
    public float restitutionThreshold;
    public float hitEventThreshold;
    public float contactHertz;
    public float contactDampingRatio;
    public float contactSpeed;
    public float maximumLinearSpeed;
// FIXME: frictionCallback: b2FrictionCallback *;
// FIXME: restitutionCallback: b2RestitutionCallback *;
#if WEB
    private byte _enableSleep;
    public bool enableSleep { get => _enableSleep != 0; set => _enableSleep = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableSleep;
#endif
#if WEB
    private byte _enableContinuous;
    public bool enableContinuous { get => _enableContinuous != 0; set => _enableContinuous = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableContinuous;
#endif
#if WEB
    private byte _enableContactSoftening;
    public bool enableContactSoftening { get => _enableContactSoftening != 0; set => _enableContactSoftening = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableContactSoftening;
#endif
    public int workerCount;
// FIXME: enqueueTask: b2EnqueueTaskCallback *;
// FIXME: finishTask: b2FinishTaskCallback *;
    public void* userTaskContext;
    public void* userData;
    public int internalValue;
}
#if WEB
public static b2WorldDef b2DefaultWorldDef()
{
    b2WorldDef result = default;
    b2DefaultWorldDef_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultWorldDef", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultWorldDef", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2WorldDef b2DefaultWorldDef();
#endif

public enum b2BodyType
{
    b2_staticBody = 0,
    b2_kinematicBody = 1,
    b2_dynamicBody = 2,
    b2_bodyTypeCount,
}
[StructLayout(LayoutKind.Sequential)]
public struct b2MotionLocks
{
#if WEB
    private byte _linearX;
    public bool linearX { get => _linearX != 0; set => _linearX = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool linearX;
#endif
#if WEB
    private byte _linearY;
    public bool linearY { get => _linearY != 0; set => _linearY = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool linearY;
#endif
#if WEB
    private byte _angularZ;
    public bool angularZ { get => _angularZ != 0; set => _angularZ = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool angularZ;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct b2BodyDef
{
    public b2BodyType type;
    public b2Vec2 position;
    public b2Rot rotation;
    public b2Vec2 linearVelocity;
    public float angularVelocity;
    public float linearDamping;
    public float angularDamping;
    public float gravityScale;
    public float sleepThreshold;
#if WEB
    private IntPtr _name;
    public string name { get => Marshal.PtrToStringAnsi(_name);  set { if (_name != IntPtr.Zero) { Marshal.FreeHGlobal(_name); _name = IntPtr.Zero; } if (value != null) { _name = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string name;
#endif
    public void* userData;
    public b2MotionLocks motionLocks;
#if WEB
    private byte _enableSleep;
    public bool enableSleep { get => _enableSleep != 0; set => _enableSleep = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableSleep;
#endif
#if WEB
    private byte _isAwake;
    public bool isAwake { get => _isAwake != 0; set => _isAwake = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool isAwake;
#endif
#if WEB
    private byte _isBullet;
    public bool isBullet { get => _isBullet != 0; set => _isBullet = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool isBullet;
#endif
#if WEB
    private byte _isEnabled;
    public bool isEnabled { get => _isEnabled != 0; set => _isEnabled = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool isEnabled;
#endif
#if WEB
    private byte _allowFastRotation;
    public bool allowFastRotation { get => _allowFastRotation != 0; set => _allowFastRotation = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool allowFastRotation;
#endif
    public int internalValue;
}
#if WEB
public static b2BodyDef b2DefaultBodyDef()
{
    b2BodyDef result = default;
    b2DefaultBodyDef_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultBodyDef", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultBodyDef", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2BodyDef b2DefaultBodyDef();
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2Filter
{
    public ulong categoryBits;
    public ulong maskBits;
    public int groupIndex;
}
#if WEB
public static b2Filter b2DefaultFilter()
{
    b2Filter result = default;
    b2DefaultFilter_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultFilter", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultFilter", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Filter b2DefaultFilter();
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2QueryFilter
{
    public ulong categoryBits;
    public ulong maskBits;
}
#if WEB
public static b2QueryFilter b2DefaultQueryFilter()
{
    b2QueryFilter result = default;
    b2DefaultQueryFilter_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultQueryFilter", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultQueryFilter", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2QueryFilter b2DefaultQueryFilter();
#endif

public enum b2ShapeType
{
    b2_circleShape,
    b2_capsuleShape,
    b2_segmentShape,
    b2_polygonShape,
    b2_chainSegmentShape,
    b2_shapeTypeCount,
}
[StructLayout(LayoutKind.Sequential)]
public struct b2SurfaceMaterial
{
    public float friction;
    public float restitution;
    public float rollingResistance;
    public float tangentSpeed;
    public ulong userMaterialId;
    public uint customColor;
}
#if WEB
public static b2SurfaceMaterial b2DefaultSurfaceMaterial()
{
    b2SurfaceMaterial result = default;
    b2DefaultSurfaceMaterial_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultSurfaceMaterial", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultSurfaceMaterial", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2SurfaceMaterial b2DefaultSurfaceMaterial();
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2ShapeDef
{
    public void* userData;
    public b2SurfaceMaterial material;
    public float density;
    public b2Filter filter;
#if WEB
    private byte _enableCustomFiltering;
    public bool enableCustomFiltering { get => _enableCustomFiltering != 0; set => _enableCustomFiltering = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableCustomFiltering;
#endif
#if WEB
    private byte _isSensor;
    public bool isSensor { get => _isSensor != 0; set => _isSensor = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool isSensor;
#endif
#if WEB
    private byte _enableSensorEvents;
    public bool enableSensorEvents { get => _enableSensorEvents != 0; set => _enableSensorEvents = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableSensorEvents;
#endif
#if WEB
    private byte _enableContactEvents;
    public bool enableContactEvents { get => _enableContactEvents != 0; set => _enableContactEvents = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableContactEvents;
#endif
#if WEB
    private byte _enableHitEvents;
    public bool enableHitEvents { get => _enableHitEvents != 0; set => _enableHitEvents = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableHitEvents;
#endif
#if WEB
    private byte _enablePreSolveEvents;
    public bool enablePreSolveEvents { get => _enablePreSolveEvents != 0; set => _enablePreSolveEvents = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enablePreSolveEvents;
#endif
#if WEB
    private byte _invokeContactCreation;
    public bool invokeContactCreation { get => _invokeContactCreation != 0; set => _invokeContactCreation = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool invokeContactCreation;
#endif
#if WEB
    private byte _updateBodyMass;
    public bool updateBodyMass { get => _updateBodyMass != 0; set => _updateBodyMass = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool updateBodyMass;
#endif
    public int internalValue;
}
#if WEB
public static b2ShapeDef b2DefaultShapeDef()
{
    b2ShapeDef result = default;
    b2DefaultShapeDef_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultShapeDef", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultShapeDef", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2ShapeDef b2DefaultShapeDef();
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2ChainDef
{
    public void* userData;
// FIXME: points: const b2Vec2 *;
    public int count;
// FIXME: materials: const b2SurfaceMaterial *;
    public int materialCount;
    public b2Filter filter;
#if WEB
    private byte _isLoop;
    public bool isLoop { get => _isLoop != 0; set => _isLoop = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool isLoop;
#endif
#if WEB
    private byte _enableSensorEvents;
    public bool enableSensorEvents { get => _enableSensorEvents != 0; set => _enableSensorEvents = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableSensorEvents;
#endif
    public int internalValue;
}
#if WEB
public static b2ChainDef b2DefaultChainDef()
{
    b2ChainDef result = default;
    b2DefaultChainDef_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultChainDef", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultChainDef", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2ChainDef b2DefaultChainDef();
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2Profile
{
    public float step;
    public float pairs;
    public float collide;
    public float solve;
    public float prepareStages;
    public float solveConstraints;
    public float prepareConstraints;
    public float integrateVelocities;
    public float warmStart;
    public float solveImpulses;
    public float integratePositions;
    public float relaxImpulses;
    public float applyRestitution;
    public float storeImpulses;
    public float splitIslands;
    public float transforms;
    public float sensorHits;
    public float jointEvents;
    public float hitEvents;
    public float refit;
    public float bullets;
    public float sleepIslands;
    public float sensors;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2Counters
{
    public int bodyCount;
    public int shapeCount;
    public int contactCount;
    public int jointCount;
    public int islandCount;
    public int stackUsed;
    public int staticTreeHeight;
    public int treeHeight;
    public int byteCount;
    public int taskCount;
    #pragma warning disable 169
    public struct colorCountsCollection
    {
        public ref int this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 24)[index];
        private int _item0;
        private int _item1;
        private int _item2;
        private int _item3;
        private int _item4;
        private int _item5;
        private int _item6;
        private int _item7;
        private int _item8;
        private int _item9;
        private int _item10;
        private int _item11;
        private int _item12;
        private int _item13;
        private int _item14;
        private int _item15;
        private int _item16;
        private int _item17;
        private int _item18;
        private int _item19;
        private int _item20;
        private int _item21;
        private int _item22;
        private int _item23;
    }
    #pragma warning restore 169
    public colorCountsCollection colorCounts;
}
public enum b2JointType
{
    b2_distanceJoint,
    b2_filterJoint,
    b2_motorJoint,
    b2_prismaticJoint,
    b2_revoluteJoint,
    b2_weldJoint,
    b2_wheelJoint,
}
[StructLayout(LayoutKind.Sequential)]
public struct b2JointDef
{
    public void* userData;
    public b2BodyId bodyIdA;
    public b2BodyId bodyIdB;
    public b2Transform localFrameA;
    public b2Transform localFrameB;
    public float forceThreshold;
    public float torqueThreshold;
    public float constraintHertz;
    public float constraintDampingRatio;
    public float drawScale;
#if WEB
    private byte _collideConnected;
    public bool collideConnected { get => _collideConnected != 0; set => _collideConnected = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool collideConnected;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct b2DistanceJointDef
{
    public b2JointDef _base;
    public float length;
#if WEB
    private byte _enableSpring;
    public bool enableSpring { get => _enableSpring != 0; set => _enableSpring = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableSpring;
#endif
    public float lowerSpringForce;
    public float upperSpringForce;
    public float hertz;
    public float dampingRatio;
#if WEB
    private byte _enableLimit;
    public bool enableLimit { get => _enableLimit != 0; set => _enableLimit = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableLimit;
#endif
    public float minLength;
    public float maxLength;
#if WEB
    private byte _enableMotor;
    public bool enableMotor { get => _enableMotor != 0; set => _enableMotor = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableMotor;
#endif
    public float maxMotorForce;
    public float motorSpeed;
    public int internalValue;
}
#if WEB
public static b2DistanceJointDef b2DefaultDistanceJointDef()
{
    b2DistanceJointDef result = default;
    b2DefaultDistanceJointDef_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultDistanceJointDef", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultDistanceJointDef", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2DistanceJointDef b2DefaultDistanceJointDef();
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2MotorJointDef
{
    public b2JointDef _base;
    public b2Vec2 linearVelocity;
    public float maxVelocityForce;
    public float angularVelocity;
    public float maxVelocityTorque;
    public float linearHertz;
    public float linearDampingRatio;
    public float maxSpringForce;
    public float angularHertz;
    public float angularDampingRatio;
    public float maxSpringTorque;
    public int internalValue;
}
#if WEB
public static b2MotorJointDef b2DefaultMotorJointDef()
{
    b2MotorJointDef result = default;
    b2DefaultMotorJointDef_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultMotorJointDef", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultMotorJointDef", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2MotorJointDef b2DefaultMotorJointDef();
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2FilterJointDef
{
    public b2JointDef _base;
    public int internalValue;
}
#if WEB
public static b2FilterJointDef b2DefaultFilterJointDef()
{
    b2FilterJointDef result = default;
    b2DefaultFilterJointDef_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultFilterJointDef", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultFilterJointDef", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2FilterJointDef b2DefaultFilterJointDef();
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2PrismaticJointDef
{
    public b2JointDef _base;
#if WEB
    private byte _enableSpring;
    public bool enableSpring { get => _enableSpring != 0; set => _enableSpring = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableSpring;
#endif
    public float hertz;
    public float dampingRatio;
    public float targetTranslation;
#if WEB
    private byte _enableLimit;
    public bool enableLimit { get => _enableLimit != 0; set => _enableLimit = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableLimit;
#endif
    public float lowerTranslation;
    public float upperTranslation;
#if WEB
    private byte _enableMotor;
    public bool enableMotor { get => _enableMotor != 0; set => _enableMotor = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableMotor;
#endif
    public float maxMotorForce;
    public float motorSpeed;
    public int internalValue;
}
#if WEB
public static b2PrismaticJointDef b2DefaultPrismaticJointDef()
{
    b2PrismaticJointDef result = default;
    b2DefaultPrismaticJointDef_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultPrismaticJointDef", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultPrismaticJointDef", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2PrismaticJointDef b2DefaultPrismaticJointDef();
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2RevoluteJointDef
{
    public b2JointDef _base;
    public float targetAngle;
#if WEB
    private byte _enableSpring;
    public bool enableSpring { get => _enableSpring != 0; set => _enableSpring = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableSpring;
#endif
    public float hertz;
    public float dampingRatio;
#if WEB
    private byte _enableLimit;
    public bool enableLimit { get => _enableLimit != 0; set => _enableLimit = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableLimit;
#endif
    public float lowerAngle;
    public float upperAngle;
#if WEB
    private byte _enableMotor;
    public bool enableMotor { get => _enableMotor != 0; set => _enableMotor = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableMotor;
#endif
    public float maxMotorTorque;
    public float motorSpeed;
    public int internalValue;
}
#if WEB
public static b2RevoluteJointDef b2DefaultRevoluteJointDef()
{
    b2RevoluteJointDef result = default;
    b2DefaultRevoluteJointDef_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultRevoluteJointDef", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultRevoluteJointDef", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2RevoluteJointDef b2DefaultRevoluteJointDef();
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2WeldJointDef
{
    public b2JointDef _base;
    public float linearHertz;
    public float angularHertz;
    public float linearDampingRatio;
    public float angularDampingRatio;
    public int internalValue;
}
#if WEB
public static b2WeldJointDef b2DefaultWeldJointDef()
{
    b2WeldJointDef result = default;
    b2DefaultWeldJointDef_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultWeldJointDef", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultWeldJointDef", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2WeldJointDef b2DefaultWeldJointDef();
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2WheelJointDef
{
    public b2JointDef _base;
#if WEB
    private byte _enableSpring;
    public bool enableSpring { get => _enableSpring != 0; set => _enableSpring = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableSpring;
#endif
    public float hertz;
    public float dampingRatio;
#if WEB
    private byte _enableLimit;
    public bool enableLimit { get => _enableLimit != 0; set => _enableLimit = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableLimit;
#endif
    public float lowerTranslation;
    public float upperTranslation;
#if WEB
    private byte _enableMotor;
    public bool enableMotor { get => _enableMotor != 0; set => _enableMotor = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enableMotor;
#endif
    public float maxMotorTorque;
    public float motorSpeed;
    public int internalValue;
}
#if WEB
public static b2WheelJointDef b2DefaultWheelJointDef()
{
    b2WheelJointDef result = default;
    b2DefaultWheelJointDef_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultWheelJointDef", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultWheelJointDef", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2WheelJointDef b2DefaultWheelJointDef();
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2ExplosionDef
{
    public ulong maskBits;
    public b2Vec2 position;
    public float radius;
    public float falloff;
    public float impulsePerLength;
}
#if WEB
public static b2ExplosionDef b2DefaultExplosionDef()
{
    b2ExplosionDef result = default;
    b2DefaultExplosionDef_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultExplosionDef", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultExplosionDef", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2ExplosionDef b2DefaultExplosionDef();
#endif

[StructLayout(LayoutKind.Sequential)]
public struct b2SensorBeginTouchEvent
{
    public b2ShapeId sensorShapeId;
    public b2ShapeId visitorShapeId;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2SensorEndTouchEvent
{
    public b2ShapeId sensorShapeId;
    public b2ShapeId visitorShapeId;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2SensorEvents
{
    public b2SensorBeginTouchEvent* beginEvents;
    public b2SensorEndTouchEvent* endEvents;
    public int beginCount;
    public int endCount;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2ContactBeginTouchEvent
{
    public b2ShapeId shapeIdA;
    public b2ShapeId shapeIdB;
    public b2ContactId contactId;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2ContactEndTouchEvent
{
    public b2ShapeId shapeIdA;
    public b2ShapeId shapeIdB;
    public b2ContactId contactId;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2ContactHitEvent
{
    public b2ShapeId shapeIdA;
    public b2ShapeId shapeIdB;
    public b2Vec2 point;
    public b2Vec2 normal;
    public float approachSpeed;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2ContactEvents
{
    public b2ContactBeginTouchEvent* beginEvents;
    public b2ContactEndTouchEvent* endEvents;
    public b2ContactHitEvent* hitEvents;
    public int beginCount;
    public int endCount;
    public int hitCount;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2BodyMoveEvent
{
    public void* userData;
    public b2Transform transform;
    public b2BodyId bodyId;
#if WEB
    private byte _fellAsleep;
    public bool fellAsleep { get => _fellAsleep != 0; set => _fellAsleep = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool fellAsleep;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct b2BodyEvents
{
    public b2BodyMoveEvent* moveEvents;
    public int moveCount;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2JointEvent
{
    public b2JointId jointId;
    public void* userData;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2JointEvents
{
    public b2JointEvent* jointEvents;
    public int count;
}
[StructLayout(LayoutKind.Sequential)]
public struct b2ContactData
{
    public b2ContactId contactId;
    public b2ShapeId shapeIdA;
    public b2ShapeId shapeIdB;
    public b2Manifold manifold;
}
public enum b2HexColor
{
    b2_colorAliceBlue = 15792383,
    b2_colorAntiqueWhite = 16444375,
    b2_colorAqua = 65535,
    b2_colorAquamarine = 8388564,
    b2_colorAzure = 15794175,
    b2_colorBeige = 16119260,
    b2_colorBisque = 16770244,
    b2_colorBlack = 0,
    b2_colorBlanchedAlmond = 16772045,
    b2_colorBlue = 255,
    b2_colorBlueViolet = 9055202,
    b2_colorBrown = 10824234,
    b2_colorBurlywood = 14596231,
    b2_colorCadetBlue = 6266528,
    b2_colorChartreuse = 8388352,
    b2_colorChocolate = 13789470,
    b2_colorCoral = 16744272,
    b2_colorCornflowerBlue = 6591981,
    b2_colorCornsilk = 16775388,
    b2_colorCrimson = 14423100,
    b2_colorCyan = 65535,
    b2_colorDarkBlue = 139,
    b2_colorDarkCyan = 35723,
    b2_colorDarkGoldenRod = 12092939,
    b2_colorDarkGray = 11119017,
    b2_colorDarkGreen = 25600,
    b2_colorDarkKhaki = 12433259,
    b2_colorDarkMagenta = 9109643,
    b2_colorDarkOliveGreen = 5597999,
    b2_colorDarkOrange = 16747520,
    b2_colorDarkOrchid = 10040012,
    b2_colorDarkRed = 9109504,
    b2_colorDarkSalmon = 15308410,
    b2_colorDarkSeaGreen = 9419919,
    b2_colorDarkSlateBlue = 4734347,
    b2_colorDarkSlateGray = 3100495,
    b2_colorDarkTurquoise = 52945,
    b2_colorDarkViolet = 9699539,
    b2_colorDeepPink = 16716947,
    b2_colorDeepSkyBlue = 49151,
    b2_colorDimGray = 6908265,
    b2_colorDodgerBlue = 2003199,
    b2_colorFireBrick = 11674146,
    b2_colorFloralWhite = 16775920,
    b2_colorForestGreen = 2263842,
    b2_colorFuchsia = 16711935,
    b2_colorGainsboro = 14474460,
    b2_colorGhostWhite = 16316671,
    b2_colorGold = 16766720,
    b2_colorGoldenRod = 14329120,
    b2_colorGray = 8421504,
    b2_colorGreen = 32768,
    b2_colorGreenYellow = 11403055,
    b2_colorHoneyDew = 15794160,
    b2_colorHotPink = 16738740,
    b2_colorIndianRed = 13458524,
    b2_colorIndigo = 4915330,
    b2_colorIvory = 16777200,
    b2_colorKhaki = 15787660,
    b2_colorLavender = 15132410,
    b2_colorLavenderBlush = 16773365,
    b2_colorLawnGreen = 8190976,
    b2_colorLemonChiffon = 16775885,
    b2_colorLightBlue = 11393254,
    b2_colorLightCoral = 15761536,
    b2_colorLightCyan = 14745599,
    b2_colorLightGoldenRodYellow = 16448210,
    b2_colorLightGray = 13882323,
    b2_colorLightGreen = 9498256,
    b2_colorLightPink = 16758465,
    b2_colorLightSalmon = 16752762,
    b2_colorLightSeaGreen = 2142890,
    b2_colorLightSkyBlue = 8900346,
    b2_colorLightSlateGray = 7833753,
    b2_colorLightSteelBlue = 11584734,
    b2_colorLightYellow = 16777184,
    b2_colorLime = 65280,
    b2_colorLimeGreen = 3329330,
    b2_colorLinen = 16445670,
    b2_colorMagenta = 16711935,
    b2_colorMaroon = 8388608,
    b2_colorMediumAquaMarine = 6737322,
    b2_colorMediumBlue = 205,
    b2_colorMediumOrchid = 12211667,
    b2_colorMediumPurple = 9662683,
    b2_colorMediumSeaGreen = 3978097,
    b2_colorMediumSlateBlue = 8087790,
    b2_colorMediumSpringGreen = 64154,
    b2_colorMediumTurquoise = 4772300,
    b2_colorMediumVioletRed = 13047173,
    b2_colorMidnightBlue = 1644912,
    b2_colorMintCream = 16121850,
    b2_colorMistyRose = 16770273,
    b2_colorMoccasin = 16770229,
    b2_colorNavajoWhite = 16768685,
    b2_colorNavy = 128,
    b2_colorOldLace = 16643558,
    b2_colorOlive = 8421376,
    b2_colorOliveDrab = 7048739,
    b2_colorOrange = 16753920,
    b2_colorOrangeRed = 16729344,
    b2_colorOrchid = 14315734,
    b2_colorPaleGoldenRod = 15657130,
    b2_colorPaleGreen = 10025880,
    b2_colorPaleTurquoise = 11529966,
    b2_colorPaleVioletRed = 14381203,
    b2_colorPapayaWhip = 16773077,
    b2_colorPeachPuff = 16767673,
    b2_colorPeru = 13468991,
    b2_colorPink = 16761035,
    b2_colorPlum = 14524637,
    b2_colorPowderBlue = 11591910,
    b2_colorPurple = 8388736,
    b2_colorRebeccaPurple = 6697881,
    b2_colorRed = 16711680,
    b2_colorRosyBrown = 12357519,
    b2_colorRoyalBlue = 4286945,
    b2_colorSaddleBrown = 9127187,
    b2_colorSalmon = 16416882,
    b2_colorSandyBrown = 16032864,
    b2_colorSeaGreen = 3050327,
    b2_colorSeaShell = 16774638,
    b2_colorSienna = 10506797,
    b2_colorSilver = 12632256,
    b2_colorSkyBlue = 8900331,
    b2_colorSlateBlue = 6970061,
    b2_colorSlateGray = 7372944,
    b2_colorSnow = 16775930,
    b2_colorSpringGreen = 65407,
    b2_colorSteelBlue = 4620980,
    b2_colorTan = 13808780,
    b2_colorTeal = 32896,
    b2_colorThistle = 14204888,
    b2_colorTomato = 16737095,
    b2_colorTurquoise = 4251856,
    b2_colorViolet = 15631086,
    b2_colorWheat = 16113331,
    b2_colorWhite = 16777215,
    b2_colorWhiteSmoke = 16119285,
    b2_colorYellow = 16776960,
    b2_colorYellowGreen = 10145074,
    b2_colorBox2DRed = 14430514,
    b2_colorBox2DBlue = 3190463,
    b2_colorBox2DGreen = 9226532,
    b2_colorBox2DYellow = 16772748,
}
[StructLayout(LayoutKind.Sequential)]
public struct b2DebugDraw
{
    public delegate* unmanaged<b2Vec2*, int, b2HexColor, void*, void> DrawPolygonFcn;
    public delegate* unmanaged<b2Transform, b2Vec2*, int, float, b2HexColor, void*, void> DrawSolidPolygonFcn;
    public delegate* unmanaged<b2Vec2, float, b2HexColor, void*, void> DrawCircleFcn;
    public delegate* unmanaged<b2Transform, float, b2HexColor, void*, void> DrawSolidCircleFcn;
    public delegate* unmanaged<b2Vec2, b2Vec2, float, b2HexColor, void*, void> DrawSolidCapsuleFcn;
    public delegate* unmanaged<b2Vec2, b2Vec2, b2HexColor, void*, void> DrawLineFcn;
    public delegate* unmanaged<b2Transform, void*, void> DrawTransformFcn;
    public delegate* unmanaged<b2Vec2, float, b2HexColor, void*, void> DrawPointFcn;
    public delegate* unmanaged<b2Vec2, byte*, b2HexColor, void*, void> DrawStringFcn;
    public b2AABB drawingBounds;
    public float forceScale;
    public float jointScale;
#if WEB
    private byte _drawShapes;
    public bool drawShapes { get => _drawShapes != 0; set => _drawShapes = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool drawShapes;
#endif
#if WEB
    private byte _drawJoints;
    public bool drawJoints { get => _drawJoints != 0; set => _drawJoints = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool drawJoints;
#endif
#if WEB
    private byte _drawJointExtras;
    public bool drawJointExtras { get => _drawJointExtras != 0; set => _drawJointExtras = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool drawJointExtras;
#endif
#if WEB
    private byte _drawBounds;
    public bool drawBounds { get => _drawBounds != 0; set => _drawBounds = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool drawBounds;
#endif
#if WEB
    private byte _drawMass;
    public bool drawMass { get => _drawMass != 0; set => _drawMass = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool drawMass;
#endif
#if WEB
    private byte _drawBodyNames;
    public bool drawBodyNames { get => _drawBodyNames != 0; set => _drawBodyNames = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool drawBodyNames;
#endif
#if WEB
    private byte _drawContactPoints;
    public bool drawContactPoints { get => _drawContactPoints != 0; set => _drawContactPoints = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool drawContactPoints;
#endif
#if WEB
    private byte _drawGraphColors;
    public bool drawGraphColors { get => _drawGraphColors != 0; set => _drawGraphColors = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool drawGraphColors;
#endif
#if WEB
    private byte _drawContactFeatures;
    public bool drawContactFeatures { get => _drawContactFeatures != 0; set => _drawContactFeatures = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool drawContactFeatures;
#endif
#if WEB
    private byte _drawContactNormals;
    public bool drawContactNormals { get => _drawContactNormals != 0; set => _drawContactNormals = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool drawContactNormals;
#endif
#if WEB
    private byte _drawContactForces;
    public bool drawContactForces { get => _drawContactForces != 0; set => _drawContactForces = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool drawContactForces;
#endif
#if WEB
    private byte _drawFrictionForces;
    public bool drawFrictionForces { get => _drawFrictionForces != 0; set => _drawFrictionForces = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool drawFrictionForces;
#endif
#if WEB
    private byte _drawIslands;
    public bool drawIslands { get => _drawIslands != 0; set => _drawIslands = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool drawIslands;
#endif
    public void* context;
}
#if WEB
public static b2DebugDraw b2DefaultDebugDraw()
{
    b2DebugDraw result = default;
    b2DefaultDebugDraw_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultDebugDraw", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultDebugDraw", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2DebugDraw b2DefaultDebugDraw();
#endif

#if WEB
public static b2WorldId b2CreateWorld(in b2WorldDef def)
{
    b2WorldId result = default;
    b2CreateWorld_internal(ref result, def);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateWorld", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateWorld", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2WorldId b2CreateWorld(in b2WorldDef def);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DestroyWorld", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DestroyWorld", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DestroyWorld(b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_IsValid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_IsValid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2World_IsValid(b2WorldId id);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_Step", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_Step", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_Step(b2WorldId worldId, float timeStep, int subStepCount);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_Draw", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_Draw", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_Draw(b2WorldId worldId, b2DebugDraw* draw);

#if WEB
public static b2BodyEvents b2World_GetBodyEvents(b2WorldId worldId)
{
    b2BodyEvents result = default;
    b2World_GetBodyEvents_internal(ref result, worldId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetBodyEvents", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetBodyEvents", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2BodyEvents b2World_GetBodyEvents(b2WorldId worldId);
#endif

#if WEB
public static b2SensorEvents b2World_GetSensorEvents(b2WorldId worldId)
{
    b2SensorEvents result = default;
    b2World_GetSensorEvents_internal(ref result, worldId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetSensorEvents", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetSensorEvents", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2SensorEvents b2World_GetSensorEvents(b2WorldId worldId);
#endif

#if WEB
public static b2ContactEvents b2World_GetContactEvents(b2WorldId worldId)
{
    b2ContactEvents result = default;
    b2World_GetContactEvents_internal(ref result, worldId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetContactEvents", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetContactEvents", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2ContactEvents b2World_GetContactEvents(b2WorldId worldId);
#endif

#if WEB
public static b2JointEvents b2World_GetJointEvents(b2WorldId worldId)
{
    b2JointEvents result = default;
    b2World_GetJointEvents_internal(ref result, worldId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetJointEvents", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetJointEvents", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2JointEvents b2World_GetJointEvents(b2WorldId worldId);
#endif

#if WEB
public static b2TreeStats b2World_OverlapAABB(b2WorldId worldId, b2AABB aabb, b2QueryFilter filter, IntPtr fcn, void* context)
{
    b2TreeStats result = default;
    b2World_OverlapAABB_internal(ref result, worldId, aabb, filter, fcn, context);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_OverlapAABB", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_OverlapAABB", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2TreeStats b2World_OverlapAABB(b2WorldId worldId, b2AABB aabb, b2QueryFilter filter, IntPtr fcn, void* context);
#endif

#if WEB
public static b2TreeStats b2World_OverlapShape(b2WorldId worldId, in b2ShapeProxy proxy, b2QueryFilter filter, IntPtr fcn, void* context)
{
    b2TreeStats result = default;
    b2World_OverlapShape_internal(ref result, worldId, proxy, filter, fcn, context);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_OverlapShape", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_OverlapShape", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2TreeStats b2World_OverlapShape(b2WorldId worldId, in b2ShapeProxy proxy, b2QueryFilter filter, IntPtr fcn, void* context);
#endif

#if WEB
public static b2TreeStats b2World_CastRay(b2WorldId worldId, b2Vec2 origin, b2Vec2 translation, b2QueryFilter filter, IntPtr fcn, void* context)
{
    b2TreeStats result = default;
    b2World_CastRay_internal(ref result, worldId, origin, translation, filter, fcn, context);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_CastRay", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_CastRay", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2TreeStats b2World_CastRay(b2WorldId worldId, b2Vec2 origin, b2Vec2 translation, b2QueryFilter filter, IntPtr fcn, void* context);
#endif

#if WEB
public static b2RayResult b2World_CastRayClosest(b2WorldId worldId, b2Vec2 origin, b2Vec2 translation, b2QueryFilter filter)
{
    b2RayResult result = default;
    b2World_CastRayClosest_internal(ref result, worldId, origin, translation, filter);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_CastRayClosest", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_CastRayClosest", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2RayResult b2World_CastRayClosest(b2WorldId worldId, b2Vec2 origin, b2Vec2 translation, b2QueryFilter filter);
#endif

#if WEB
public static b2TreeStats b2World_CastShape(b2WorldId worldId, in b2ShapeProxy proxy, b2Vec2 translation, b2QueryFilter filter, IntPtr fcn, void* context)
{
    b2TreeStats result = default;
    b2World_CastShape_internal(ref result, worldId, proxy, translation, filter, fcn, context);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_CastShape", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_CastShape", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2TreeStats b2World_CastShape(b2WorldId worldId, in b2ShapeProxy proxy, b2Vec2 translation, b2QueryFilter filter, IntPtr fcn, void* context);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_CastMover", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_CastMover", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2World_CastMover(b2WorldId worldId, in b2Capsule mover, b2Vec2 translation, b2QueryFilter filter);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_CollideMover", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_CollideMover", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_CollideMover(b2WorldId worldId, in b2Capsule mover, b2QueryFilter filter, IntPtr fcn, void* context);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_EnableSleeping", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_EnableSleeping", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_EnableSleeping(b2WorldId worldId, bool flag);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_IsSleepingEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_IsSleepingEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2World_IsSleepingEnabled(b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_EnableContinuous", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_EnableContinuous", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_EnableContinuous(b2WorldId worldId, bool flag);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_IsContinuousEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_IsContinuousEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2World_IsContinuousEnabled(b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_SetRestitutionThreshold", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_SetRestitutionThreshold", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_SetRestitutionThreshold(b2WorldId worldId, float value);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetRestitutionThreshold", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetRestitutionThreshold", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2World_GetRestitutionThreshold(b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_SetHitEventThreshold", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_SetHitEventThreshold", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_SetHitEventThreshold(b2WorldId worldId, float value);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetHitEventThreshold", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetHitEventThreshold", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2World_GetHitEventThreshold(b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_SetCustomFilterCallback", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_SetCustomFilterCallback", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_SetCustomFilterCallback(b2WorldId worldId, IntPtr fcn, void* context);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_SetPreSolveCallback", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_SetPreSolveCallback", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_SetPreSolveCallback(b2WorldId worldId, IntPtr fcn, void* context);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_SetGravity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_SetGravity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_SetGravity(b2WorldId worldId, b2Vec2 gravity);

#if WEB
public static b2Vec2 b2World_GetGravity(b2WorldId worldId)
{
    b2Vec2 result = default;
    b2World_GetGravity_internal(ref result, worldId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetGravity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetGravity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Vec2 b2World_GetGravity(b2WorldId worldId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_Explode", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_Explode", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_Explode(b2WorldId worldId, in b2ExplosionDef explosionDef);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_SetContactTuning", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_SetContactTuning", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_SetContactTuning(b2WorldId worldId, float hertz, float dampingRatio, float pushSpeed);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_SetMaximumLinearSpeed", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_SetMaximumLinearSpeed", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_SetMaximumLinearSpeed(b2WorldId worldId, float maximumLinearSpeed);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetMaximumLinearSpeed", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetMaximumLinearSpeed", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2World_GetMaximumLinearSpeed(b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_EnableWarmStarting", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_EnableWarmStarting", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_EnableWarmStarting(b2WorldId worldId, bool flag);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_IsWarmStartingEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_IsWarmStartingEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2World_IsWarmStartingEnabled(b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetAwakeBodyCount", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetAwakeBodyCount", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2World_GetAwakeBodyCount(b2WorldId worldId);

#if WEB
public static b2Profile b2World_GetProfile(b2WorldId worldId)
{
    b2Profile result = default;
    b2World_GetProfile_internal(ref result, worldId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetProfile", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetProfile", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Profile b2World_GetProfile(b2WorldId worldId);
#endif

#if WEB
public static b2Counters b2World_GetCounters(b2WorldId worldId)
{
    b2Counters result = default;
    b2World_GetCounters_internal(ref result, worldId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetCounters", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetCounters", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Counters b2World_GetCounters(b2WorldId worldId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_SetUserData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_SetUserData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_SetUserData(b2WorldId worldId, void* userData);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetUserData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetUserData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* b2World_GetUserData(b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_SetFrictionCallback", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_SetFrictionCallback", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_SetFrictionCallback(b2WorldId worldId, IntPtr callback);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_SetRestitutionCallback", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_SetRestitutionCallback", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_SetRestitutionCallback(b2WorldId worldId, IntPtr callback);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_DumpMemoryStats", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_DumpMemoryStats", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_DumpMemoryStats(b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_RebuildStaticTree", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_RebuildStaticTree", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_RebuildStaticTree(b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_EnableSpeculative", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_EnableSpeculative", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_EnableSpeculative(b2WorldId worldId, bool flag);

#if WEB
public static b2BodyId b2CreateBody(b2WorldId worldId, in b2BodyDef def)
{
    b2BodyId result = default;
    b2CreateBody_internal(ref result, worldId, def);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateBody", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateBody", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2BodyId b2CreateBody(b2WorldId worldId, in b2BodyDef def);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DestroyBody", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DestroyBody", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DestroyBody(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_IsValid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_IsValid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2Body_IsValid(b2BodyId id);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetType", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetType", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2BodyType b2Body_GetType(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_SetType", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_SetType", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_SetType(b2BodyId bodyId, b2BodyType type);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_SetName", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_SetName", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_SetName(b2BodyId bodyId, [M(U.LPUTF8Str)] string name);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetName", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetName", CallingConvention = CallingConvention.Cdecl)]
#endif
private static extern IntPtr b2Body_GetName_native(b2BodyId bodyId);

public static string b2Body_GetName(b2BodyId bodyId)
{
    IntPtr ptr = b2Body_GetName_native(bodyId);
    if (ptr == IntPtr.Zero)
        return "";

    // Manual UTF-8 to string conversion to avoid marshalling corruption
    try
    {
        return Marshal.PtrToStringUTF8(ptr) ?? "";
    }
    catch
    {
        // Fallback in case of any marshalling issues
        return "";
    }
}

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_SetUserData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_SetUserData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_SetUserData(b2BodyId bodyId, void* userData);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetUserData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetUserData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* b2Body_GetUserData(b2BodyId bodyId);

#if WEB
public static b2Vec2 b2Body_GetPosition(b2BodyId bodyId)
{
    b2Vec2 result = default;
    b2Body_GetPosition_internal(ref result, bodyId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetPosition", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetPosition", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Vec2 b2Body_GetPosition(b2BodyId bodyId);
#endif

#if WEB
public static b2Rot b2Body_GetRotation(b2BodyId bodyId)
{
    b2Rot result = default;
    b2Body_GetRotation_internal(ref result, bodyId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetRotation", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetRotation", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Rot b2Body_GetRotation(b2BodyId bodyId);
#endif

#if WEB
public static b2Transform b2Body_GetTransform(b2BodyId bodyId)
{
    b2Transform result = default;
    b2Body_GetTransform_internal(ref result, bodyId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetTransform", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetTransform", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Transform b2Body_GetTransform(b2BodyId bodyId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_SetTransform", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_SetTransform", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_SetTransform(b2BodyId bodyId, b2Vec2 position, b2Rot rotation);

#if WEB
public static b2Vec2 b2Body_GetLocalPoint(b2BodyId bodyId, b2Vec2 worldPoint)
{
    b2Vec2 result = default;
    b2Body_GetLocalPoint_internal(ref result, bodyId, worldPoint);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetLocalPoint", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetLocalPoint", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Vec2 b2Body_GetLocalPoint(b2BodyId bodyId, b2Vec2 worldPoint);
#endif

#if WEB
public static b2Vec2 b2Body_GetWorldPoint(b2BodyId bodyId, b2Vec2 localPoint)
{
    b2Vec2 result = default;
    b2Body_GetWorldPoint_internal(ref result, bodyId, localPoint);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetWorldPoint", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetWorldPoint", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Vec2 b2Body_GetWorldPoint(b2BodyId bodyId, b2Vec2 localPoint);
#endif

#if WEB
public static b2Vec2 b2Body_GetLocalVector(b2BodyId bodyId, b2Vec2 worldVector)
{
    b2Vec2 result = default;
    b2Body_GetLocalVector_internal(ref result, bodyId, worldVector);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetLocalVector", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetLocalVector", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Vec2 b2Body_GetLocalVector(b2BodyId bodyId, b2Vec2 worldVector);
#endif

#if WEB
public static b2Vec2 b2Body_GetWorldVector(b2BodyId bodyId, b2Vec2 localVector)
{
    b2Vec2 result = default;
    b2Body_GetWorldVector_internal(ref result, bodyId, localVector);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetWorldVector", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetWorldVector", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Vec2 b2Body_GetWorldVector(b2BodyId bodyId, b2Vec2 localVector);
#endif

#if WEB
public static b2Vec2 b2Body_GetLinearVelocity(b2BodyId bodyId)
{
    b2Vec2 result = default;
    b2Body_GetLinearVelocity_internal(ref result, bodyId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetLinearVelocity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetLinearVelocity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Vec2 b2Body_GetLinearVelocity(b2BodyId bodyId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetAngularVelocity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetAngularVelocity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2Body_GetAngularVelocity(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_SetLinearVelocity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_SetLinearVelocity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_SetLinearVelocity(b2BodyId bodyId, b2Vec2 linearVelocity);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_SetAngularVelocity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_SetAngularVelocity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_SetAngularVelocity(b2BodyId bodyId, float angularVelocity);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_SetTargetTransform", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_SetTargetTransform", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_SetTargetTransform(b2BodyId bodyId, b2Transform target, float timeStep);

#if WEB
public static b2Vec2 b2Body_GetLocalPointVelocity(b2BodyId bodyId, b2Vec2 localPoint)
{
    b2Vec2 result = default;
    b2Body_GetLocalPointVelocity_internal(ref result, bodyId, localPoint);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetLocalPointVelocity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetLocalPointVelocity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Vec2 b2Body_GetLocalPointVelocity(b2BodyId bodyId, b2Vec2 localPoint);
#endif

#if WEB
public static b2Vec2 b2Body_GetWorldPointVelocity(b2BodyId bodyId, b2Vec2 worldPoint)
{
    b2Vec2 result = default;
    b2Body_GetWorldPointVelocity_internal(ref result, bodyId, worldPoint);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetWorldPointVelocity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetWorldPointVelocity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Vec2 b2Body_GetWorldPointVelocity(b2BodyId bodyId, b2Vec2 worldPoint);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_ApplyForce", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_ApplyForce", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_ApplyForce(b2BodyId bodyId, b2Vec2 force, b2Vec2 point, bool wake);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_ApplyForceToCenter", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_ApplyForceToCenter", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_ApplyForceToCenter(b2BodyId bodyId, b2Vec2 force, bool wake);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_ApplyTorque", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_ApplyTorque", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_ApplyTorque(b2BodyId bodyId, float torque, bool wake);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_ClearForces", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_ClearForces", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_ClearForces(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_ApplyLinearImpulse", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_ApplyLinearImpulse", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_ApplyLinearImpulse(b2BodyId bodyId, b2Vec2 impulse, b2Vec2 point, bool wake);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_ApplyLinearImpulseToCenter", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_ApplyLinearImpulseToCenter", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_ApplyLinearImpulseToCenter(b2BodyId bodyId, b2Vec2 impulse, bool wake);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_ApplyAngularImpulse", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_ApplyAngularImpulse", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_ApplyAngularImpulse(b2BodyId bodyId, float impulse, bool wake);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetMass", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetMass", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2Body_GetMass(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetRotationalInertia", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetRotationalInertia", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2Body_GetRotationalInertia(b2BodyId bodyId);

#if WEB
public static b2Vec2 b2Body_GetLocalCenterOfMass(b2BodyId bodyId)
{
    b2Vec2 result = default;
    b2Body_GetLocalCenterOfMass_internal(ref result, bodyId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetLocalCenterOfMass", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetLocalCenterOfMass", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Vec2 b2Body_GetLocalCenterOfMass(b2BodyId bodyId);
#endif

#if WEB
public static b2Vec2 b2Body_GetWorldCenterOfMass(b2BodyId bodyId)
{
    b2Vec2 result = default;
    b2Body_GetWorldCenterOfMass_internal(ref result, bodyId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetWorldCenterOfMass", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetWorldCenterOfMass", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Vec2 b2Body_GetWorldCenterOfMass(b2BodyId bodyId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_SetMassData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_SetMassData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_SetMassData(b2BodyId bodyId, b2MassData massData);

#if WEB
public static b2MassData b2Body_GetMassData(b2BodyId bodyId)
{
    b2MassData result = default;
    b2Body_GetMassData_internal(ref result, bodyId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetMassData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetMassData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2MassData b2Body_GetMassData(b2BodyId bodyId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_ApplyMassFromShapes", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_ApplyMassFromShapes", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_ApplyMassFromShapes(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_SetLinearDamping", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_SetLinearDamping", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_SetLinearDamping(b2BodyId bodyId, float linearDamping);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetLinearDamping", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetLinearDamping", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2Body_GetLinearDamping(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_SetAngularDamping", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_SetAngularDamping", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_SetAngularDamping(b2BodyId bodyId, float angularDamping);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetAngularDamping", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetAngularDamping", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2Body_GetAngularDamping(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_SetGravityScale", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_SetGravityScale", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_SetGravityScale(b2BodyId bodyId, float gravityScale);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetGravityScale", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetGravityScale", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2Body_GetGravityScale(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_IsAwake", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_IsAwake", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2Body_IsAwake(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_SetAwake", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_SetAwake", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_SetAwake(b2BodyId bodyId, bool awake);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_WakeTouching", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_WakeTouching", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_WakeTouching(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_EnableSleep", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_EnableSleep", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_EnableSleep(b2BodyId bodyId, bool enableSleep);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_IsSleepEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_IsSleepEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2Body_IsSleepEnabled(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_SetSleepThreshold", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_SetSleepThreshold", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_SetSleepThreshold(b2BodyId bodyId, float sleepThreshold);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetSleepThreshold", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetSleepThreshold", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2Body_GetSleepThreshold(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_IsEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_IsEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2Body_IsEnabled(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_Disable", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_Disable", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_Disable(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_Enable", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_Enable", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_Enable(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_SetMotionLocks", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_SetMotionLocks", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_SetMotionLocks(b2BodyId bodyId, b2MotionLocks locks);

#if WEB
public static b2MotionLocks b2Body_GetMotionLocks(b2BodyId bodyId)
{
    b2MotionLocks result = default;
    b2Body_GetMotionLocks_internal(ref result, bodyId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetMotionLocks", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetMotionLocks", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2MotionLocks b2Body_GetMotionLocks(b2BodyId bodyId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_SetBullet", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_SetBullet", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_SetBullet(b2BodyId bodyId, bool flag);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_IsBullet", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_IsBullet", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2Body_IsBullet(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_EnableContactEvents", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_EnableContactEvents", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_EnableContactEvents(b2BodyId bodyId, bool flag);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_EnableHitEvents", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_EnableHitEvents", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_EnableHitEvents(b2BodyId bodyId, bool flag);

#if WEB
public static b2WorldId b2Body_GetWorld(b2BodyId bodyId)
{
    b2WorldId result = default;
    b2Body_GetWorld_internal(ref result, bodyId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetWorld", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetWorld", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2WorldId b2Body_GetWorld(b2BodyId bodyId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetShapeCount", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetShapeCount", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2Body_GetShapeCount(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetShapes", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetShapes", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2Body_GetShapes(b2BodyId bodyId, b2ShapeId* shapeArray, int capacity);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetJointCount", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetJointCount", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2Body_GetJointCount(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetJoints", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetJoints", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2Body_GetJoints(b2BodyId bodyId, b2JointId* jointArray, int capacity);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetContactCapacity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetContactCapacity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2Body_GetContactCapacity(b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetContactData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetContactData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2Body_GetContactData(b2BodyId bodyId, b2ContactData* contactData, int capacity);

#if WEB
public static b2AABB b2Body_ComputeAABB(b2BodyId bodyId)
{
    b2AABB result = default;
    b2Body_ComputeAABB_internal(ref result, bodyId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_ComputeAABB", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_ComputeAABB", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2AABB b2Body_ComputeAABB(b2BodyId bodyId);
#endif

#if WEB
public static b2ShapeId b2CreateCircleShape(b2BodyId bodyId, in b2ShapeDef def, in b2Circle circle)
{
    b2ShapeId result = default;
    b2CreateCircleShape_internal(ref result, bodyId, def, circle);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateCircleShape", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateCircleShape", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2ShapeId b2CreateCircleShape(b2BodyId bodyId, in b2ShapeDef def, in b2Circle circle);
#endif

#if WEB
public static b2ShapeId b2CreateSegmentShape(b2BodyId bodyId, in b2ShapeDef def, in b2Segment segment)
{
    b2ShapeId result = default;
    b2CreateSegmentShape_internal(ref result, bodyId, def, segment);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateSegmentShape", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateSegmentShape", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2ShapeId b2CreateSegmentShape(b2BodyId bodyId, in b2ShapeDef def, in b2Segment segment);
#endif

#if WEB
public static b2ShapeId b2CreateCapsuleShape(b2BodyId bodyId, in b2ShapeDef def, in b2Capsule capsule)
{
    b2ShapeId result = default;
    b2CreateCapsuleShape_internal(ref result, bodyId, def, capsule);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateCapsuleShape", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateCapsuleShape", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2ShapeId b2CreateCapsuleShape(b2BodyId bodyId, in b2ShapeDef def, in b2Capsule capsule);
#endif

#if WEB
public static b2ShapeId b2CreatePolygonShape(b2BodyId bodyId, in b2ShapeDef def, in b2Polygon polygon)
{
    b2ShapeId result = default;
    b2CreatePolygonShape_internal(ref result, bodyId, def, polygon);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreatePolygonShape", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreatePolygonShape", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2ShapeId b2CreatePolygonShape(b2BodyId bodyId, in b2ShapeDef def, in b2Polygon polygon);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DestroyShape", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DestroyShape", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DestroyShape(b2ShapeId shapeId, bool updateBodyMass);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_IsValid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_IsValid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2Shape_IsValid(b2ShapeId id);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetType", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetType", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2ShapeType b2Shape_GetType(b2ShapeId shapeId);

#if WEB
public static b2BodyId b2Shape_GetBody(b2ShapeId shapeId)
{
    b2BodyId result = default;
    b2Shape_GetBody_internal(ref result, shapeId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetBody", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetBody", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2BodyId b2Shape_GetBody(b2ShapeId shapeId);
#endif

#if WEB
public static b2WorldId b2Shape_GetWorld(b2ShapeId shapeId)
{
    b2WorldId result = default;
    b2Shape_GetWorld_internal(ref result, shapeId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetWorld", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetWorld", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2WorldId b2Shape_GetWorld(b2ShapeId shapeId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_IsSensor", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_IsSensor", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2Shape_IsSensor(b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_SetUserData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_SetUserData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_SetUserData(b2ShapeId shapeId, void* userData);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetUserData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetUserData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* b2Shape_GetUserData(b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_SetDensity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_SetDensity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_SetDensity(b2ShapeId shapeId, float density, bool updateBodyMass);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetDensity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetDensity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2Shape_GetDensity(b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_SetFriction", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_SetFriction", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_SetFriction(b2ShapeId shapeId, float friction);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetFriction", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetFriction", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2Shape_GetFriction(b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_SetRestitution", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_SetRestitution", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_SetRestitution(b2ShapeId shapeId, float restitution);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetRestitution", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetRestitution", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2Shape_GetRestitution(b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_SetUserMaterial", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_SetUserMaterial", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_SetUserMaterial(b2ShapeId shapeId, ulong material);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetUserMaterial", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetUserMaterial", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern ulong b2Shape_GetUserMaterial(b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_SetSurfaceMaterial", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_SetSurfaceMaterial", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_SetSurfaceMaterial(b2ShapeId shapeId, in b2SurfaceMaterial surfaceMaterial);

#if WEB
public static b2SurfaceMaterial b2Shape_GetSurfaceMaterial(b2ShapeId shapeId)
{
    b2SurfaceMaterial result = default;
    b2Shape_GetSurfaceMaterial_internal(ref result, shapeId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetSurfaceMaterial", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetSurfaceMaterial", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2SurfaceMaterial b2Shape_GetSurfaceMaterial(b2ShapeId shapeId);
#endif

#if WEB
public static b2Filter b2Shape_GetFilter(b2ShapeId shapeId)
{
    b2Filter result = default;
    b2Shape_GetFilter_internal(ref result, shapeId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetFilter", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetFilter", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Filter b2Shape_GetFilter(b2ShapeId shapeId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_SetFilter", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_SetFilter", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_SetFilter(b2ShapeId shapeId, b2Filter filter);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_EnableSensorEvents", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_EnableSensorEvents", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_EnableSensorEvents(b2ShapeId shapeId, bool flag);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_AreSensorEventsEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_AreSensorEventsEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2Shape_AreSensorEventsEnabled(b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_EnableContactEvents", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_EnableContactEvents", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_EnableContactEvents(b2ShapeId shapeId, bool flag);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_AreContactEventsEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_AreContactEventsEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2Shape_AreContactEventsEnabled(b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_EnablePreSolveEvents", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_EnablePreSolveEvents", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_EnablePreSolveEvents(b2ShapeId shapeId, bool flag);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_ArePreSolveEventsEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_ArePreSolveEventsEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2Shape_ArePreSolveEventsEnabled(b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_EnableHitEvents", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_EnableHitEvents", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_EnableHitEvents(b2ShapeId shapeId, bool flag);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_AreHitEventsEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_AreHitEventsEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2Shape_AreHitEventsEnabled(b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_TestPoint", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_TestPoint", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2Shape_TestPoint(b2ShapeId shapeId, b2Vec2 point);

#if WEB
public static b2CastOutput b2Shape_RayCast(b2ShapeId shapeId, in b2RayCastInput input)
{
    b2CastOutput result = default;
    b2Shape_RayCast_internal(ref result, shapeId, input);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_RayCast", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_RayCast", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2CastOutput b2Shape_RayCast(b2ShapeId shapeId, in b2RayCastInput input);
#endif

#if WEB
public static b2Circle b2Shape_GetCircle(b2ShapeId shapeId)
{
    b2Circle result = default;
    b2Shape_GetCircle_internal(ref result, shapeId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetCircle", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetCircle", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Circle b2Shape_GetCircle(b2ShapeId shapeId);
#endif

#if WEB
public static b2Segment b2Shape_GetSegment(b2ShapeId shapeId)
{
    b2Segment result = default;
    b2Shape_GetSegment_internal(ref result, shapeId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetSegment", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetSegment", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Segment b2Shape_GetSegment(b2ShapeId shapeId);
#endif

#if WEB
public static b2ChainSegment b2Shape_GetChainSegment(b2ShapeId shapeId)
{
    b2ChainSegment result = default;
    b2Shape_GetChainSegment_internal(ref result, shapeId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetChainSegment", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetChainSegment", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2ChainSegment b2Shape_GetChainSegment(b2ShapeId shapeId);
#endif

#if WEB
public static b2Capsule b2Shape_GetCapsule(b2ShapeId shapeId)
{
    b2Capsule result = default;
    b2Shape_GetCapsule_internal(ref result, shapeId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetCapsule", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetCapsule", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Capsule b2Shape_GetCapsule(b2ShapeId shapeId);
#endif

#if WEB
public static b2Polygon b2Shape_GetPolygon(b2ShapeId shapeId)
{
    b2Polygon result = default;
    b2Shape_GetPolygon_internal(ref result, shapeId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetPolygon", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetPolygon", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Polygon b2Shape_GetPolygon(b2ShapeId shapeId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_SetCircle", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_SetCircle", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_SetCircle(b2ShapeId shapeId, in b2Circle circle);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_SetCapsule", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_SetCapsule", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_SetCapsule(b2ShapeId shapeId, in b2Capsule capsule);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_SetSegment", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_SetSegment", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_SetSegment(b2ShapeId shapeId, in b2Segment segment);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_SetPolygon", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_SetPolygon", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_SetPolygon(b2ShapeId shapeId, in b2Polygon polygon);

#if WEB
public static b2ChainId b2Shape_GetParentChain(b2ShapeId shapeId)
{
    b2ChainId result = default;
    b2Shape_GetParentChain_internal(ref result, shapeId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetParentChain", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetParentChain", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2ChainId b2Shape_GetParentChain(b2ShapeId shapeId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetContactCapacity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetContactCapacity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2Shape_GetContactCapacity(b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetContactData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetContactData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2Shape_GetContactData(b2ShapeId shapeId, b2ContactData* contactData, int capacity);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetSensorCapacity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetSensorCapacity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2Shape_GetSensorCapacity(b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetSensorData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetSensorData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2Shape_GetSensorData(b2ShapeId shapeId, b2ShapeId* visitorIds, int capacity);

#if WEB
public static b2AABB b2Shape_GetAABB(b2ShapeId shapeId)
{
    b2AABB result = default;
    b2Shape_GetAABB_internal(ref result, shapeId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetAABB", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetAABB", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2AABB b2Shape_GetAABB(b2ShapeId shapeId);
#endif

#if WEB
public static b2MassData b2Shape_ComputeMassData(b2ShapeId shapeId)
{
    b2MassData result = default;
    b2Shape_ComputeMassData_internal(ref result, shapeId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_ComputeMassData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_ComputeMassData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2MassData b2Shape_ComputeMassData(b2ShapeId shapeId);
#endif

#if WEB
public static b2Vec2 b2Shape_GetClosestPoint(b2ShapeId shapeId, b2Vec2 target)
{
    b2Vec2 result = default;
    b2Shape_GetClosestPoint_internal(ref result, shapeId, target);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetClosestPoint", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetClosestPoint", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Vec2 b2Shape_GetClosestPoint(b2ShapeId shapeId, b2Vec2 target);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_ApplyWind", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_ApplyWind", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_ApplyWind(b2ShapeId shapeId, b2Vec2 wind, float drag, float lift, bool wake);

#if WEB
public static b2ChainId b2CreateChain(b2BodyId bodyId, in b2ChainDef def)
{
    b2ChainId result = default;
    b2CreateChain_internal(ref result, bodyId, def);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateChain", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateChain", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2ChainId b2CreateChain(b2BodyId bodyId, in b2ChainDef def);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DestroyChain", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DestroyChain", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DestroyChain(b2ChainId chainId);

#if WEB
public static b2WorldId b2Chain_GetWorld(b2ChainId chainId)
{
    b2WorldId result = default;
    b2Chain_GetWorld_internal(ref result, chainId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Chain_GetWorld", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Chain_GetWorld", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2WorldId b2Chain_GetWorld(b2ChainId chainId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Chain_GetSegmentCount", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Chain_GetSegmentCount", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2Chain_GetSegmentCount(b2ChainId chainId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Chain_GetSegments", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Chain_GetSegments", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2Chain_GetSegments(b2ChainId chainId, b2ShapeId* segmentArray, int capacity);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Chain_GetSurfaceMaterialCount", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Chain_GetSurfaceMaterialCount", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int b2Chain_GetSurfaceMaterialCount(b2ChainId chainId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Chain_SetSurfaceMaterial", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Chain_SetSurfaceMaterial", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Chain_SetSurfaceMaterial(b2ChainId chainId, in b2SurfaceMaterial material, int materialIndex);

#if WEB
public static b2SurfaceMaterial b2Chain_GetSurfaceMaterial(b2ChainId chainId, int materialIndex)
{
    b2SurfaceMaterial result = default;
    b2Chain_GetSurfaceMaterial_internal(ref result, chainId, materialIndex);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Chain_GetSurfaceMaterial", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Chain_GetSurfaceMaterial", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2SurfaceMaterial b2Chain_GetSurfaceMaterial(b2ChainId chainId, int materialIndex);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Chain_IsValid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Chain_IsValid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2Chain_IsValid(b2ChainId id);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DestroyJoint", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DestroyJoint", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DestroyJoint(b2JointId jointId, bool wakeAttached);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_IsValid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_IsValid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2Joint_IsValid(b2JointId id);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetType", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetType", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2JointType b2Joint_GetType(b2JointId jointId);

#if WEB
public static b2BodyId b2Joint_GetBodyA(b2JointId jointId)
{
    b2BodyId result = default;
    b2Joint_GetBodyA_internal(ref result, jointId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetBodyA", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetBodyA", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2BodyId b2Joint_GetBodyA(b2JointId jointId);
#endif

#if WEB
public static b2BodyId b2Joint_GetBodyB(b2JointId jointId)
{
    b2BodyId result = default;
    b2Joint_GetBodyB_internal(ref result, jointId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetBodyB", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetBodyB", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2BodyId b2Joint_GetBodyB(b2JointId jointId);
#endif

#if WEB
public static b2WorldId b2Joint_GetWorld(b2JointId jointId)
{
    b2WorldId result = default;
    b2Joint_GetWorld_internal(ref result, jointId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetWorld", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetWorld", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2WorldId b2Joint_GetWorld(b2JointId jointId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_SetLocalFrameA", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_SetLocalFrameA", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Joint_SetLocalFrameA(b2JointId jointId, b2Transform localFrame);

#if WEB
public static b2Transform b2Joint_GetLocalFrameA(b2JointId jointId)
{
    b2Transform result = default;
    b2Joint_GetLocalFrameA_internal(ref result, jointId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetLocalFrameA", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetLocalFrameA", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Transform b2Joint_GetLocalFrameA(b2JointId jointId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_SetLocalFrameB", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_SetLocalFrameB", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Joint_SetLocalFrameB(b2JointId jointId, b2Transform localFrame);

#if WEB
public static b2Transform b2Joint_GetLocalFrameB(b2JointId jointId)
{
    b2Transform result = default;
    b2Joint_GetLocalFrameB_internal(ref result, jointId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetLocalFrameB", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetLocalFrameB", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Transform b2Joint_GetLocalFrameB(b2JointId jointId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_SetCollideConnected", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_SetCollideConnected", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Joint_SetCollideConnected(b2JointId jointId, bool shouldCollide);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetCollideConnected", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetCollideConnected", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2Joint_GetCollideConnected(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_SetUserData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_SetUserData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Joint_SetUserData(b2JointId jointId, void* userData);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetUserData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetUserData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* b2Joint_GetUserData(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_WakeBodies", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_WakeBodies", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Joint_WakeBodies(b2JointId jointId);

#if WEB
public static b2Vec2 b2Joint_GetConstraintForce(b2JointId jointId)
{
    b2Vec2 result = default;
    b2Joint_GetConstraintForce_internal(ref result, jointId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetConstraintForce", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetConstraintForce", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Vec2 b2Joint_GetConstraintForce(b2JointId jointId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetConstraintTorque", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetConstraintTorque", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2Joint_GetConstraintTorque(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetLinearSeparation", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetLinearSeparation", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2Joint_GetLinearSeparation(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetAngularSeparation", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetAngularSeparation", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2Joint_GetAngularSeparation(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_SetConstraintTuning", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_SetConstraintTuning", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Joint_SetConstraintTuning(b2JointId jointId, float hertz, float dampingRatio);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetConstraintTuning", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetConstraintTuning", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Joint_GetConstraintTuning(b2JointId jointId, ref float hertz, ref float dampingRatio);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_SetForceThreshold", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_SetForceThreshold", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Joint_SetForceThreshold(b2JointId jointId, float threshold);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetForceThreshold", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetForceThreshold", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2Joint_GetForceThreshold(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_SetTorqueThreshold", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_SetTorqueThreshold", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Joint_SetTorqueThreshold(b2JointId jointId, float threshold);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetTorqueThreshold", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetTorqueThreshold", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2Joint_GetTorqueThreshold(b2JointId jointId);

#if WEB
public static b2JointId b2CreateDistanceJoint(b2WorldId worldId, in b2DistanceJointDef def)
{
    b2JointId result = default;
    b2CreateDistanceJoint_internal(ref result, worldId, def);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateDistanceJoint", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateDistanceJoint", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2JointId b2CreateDistanceJoint(b2WorldId worldId, in b2DistanceJointDef def);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_SetLength", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_SetLength", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DistanceJoint_SetLength(b2JointId jointId, float length);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_GetLength", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_GetLength", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2DistanceJoint_GetLength(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_EnableSpring", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_EnableSpring", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DistanceJoint_EnableSpring(b2JointId jointId, bool enableSpring);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_IsSpringEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_IsSpringEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2DistanceJoint_IsSpringEnabled(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_SetSpringForceRange", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_SetSpringForceRange", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DistanceJoint_SetSpringForceRange(b2JointId jointId, float lowerForce, float upperForce);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_GetSpringForceRange", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_GetSpringForceRange", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DistanceJoint_GetSpringForceRange(b2JointId jointId, ref float lowerForce, ref float upperForce);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_SetSpringHertz", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_SetSpringHertz", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DistanceJoint_SetSpringHertz(b2JointId jointId, float hertz);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_SetSpringDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_SetSpringDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DistanceJoint_SetSpringDampingRatio(b2JointId jointId, float dampingRatio);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_GetSpringHertz", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_GetSpringHertz", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2DistanceJoint_GetSpringHertz(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_GetSpringDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_GetSpringDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2DistanceJoint_GetSpringDampingRatio(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_EnableLimit", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_EnableLimit", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DistanceJoint_EnableLimit(b2JointId jointId, bool enableLimit);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_IsLimitEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_IsLimitEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2DistanceJoint_IsLimitEnabled(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_SetLengthRange", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_SetLengthRange", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DistanceJoint_SetLengthRange(b2JointId jointId, float minLength, float maxLength);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_GetMinLength", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_GetMinLength", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2DistanceJoint_GetMinLength(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_GetMaxLength", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_GetMaxLength", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2DistanceJoint_GetMaxLength(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_GetCurrentLength", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_GetCurrentLength", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2DistanceJoint_GetCurrentLength(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_EnableMotor", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_EnableMotor", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DistanceJoint_EnableMotor(b2JointId jointId, bool enableMotor);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_IsMotorEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_IsMotorEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2DistanceJoint_IsMotorEnabled(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_SetMotorSpeed", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_SetMotorSpeed", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DistanceJoint_SetMotorSpeed(b2JointId jointId, float motorSpeed);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_GetMotorSpeed", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_GetMotorSpeed", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2DistanceJoint_GetMotorSpeed(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_SetMaxMotorForce", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_SetMaxMotorForce", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DistanceJoint_SetMaxMotorForce(b2JointId jointId, float force);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_GetMaxMotorForce", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_GetMaxMotorForce", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2DistanceJoint_GetMaxMotorForce(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DistanceJoint_GetMotorForce", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DistanceJoint_GetMotorForce", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2DistanceJoint_GetMotorForce(b2JointId jointId);

#if WEB
public static b2JointId b2CreateMotorJoint(b2WorldId worldId, in b2MotorJointDef def)
{
    b2JointId result = default;
    b2CreateMotorJoint_internal(ref result, worldId, def);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateMotorJoint", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateMotorJoint", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2JointId b2CreateMotorJoint(b2WorldId worldId, in b2MotorJointDef def);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_SetLinearVelocity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_SetLinearVelocity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MotorJoint_SetLinearVelocity(b2JointId jointId, b2Vec2 velocity);

#if WEB
public static b2Vec2 b2MotorJoint_GetLinearVelocity(b2JointId jointId)
{
    b2Vec2 result = default;
    b2MotorJoint_GetLinearVelocity_internal(ref result, jointId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_GetLinearVelocity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_GetLinearVelocity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2Vec2 b2MotorJoint_GetLinearVelocity(b2JointId jointId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_SetAngularVelocity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_SetAngularVelocity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MotorJoint_SetAngularVelocity(b2JointId jointId, float velocity);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_GetAngularVelocity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_GetAngularVelocity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2MotorJoint_GetAngularVelocity(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_SetMaxVelocityForce", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_SetMaxVelocityForce", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MotorJoint_SetMaxVelocityForce(b2JointId jointId, float maxForce);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_GetMaxVelocityForce", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_GetMaxVelocityForce", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2MotorJoint_GetMaxVelocityForce(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_SetMaxVelocityTorque", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_SetMaxVelocityTorque", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MotorJoint_SetMaxVelocityTorque(b2JointId jointId, float maxTorque);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_GetMaxVelocityTorque", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_GetMaxVelocityTorque", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2MotorJoint_GetMaxVelocityTorque(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_SetLinearHertz", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_SetLinearHertz", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MotorJoint_SetLinearHertz(b2JointId jointId, float hertz);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_GetLinearHertz", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_GetLinearHertz", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2MotorJoint_GetLinearHertz(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_SetLinearDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_SetLinearDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MotorJoint_SetLinearDampingRatio(b2JointId jointId, float damping);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_GetLinearDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_GetLinearDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2MotorJoint_GetLinearDampingRatio(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_SetAngularHertz", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_SetAngularHertz", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MotorJoint_SetAngularHertz(b2JointId jointId, float hertz);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_GetAngularHertz", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_GetAngularHertz", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2MotorJoint_GetAngularHertz(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_SetAngularDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_SetAngularDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MotorJoint_SetAngularDampingRatio(b2JointId jointId, float damping);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_GetAngularDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_GetAngularDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2MotorJoint_GetAngularDampingRatio(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_SetMaxSpringForce", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_SetMaxSpringForce", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MotorJoint_SetMaxSpringForce(b2JointId jointId, float maxForce);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_GetMaxSpringForce", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_GetMaxSpringForce", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2MotorJoint_GetMaxSpringForce(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_SetMaxSpringTorque", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_SetMaxSpringTorque", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MotorJoint_SetMaxSpringTorque(b2JointId jointId, float maxTorque);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_GetMaxSpringTorque", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_GetMaxSpringTorque", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2MotorJoint_GetMaxSpringTorque(b2JointId jointId);

#if WEB
public static b2JointId b2CreateFilterJoint(b2WorldId worldId, in b2FilterJointDef def)
{
    b2JointId result = default;
    b2CreateFilterJoint_internal(ref result, worldId, def);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateFilterJoint", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateFilterJoint", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2JointId b2CreateFilterJoint(b2WorldId worldId, in b2FilterJointDef def);
#endif

#if WEB
public static b2JointId b2CreatePrismaticJoint(b2WorldId worldId, in b2PrismaticJointDef def)
{
    b2JointId result = default;
    b2CreatePrismaticJoint_internal(ref result, worldId, def);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreatePrismaticJoint", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreatePrismaticJoint", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2JointId b2CreatePrismaticJoint(b2WorldId worldId, in b2PrismaticJointDef def);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_EnableSpring", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_EnableSpring", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2PrismaticJoint_EnableSpring(b2JointId jointId, bool enableSpring);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_IsSpringEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_IsSpringEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2PrismaticJoint_IsSpringEnabled(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_SetSpringHertz", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_SetSpringHertz", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2PrismaticJoint_SetSpringHertz(b2JointId jointId, float hertz);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_GetSpringHertz", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_GetSpringHertz", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2PrismaticJoint_GetSpringHertz(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_SetSpringDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_SetSpringDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2PrismaticJoint_SetSpringDampingRatio(b2JointId jointId, float dampingRatio);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_GetSpringDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_GetSpringDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2PrismaticJoint_GetSpringDampingRatio(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_SetTargetTranslation", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_SetTargetTranslation", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2PrismaticJoint_SetTargetTranslation(b2JointId jointId, float translation);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_GetTargetTranslation", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_GetTargetTranslation", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2PrismaticJoint_GetTargetTranslation(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_EnableLimit", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_EnableLimit", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2PrismaticJoint_EnableLimit(b2JointId jointId, bool enableLimit);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_IsLimitEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_IsLimitEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2PrismaticJoint_IsLimitEnabled(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_GetLowerLimit", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_GetLowerLimit", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2PrismaticJoint_GetLowerLimit(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_GetUpperLimit", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_GetUpperLimit", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2PrismaticJoint_GetUpperLimit(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_SetLimits", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_SetLimits", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2PrismaticJoint_SetLimits(b2JointId jointId, float lower, float upper);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_EnableMotor", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_EnableMotor", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2PrismaticJoint_EnableMotor(b2JointId jointId, bool enableMotor);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_IsMotorEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_IsMotorEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2PrismaticJoint_IsMotorEnabled(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_SetMotorSpeed", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_SetMotorSpeed", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2PrismaticJoint_SetMotorSpeed(b2JointId jointId, float motorSpeed);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_GetMotorSpeed", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_GetMotorSpeed", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2PrismaticJoint_GetMotorSpeed(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_SetMaxMotorForce", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_SetMaxMotorForce", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2PrismaticJoint_SetMaxMotorForce(b2JointId jointId, float force);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_GetMaxMotorForce", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_GetMaxMotorForce", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2PrismaticJoint_GetMaxMotorForce(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_GetMotorForce", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_GetMotorForce", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2PrismaticJoint_GetMotorForce(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_GetTranslation", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_GetTranslation", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2PrismaticJoint_GetTranslation(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2PrismaticJoint_GetSpeed", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2PrismaticJoint_GetSpeed", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2PrismaticJoint_GetSpeed(b2JointId jointId);

#if WEB
public static b2JointId b2CreateRevoluteJoint(b2WorldId worldId, in b2RevoluteJointDef def)
{
    b2JointId result = default;
    b2CreateRevoluteJoint_internal(ref result, worldId, def);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateRevoluteJoint", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateRevoluteJoint", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2JointId b2CreateRevoluteJoint(b2WorldId worldId, in b2RevoluteJointDef def);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_EnableSpring", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_EnableSpring", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2RevoluteJoint_EnableSpring(b2JointId jointId, bool enableSpring);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_IsSpringEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_IsSpringEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2RevoluteJoint_IsSpringEnabled(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_SetSpringHertz", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_SetSpringHertz", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2RevoluteJoint_SetSpringHertz(b2JointId jointId, float hertz);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_GetSpringHertz", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_GetSpringHertz", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2RevoluteJoint_GetSpringHertz(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_SetSpringDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_SetSpringDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2RevoluteJoint_SetSpringDampingRatio(b2JointId jointId, float dampingRatio);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_GetSpringDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_GetSpringDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2RevoluteJoint_GetSpringDampingRatio(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_SetTargetAngle", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_SetTargetAngle", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2RevoluteJoint_SetTargetAngle(b2JointId jointId, float angle);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_GetTargetAngle", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_GetTargetAngle", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2RevoluteJoint_GetTargetAngle(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_GetAngle", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_GetAngle", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2RevoluteJoint_GetAngle(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_EnableLimit", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_EnableLimit", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2RevoluteJoint_EnableLimit(b2JointId jointId, bool enableLimit);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_IsLimitEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_IsLimitEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2RevoluteJoint_IsLimitEnabled(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_GetLowerLimit", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_GetLowerLimit", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2RevoluteJoint_GetLowerLimit(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_GetUpperLimit", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_GetUpperLimit", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2RevoluteJoint_GetUpperLimit(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_SetLimits", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_SetLimits", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2RevoluteJoint_SetLimits(b2JointId jointId, float lower, float upper);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_EnableMotor", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_EnableMotor", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2RevoluteJoint_EnableMotor(b2JointId jointId, bool enableMotor);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_IsMotorEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_IsMotorEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2RevoluteJoint_IsMotorEnabled(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_SetMotorSpeed", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_SetMotorSpeed", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2RevoluteJoint_SetMotorSpeed(b2JointId jointId, float motorSpeed);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_GetMotorSpeed", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_GetMotorSpeed", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2RevoluteJoint_GetMotorSpeed(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_GetMotorTorque", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_GetMotorTorque", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2RevoluteJoint_GetMotorTorque(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_SetMaxMotorTorque", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_SetMaxMotorTorque", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2RevoluteJoint_SetMaxMotorTorque(b2JointId jointId, float torque);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RevoluteJoint_GetMaxMotorTorque", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RevoluteJoint_GetMaxMotorTorque", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2RevoluteJoint_GetMaxMotorTorque(b2JointId jointId);

#if WEB
public static b2JointId b2CreateWeldJoint(b2WorldId worldId, in b2WeldJointDef def)
{
    b2JointId result = default;
    b2CreateWeldJoint_internal(ref result, worldId, def);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateWeldJoint", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateWeldJoint", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2JointId b2CreateWeldJoint(b2WorldId worldId, in b2WeldJointDef def);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WeldJoint_SetLinearHertz", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WeldJoint_SetLinearHertz", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2WeldJoint_SetLinearHertz(b2JointId jointId, float hertz);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WeldJoint_GetLinearHertz", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WeldJoint_GetLinearHertz", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2WeldJoint_GetLinearHertz(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WeldJoint_SetLinearDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WeldJoint_SetLinearDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2WeldJoint_SetLinearDampingRatio(b2JointId jointId, float dampingRatio);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WeldJoint_GetLinearDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WeldJoint_GetLinearDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2WeldJoint_GetLinearDampingRatio(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WeldJoint_SetAngularHertz", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WeldJoint_SetAngularHertz", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2WeldJoint_SetAngularHertz(b2JointId jointId, float hertz);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WeldJoint_GetAngularHertz", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WeldJoint_GetAngularHertz", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2WeldJoint_GetAngularHertz(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WeldJoint_SetAngularDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WeldJoint_SetAngularDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2WeldJoint_SetAngularDampingRatio(b2JointId jointId, float dampingRatio);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WeldJoint_GetAngularDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WeldJoint_GetAngularDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2WeldJoint_GetAngularDampingRatio(b2JointId jointId);

#if WEB
public static b2JointId b2CreateWheelJoint(b2WorldId worldId, in b2WheelJointDef def)
{
    b2JointId result = default;
    b2CreateWheelJoint_internal(ref result, worldId, def);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateWheelJoint", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateWheelJoint", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2JointId b2CreateWheelJoint(b2WorldId worldId, in b2WheelJointDef def);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_EnableSpring", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_EnableSpring", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2WheelJoint_EnableSpring(b2JointId jointId, bool enableSpring);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_IsSpringEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_IsSpringEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2WheelJoint_IsSpringEnabled(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_SetSpringHertz", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_SetSpringHertz", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2WheelJoint_SetSpringHertz(b2JointId jointId, float hertz);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_GetSpringHertz", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_GetSpringHertz", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2WheelJoint_GetSpringHertz(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_SetSpringDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_SetSpringDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2WheelJoint_SetSpringDampingRatio(b2JointId jointId, float dampingRatio);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_GetSpringDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_GetSpringDampingRatio", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2WheelJoint_GetSpringDampingRatio(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_EnableLimit", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_EnableLimit", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2WheelJoint_EnableLimit(b2JointId jointId, bool enableLimit);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_IsLimitEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_IsLimitEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2WheelJoint_IsLimitEnabled(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_GetLowerLimit", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_GetLowerLimit", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2WheelJoint_GetLowerLimit(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_GetUpperLimit", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_GetUpperLimit", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2WheelJoint_GetUpperLimit(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_SetLimits", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_SetLimits", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2WheelJoint_SetLimits(b2JointId jointId, float lower, float upper);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_EnableMotor", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_EnableMotor", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2WheelJoint_EnableMotor(b2JointId jointId, bool enableMotor);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_IsMotorEnabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_IsMotorEnabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2WheelJoint_IsMotorEnabled(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_SetMotorSpeed", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_SetMotorSpeed", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2WheelJoint_SetMotorSpeed(b2JointId jointId, float motorSpeed);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_GetMotorSpeed", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_GetMotorSpeed", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2WheelJoint_GetMotorSpeed(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_SetMaxMotorTorque", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_SetMaxMotorTorque", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2WheelJoint_SetMaxMotorTorque(b2JointId jointId, float torque);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_GetMaxMotorTorque", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_GetMaxMotorTorque", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2WheelJoint_GetMaxMotorTorque(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2WheelJoint_GetMotorTorque", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2WheelJoint_GetMotorTorque", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float b2WheelJoint_GetMotorTorque(b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Contact_IsValid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Contact_IsValid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool b2Contact_IsValid(b2ContactId id);

#if WEB
public static b2ContactData b2Contact_GetData(b2ContactId contactId)
{
    b2ContactData result = default;
    b2Contact_GetData_internal(ref result, contactId);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Contact_GetData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Contact_GetData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern b2ContactData b2Contact_GetData(b2ContactId contactId);
#endif

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2GetVersion_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2GetVersion_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2GetVersion_internal(ref b2Version result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputeCosSin_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputeCosSin_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ComputeCosSin_internal(ref b2CosSin result, float radians);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputeRotationBetweenUnitVectors_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputeRotationBetweenUnitVectors_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ComputeRotationBetweenUnitVectors_internal(ref b2Rot result, b2Vec2 v1, b2Vec2 v2);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakePolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakePolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MakePolygon_internal(ref b2Polygon result, in b2Hull hull, float radius);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeOffsetPolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeOffsetPolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MakeOffsetPolygon_internal(ref b2Polygon result, in b2Hull hull, b2Vec2 position, b2Rot rotation);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeOffsetRoundedPolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeOffsetRoundedPolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MakeOffsetRoundedPolygon_internal(ref b2Polygon result, in b2Hull hull, b2Vec2 position, b2Rot rotation, float radius);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeSquare_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeSquare_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MakeSquare_internal(ref b2Polygon result, float halfWidth);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeBox_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeBox_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MakeBox_internal(ref b2Polygon result, float halfWidth, float halfHeight);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeRoundedBox_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeRoundedBox_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MakeRoundedBox_internal(ref b2Polygon result, float halfWidth, float halfHeight, float radius);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeOffsetBox_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeOffsetBox_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MakeOffsetBox_internal(ref b2Polygon result, float halfWidth, float halfHeight, b2Vec2 center, b2Rot rotation);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeOffsetRoundedBox_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeOffsetRoundedBox_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MakeOffsetRoundedBox_internal(ref b2Polygon result, float halfWidth, float halfHeight, b2Vec2 center, b2Rot rotation, float radius);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2TransformPolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2TransformPolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2TransformPolygon_internal(ref b2Polygon result, b2Transform transform, in b2Polygon polygon);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputeCircleMass_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputeCircleMass_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ComputeCircleMass_internal(ref b2MassData result, in b2Circle shape, float density);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputeCapsuleMass_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputeCapsuleMass_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ComputeCapsuleMass_internal(ref b2MassData result, in b2Capsule shape, float density);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputePolygonMass_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputePolygonMass_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ComputePolygonMass_internal(ref b2MassData result, in b2Polygon shape, float density);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputeCircleAABB_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputeCircleAABB_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ComputeCircleAABB_internal(ref b2AABB result, in b2Circle shape, b2Transform transform);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputeCapsuleAABB_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputeCapsuleAABB_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ComputeCapsuleAABB_internal(ref b2AABB result, in b2Capsule shape, b2Transform transform);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputePolygonAABB_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputePolygonAABB_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ComputePolygonAABB_internal(ref b2AABB result, in b2Polygon shape, b2Transform transform);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputeSegmentAABB_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputeSegmentAABB_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ComputeSegmentAABB_internal(ref b2AABB result, in b2Segment shape, b2Transform transform);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RayCastCircle_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RayCastCircle_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2RayCastCircle_internal(ref b2CastOutput result, in b2Circle shape, in b2RayCastInput input);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RayCastCapsule_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RayCastCapsule_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2RayCastCapsule_internal(ref b2CastOutput result, in b2Capsule shape, in b2RayCastInput input);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RayCastSegment_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RayCastSegment_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2RayCastSegment_internal(ref b2CastOutput result, in b2Segment shape, in b2RayCastInput input, bool oneSided);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2RayCastPolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2RayCastPolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2RayCastPolygon_internal(ref b2CastOutput result, in b2Polygon shape, in b2RayCastInput input);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ShapeCastCircle_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ShapeCastCircle_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ShapeCastCircle_internal(ref b2CastOutput result, in b2Circle shape, in b2ShapeCastInput input);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ShapeCastCapsule_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ShapeCastCapsule_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ShapeCastCapsule_internal(ref b2CastOutput result, in b2Capsule shape, in b2ShapeCastInput input);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ShapeCastSegment_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ShapeCastSegment_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ShapeCastSegment_internal(ref b2CastOutput result, in b2Segment shape, in b2ShapeCastInput input);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ShapeCastPolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ShapeCastPolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ShapeCastPolygon_internal(ref b2CastOutput result, in b2Polygon shape, in b2ShapeCastInput input);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ComputeHull_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ComputeHull_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ComputeHull_internal(ref b2Hull result, in b2Vec2 points, int count);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2SegmentDistance_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2SegmentDistance_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2SegmentDistance_internal(ref b2SegmentDistanceResult result, b2Vec2 p1, b2Vec2 q1, b2Vec2 p2, b2Vec2 q2);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ShapeDistance_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ShapeDistance_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ShapeDistance_internal(ref b2DistanceOutput result, in b2DistanceInput input, b2SimplexCache* cache, b2Simplex* simplexes, int simplexCapacity);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ShapeCast_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ShapeCast_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ShapeCast_internal(ref b2CastOutput result, in b2ShapeCastPairInput input);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeProxy_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeProxy_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MakeProxy_internal(ref b2ShapeProxy result, in b2Vec2 points, int count, float radius);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MakeOffsetProxy_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MakeOffsetProxy_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MakeOffsetProxy_internal(ref b2ShapeProxy result, in b2Vec2 points, int count, float radius, b2Vec2 position, b2Rot rotation);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2GetSweepTransform_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2GetSweepTransform_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2GetSweepTransform_internal(ref b2Transform result, in b2Sweep sweep, float time);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2TimeOfImpact_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2TimeOfImpact_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2TimeOfImpact_internal(ref b2TOIOutput result, in b2TOIInput input);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideCircles_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideCircles_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CollideCircles_internal(ref b2Manifold result, in b2Circle circleA, b2Transform xfA, in b2Circle circleB, b2Transform xfB);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideCapsuleAndCircle_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideCapsuleAndCircle_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CollideCapsuleAndCircle_internal(ref b2Manifold result, in b2Capsule capsuleA, b2Transform xfA, in b2Circle circleB, b2Transform xfB);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideSegmentAndCircle_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideSegmentAndCircle_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CollideSegmentAndCircle_internal(ref b2Manifold result, in b2Segment segmentA, b2Transform xfA, in b2Circle circleB, b2Transform xfB);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollidePolygonAndCircle_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollidePolygonAndCircle_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CollidePolygonAndCircle_internal(ref b2Manifold result, in b2Polygon polygonA, b2Transform xfA, in b2Circle circleB, b2Transform xfB);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideCapsules_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideCapsules_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CollideCapsules_internal(ref b2Manifold result, in b2Capsule capsuleA, b2Transform xfA, in b2Capsule capsuleB, b2Transform xfB);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideSegmentAndCapsule_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideSegmentAndCapsule_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CollideSegmentAndCapsule_internal(ref b2Manifold result, in b2Segment segmentA, b2Transform xfA, in b2Capsule capsuleB, b2Transform xfB);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollidePolygonAndCapsule_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollidePolygonAndCapsule_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CollidePolygonAndCapsule_internal(ref b2Manifold result, in b2Polygon polygonA, b2Transform xfA, in b2Capsule capsuleB, b2Transform xfB);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollidePolygons_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollidePolygons_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CollidePolygons_internal(ref b2Manifold result, in b2Polygon polygonA, b2Transform xfA, in b2Polygon polygonB, b2Transform xfB);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideSegmentAndPolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideSegmentAndPolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CollideSegmentAndPolygon_internal(ref b2Manifold result, in b2Segment segmentA, b2Transform xfA, in b2Polygon polygonB, b2Transform xfB);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideChainSegmentAndCircle_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideChainSegmentAndCircle_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CollideChainSegmentAndCircle_internal(ref b2Manifold result, in b2ChainSegment segmentA, b2Transform xfA, in b2Circle circleB, b2Transform xfB);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideChainSegmentAndCapsule_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideChainSegmentAndCapsule_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CollideChainSegmentAndCapsule_internal(ref b2Manifold result, in b2ChainSegment segmentA, b2Transform xfA, in b2Capsule capsuleB, b2Transform xfB, b2SimplexCache* cache);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CollideChainSegmentAndPolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CollideChainSegmentAndPolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CollideChainSegmentAndPolygon_internal(ref b2Manifold result, in b2ChainSegment segmentA, b2Transform xfA, in b2Polygon polygonB, b2Transform xfB, b2SimplexCache* cache);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_Create_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_Create_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DynamicTree_Create_internal(ref b2DynamicTree result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_Query_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_Query_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DynamicTree_Query_internal(ref b2TreeStats result, in b2DynamicTree tree, b2AABB aabb, ulong maskBits, IntPtr callback, void* context);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_QueryAll_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_QueryAll_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DynamicTree_QueryAll_internal(ref b2TreeStats result, in b2DynamicTree tree, b2AABB aabb, IntPtr callback, void* context);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_RayCast_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_RayCast_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DynamicTree_RayCast_internal(ref b2TreeStats result, in b2DynamicTree tree, in b2RayCastInput input, ulong maskBits, IntPtr callback, void* context);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_ShapeCast_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_ShapeCast_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DynamicTree_ShapeCast_internal(ref b2TreeStats result, in b2DynamicTree tree, in b2ShapeCastInput input, ulong maskBits, IntPtr callback, void* context);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_GetRootBounds_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_GetRootBounds_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DynamicTree_GetRootBounds_internal(ref b2AABB result, in b2DynamicTree tree);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DynamicTree_GetAABB_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DynamicTree_GetAABB_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DynamicTree_GetAABB_internal(ref b2AABB result, in b2DynamicTree tree, int proxyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2SolvePlanes_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2SolvePlanes_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2SolvePlanes_internal(ref b2PlaneSolverResult result, b2Vec2 targetDelta, b2CollisionPlane* planes, int count);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2ClipVector_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2ClipVector_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2ClipVector_internal(ref b2Vec2 result, b2Vec2 vector, in b2CollisionPlane planes, int count);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultWorldDef_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultWorldDef_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DefaultWorldDef_internal(ref b2WorldDef result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultBodyDef_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultBodyDef_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DefaultBodyDef_internal(ref b2BodyDef result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultFilter_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultFilter_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DefaultFilter_internal(ref b2Filter result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultQueryFilter_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultQueryFilter_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DefaultQueryFilter_internal(ref b2QueryFilter result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultSurfaceMaterial_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultSurfaceMaterial_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DefaultSurfaceMaterial_internal(ref b2SurfaceMaterial result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultShapeDef_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultShapeDef_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DefaultShapeDef_internal(ref b2ShapeDef result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultChainDef_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultChainDef_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DefaultChainDef_internal(ref b2ChainDef result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultDistanceJointDef_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultDistanceJointDef_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DefaultDistanceJointDef_internal(ref b2DistanceJointDef result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultMotorJointDef_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultMotorJointDef_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DefaultMotorJointDef_internal(ref b2MotorJointDef result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultFilterJointDef_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultFilterJointDef_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DefaultFilterJointDef_internal(ref b2FilterJointDef result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultPrismaticJointDef_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultPrismaticJointDef_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DefaultPrismaticJointDef_internal(ref b2PrismaticJointDef result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultRevoluteJointDef_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultRevoluteJointDef_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DefaultRevoluteJointDef_internal(ref b2RevoluteJointDef result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultWeldJointDef_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultWeldJointDef_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DefaultWeldJointDef_internal(ref b2WeldJointDef result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultWheelJointDef_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultWheelJointDef_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DefaultWheelJointDef_internal(ref b2WheelJointDef result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultExplosionDef_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultExplosionDef_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DefaultExplosionDef_internal(ref b2ExplosionDef result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2DefaultDebugDraw_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2DefaultDebugDraw_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2DefaultDebugDraw_internal(ref b2DebugDraw result);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateWorld_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateWorld_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CreateWorld_internal(ref b2WorldId result, in b2WorldDef def);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetBodyEvents_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetBodyEvents_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_GetBodyEvents_internal(ref b2BodyEvents result, b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetSensorEvents_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetSensorEvents_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_GetSensorEvents_internal(ref b2SensorEvents result, b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetContactEvents_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetContactEvents_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_GetContactEvents_internal(ref b2ContactEvents result, b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetJointEvents_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetJointEvents_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_GetJointEvents_internal(ref b2JointEvents result, b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_OverlapAABB_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_OverlapAABB_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_OverlapAABB_internal(ref b2TreeStats result, b2WorldId worldId, b2AABB aabb, b2QueryFilter filter, IntPtr fcn, void* context);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_OverlapShape_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_OverlapShape_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_OverlapShape_internal(ref b2TreeStats result, b2WorldId worldId, in b2ShapeProxy proxy, b2QueryFilter filter, IntPtr fcn, void* context);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_CastRay_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_CastRay_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_CastRay_internal(ref b2TreeStats result, b2WorldId worldId, b2Vec2 origin, b2Vec2 translation, b2QueryFilter filter, IntPtr fcn, void* context);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_CastRayClosest_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_CastRayClosest_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_CastRayClosest_internal(ref b2RayResult result, b2WorldId worldId, b2Vec2 origin, b2Vec2 translation, b2QueryFilter filter);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_CastShape_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_CastShape_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_CastShape_internal(ref b2TreeStats result, b2WorldId worldId, in b2ShapeProxy proxy, b2Vec2 translation, b2QueryFilter filter, IntPtr fcn, void* context);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetGravity_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetGravity_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_GetGravity_internal(ref b2Vec2 result, b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetProfile_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetProfile_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_GetProfile_internal(ref b2Profile result, b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2World_GetCounters_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2World_GetCounters_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2World_GetCounters_internal(ref b2Counters result, b2WorldId worldId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateBody_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateBody_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CreateBody_internal(ref b2BodyId result, b2WorldId worldId, in b2BodyDef def);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetPosition_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetPosition_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_GetPosition_internal(ref b2Vec2 result, b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetRotation_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetRotation_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_GetRotation_internal(ref b2Rot result, b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetTransform_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetTransform_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_GetTransform_internal(ref b2Transform result, b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetLocalPoint_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetLocalPoint_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_GetLocalPoint_internal(ref b2Vec2 result, b2BodyId bodyId, b2Vec2 worldPoint);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetWorldPoint_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetWorldPoint_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_GetWorldPoint_internal(ref b2Vec2 result, b2BodyId bodyId, b2Vec2 localPoint);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetLocalVector_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetLocalVector_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_GetLocalVector_internal(ref b2Vec2 result, b2BodyId bodyId, b2Vec2 worldVector);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetWorldVector_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetWorldVector_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_GetWorldVector_internal(ref b2Vec2 result, b2BodyId bodyId, b2Vec2 localVector);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetLinearVelocity_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetLinearVelocity_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_GetLinearVelocity_internal(ref b2Vec2 result, b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetLocalPointVelocity_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetLocalPointVelocity_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_GetLocalPointVelocity_internal(ref b2Vec2 result, b2BodyId bodyId, b2Vec2 localPoint);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetWorldPointVelocity_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetWorldPointVelocity_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_GetWorldPointVelocity_internal(ref b2Vec2 result, b2BodyId bodyId, b2Vec2 worldPoint);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetLocalCenterOfMass_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetLocalCenterOfMass_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_GetLocalCenterOfMass_internal(ref b2Vec2 result, b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetWorldCenterOfMass_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetWorldCenterOfMass_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_GetWorldCenterOfMass_internal(ref b2Vec2 result, b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetMassData_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetMassData_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_GetMassData_internal(ref b2MassData result, b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetMotionLocks_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetMotionLocks_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_GetMotionLocks_internal(ref b2MotionLocks result, b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_GetWorld_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_GetWorld_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_GetWorld_internal(ref b2WorldId result, b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Body_ComputeAABB_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Body_ComputeAABB_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Body_ComputeAABB_internal(ref b2AABB result, b2BodyId bodyId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateCircleShape_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateCircleShape_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CreateCircleShape_internal(ref b2ShapeId result, b2BodyId bodyId, in b2ShapeDef def, in b2Circle circle);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateSegmentShape_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateSegmentShape_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CreateSegmentShape_internal(ref b2ShapeId result, b2BodyId bodyId, in b2ShapeDef def, in b2Segment segment);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateCapsuleShape_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateCapsuleShape_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CreateCapsuleShape_internal(ref b2ShapeId result, b2BodyId bodyId, in b2ShapeDef def, in b2Capsule capsule);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreatePolygonShape_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreatePolygonShape_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CreatePolygonShape_internal(ref b2ShapeId result, b2BodyId bodyId, in b2ShapeDef def, in b2Polygon polygon);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetBody_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetBody_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_GetBody_internal(ref b2BodyId result, b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetWorld_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetWorld_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_GetWorld_internal(ref b2WorldId result, b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetSurfaceMaterial_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetSurfaceMaterial_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_GetSurfaceMaterial_internal(ref b2SurfaceMaterial result, b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetFilter_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetFilter_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_GetFilter_internal(ref b2Filter result, b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_RayCast_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_RayCast_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_RayCast_internal(ref b2CastOutput result, b2ShapeId shapeId, in b2RayCastInput input);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetCircle_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetCircle_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_GetCircle_internal(ref b2Circle result, b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetSegment_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetSegment_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_GetSegment_internal(ref b2Segment result, b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetChainSegment_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetChainSegment_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_GetChainSegment_internal(ref b2ChainSegment result, b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetCapsule_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetCapsule_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_GetCapsule_internal(ref b2Capsule result, b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetPolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetPolygon_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_GetPolygon_internal(ref b2Polygon result, b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetParentChain_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetParentChain_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_GetParentChain_internal(ref b2ChainId result, b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetAABB_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetAABB_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_GetAABB_internal(ref b2AABB result, b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_ComputeMassData_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_ComputeMassData_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_ComputeMassData_internal(ref b2MassData result, b2ShapeId shapeId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Shape_GetClosestPoint_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Shape_GetClosestPoint_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Shape_GetClosestPoint_internal(ref b2Vec2 result, b2ShapeId shapeId, b2Vec2 target);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateChain_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateChain_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CreateChain_internal(ref b2ChainId result, b2BodyId bodyId, in b2ChainDef def);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Chain_GetWorld_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Chain_GetWorld_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Chain_GetWorld_internal(ref b2WorldId result, b2ChainId chainId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Chain_GetSurfaceMaterial_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Chain_GetSurfaceMaterial_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Chain_GetSurfaceMaterial_internal(ref b2SurfaceMaterial result, b2ChainId chainId, int materialIndex);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetBodyA_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetBodyA_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Joint_GetBodyA_internal(ref b2BodyId result, b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetBodyB_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetBodyB_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Joint_GetBodyB_internal(ref b2BodyId result, b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetWorld_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetWorld_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Joint_GetWorld_internal(ref b2WorldId result, b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetLocalFrameA_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetLocalFrameA_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Joint_GetLocalFrameA_internal(ref b2Transform result, b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetLocalFrameB_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetLocalFrameB_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Joint_GetLocalFrameB_internal(ref b2Transform result, b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Joint_GetConstraintForce_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Joint_GetConstraintForce_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Joint_GetConstraintForce_internal(ref b2Vec2 result, b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateDistanceJoint_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateDistanceJoint_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CreateDistanceJoint_internal(ref b2JointId result, b2WorldId worldId, in b2DistanceJointDef def);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateMotorJoint_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateMotorJoint_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CreateMotorJoint_internal(ref b2JointId result, b2WorldId worldId, in b2MotorJointDef def);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2MotorJoint_GetLinearVelocity_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2MotorJoint_GetLinearVelocity_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2MotorJoint_GetLinearVelocity_internal(ref b2Vec2 result, b2JointId jointId);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateFilterJoint_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateFilterJoint_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CreateFilterJoint_internal(ref b2JointId result, b2WorldId worldId, in b2FilterJointDef def);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreatePrismaticJoint_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreatePrismaticJoint_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CreatePrismaticJoint_internal(ref b2JointId result, b2WorldId worldId, in b2PrismaticJointDef def);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateRevoluteJoint_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateRevoluteJoint_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CreateRevoluteJoint_internal(ref b2JointId result, b2WorldId worldId, in b2RevoluteJointDef def);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateWeldJoint_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateWeldJoint_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CreateWeldJoint_internal(ref b2JointId result, b2WorldId worldId, in b2WeldJointDef def);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2CreateWheelJoint_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2CreateWheelJoint_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2CreateWheelJoint_internal(ref b2JointId result, b2WorldId worldId, in b2WheelJointDef def);

#if __IOS__
[DllImport("@rpath/box2d.framework/box2d", EntryPoint = "b2Contact_GetData_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("box2d", EntryPoint = "b2Contact_GetData_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void b2Contact_GetData_internal(ref b2ContactData result, b2ContactId contactId);

}
}
