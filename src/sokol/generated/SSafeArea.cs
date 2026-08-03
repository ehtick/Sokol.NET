// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

using static Sokol.SApp;

namespace Sokol
{
public static unsafe partial class SSafeArea
{
#if WEB
[DllImport("sokol", EntryPoint = "ssafe_supported", CallingConvention = CallingConvention.Cdecl)]
private static extern int ssafe_supported_native();
public static bool ssafe_supported() => ssafe_supported_native() != 0;
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "ssafe_supported", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "ssafe_supported", CallingConvention = CallingConvention.Cdecl)]
#endif
[return: M(U.I1)]
public static extern bool ssafe_supported();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "ssafe_get", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "ssafe_get", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ssafe_get(ref float out_ltrb);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "ssafe_defer_bottom_gestures", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "ssafe_defer_bottom_gestures", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ssafe_defer_bottom_gestures(bool enable);

}
}
