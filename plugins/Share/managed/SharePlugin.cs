// High-level cross-platform share API.
// Call SharePlugin.ShareScore(score, gameName) when the player taps Share.
//
// Android / iOS / macOS: native share sheet with image + text.
// Windows             : MAPI — opens default mail client with image pre-attached.
// Linux               : xdg-email with --attach for image + text.
// Web                 : not supported (call sites guarded by #if !WEB).
//
// All file I/O uses Sokol.SFilesystem exclusively — no System.IO file access.
#if !WEB
using static Sokol.SFilesystem;
using System.Runtime.InteropServices;

public static unsafe class SharePlugin
{
    // Entry point — call at game-over when the player taps the Share button.
    public static void ShareScore(int score, string gameName = "Sokol.NET Game")
    {
        string imagePath = GenerateScoreCard(score, gameName);
        string text      = $"I scored {score:N0} pts in {gameName}! Can you beat me?";
        ShareImageAndText(imagePath, text);
    }

    // ── Image generation ──────────────────────────────────────────────────────

    static string GenerateScoreCard(int score, string gameName)
    {
        IntPtr dirPtr = sfs_get_temp_dir();
        string dir = Marshal.PtrToStringUTF8(dirPtr) ?? "";
        sfs_free_path(dirPtr);

        string path     = dir + "score_card.png";
        byte[] pngBytes = ScoreCardRenderer.Render(score, gameName);

        var file = sfs_open_file(path, sfs_open_mode_t.SFS_OPEN_CREATE_WRITE);
        if (file != IntPtr.Zero)
        {
            fixed (byte* p = pngBytes) sfs_write_file(file, p, pngBytes.Length);
            sfs_close_file(file);
        }
        return path;
    }

    // ── Platform dispatch ─────────────────────────────────────────────────────

    static void ShareImageAndText(string imagePath, string text)
    {
#if __ANDROID__ || __IOS__ || __MACOS__
        SokolShare.ShareImageText(imagePath, text);
#elif __WINDOWS__
        ShareWindows(imagePath);
#elif __LINUX__
        ShareLinux(imagePath, text);
#endif
    }

#if __WINDOWS__
    // MAPI: opens the default Windows mail client with the image pre-attached.
    // MAPISendDocuments is in mapi32.dll, present on all modern Windows.
    [DllImport("mapi32.dll", CharSet = CharSet.Ansi,
               BestFitMapping = false, ThrowOnUnmappableChar = true)]
    static extern int MAPISendDocuments(
        nint   ulUIParam,
        [MarshalAs(UnmanagedType.LPStr)] string lpszDelimChar,
        [MarshalAs(UnmanagedType.LPStr)] string lpszFilePaths,
        [MarshalAs(UnmanagedType.LPStr)] string lpszFileNames,
        uint   ulReserved);

    static void ShareWindows(string imagePath)
    {
        try
        {
            string fileName = Path.GetFileName(imagePath);
            int result = MAPISendDocuments(0, ";", imagePath, fileName, 0);
            if (result != 0)
                throw new Exception($"MAPI error {result}");
        }
        catch
        {
            // Fallback: reveal in Explorer so the user can attach manually.
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(
                        "explorer.exe", $"/select,\"{imagePath}\"")
                    { UseShellExecute = true });
            }
            catch { /* best-effort */ }
        }
    }
#endif

#if __LINUX__
    static void ShareLinux(string imagePath, string text)
    {
        try
        {
            // xdg-email opens the default mail client with an attachment and body pre-filled.
            var psi = new System.Diagnostics.ProcessStartInfo("xdg-email")
            {
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("--attach");
            psi.ArgumentList.Add(imagePath);
            psi.ArgumentList.Add("--body");
            psi.ArgumentList.Add(text);
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // Fallback: open the image in the system viewer.
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(imagePath)
                    { UseShellExecute = true });
            }
            catch { /* best-effort */ }
        }
    }
#endif
}
#endif  // !WEB
