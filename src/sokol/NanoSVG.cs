using System;
using System.Runtime.InteropServices;

namespace Sokol
{
    public static unsafe partial class NanoSVG
    {
        // NSVGpaint has an anonymous union { unsigned int color; NSVGgradient* gradient; }
        // after a signed char type field.  The union is aligned to pointer-size, so the
        // offset differs between 32-bit platforms (WASM and Android armeabi-v7a: offset 4)
        // and 64-bit platforms (offset 8).
#if WEB || __ANDROID_ARM32__
        [StructLayout(LayoutKind.Explicit, Size = 8)]
        public struct NSVGpaint
        {
            [FieldOffset(0)] public sbyte type;
            [FieldOffset(4)] public uint color;
            [FieldOffset(4)] public uint gradient;   // NSVGgradient* (32-bit ptr)
        }
#else
        [StructLayout(LayoutKind.Explicit, Size = 16)]
        public struct NSVGpaint
        {
            [FieldOffset(0)]  public sbyte type;
            [FieldOffset(8)]  public uint color;
            [FieldOffset(8)]  public NSVGgradient* gradient;
        }
#endif

        // Rasterizer bindings — NSVGrasterizer is opaque, exposed as IntPtr.
#if __IOS__
        [DllImport("@rpath/sokol.framework/sokol", EntryPoint = "nsvgCreateRasterizer", CallingConvention = CallingConvention.Cdecl)]
#else
        [DllImport("sokol", EntryPoint = "nsvgCreateRasterizer", CallingConvention = CallingConvention.Cdecl)]
#endif
        public static extern IntPtr nsvgCreateRasterizer();

#if __IOS__
        [DllImport("@rpath/sokol.framework/sokol", EntryPoint = "nsvgRasterize", CallingConvention = CallingConvention.Cdecl)]
#else
        [DllImport("sokol", EntryPoint = "nsvgRasterize", CallingConvention = CallingConvention.Cdecl)]
#endif
        public static extern void nsvgRasterize(IntPtr r, NSVGimage* image, float tx, float ty, float scale, byte* dst, int w, int h, int stride);

#if __IOS__
        [DllImport("@rpath/sokol.framework/sokol", EntryPoint = "nsvgDeleteRasterizer", CallingConvention = CallingConvention.Cdecl)]
#else
        [DllImport("sokol", EntryPoint = "nsvgDeleteRasterizer", CallingConvention = CallingConvention.Cdecl)]
#endif
        public static extern void nsvgDeleteRasterizer(IntPtr r);
    }
}
