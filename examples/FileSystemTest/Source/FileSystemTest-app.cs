using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SImgui;
using static Sokol.SFilesystem;
using static Sokol.SFilesystem.sfs_result_t;
using static Sokol.SFilesystem.sfs_open_mode_t;
using static Sokol.SFilesystem.sfs_folder_t;
using static Sokol.SFilesystem.sfs_whence_t;
using static Sokol.SFilesystem.sfs_glob_flags_t;
using static Sokol.SFilesystem.sfs_enum_result_t;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Imgui.ImguiNative;
using Imgui;
using static filesystemtest_bg_shader_cs.Shaders;

/// <summary>
/// Comprehensive SFilesystem API test app.
/// Covers: path queries, user folders, asset reads, file I/O (write/read/seek/tell/
/// filesize/flush/eof/append/copy/rename/remove), directory enumeration, glob,
/// and set_current_directory.  ImGui shows per-category tabs with coloured pass/fail rows.
/// </summary>
public static unsafe class FilesystemTestApp
{
    // -------------------------------------------------------------------------
    // Graphics state
    // -------------------------------------------------------------------------
    struct GfxState
    {
        public sg_pipeline pip;
        public sg_bindings bind;
        public sg_pass_action pass_action;
    }
    static GfxState _gfx;

    // -------------------------------------------------------------------------
    // Test result storage (all static managed lists — safe in NativeAOT)
    // -------------------------------------------------------------------------
    static string _basePath  = "";
    static string _prefPath  = "";
    static string _assetsDir = "";
    static string _tempDir   = "";
    static string _cwd       = "";

    static (string name, sfs_folder_t id, string path)[] _folders =
    {
        ("Home",       SFS_FOLDER_HOME,      ""),
        ("Desktop",    SFS_FOLDER_DESKTOP,   ""),
        ("Documents",  SFS_FOLDER_DOCUMENTS, ""),
        ("Downloads",  SFS_FOLDER_DOWNLOADS, ""),
        ("Music",      SFS_FOLDER_MUSIC,     ""),
        ("Pictures",   SFS_FOLDER_PICTURES,  ""),
        ("SavedGames", SFS_FOLDER_SAVEDGAMES,""),
    };

    static readonly List<(string sec, string name, bool ok, string detail)> _results = new();
    static readonly List<string> _enumEntries = new();
    static readonly List<string> _globMatches  = new();
    static string _assetFileContent = "";
    static bool   _fileIODone  = false;
    static bool   _dirScanDone = false;

    // ImGui tab-visible bytes (reset to 1 every frame so tabs are never permanently closed)
    static byte _t0=1, _t1=1, _t2=1, _t3=1, _t4=1, _t5=1;

    // Colour constants for pass/fail/neutral text
    static readonly Vector4 CPass = new(0.40f, 1.00f, 0.40f, 1f);
    static readonly Vector4 CFail = new(1.00f, 0.40f, 0.40f, 1f);
    static readonly Vector4 CGray = new(0.70f, 0.70f, 0.70f, 1f);
    static readonly Vector4 CGold = new(1.00f, 0.85f, 0.40f, 1f);

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Marshal an IntPtr returned by any sfs_get_* function and immediately free it.</summary>
    static string SfsStr(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return $"<null: {sfs_get_error()}>";
        var s = Marshal.PtrToStringUTF8(ptr) ?? "";
        sfs_free_path(ptr);
        return s;
    }

    static bool Ok(sfs_result_t r) => r == SFS_RESULT_OK;

    static void Add(string sec, string name, bool ok, string detail)
        => _results.Add((sec, name, ok, detail));

    // -------------------------------------------------------------------------
    // Enumerate directory callback — must be a static unmanaged callable
    // -------------------------------------------------------------------------
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    static sfs_enum_result_t OnEnumEntry(void* userdata, byte* dirname, byte* fname)
    {
        string? n = Marshal.PtrToStringUTF8((IntPtr)fname);
        if (n != null) _enumEntries.Add(n);
        return SFS_ENUM_CONTINUE;
    }

    // =========================================================================
    // Test phases
    // =========================================================================

