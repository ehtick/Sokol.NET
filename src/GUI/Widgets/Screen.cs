using System;
using static Sokol.SApp;

namespace Sokol.GUI;

/// <summary>
/// Root widget.  One per application.  Owns the <see cref="Renderer"/>,
/// <see cref="FontRegistry"/>, <see cref="FocusManager"/> and <see cref="InputRouter"/>.
/// </summary>
public sealed class Screen : Widget
{
    // ─── Singleton ───────────────────────────────────────────────────────────
    private static Screen? _instance;
    public  static Screen   Instance =>
        _instance ?? throw new InvalidOperationException("Screen.Initialize() not called.");

    // ─── Owned resources ─────────────────────────────────────────────────────
    public Renderer      Renderer      { get; private set; } = null!;
    public FontRegistry  Fonts         { get; } = FontRegistry.Instance;
    public FocusManager  Focus         { get; } = new();
    public InputRouter   Input         { get; private set; } = null!;

    /// <summary>
    /// Lazily-constructed docking manager. The first access wires a root
    /// <see cref="DockSpace"/> and a <see cref="FloatingPanelHost"/> into the
    /// screen's children so dock panels are drawn and hit-tested alongside
    /// other root-level UI.
    /// </summary>
    public DockManager DockManager
    {
        get
        {
            if (_dockManager == null)
            {
                var dockSpace = new DockSpace { Id = "__RootDockSpace__" };
                var floatHost = new FloatingPanelHost { Id = "__FloatingPanelHost__" };
                AddChild(dockSpace);
                AddChild(floatHost); // added last → draws on top.
                _dockManager = new DockManager(dockSpace, floatHost);
            }
            return _dockManager;
        }
    }
    private DockManager? _dockManager;

    /// <summary>Returns the DockManager only if already created (no lazy init).</summary>
    internal DockManager? DockManagerOrNull => _dockManager;

    /// <summary>Manages cross-widget drag-and-drop.</summary>
    public DragManager Drag { get; } = new();

    // ─── Logical size ────────────────────────────────────────────────────────
    public float LogicalWidth  { get; private set; }
    public float LogicalHeight { get; private set; }

    // ─── Debug frame counter ─────────────────────────────────────────────────
    /// <summary>Increments each Update(). Used to gate per-frame debug logs.</summary>
    internal static int DbgFrame { get; private set; }

    // ─── Virtual keyboard ────────────────────────────────────────────────────
    /// <summary>
    /// Height (logical pixels) currently occupied by the virtual keyboard.
    /// Auto-detected from window-height reduction when keyboard is shown
    /// (requires keyboard_resizes_canvas=true on iOS or adjustResize on Android).
    /// Zero on desktop or when keyboard is hidden.
    /// </summary>
    public float KeyboardHeight { get; private set; }
    private float _noKeyboardHeight;
#if __ANDROID__ || __IOS__
    private bool _keyboardConfirmed; // true once sapp_keyboard_shown() returns true after Show()
#endif

    /// <summary>
    /// Overlay that floats a proxy TextBox/TextArea above the virtual keyboard.
    /// Added to the screen as a top-level child on first access.
    /// </summary>
    public MobileKeyboardOverlay MobileOverlay
    {
        get
        {
            if (_mobileOverlay == null)
            {
                _mobileOverlay = new MobileKeyboardOverlay();
                AddChild(_mobileOverlay);
            }
            return _mobileOverlay;
        }
    }
    private MobileKeyboardOverlay? _mobileOverlay;

    // ─── Popup / overlay ─────────────────────────────────────────────────────
    /// <summary>
    /// The currently active popup widget (e.g. an open ComboBox dropdown).
    /// Screen.HitTestDeep tests this widget first, bypassing the normal tree walk,
    /// so clicks on overlay regions outside the widget's Bounds still reach it.
    /// </summary>
    private static Widget? _activePopup;

    private NotificationHost _notificationHost = new();

    public static void SetActivePopup(Widget? popup)
    {
        _activePopup = popup;
    }

    // ─── Init / Shutdown ─────────────────────────────────────────────────────
    public static Screen Initialize(IntPtr vg)
    {
        _instance          = new Screen();
        _instance.Renderer = new Renderer(vg);
        _instance.Input    = new InputRouter(_instance, _instance.Focus);
        _ = new AnimationManager();  // sets AnimationManager.Instance
        Sokol.SLog.Info("GUI: Screen initialized", "Sokol.GUI");
        return _instance;
    }

