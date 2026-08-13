using System;
using static Sokol.SApp;

namespace Sokol.GUI;

/// <summary>
/// Screen-level overlay that floats a copy of the focused TextBox or TextArea
/// just above the virtual keyboard on mobile (iOS / Android).
///
/// Lifecycle:
///   - TextBox/TextArea.OnFocusGained calls Show() immediately (no async wait).
///   - Screen.Update monitors Focus.Focused as a safety net and hides when focus
///     leaves all text inputs.
///   - sapp_keyboard_shown() is used only to detect external keyboard dismissal
///     (Android back button / iOS swipe-down) after the keyboard was confirmed shown.
///
/// Text is synced live from proxy → original via TextChanged.
/// Focus is transferred to the proxy with SuppressingKeyboard = true so the
/// original's OnFocusLost does not call sapp_show_keyboard(false).
/// </summary>
public sealed class MobileKeyboardOverlay : Widget
{
    private new const float Padding   = 12f;
    private const float BoxHeight = 44f;
    private const float AreaHeight = 130f;

    private Widget?  _original;
    private float    _keyboardHeight;

    /// <summary>
    /// True while focus is being transferred from the original input to the
    /// proxy. TextBox / TextArea check this in OnFocusLost to keep the keyboard
    /// visible during the hand-off.
    /// </summary>
    public bool SuppressingKeyboard { get; private set; }

    public bool IsActive => Visible;

    public MobileKeyboardOverlay() { Visible = false; }

    // ─── Show / Hide ─────────────────────────────────────────────────────────

    public void Show(Widget focused, float keyboardHeight)
    {
        // No early-return on Visible — allow re-init when the user switches text fields.
        _original       = focused;
        _keyboardHeight = keyboardHeight;
        ClearChildren();

        Widget? proxy = null;

        if (focused is NumberInput nin)
        {
            var p = new NumberInput
            {
                Text                   = nin.Text,
                Min                    = nin.Min,
                Max                    = nin.Max,
                DecimalPlaces          = nin.DecimalPlaces,
                SkipKeyboardManagement = true,
            };
            p.TextChanged += t =>
            {
                if (_original is NumberInput o) { o.Text = t; o.NotifyValueChanged(); }
            };
            p.Submitted += () => Hide();
            proxy = p;
        }
        else if (focused is TextBox tb)
        {
            var p = new TextBox
            {
                Text                   = tb.Text,
                Placeholder            = tb.Placeholder,
                Uppercase              = tb.Uppercase,   // capitalize on the proxy too — its text syncs back to the original
                SkipKeyboardManagement = true,
            };
            p.TextChanged += t => { if (_original is TextBox o) o.Text = t; };
            p.Submitted   += () => Hide();
            proxy = p;
        }
        else if (focused is TextArea ta && ta.IsEditable)
        {
            var p = new TextArea
            {
                Text                   = ta.Text,
                IsEditable             = true,
                SkipKeyboardManagement = true,
                Padding                = ta.Padding.Left > 0 ? ta.Padding : new Thickness(6),
            };
            p.TextChanged += t => { if (_original is TextArea o) o.Text = t; };
            proxy = p;
        }

        if (proxy == null) { Visible = false; return; }

        AddChild(proxy);
        Visible = true;
        InvalidateLayout();

        // Transfer focus without letting the original hide the keyboard.
        SuppressingKeyboard = true;
        Screen.Instance?.Focus.SetFocus(proxy);
        SuppressingKeyboard = false;
    }

    public void Hide()
    {
        if (!Visible) return;

        // If the proxy still holds focus (e.g. keyboard was externally dismissed),
        // clear it. If focus already moved elsewhere, leave it alone.
        var screen = Screen.Instance;
        var proxy  = Children.Count > 0 ? Children[0] : null;
        if (proxy != null && screen?.Focus.Focused == proxy)
            screen!.Focus.SetFocus(null);

        sapp_show_keyboard(false);

        _original = null;
        ClearChildren();
        Visible = false;
    }