    // -------------------------------------------------------------------------
    // 1. Path queries
    // -------------------------------------------------------------------------
    static void RunPathTests()
    {
        _basePath  = SfsStr(sfs_get_base_path());
        Add("Paths", "sfs_get_base_path",         _basePath.Length  > 0, _basePath);

        _prefPath  = SfsStr(sfs_get_pref_path("SokolNET", "FileSystemTest"));
        Add("Paths", "sfs_get_pref_path",          _prefPath.Length  > 0, _prefPath);

        _assetsDir = SfsStr(sfs_get_assets_dir());
        Add("Paths", "sfs_get_assets_dir",         _assetsDir.Length > 0, _assetsDir);

        _tempDir   = SfsStr(sfs_get_temp_dir());
        Add("Paths", "sfs_get_temp_dir",           _tempDir.Length   > 0, _tempDir);

        _cwd       = SfsStr(sfs_get_current_directory());
        Add("Paths", "sfs_get_current_directory",  _cwd.Length       > 0, _cwd);

        // sfs_set_current_directory — change to pref path then restore
        if (_prefPath.Length > 0)
        {
            sfs_create_directory(_prefPath);
            var r = sfs_set_current_directory(_prefPath);
            Add("Paths", "sfs_set_current_directory", Ok(r),
                Ok(r) ? "→ " + _prefPath : sfs_get_error());
            if (_cwd.Length > 0) sfs_set_current_directory(_cwd);
        }
    }

    // -------------------------------------------------------------------------
    // 2. User folders
    // -------------------------------------------------------------------------
    static void RunUserFolderTests()
    {
        for (int i = 0; i < _folders.Length; i++)
        {
            string path = SfsStr(sfs_get_user_folder(_folders[i].id));
            _folders[i] = (_folders[i].name, _folders[i].id, path);
            Add("Folders", $"sfs_get_user_folder({_folders[i].name})", path.Length > 0, path);
        }
    }