    /// <summary>
    /// Fired once during <see cref="Shutdown"/> before the singleton is cleared.
    /// Applications can use this to persist layout / widget state.
    /// </summary>
    public static event Action? ShuttingDown;

    public static void Shutdown()
    {
        try { ShuttingDown?.Invoke(); } catch { /* persistence errors must not prevent shutdown */ }
        _instance?.Fonts.Clear();
        _instance = null;
    }

    // ─── Per-frame ───────────────────────────────────────────────────────────
    public void Update(float width, float height, float dpiScale)
    {
        LogicalWidth  = width;
        LogicalHeight = height;

        if (Renderer == null || Renderer.VGContext == IntPtr.Zero) return; // can happen if Update is called before Init

        // Track actual keyboard height from window-height reduction
        // (keyboard_resizes_canvas on iOS, adjustResize on Android).
        bool kbShown = sapp_keyboard_shown();
        if (!kbShown)
        {
            _noKeyboardHeight = height;
            KeyboardHeight    = 0f;
        }
        else if (_noKeyboardHeight > 0 && height < _noKeyboardHeight)
        {
            KeyboardHeight = _noKeyboardHeight - height;
        }

        // Mobile overlay lifecycle — driven by focus state, not by sapp_keyboard_shown().
        // sapp_keyboard_shown() is async on iOS (set only when keyboard finishes animating),
        // so we can't use it to gate Show(). Instead we watch Focus.Focused.
#if __ANDROID__ || __IOS__
        {
            float kbH = KeyboardHeight > 0 ? KeyboardHeight : height * 0.45f;
            var focused = Focus.Focused;

            bool nonProxyTextFocused =
                focused is TextBox ftb && !ftb.SkipKeyboardManagement ||
                focused is TextArea fta && fta.IsEditable && !fta.SkipKeyboardManagement;

            bool proxyHasFocus =
                _mobileOverlay?.IsActive == true &&
                _mobileOverlay.Children.Count > 0 &&
                focused == _mobileOverlay.Children[0];

            if (nonProxyTextFocused && !MobileOverlay.IsActive)
                MobileOverlay.Show(focused!, kbH);
            else if (!nonProxyTextFocused && !proxyHasFocus && MobileOverlay.IsActive)
                MobileOverlay.Hide();
            else if (MobileOverlay.IsActive && KeyboardHeight > 0)
                MobileOverlay.UpdateKeyboardHeight(kbH);

            // Also hide if keyboard was externally dismissed (Android back button, iOS swipe-down)
            // while proxy still has focus. Only fires after keyboard was confirmed shown.
            if (_mobileOverlay?.IsActive == true)
            {
                if (kbShown)  _keyboardConfirmed = true;
                else if (_keyboardConfirmed) { MobileOverlay.Hide(); _keyboardConfirmed = false; }
            }
            else
            {
                _keyboardConfirmed = false;
            }
        }
#endif

        // Keep Bounds in sync with the window size.
        if (Bounds.Width != width || Bounds.Height != height)
        {
            Sokol.SLog.Info($"GUI: Window resized → {width:F0}x{height:F0} (dpi={dpiScale:F2})", "Sokol.GUI");
            Bounds = new Rect(0, 0, width, height);
            InvalidateLayout();
        }

        DbgFrame++;

        // Animate all widgets.
        AnimationManager.Instance?.Update();

        // Check tooltip hover-delay each frame.
        Input.UpdateTooltip();

        // Screen root children fill the window — bypass CanvasLayout measurement.
        // CanvasLayout would measure TabView as (0,0) because tabs live in _tabs not _children.
        // Instead, set each root child's Bounds to the full window then run its internal layout.
        // bool logLayout = DbgFrame <= 5 || DbgFrame % 300 == 0;
        // if (logLayout)
        //     Sokol.SLog.Info($"GUI.Layout[{DbgFrame}]: filling {Children.Count} root children to {width:F0}x{height:F0}", "Sokol.GUI");

        foreach (var child in Children)
        {
            child.Bounds = new Rect(0, 0, width, height);
            child.PerformLayout(Renderer, true);
            // if (logLayout)
            //     Sokol.SLog.Info($"GUI.Layout[{DbgFrame}]:   {child.GetType().Name} Bounds={child.Bounds}", "Sokol.GUI");
        }
    }

