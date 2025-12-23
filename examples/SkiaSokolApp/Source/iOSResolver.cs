using System;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static System.Numerics.Matrix4x4;
using static Sokol.SDebugUI;
using static skia_shader_cs.Shaders;
using SkiaSharp;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public static unsafe partial class SkiaSokolApp
{
#if __IOS__
    // iOS DllImport resolver to map libSkiaSharp to framework path
    static SkiaSokolApp()
    {
        // Resolver for main SkiaSharp assembly
        NativeLibrary.SetDllImportResolver(typeof(SKImageInfo).Assembly, DllImportResolver);
        
        // Resolver for SkiaSharp.Skottie assembly
        try
        {
            var skottieAssembly = typeof(SkiaSharp.Skottie.Animation).Assembly;
            NativeLibrary.SetDllImportResolver(skottieAssembly, DllImportResolver);
        }
        catch (Exception ex)
        {
            Error($"Warning: Could not set resolver for Skottie assembly: {ex.Message}");
        }

        // Resolver for HarfBuzzSharp assembly
        try
        {
            var harfBuzzAssembly = typeof(HarfBuzzSharp.Blob).Assembly;
            NativeLibrary.SetDllImportResolver(harfBuzzAssembly, DllImportResolver);
        }
        catch (Exception ex)
        {
            Error($"Warning: Could not set resolver for HarfBuzzSharp assembly: {ex.Message}");
        }

        // Resolver for SkiaSharp.HarfBuzz assembly
        try
        {
            var skiaHarfBuzzAssembly = typeof(SkiaSharp.HarfBuzz.SKShaper).Assembly;
            NativeLibrary.SetDllImportResolver(skiaHarfBuzzAssembly, DllImportResolver);
        }
        catch (Exception ex)
        {
            Error($"Warning: Could not set resolver for SkiaSharp.HarfBuzz assembly: {ex.Message}");
        }
    }

    private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName == "libSkiaSharp")
        {
            // Load from framework path on iOS
            if (NativeLibrary.TryLoad("@rpath/libSkiaSharp.framework/libSkiaSharp", assembly, searchPath, out IntPtr handle))
            {
                return handle;
            }
        }
        else if (libraryName == "libHarfBuzzSharp")
        {
            // Load from framework path on iOS
            if (NativeLibrary.TryLoad("@rpath/libHarfBuzzSharp.framework/libHarfBuzzSharp", assembly, searchPath, out IntPtr handle))
            {
                return handle;
            }
        }
        return IntPtr.Zero;
    }
#endif
}