    // -------------------------------------------------------------------------
    // 3. Read existing asset files (the Assets/ folder ships with the build)
    // -------------------------------------------------------------------------
    static void RunAssetTests()
    {
        // Check assets directory itself
        bool isDir = sfs_is_directory(_assetsDir);
        Add("Assets", "sfs_get_assets_dir is_directory", isDir, _assetsDir);

        // --- sample.txt ---
        string samplePath = _assetsDir + "test_data/sample.txt";
        Add("Assets", "sample.txt sfs_is_file", sfs_is_file(samplePath), samplePath);

        if (sfs_is_file(samplePath))
        {
            // sfs_get_path_info
            sfs_path_info_t info;
            var pi = sfs_get_path_info(samplePath, &info);
            Add("Assets", "sfs_get_path_info",
                Ok(pi),
                Ok(pi) ? $"size={info.size} mtime={info.modify_time} ctime={info.create_time}"
                       : sfs_get_error());

            // sfs_get_last_modified_time
            long mtime = sfs_get_last_modified_time(samplePath);
#if __ANDROID__
            // APK-bundled assets have no modification time; 0 is the expected value
            Add("Assets", "sfs_get_last_modified_time", mtime >= 0, $"unix={mtime}");
#else
            Add("Assets", "sfs_get_last_modified_time", mtime > 0, $"unix={mtime}");
#endif

            // Open + get file size
            var fh = sfs_open_file(samplePath, SFS_OPEN_READ);
            if (fh != IntPtr.Zero)
            {
                long sz = sfs_get_file_size(fh);
                Add("Assets", "sfs_get_file_size", sz > 0, $"{sz} bytes");

                // Read entire file
                byte[] buf = new byte[sz > 0 ? sz : 1024];
                fixed (byte* p = buf)
                {
                    long n = sfs_read_file(fh, p, buf.Length);
                    Add("Assets", "sfs_read_file", n > 0, $"read {n} bytes");
                    if (n > 0)
                        _assetFileContent = System.Text.Encoding.UTF8.GetString(buf, 0, (int)n);
                }

                // sfs_eof_file: probe one byte past end to trigger the flag
                byte probe = 0;
                sfs_read_file(fh, &probe, 1);
                bool eof = sfs_eof_file(fh);
                Add("Assets", "sfs_eof_file (after full read)", eof, eof ? "EOF" : "not EOF");

                // sfs_seek_file SET
                long p1 = sfs_seek_file(fh, 6, SFS_WHENCE_SET);
                Add("Assets", "sfs_seek_file(SET, 6)", p1 == 6, $"pos={p1}");

                // sfs_tell_file
                long told = sfs_tell_file(fh);
                Add("Assets", "sfs_tell_file == 6", told == 6, $"pos={told}");

                // sfs_seek_file from END
                long p2 = sfs_seek_file(fh, 0, SFS_WHENCE_END);
                Add("Assets", "sfs_seek_file(END, 0)", p2 == sz, $"pos={p2} size={sz}");

                sfs_close_file(fh);
            }
            else
            {
                Add("Assets", "sfs_open_file (sample.txt)", false, sfs_get_error());
            }
        }

        // --- numbers.dat ---
        string datPath = _assetsDir + "test_data/numbers.dat";
        Add("Assets", "numbers.dat sfs_path_exists", sfs_path_exists(datPath), datPath);

        // --- a path that is NOT there must not report as present ---
        // Regression guard: Android falls back to AAssetManager_openDir() when stat() fails, and
        // that call hands back a valid (empty) handle for a directory that does not exist. Until
        // it was made to require at least one entry, these two answered TRUE for every path.
        string ghostPath = _assetsDir + "test_data/no_such_entry_here";
        Add("Assets", "missing path !sfs_path_exists",  !sfs_path_exists(ghostPath),  ghostPath);
        Add("Assets", "missing path !sfs_is_directory", !sfs_is_directory(ghostPath), ghostPath);

        // --- binary.bin (all 256 byte values 0x00-0xFF) ---
        string binPath = _assetsDir + "test_data/binary.bin";
        Add("Assets", "binary.bin sfs_path_exists", sfs_path_exists(binPath), binPath);
        Add("Assets", "binary.bin sfs_is_file",     sfs_is_file(binPath),     binPath);
        var bh = sfs_open_file(binPath, SFS_OPEN_READ);
        if (bh != IntPtr.Zero)
        {
            long bsz = sfs_get_file_size(bh);
            Add("Assets", "binary.bin sfs_get_file_size == 256", bsz == 256, $"{bsz} bytes");
            byte[] bb = new byte[256];
            fixed (byte* p = bb)
            {
                long n = sfs_read_file(bh, p, 256);
                Add("Assets", "binary.bin sfs_read_file (256 bytes)", n == 256, $"{n}/256 bytes");
                bool byteOk = (n == 256);
                for (int i = 0; i < (int)n && byteOk; i++)
                    if (bb[i] != (byte)i) { byteOk = false; }
                Add("Assets", "binary.bin byte integrity (all 256 values)", byteOk,
                    byteOk ? "buf[0x00]=0 buf[0x7F]=127 buf[0x80]=128 buf[0xFF]=255" : "MISMATCH");
            }
            sfs_close_file(bh);
        }
        else Add("Assets", "sfs_open_file (binary.bin)", false, sfs_get_error());

        // --- readme.txt ---
        string readmePath = _assetsDir + "readme.txt";
        Add("Assets", "readme.txt sfs_is_file", sfs_is_file(readmePath), readmePath);
    }