    public void Draw(float width, float height, float dpiScale)
    {
        if (Renderer == null || Renderer.VGContext == IntPtr.Zero ) return; // can happen if Draw is called before Init   

        Renderer.BeginFrame(width, height, dpiScale);
        DrawChildren(Renderer);
        // Draw any active popup on top of everything else.
        if (_activePopup != null)
        {
            var sp = _activePopup.ScreenPosition;
            Renderer.Save();
            Renderer.Translate(sp.X, sp.Y);
            _activePopup.DrawPopupOverlay(Renderer);
            Renderer.Restore();
        }
        // Draw notification toasts on top of everything.
        _notificationHost.Bounds = new Rect(0, 0, width, height);
        _notificationHost.Draw(Renderer);

        // Drag ghost on top of popups/notifications, under tooltip.
        Drag.DrawGhost(Renderer);

        // Draw tooltip overlay on top of everything else.
        var tip = TooltipControl.Shared;
        if (tip.Visible)
        {
            Renderer.Save();
            Renderer.Translate(tip.Bounds.X, tip.Bounds.Y);
            tip.Draw(Renderer);
            Renderer.Restore();
        }

        Renderer.EndFrame();
    }

    /// <summary>Draw only children (Screen itself has no visual background).</summary>
    private void DrawChildren(Renderer renderer)
    {
        // bool logDraw = false; // DbgFrame <= 5 || DbgFrame % 300 == 0;
        // if (logDraw)
        //     Sokol.SLog.Info($"GUI.Draw[{DbgFrame}]: {Children.Count} direct screen children", "Sokol.GUI");

        // Snapshot the list to avoid "collection modified during enumeration" if a
        // child adds/removes siblings during Draw (e.g. Notification, Tooltip, Popup).
        var count = Children.Count;
        var list = new Widget[count];
        for (int i = 0; i < count; i++) list[i] = Children[i];

        for (int i = 0; i < count; i++)
        {
            var child = list[i];
            // if (logDraw)
            //     Sokol.SLog.Info($"GUI.Draw[{DbgFrame}]:   {child.GetType().Name} Bounds={child.Bounds} Visible={child.Visible}", "Sokol.GUI");

            if (!child.Visible) continue;
            renderer.Save();
            renderer.Translate(child.Bounds.X, child.Bounds.Y);
            child.Draw(renderer);
            renderer.Restore();
        }
    }

    public unsafe void DispatchEvent(sapp_event* e) => Input.Dispatch(e);

    // ─── Popup hit-test override ─────────────────────────────────────────────
    /// <summary>
    /// Check the active popup widget first (it may draw outside its parent's bounds),
    /// then fall back to the normal tree walk.
    /// </summary>
    // ─── Popup support ───────────────────────────────────────────────────────
    /// <summary>
    /// Dismiss the active popup if the click target is outside it.
    /// Called only on mouse-down so hover/move never close the popup prematurely.
    /// </summary>
    internal static void DismissActivePopupIfNeeded(Widget? clickTarget)
    {
        if (_activePopup != null && clickTarget != _activePopup)
        {
            _activePopup.OnPopupDismiss();
            _activePopup = null;
        }
    }

    public override Widget? HitTestDeep(Vector2 screenPoint)
    {
        if (_activePopup != null)
        {
            // Ask the popup to test the point using its own local coordinate space.
            var local = _activePopup.ToLocal(screenPoint);
            if (_activePopup.HitTest(local))
            {
                Sokol.SLog.Info($"HitTest: popup {_activePopup.GetType().Name} captured screenPoint={screenPoint} local={local}", "Sokol.GUI");
                return _activePopup;
            }
            // Mouse is outside the popup — do NOT dismiss here (dismissal
            // happens only on click via DismissActivePopupIfNeeded).
            // Fall through to normal tree walk so other widgets can be hovered.
        }
        return base.HitTestDeep(screenPoint);
    }

    // ─── Override Draw to avoid double-translation ───────────────────────────
    public override void Draw(Renderer renderer) => DrawChildren(renderer);
}
