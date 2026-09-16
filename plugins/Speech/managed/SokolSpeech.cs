#if __ANDROID__ || __IOS__ || __MACOS__ || __WINDOWS__ || WEB
using System;
using System.Runtime.InteropServices;

internal static class SokolSpeech
{
#if __IOS__
    const string Lib = "@rpath/sokol_speech.framework/sokol_speech";
#else
    const string Lib = "sokol_speech";
#endif

    /* Mirrors sokolspeech_event in sokol_speech.h. */
    [StructLayout(LayoutKind.Sequential)]
    internal struct Event
    {
        public int type;
        public int id;
        public int code;
    }

    [DllImport(Lib, EntryPoint = "sokolspeech_init", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Init();

    [DllImport(Lib, EntryPoint = "sokolspeech_available", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Available([MarshalAs(UnmanagedType.LPUTF8Str)] string lang);

    [DllImport(Lib, EntryPoint = "sokolspeech_say", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Say([MarshalAs(UnmanagedType.LPUTF8Str)] string text,
                                   [MarshalAs(UnmanagedType.LPUTF8Str)] string lang);

    [DllImport(Lib, EntryPoint = "sokolspeech_stop", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Stop();

    [DllImport(Lib, EntryPoint = "sokolspeech_open_voice_settings", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void OpenVoiceSettings();

    [DllImport(Lib, EntryPoint = "sokolspeech_poll_event", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool PollEvent(out Event evt);

    [DllImport(Lib, EntryPoint = "sokolspeech_shutdown", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Shutdown();
}
#endif