    // -------------------------------------------------------------------------
    // 4. Full write/read/append/copy/rename/delete cycle in writable pref path
    // -------------------------------------------------------------------------
    static void RunFileIOTests()
    {
        _fileIODone = false;
        if (_prefPath.Length == 0)
        {
            Add("FileIO", "SKIP — pref path unavailable", false, "");
            return;
        }

        sfs_create_directory(_prefPath);
        string tf  = _prefPath + "sfs_test.txt";
        string cp  = _prefPath + "sfs_test_copy.txt";
        string rn  = _prefPath + "sfs_test_renamed.txt";
        string sub = _prefPath + "subdir";
        const string content = "Hello SFilesystem!\nLine two.\nLine three.\n";

        // --- sfs_open_file (CREATE_WRITE) + sfs_write_file + sfs_flush_file ---
        var wh = sfs_open_file(tf, SFS_OPEN_CREATE_WRITE);
        bool wrote = false;
        if (wh != IntPtr.Zero)
        {
            byte[] wb = System.Text.Encoding.UTF8.GetBytes(content);
            fixed (byte* p = wb)
            {
                long n = sfs_write_file(wh, p, wb.Length);
                wrote = n == wb.Length;
                Add("FileIO", "sfs_write_file (CREATE_WRITE)", wrote, $"{n}/{wb.Length} bytes");
            }
            var fr = sfs_flush_file(wh);
            Add("FileIO", "sfs_flush_file", Ok(fr), Ok(fr) ? "ok" : sfs_get_error());
            sfs_close_file(wh);
        }
        else Add("FileIO", "sfs_open_file (CREATE_WRITE)", false, sfs_get_error());

        // --- sfs_open_file (READ) + sfs_get_file_size + sfs_read_file ---
        var rh = sfs_open_file(tf, SFS_OPEN_READ);
        if (rh != IntPtr.Zero)
        {
            long sz = sfs_get_file_size(rh);
            Add("FileIO", "sfs_get_file_size", sz > 0, $"{sz} bytes");

            byte[] rb = new byte[sz];
            fixed (byte* p = rb)
            {
                long n = sfs_read_file(rh, p, sz);
                string readBack = System.Text.Encoding.UTF8.GetString(rb, 0, (int)n);
                bool match = readBack == content;
                Add("FileIO", "sfs_read_file content match", match, match ? "ok" : "MISMATCH");
            }

            // sfs_seek_file and sfs_tell_file
            long p1 = sfs_seek_file(rh, 6, SFS_WHENCE_SET);
            Add("FileIO", "sfs_seek_file(SET, 6)",  p1 == 6, $"pos={p1}");
            Add("FileIO", "sfs_tell_file == 6",     sfs_tell_file(rh) == 6, $"pos={sfs_tell_file(rh)}");
            long p2 = sfs_seek_file(rh, 2, SFS_WHENCE_CUR);
            Add("FileIO", "sfs_seek_file(CUR, 2)",  p2 == 8, $"pos={p2}");
            long p3 = sfs_seek_file(rh, 0, SFS_WHENCE_END);
            Add("FileIO", "sfs_seek_file(END, 0)",  p3 == sz, $"pos={p3} size={sz}");

            sfs_close_file(rh);
        }
        else Add("FileIO", "sfs_open_file (READ)", false, sfs_get_error());

        // --- sfs_open_file (APPEND) + sfs_write_file ---
        var ah = sfs_open_file(tf, SFS_OPEN_APPEND);
        if (ah != IntPtr.Zero)
        {
            const string extra = "Appended line.\n";
            byte[] ab = System.Text.Encoding.UTF8.GetBytes(extra);
            fixed (byte* p = ab)
            {
                long n = sfs_write_file(ah, p, ab.Length);
                Add("FileIO", "sfs_write_file (APPEND)", n == ab.Length, $"{n} bytes");
            }
            sfs_close_file(ah);
        }

        // Verify file grew after append
        {
            var vh = sfs_open_file(tf, SFS_OPEN_READ);
            if (vh != IntPtr.Zero)
            {
                long newSz = sfs_get_file_size(vh);
                Add("FileIO", "file grew after append", newSz > content.Length, $"size={newSz}");
                sfs_close_file(vh);
            }
        }

        // --- sfs_get_path_info + sfs_get_last_modified_time on written file ---
        {
            sfs_path_info_t info2;
            var pi2 = sfs_get_path_info(tf, &info2);
            Add("FileIO", "sfs_get_path_info (written file)", Ok(pi2),
                Ok(pi2) ? $"size={info2.size} mtime={info2.modify_time}" : sfs_get_error());
            long lm = sfs_get_last_modified_time(tf);
            Add("FileIO", "sfs_get_last_modified_time", lm > 0, $"unix={lm}");
        }

        // --- sfs_copy_file ---
        var cr = sfs_copy_file(tf, cp);
        Add("FileIO", "sfs_copy_file", Ok(cr), Ok(cr) ? cp : sfs_get_error());
        if (Ok(cr))
            Add("FileIO", "copy sfs_path_exists", sfs_path_exists(cp), "");

        // --- sfs_rename_path ---
        var mr = sfs_rename_path(cp, rn);
        Add("FileIO", "sfs_rename_path", Ok(mr), Ok(mr) ? rn : sfs_get_error());
        if (Ok(mr))
        {
            Add("FileIO", "old copy gone  (sfs_is_file=false)", !sfs_is_file(cp), "");
            Add("FileIO", "renamed exists (sfs_is_file=true)",   sfs_is_file(rn),  "");
        }

        // --- sfs_create_directory + sfs_is_directory ---
        var md = sfs_create_directory(sub);
        Add("FileIO", "sfs_create_directory", Ok(md), Ok(md) ? sub : sfs_get_error());
        if (Ok(md))
            Add("FileIO", "subdir sfs_is_directory", sfs_is_directory(sub), "");

        // --- binary write / read-back (tests all 256 byte values including null) ---
        {
            string bf = _prefPath + "sfs_binary_test.bin";
            byte[] bw = new byte[256];
            for (int i = 0; i < 256; i++) bw[i] = (byte)i;
            var bwh = sfs_open_file(bf, SFS_OPEN_CREATE_WRITE);
            if (bwh != IntPtr.Zero)
            {
                fixed (byte* p = bw)
                {
                    long n = sfs_write_file(bwh, p, 256);
                    Add("FileIO", "sfs_write_file (binary 256 bytes)", n == 256, $"{n}/256 bytes");
                }
                sfs_close_file(bwh);
                var brh = sfs_open_file(bf, SFS_OPEN_READ);
                if (brh != IntPtr.Zero)
                {
                    long brsz = sfs_get_file_size(brh);
                    Add("FileIO", "sfs_get_file_size (binary) == 256", brsz == 256, $"{brsz} bytes");
                    byte[] br = new byte[256];
                    fixed (byte* p = br)
                    {
                        long n = sfs_read_file(brh, p, 256);
                        bool ok = (n == 256);
                        for (int i = 0; i < (int)n && ok; i++)
                            if (br[i] != (byte)i) { ok = false; }
                        Add("FileIO", "sfs_read_file binary round-trip", ok,
                            ok ? "all 256 bytes match" : "MISMATCH");
                    }
                    sfs_close_file(brh);
                }
                else Add("FileIO", "sfs_open_file (binary read-back)", false, sfs_get_error());
                sfs_remove_path(bf);
            }
            else Add("FileIO", "sfs_open_file (binary CREATE_WRITE)", false, sfs_get_error());
        }

        // --- sfs_remove_path ---
        Add("FileIO", "sfs_remove_path (testFile)",    Ok(sfs_remove_path(tf)), "");
        if (Ok(mr)) Add("FileIO", "sfs_remove_path (renamed)", Ok(sfs_remove_path(rn)), "");
        if (Ok(md)) Add("FileIO", "sfs_remove_path (subdir)",  Ok(sfs_remove_path(sub)), "");

        _fileIODone = true;
    }

