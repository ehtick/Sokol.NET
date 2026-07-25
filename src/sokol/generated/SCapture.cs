// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

using static Sokol.SG;

namespace Sokol
{
public static unsafe partial class SCapture
{
#if WEB
[DllImport("sokol", EntryPoint = "scap_supported", CallingConvention = CallingConvention.Cdecl)]
private static extern int scap_supported_native();
public static bool scap_supported() => scap_supported_native() != 0;
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "scap_supported", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "scap_supported", CallingConvention = CallingConvention.Cdecl)]
#endif
[return: M(U.I1)]
public static extern bool scap_supported();
#endif

#if WEB
[DllImport("sokol", EntryPoint = "scap_read_image", CallingConvention = CallingConvention.Cdecl)]
private static extern int scap_read_image_native(sg_image img, int width, int height, byte* out_rgba, int out_size);
public static bool scap_read_image(sg_image img, int width, int height, byte* out_rgba, int out_size) => scap_read_image_native(img, width, height, out_rgba, out_size) != 0;
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "scap_read_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "scap_read_image", CallingConvention = CallingConvention.Cdecl)]
#endif
[return: M(U.I1)]
public static extern bool scap_read_image(sg_image img, int width, int height, byte* out_rgba, int out_size);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "scap_error", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "scap_error", CallingConvention = CallingConvention.Cdecl)]
#endif
private static extern IntPtr scap_error_native();

public static string scap_error()
{
    IntPtr ptr = scap_error_native();
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

}
}
