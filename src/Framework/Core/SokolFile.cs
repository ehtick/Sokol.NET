using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using static Sokol.SFilesystem;

namespace GameEditor.Framework.Core
{
    /// <summary>
    /// Cross-platform file/directory helpers wrapping SFilesystem P/Invoke bindings.
    /// Mirrors the most-used System.IO.File / Directory patterns so call-sites migrate
    /// one-by-one.  Path string operations (Path.Combine etc.) are not wrapped here —
    /// keep using System.IO.Path for those.
    /// </summary>
    public static unsafe class SokolFile
    {
        // ── Existence checks ──────────────────────────────────────────────────

        public static bool Exists(string path)      => sfs_path_exists(path);
        public static bool IsFile(string path)      => sfs_is_file(path);
        public static bool IsDirectory(string path) => sfs_is_directory(path);

        // ── Read ──────────────────────────────────────────────────────────────

        public static string ReadAllText(string path)
        {
            IntPtr h = sfs_open_file(path, sfs_open_mode_t.SFS_OPEN_READ);
            if (h == IntPtr.Zero)
                throw new IOException($"SokolFile: cannot open '{path}' — {sfs_get_error()}");
            try
            {
                long size = sfs_get_file_size(h);
                if (size <= 0) return string.Empty;
                byte[] buf = new byte[size];
                fixed (byte* p = buf)
                    sfs_read_file(h, p, size);
                return Encoding.UTF8.GetString(buf);
            }
            finally { sfs_close_file(h); }
        }

        // ── Write ─────────────────────────────────────────────────────────────

        public static void WriteAllText(string path, string text)
        {
            IntPtr h = sfs_open_file(path, sfs_open_mode_t.SFS_OPEN_CREATE_WRITE);
            if (h == IntPtr.Zero)
                throw new IOException($"SokolFile: cannot create '{path}' — {sfs_get_error()}");
            try
            {
                byte[] buf = Encoding.UTF8.GetBytes(text);
                fixed (byte* p = buf)
                    sfs_write_file(h, p, buf.Length);
                sfs_flush_file(h);
            }
            finally { sfs_close_file(h); }
        }

        // ── Directory operations ──────────────────────────────────────────────

        public static void CreateDirectory(string path)
        {
            if (!sfs_is_directory(path))
                sfs_create_directory(path);
        }

        /// <summary>Creates <paramref name="path"/> and all missing parent directories.</summary>
        public static void EnsureDirectory(string path)
        {
            if (string.IsNullOrEmpty(path) || sfs_is_directory(path)) return;
            string? parent = Path.GetDirectoryName(path);
            if (parent != null && parent != path)
                EnsureDirectory(parent);
            sfs_create_directory(path);
        }

        /// <summary>Recursively deletes a directory and all its contents.</summary>
        public static void DeleteDirectory(string dirPath)
        {
            if (!sfs_is_directory(dirPath)) return;
            foreach (string entry in EnumerateEntries(dirPath))
            {
                if (sfs_is_directory(entry))
                    DeleteDirectory(entry);
                else
                    sfs_remove_path(entry);
            }
            sfs_remove_path(dirPath);
        }

        public static void Delete(string path) => sfs_remove_path(path);
        public static void Move(string oldPath, string newPath) => sfs_rename_path(oldPath, newPath);
        public static void Copy(string src, string dst) => sfs_copy_file(src, dst);

        public static long GetLastWriteTime(string path) => sfs_get_last_modified_time(path);

        // ── Directory enumeration ─────────────────────────────────────────────

        /// <summary>All entries (files + subdirs) directly inside <paramref name="path"/>.</summary>
        public static List<string> EnumerateEntries(string path)
            => GlobFull(path, "*");

        /// <summary>Subdirectory full paths directly inside <paramref name="path"/>.</summary>
        public static List<string> EnumerateDirectories(string path)
        {
            var all = GlobFull(path, "*");
            all.RemoveAll(e => !sfs_is_directory(e));
            return all;
        }

        /// <summary>File full paths directly inside <paramref name="path"/>.</summary>
        public static List<string> EnumerateFiles(string path)
        {
            var all = GlobFull(path, "*");
            all.RemoveAll(e => !sfs_is_file(e));
            return all;
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private static List<string> GlobFull(string dir, string pattern)
        {
            int count = 0;
            IntPtr results = sfs_glob_directory(dir, pattern, sfs_glob_flags_t.SFS_GLOB_NONE, ref count);
            if (results == IntPtr.Zero || count == 0)
                return new List<string>();
            try
            {
                var list = new List<string>(count);
                IntPtr* ptrs = (IntPtr*)results;
                for (int i = 0; i < count; i++)
                {
                    string? name = Marshal.PtrToStringUTF8(ptrs[i]);
                    if (name != null)
                        list.Add(Path.Combine(dir, name));
                }
                return list;
            }
            finally { sfs_free_glob_results(results, count); }
        }
    }
}
