// P/Invoke bindings to the native sokolshare_image_text() function.
// Android, iOS, macOS: native share sheet with image.
// Windows / Linux: managed fallback in SharePlugin.cs.
#if __ANDROID__ || __IOS__ || __MACOS__
using System.Runtime.InteropServices;

internal static class SokolShare
{
#if __IOS__
    const string Lib = "@rpath/sokol_share.framework/sokol_share";
#else
    const string Lib = "sokol_share";
#endif

    // Share an image file and a text string via the platform's native share sheet.
    // imagePath: absolute path to a PNG written by SFilesystem (pass null for text-only).
    // text:      UTF-8 share message.
    [DllImport(Lib, EntryPoint = "sokolshare_image_text",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ShareImageText(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? imagePath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string  text);
}
#endif