    // -------------------------------------------------------------------------
    // 5. Enumerate directory + glob
    // -------------------------------------------------------------------------
    static void RunDirectoryScan()
    {
        _dirScanDone = false;
        if (_prefPath.Length == 0) return;

        sfs_create_directory(_prefPath);

        // Create some test files to scan
        string[] files = { "enum_a.txt", "enum_b.txt", "enum_c.dat", "enum_d.txt" };
        foreach (var f in files)
        {
            var fh = sfs_open_file(_prefPath + f, SFS_OPEN_CREATE_WRITE);
            if (fh != IntPtr.Zero)
            {
                byte[] d = System.Text.Encoding.UTF8.GetBytes($"test:{f}");
                fixed (byte* p = d) sfs_write_file(fh, p, d.Length);
                sfs_close_file(fh);
            }
        }

        // sfs_enumerate_directory
        _enumEntries.Clear();
        delegate* unmanaged[Cdecl]<void*, byte*, byte*, sfs_enum_result_t> fp = &OnEnumEntry;
        var er = sfs_enumerate_directory(_prefPath, (IntPtr)fp, null);
        Add("DirScan", "sfs_enumerate_directory", Ok(er),
            Ok(er) ? $"{_enumEntries.Count} entries" : sfs_get_error());

        // sfs_glob_directory (case-sensitive)
        _globMatches.Clear();
        int cnt = 0;
        IntPtr gr = sfs_glob_directory(_prefPath, "*.txt", SFS_GLOB_NONE, ref cnt);
        if (gr != IntPtr.Zero)
        {
            for (int i = 0; i < cnt; i++)
            {
                IntPtr sp = Marshal.ReadIntPtr(gr, i * IntPtr.Size);
                string? m = Marshal.PtrToStringUTF8(sp);
                if (m != null) _globMatches.Add(m);
            }
            sfs_free_glob_results(gr, cnt);
        }
        Add("DirScan", "sfs_glob_directory (*.txt)", cnt >= 3, $"{cnt} matches");

        // sfs_glob_directory (case-insensitive)
        int cnt2 = 0;
        IntPtr gr2 = sfs_glob_directory(_prefPath, "*.TXT", SFS_GLOB_CASE_INSENSITIVE, ref cnt2);
        if (gr2 != IntPtr.Zero) sfs_free_glob_results(gr2, cnt2);
        Add("DirScan", "sfs_glob (*.TXT case-insens.)", cnt2 == cnt, $"{cnt2} matches");

        // sfs_glob_directory with wildcard prefix
        int cnt3 = 0;
        IntPtr gr3 = sfs_glob_directory(_prefPath, "enum_*.dat", SFS_GLOB_NONE, ref cnt3);
        if (gr3 != IntPtr.Zero) sfs_free_glob_results(gr3, cnt3);
        Add("DirScan", "sfs_glob (enum_*.dat)", cnt3 == 1, $"{cnt3} matches");

        // Cleanup
        foreach (var f in files) sfs_remove_path(_prefPath + f);

        _dirScanDone = true;
    }

