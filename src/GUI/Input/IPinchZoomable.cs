namespace Sokol.GUI;

/// <summary>
/// Opt-in for router-level two-finger pinch-to-zoom. Touches are owned by the DEEPEST widget under
/// each finger, so a zooming CONTAINER (a magnifier hosting an arbitrary child) can never see a pinch
/// through its child's ownership — and teaching every child widget to forward one would be the wrong
/// contract. Instead the <see cref="InputRouter"/> arbitrates the gesture the same way it already
/// arbitrates drag-to-scroll over a <see cref="ScrollView"/>: when a second finger lands and both
/// fingers stand over the SAME <see cref="IPinchZoomable"/> ancestor, the router takes the touch
/// stream over — un-pressing whatever the first finger pressed, so a pinch can never double as a tap —
/// and feeds the scale changes here until the fingers lift.
/// </summary>
public interface IPinchZoomable
{
    /// <summary>One pinch increment: <paramref name="scale"/> is the ratio of the current finger
    /// distance to the previous one (multiply your zoom by it), <paramref name="center"/> is the
    /// midpoint between the fingers in screen-space logical pixels — a host with its own focus policy
    /// (e.g. one that auto-centres on known content) is free to ignore it.</summary>
    void OnPinchZoom(float scale, Vector2 center);
}
