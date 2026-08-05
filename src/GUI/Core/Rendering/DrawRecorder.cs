using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// Opt-in recording of every element the two draw funnels put on screen — GPU shapes from
/// <c>Sokol.Render2D.Render2DSurface</c> and NanoVG shapes / text / images from
/// <see cref="Renderer"/> — as flat (rect, kind, tag) entries in draw order.
///
/// Off by default and zero-cost when off: every instrumented call site tests
/// <see cref="Recording"/> BEFORE computing anything (text measuring, transform math), so the
/// only cost of an idle recorder is one static-bool branch per draw call.
///
/// Consumers arm it for ONE frame and read <see cref="Drawn"/> on the next (the render runs
/// after their tick), then turn it off — the list grows every frame it stays on.
///
/// Coordinate spaces differ by funnel and are NOT unified here (the consumer knows its own
/// dpi mapping): <see cref="Kind.GpuShape"/> rects are in the surface's own pixel space with
/// its content transform applied; the Nvg* kinds are in NanoVG logical px with the current
/// NVG transform applied. Particles (<c>PushParticle</c>) and drop shadows are deliberately
/// not recorded — they are decorative and would drown an occlusion audit in noise.
/// </summary>
public static class DrawRecorder
{
    public enum Kind : byte
    {
        /// <summary>Render2DSurface primitive (rect in the surface's pixel space).</summary>
        GpuShape,
        /// <summary>Renderer (NanoVG) shape (rect in logical px).</summary>
        NvgShape,
        /// <summary>Renderer text — the measured bounds of the string as drawn; the tag carries
        /// the logical text after <c>"text:"</c>.</summary>
        NvgText,
        /// <summary>Renderer image blit.</summary>
        NvgImage,
    }

    /// <summary>While set, both funnels append everything they draw to <see cref="Drawn"/>.</summary>
    public static bool Recording;

    /// <summary>Ambient semantic scope prepended to every recorded tag as <c>"context/tag"</c>
    /// while non-empty. Set by an app's draw HELPERS (a card renderer, a seat-pod painter, a button
    /// plate) around their primitive calls, so an audit can tell "a button's plate" from "a card
    /// back" when both are plain rounded rects. Purely additive: leave it empty and tags are the
    /// bare primitive names. Save/restore the previous value around nested helpers.</summary>
    public static string Context = "";

    /// <summary>Everything drawn since the last <see cref="Reset"/>, in draw order.</summary>
    public static readonly List<(Rect rect, Kind kind, string tag)> Drawn = new();

    public static void Reset() => Drawn.Clear();

    /// <summary>Append one entry. Call sites gate on <see cref="Recording"/> before building the
    /// rect/tag; the re-check here is only a guard against un-gated callers.</summary>
    public static void Add(Rect rect, Kind kind, string tag)
    {
        if (Recording) Drawn.Add((rect, kind, Context.Length == 0 ? tag : Context + "/" + tag));
    }
}
