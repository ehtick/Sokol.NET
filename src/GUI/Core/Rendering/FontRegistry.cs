using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using static Sokol.NanoVG;

namespace Sokol.GUI;

/// <summary>
/// Manages loaded NanoVG fonts.  Fonts are registered by name and resolved by
/// name throughout the widget tree.  Loading is done via the Sokol FileSystem.
/// </summary>
public sealed class FontRegistry
{
    private static FontRegistry? _instance;
    public static FontRegistry Instance => _instance ??= new FontRegistry();

    private readonly Dictionary<string, Font> _fonts = new();
    private Font? _default;

    private FontRegistry() { }

    // -------------------------------------------------------------------------
    // Registration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Register a font that was already loaded via <c>nvgCreateFontMem</c>.
    /// The first registered font becomes the default fallback.
    /// </summary>
    public Font Register(string name, int fontId)
    {
        var font = new Font(name, fontId);
        _fonts[name] = font;
        _default ??= font;
        return font;
    }

    /// <summary>
    /// Synchronously load a font from memory and register it.
    /// <paramref name="data"/> must outlive the NanoVG context (use
    /// <c>Marshal.AllocHGlobal</c> and pass ownership to NanoVG via
    /// <paramref name="nvgOwnsMemory"/> = true).
    /// </summary>
    public unsafe Font RegisterFromMemory(IntPtr vg, string name, byte[] data, bool nvgOwnsMemory = false)
    {
        IntPtr unmanaged = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data, 0, unmanaged, data.Length);
        int id = nvgCreateFontMem(vg, name, (byte*)unmanaged, data.Length, nvgOwnsMemory ? 1 : 0);
        if (!nvgOwnsMemory)
        {
            // We must keep memory pinned; store reference so GC doesn't collect.
            // NanoVG will NOT free it, so we register for cleanup on Shutdown.
            _unmanagedBuffers.Add(unmanaged);
        }
        return Register(name, id);
    }

    /// <summary>
    /// Load a font synchronously from the Sokol file system and register it
    /// immediately.  Returns null if the file cannot be opened.
    /// </summary>
    public unsafe Font? RegisterSync(IntPtr vg, string name, string assetPath)
    {
        IntPtr dirPtr = SFilesystem.sfs_get_assets_dir();
        string dir = Marshal.PtrToStringUTF8(dirPtr) ?? "";
        SFilesystem.sfs_free_path(dirPtr);
        if (dir == "") return null;

        string fullPath = Path.Combine(dir, assetPath);
        IntPtr fh = SFilesystem.sfs_open_file(fullPath, SFilesystem.sfs_open_mode_t.SFS_OPEN_READ);
        if (fh == IntPtr.Zero) return null;

        long size = SFilesystem.sfs_get_file_size(fh);
        IntPtr unmanaged = Marshal.AllocHGlobal((int)size);
        SFilesystem.sfs_read_file(fh, (void*)unmanaged, size);
        SFilesystem.sfs_close_file(fh);

        int id = nvgCreateFontMem(vg, name, (byte*)unmanaged, (int)size, 1);
        Sokol.SLog.Info($"GUI: Font '{name}' loaded sync — nvgId={id}, {size} bytes", "Sokol.GUI");
        ResolvePendingFallbacks(vg);
        return Register(name, id);
    }

    /// <summary>
    /// Load a font synchronously and immediately register it as a NanoVG fallback
    /// for one or more base fonts.  Guarantees the fallback is wired before
    /// any subsequent <c>RegisterFallbackAsync</c> callbacks fire.
    /// </summary>
    public Font? RegisterFallbackSync(IntPtr vg, string name, string assetPath,
        string[] baseFontNames)
    {
        var font = RegisterSync(vg, name, assetPath);
        if (font == null || !font.IsValid) return null;

        foreach (var baseName in baseFontNames)
        {
            var baseFont = Get(baseName);
            if (baseFont != null && baseFont.IsValid)
                WireFallback(vg, baseName, baseFont.Id, font.Id, name);
            else
            {
                _pendingFallbacks.Add((baseName, font.Id));
                Sokol.SLog.Info($"GUI: Deferred fallback '{name}' for '{baseName}' (base not yet loaded)", "Sokol.GUI");
            }
        }
        return font;
    }

    /// <summary>
    /// Load a font asynchronously from the Sokol file system and register it
    /// once the data arrives.  Safe to call during Init() — the font becomes
    /// available once FileSystem delivers the bytes (usually next frame).
    /// </summary>
    public unsafe void RegisterAsync(IntPtr vg, string name, string assetPath,
        uint bufferSize = 512 * 1024)
    {
        Sokol.SLog.Info($"GUI: Requesting font '{name}' from '{assetPath}'", "Sokol.GUI");
        SFilesystem.LoadFileAsync(assetPath, (path, bytes, status) =>
        {
            if (status == SFileLoadStatus.Success && bytes != null)
            {
                IntPtr unmanaged = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, unmanaged, bytes.Length);
                // NanoVG owns the memory (freeData=1) — it will free it.
                int id = nvgCreateFontMem(vg, name, (byte*)unmanaged, bytes.Length, 1);
                Register(name, id);
                Sokol.SLog.Info($"GUI: Font '{name}' loaded — nvgId={id}, {bytes.Length} bytes", "Sokol.GUI");
                // Resolve any fallback fonts waiting for this base font
                ResolvePendingFallbacks(vg);
            }
            else
            {
                Sokol.SLog.Warning($"GUI: Font '{name}' FAILED to load from '{assetPath}' (status={status})", "Sokol.GUI");
            }
        }, bufferSize);
    }

    /// <summary>
    /// Load a font asynchronously and register it as a NanoVG fallback for one
    /// or more base fonts.  When NanoVG renders text with a base font and
    /// encounters a glyph not found in it, it automatically tries the fallback.
    /// </summary>
    /// <param name="vg">NanoVG context.</param>
    /// <param name="name">Internal name for the fallback font.</param>
    /// <param name="assetPath">Sokol FileSystem asset path (e.g. "fonts/NotoSansHebrew-Regular.ttf").</param>
    /// <param name="baseFontNames">Names of already-registered fonts that should use this as fallback.</param>
    /// <param name="bufferSize">File read buffer size.</param>
    public unsafe void RegisterFallbackAsync(IntPtr vg, string name, string assetPath,
        string[] baseFontNames, uint bufferSize = 512 * 1024)
    {
        Sokol.SLog.Info($"GUI: Requesting fallback font '{name}' from '{assetPath}'", "Sokol.GUI");
        SFilesystem.LoadFileAsync(assetPath, (path, bytes, status) =>
        {
            if (status == SFileLoadStatus.Success && bytes != null)
            {
                IntPtr unmanaged = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, unmanaged, bytes.Length);
                int id = nvgCreateFontMem(vg, name, (byte*)unmanaged, bytes.Length, 1);
                Register(name, id);
                // Wire up as NanoVG fallback for each base font
                foreach (var baseName in baseFontNames)
                {
                    var baseFont = Get(baseName);
                    if (baseFont != null && baseFont.IsValid)
                        WireFallback(vg, baseName, baseFont.Id, id, name);
                    else
                    {
                        // Base font not loaded yet — store for deferred wiring
                        _pendingFallbacks.Add((baseName, id));
                        Sokol.SLog.Info($"GUI: Deferred fallback '{name}' for '{baseName}' (base not yet loaded)", "Sokol.GUI");
                    }
                }
                // Also check if any pending fallbacks can now be resolved
                ResolvePendingFallbacks(vg);
            }
            else
            {
                Sokol.SLog.Warning($"GUI: Fallback font '{name}' FAILED to load from '{assetPath}' (status={status})", "Sokol.GUI");
            }
        }, bufferSize);
    }

    // -------------------------------------------------------------------------
    // Lookup
    // -------------------------------------------------------------------------

    /// <summary>Returns the font registered under <paramref name="name"/>, or null.</summary>
    public Font? Get(string name) => _fonts.TryGetValue(name, out var f) ? f : null;

    /// <summary>
    /// Returns the font registered under <paramref name="name"/>, or the
    /// default fallback font if the name is not found.
    /// </summary>
    public Font? GetOrDefault(string name) =>
        _fonts.TryGetValue(name, out var f) ? f : _default;

    /// <summary>The first font registered, used as the framework default.</summary>
    public Font? Default => _default;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <summary>Release unmanaged buffers and clear all registered fonts.</summary>
    public void Clear()
    {
        foreach (var ptr in _unmanagedBuffers)
            Marshal.FreeHGlobal(ptr);
        _unmanagedBuffers.Clear();
        _pendingFallbacks.Clear();
        _wiredFallbacks.Clear();
        _fonts.Clear();
        _default = null;
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Wire <paramref name="fallbackId"/> as the next fallback for <paramref name="baseFontId"/>.
    /// Calls <c>nvgResetFallbackFontsId</c> first to clear any glyph-cache entries that were
    /// cached as "not found" before this fallback font arrived (async timing fix), then
    /// re-adds all previously wired fallbacks in registration order before appending the new one.
    /// </summary>
    private void WireFallback(IntPtr vg, string baseName, int baseFontId, int fallbackId, string fallbackName)
    {
        if (!_wiredFallbacks.TryGetValue(baseName, out var list))
            _wiredFallbacks[baseName] = list = [];

        list.Add(fallbackId);

        // Reset clears both the glyph cache (removing poisoned "not found" entries cached
        // before this font arrived) and all existing fallback registrations for the base font.
        nvgResetFallbackFontsId(vg, baseFontId);

        // Re-add all fallbacks in registration order so priority is deterministic.
        foreach (var fid in list)
            _ = nvgAddFallbackFontId(vg, baseFontId, fid);

        SLog.Info($"GUI: Wired '{fallbackName}' (id={fallbackId}) as fallback for '{baseName}' (id={baseFontId}), total fallbacks={list.Count}", "Sokol.GUI");
    }

    /// <summary>
    /// Resolve pending fallbacks whose base font has now loaded.
    /// Iterates forward to preserve registration order.
    /// </summary>
    private void ResolvePendingFallbacks(IntPtr vg)
    {
        int i = 0;
        while (i < _pendingFallbacks.Count)
        {
            var (baseName, fallbackId) = _pendingFallbacks[i];
            var baseFont = Get(baseName);
            if (baseFont != null && baseFont.IsValid)
            {
                WireFallback(vg, baseName, baseFont.Id, fallbackId, $"id={fallbackId}");
                _pendingFallbacks.RemoveAt(i);
            }
            else
                i++;
        }
    }

    // Keep references to any unmanaged memory we manage (not owned by NanoVG).
    private readonly List<IntPtr> _unmanagedBuffers = new();
    // Fallbacks waiting for their base font to load (baseFontName, fallbackFontId).
    private readonly List<(string baseName, int fallbackId)> _pendingFallbacks = new();
    // Ordered list of wired fallback IDs per base font name — used to re-apply after reset.
    private readonly Dictionary<string, List<int>> _wiredFallbacks = [];
}