    // =========================================================================
    // ImGui UI
    // =========================================================================
    static void DrawResultRow(string name, bool ok, string detail)
    {
        igTextColored(ok ? CPass : CFail, ok ? "[OK] " : "[FAIL]");
        igSameLine(0, 6);
        igText(name);
        if (detail.Length > 0)
        {
            igSameLine(0, 8);
            igTextColored(CGray, detail.Length > 70 ? detail[..67] + "..." : detail);
        }
    }

    static void DrawSection(string section)
    {
        foreach (var (sec, name, ok, detail) in _results)
            if (sec == section) DrawResultRow(name, ok, detail);
    }

    static void DrawUI()
    {
        // Reset tab-open state every frame (prevents permanent tab closure)
        _t0 = _t1 = _t2 = _t3 = _t4 = _t5 = 1;

        igSetNextWindowPos(Vector2.Zero, ImGuiCond.Always, Vector2.Zero);
        igSetNextWindowSize(new Vector2(sapp_widthf(), sapp_heightf()), ImGuiCond.Always);
        byte open = 1;
        var wflags = ImGuiWindowFlags.NoTitleBar  | ImGuiWindowFlags.NoResize |
                     ImGuiWindowFlags.NoMove      | ImGuiWindowFlags.NoBringToFrontOnFocus |
                     ImGuiWindowFlags.NoBackground;
        igBegin("##fstest", ref open, wflags);

        // Header
        igTextColored(CGold, "SFilesystem Comprehensive API Test");
        igSameLine(0, 14);
        igTextColored(CGray, Platform());
        igSeparator();

        // Summary line
        int nOk = 0, nFail = 0;
        foreach (var (_, _, ok, _) in _results) { if (ok) nOk++; else nFail++; }
        igText($"Tests: {nOk + nFail}  |  ");
        igSameLine(0, 0);
        igTextColored(CPass, $"{nOk} PASS");
        igSameLine(0, 8);
        igTextColored(CFail, $"{nFail} FAIL");
        igSeparator();

        // Buttons
        if (igButton("Run All Tests", Vector2.Zero))
        {
            _results.Clear();
            _assetFileContent = "";
            RunPathTests();
            RunUserFolderTests();
            RunAssetTests();
            RunFileIOTests();
            RunDirectoryScan();
        }
        igSameLine(0, 8);
        if (igButton("File I/O Only",  Vector2.Zero)) RunFileIOTests();
        igSameLine(0, 8);
        if (igButton("Dir / Glob",     Vector2.Zero)) RunDirectoryScan();
        igSeparator();

        // Tab bar
        if (igBeginTabBar("##tabs", ImGuiTabBarFlags.None))
        {
            // -------- Paths --------
            if (igBeginTabItem("Paths", ref _t0, ImGuiTabItemFlags.None))
            {
                igText($"Base:   {_basePath}");
                igText($"Pref:   {_prefPath}");
                igText($"Assets: {_assetsDir}");
                igText($"Temp:   {_tempDir}");
                igText($"CWD:    {_cwd}");
                igSeparator();
                DrawSection("Paths");
                igEndTabItem();
            }

            // -------- Assets --------
            if (igBeginTabItem("Assets", ref _t1, ImGuiTabItemFlags.None))
            {
                DrawSection("Assets");
                if (_assetFileContent.Length > 0)
                {
                    igSeparator();
                    igTextColored(CGray, "sample.txt content preview:");
                    string preview = _assetFileContent.Length > 320
                        ? _assetFileContent[..320] + "..."
                        : _assetFileContent;
                    igText(preview);
                }
                igEndTabItem();
            }

            // -------- File I/O --------
            if (igBeginTabItem("File I/O", ref _t2, ImGuiTabItemFlags.None))
            {
                if (!_fileIODone)
                    igTextColored(CGray, "Press 'File I/O Only' or 'Run All Tests'.");
                DrawSection("FileIO");
                igEndTabItem();
            }

            // -------- Dir / Glob --------
            if (igBeginTabItem("Dir/Glob", ref _t3, ImGuiTabItemFlags.None))
            {
                if (!_dirScanDone)
                    igTextColored(CGray, "Press 'Dir / Glob' or 'Run All Tests'.");
                DrawSection("DirScan");
                if (_enumEntries.Count > 0)
                {
                    igSeparator();
                    igText($"Enumerated {_enumEntries.Count} entries:");
                    foreach (var e in _enumEntries) { igBullet(); igSameLine(0, 4); igText(e); }
                }
                if (_globMatches.Count > 0)
                {
                    igSeparator();
                    igText($"Glob (*.txt) — {_globMatches.Count} matches:");
                    foreach (var m in _globMatches) { igBullet(); igSameLine(0, 4); igText(m); }
                }
                igEndTabItem();
            }

            // -------- User Folders --------
            if (igBeginTabItem("User Folders", ref _t4, ImGuiTabItemFlags.None))
            {
                DrawSection("Folders");
                igEndTabItem();
            }

            // -------- All Results --------
            if (igBeginTabItem("All Results", ref _t5, ImGuiTabItemFlags.None))
            {
                igText($"{nOk + nFail} tests  |  {nOk} pass  |  {nFail} fail");
                igSeparator();
                foreach (var (sec, name, ok, detail) in _results)
                {
                    igTextColored(ok ? CPass : CFail, ok ? "[OK]  " : "[FAIL]");
                    igSameLine(0, 4);
                    igText($"[{sec,-8}] {name}");
                    if (detail.Length > 0)
                    {
                        igSameLine(0, 6);
                        igTextColored(CGray, detail.Length > 52 ? detail[..49] + "..." : detail);
                    }
                }
                igEndTabItem();
            }

            igEndTabBar();
        }

        igEnd();
    }

