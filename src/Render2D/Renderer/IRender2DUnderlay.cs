namespace Sokol.Render2D;

/// <summary>The compositing contract the app frame loop drives for a screen that renders a
/// <b>Sokol.SG underlay</b> beneath the NanoVG HUD (Strategy C — see <c>docs/RENDER2D.md §5</c>).
/// <para>
/// <see cref="RenderOffscreen"/> runs its OWN offscreen pass <b>before</b> the swapchain pass (so it
/// must not be called inside one). <see cref="Blit"/> composites the offscreen result into the
/// <b>currently active swapchain pass</b>, issued before NanoVG begins its frame, so the HUD draws on
/// top. A screen with no GPU underlay simply doesn't implement this (the app returns <c>null</c>).
/// </para></summary>
public interface IRender2DUnderlay
{
    /// <summary>Render the world into the offscreen target. Opens and closes its own
    /// <c>sg_begin_pass</c>/<c>sg_end_pass</c>; <paramref name="pxW"/>/<paramref name="pxH"/> are the
    /// physical framebuffer size.</summary>
    void RenderOffscreen(int pxW, int pxH);

    /// <summary>Blit the offscreen target into the active swapchain pass (fullscreen, replace).</summary>
    void Blit();
}