    public void UpdateKeyboardHeight(float keyboardHeight)
    {
        if (_keyboardHeight == keyboardHeight) return;
        _keyboardHeight = keyboardHeight;
        InvalidateLayout();
    }

    // ─── Layout ──────────────────────────────────────────────────────────────

    public override void PerformLayout(Renderer renderer, bool force = false)
    {
        if (!Visible) return;

        float sw   = Bounds.Width;
        float sh   = Bounds.Height;
        bool  area = Children.Count > 0 && Children[0] is TextArea;
        float ih   = area ? AreaHeight : BoxHeight;

        // This overlay floats OUTSIDE the app's content root, so nothing else insets it: it must
        // honour the display's safe area itself or the proxy lands under the notch / Dynamic Island
        // / curved edge (in landscape those are the LEFT and RIGHT insets). Only the editable box is
        // inset — the surface bar behind it still spans the full width, like any background.
        var   sa    = (Parent as Screen)?.SafeAreaInsets ?? Thickness.Zero;
        float left  = sa.Left  + Padding;
        float right = sa.Right + Padding;

        // Where to put the bar depends on whether we actually KNOW how tall the keyboard is.
        //
        //  • Known (_keyboardHeight > 0) — keyboard_resizes_canvas (iOS) or adjustResize (Android)
        //    shrank the canvas, so sh already excludes the keyboard: sit just above the bottom edge,
        //    right under the player's thumb.
        //  • Unknown (0) — a fullscreen/immersive Android activity gets no resize at all, and iOS
        //    reports its keyboard asynchronously, so there is NOTHING to anchor to. Dock the bar at
        //    the TOP of the safe area, the one band no keyboard can ever cover. ⛔ Do not guess a
        //    fraction of the screen instead: the old `sh * 0.45f` guess put the box straight under a
        //    landscape Samsung/Redmi keyboard (measured 55 % of a 1080px-tall canvas), which is the
        //    defect this replaces — and any fixed fraction is wrong on the next keyboard.
        float extraLift = area ? 0f : 16f;
        float panY = _keyboardHeight > 0f
            ? sh - ih - Padding * 2 - ih * 0.5f - extraLift
            : sa.Top;
        if (panY < sa.Top) panY = sa.Top;

        if (Children.Count > 0)
        {
            var child  = Children[0];
            child.Bounds = new Rect(left, panY + Padding, MathF.Max(0f, sw - left - right), ih);
            child.PerformLayout(renderer, force: true);
        }
    }

    // ─── Draw ────────────────────────────────────────────────────────────────

    public override void Draw(Renderer renderer)
    {
        if (!Visible || Children.Count == 0) return;

        var   child = Children[0];
        var   b     = child.Bounds;
        var   theme = ThemeManager.Current;
        float bgY   = b.Y - Padding;
        float bgH   = b.Height + Padding * 2;

        // Full-width background bar
        renderer.FillRect(new Rect(0, bgY, Bounds.Width, bgH), theme.SurfaceColor);
        renderer.DrawLine(0, bgY,        Bounds.Width, bgY,        1f, theme.Border);
        renderer.DrawLine(0, bgY + bgH,  Bounds.Width, bgY + bgH,  1f, theme.Border);

        // Draw the proxy input
        renderer.Save();
        renderer.Translate(b.X, b.Y);
        child.Draw(renderer);
        renderer.Restore();
    }

    // ─── Hit-testing ─────────────────────────────────────────────────────────

    public override bool HitTest(Vector2 localPoint)
    {
        if (!Visible || Children.Count == 0) return false;
        return Children[0].Bounds.Contains(localPoint);
    }

    public override Widget? HitTestDeep(Vector2 screenPoint)
    {
        if (!Visible || !Enabled || Children.Count == 0) return null;
        var child = Children[0];
        if (!child.Bounds.Contains(ToLocal(screenPoint))) return null;
        return child.HitTestDeep(screenPoint) ?? this;
    }
}