    static string Platform()
    {
#if __IOS__
        return "[iOS]";
#elif __ANDROID__
        return "[Android]";
#elif WEB
        return "[WebAssembly]";
#else
        return $"[{System.Runtime.InteropServices.RuntimeInformation.OSDescription}]";
#endif
    }

    // =========================================================================
    // Sokol callbacks
    // =========================================================================
    [UnmanagedCallersOnly]
    static void Init()
    {
        sg_setup(new sg_desc
        {
            environment = sglue_environment(),
            logger      = { func = &slog_func }
        });

        simgui_setup(new simgui_desc_t { logger = { func = &slog_func } });

        _gfx.pass_action = default;
        _gfx.pass_action.colors[0].load_action  = sg_load_action.SG_LOADACTION_CLEAR;
        _gfx.pass_action.colors[0].clear_value  = new sg_color { r = 0f, g = 0f, b = 0f, a = 1f };

        // Fullscreen quad (pos.xy  uv.xy)
        float[] verts =
        {
            -1f, -1f,  0f, 0f,
             1f, -1f,  1f, 0f,
             1f,  1f,  1f, 1f,
            -1f,  1f,  0f, 1f,
        };
        ushort[] indices = { 0, 1, 2,  0, 2, 3 };

        var vbuf = sg_make_buffer(new sg_buffer_desc { data = SG_RANGE(verts), label = "bg-vbuf" });
        var ibuf = sg_make_buffer(new sg_buffer_desc
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data  = SG_RANGE(indices),
            label = "bg-ibuf"
        });

        var shd = sg_make_shader(bg_shader_desc(sg_query_backend()));

        var pd = default(sg_pipeline_desc);
        pd.layout.attrs[ATTR_bg_in_pos].format = SG_VERTEXFORMAT_FLOAT2;
        pd.layout.attrs[ATTR_bg_in_uv].format  = SG_VERTEXFORMAT_FLOAT2;
        pd.layout.buffers[0].stride            = 16;  // 4 floats
        pd.shader                              = shd;
        pd.index_type                          = SG_INDEXTYPE_UINT16;
        pd.depth.write_enabled                 = false;
        pd.depth.compare                       = SG_COMPAREFUNC_ALWAYS;
        pd.label                               = "bg-pipeline";
        _gfx.pip = sg_make_pipeline(pd);

        _gfx.bind                   = new sg_bindings();
        _gfx.bind.vertex_buffers[0] = vbuf;
        _gfx.bind.index_buffer      = ibuf;

        // Run the read-only tests immediately on startup
        RunPathTests();
        RunUserFolderTests();
        RunAssetTests();
    }

    [UnmanagedCallersOnly]
    static void Frame()
    {
        simgui_new_frame(new simgui_frame_desc_t
        {
            width      = sapp_width(),
            height     = sapp_height(),
            delta_time = sapp_frame_duration(),
        });

        DrawUI();

        sg_begin_pass(new sg_pass { action = _gfx.pass_action, swapchain = sglue_swapchain() });

        // 1 — render gradient background
        var bgp = new bg_params_t
        {
            color_top    = new Vector4(0.08f, 0.11f, 0.18f, 1f),
            color_bottom = new Vector4(0.18f, 0.24f, 0.42f, 1f),
        };
        sg_apply_pipeline(_gfx.pip);
        sg_apply_bindings(_gfx.bind);
        sg_apply_uniforms(UB_bg_params, SG_RANGE<bg_params_t>(ref bgp));
        sg_draw(0, 6, 1);

        // 2 — ImGui on top
        simgui_render();

        sg_end_pass();
        sg_commit();
    }

    [UnmanagedCallersOnly]
    static void Event(sapp_event* e)
    {
        if (e != null) simgui_handle_event(in *e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        simgui_shutdown();
        sg_shutdown();
        if (Debugger.IsAttached) Environment.Exit(0);
    }

    public static sapp_desc sokol_main()
    {
        return new sapp_desc
        {
            init_cb          = &Init,
            frame_cb         = &Frame,
            event_cb         = &Event,
            cleanup_cb       = &Cleanup,
            width            = 0,
            height           = 0,
            sample_count     = 4,
            window_title     = "SFilesystem Test (sokol-app)",
            icon             = { sokol_default = true },
            enable_clipboard = true,
            clipboard_size   = 4096,
            logger           = { func = &slog_func },
        };
    }
}

